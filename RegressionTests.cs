using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace PolyPDF;

internal static class RegressionTests
{
 static void Check(bool condition, string description) { if (!condition) throw new Exception("FAIL: " + description); }

 public static void Run(string folder)
 {
  using var doc = PdfDocument.Create();
  doc.Change(() => doc.AddText(0,new Point(50,750),"First insertion",14,Colors.Black));
  var firstSize = doc.Bytes().Length;
  for (int i = 0; i < 8; i++) doc.Change(() => doc.AddText(0,new Point(50,720-i*20),"Repeated insertion " + i,14,Colors.Black));
  var repeatedSize = doc.Bytes().Length;
  Check(repeatedSize-firstSize < 50000,"repeated text reuses embedded font");
  var file = Path.Combine(folder,"saved-state.pdf");
  doc.Save(file); Check(!doc.Dirty,"save sets clean revision");
  doc.Change(() => doc.Rotate(0)); Check(doc.Dirty,"new change is dirty");
  doc.Undo(); Check(!doc.Dirty,"undo returns to saved revision");
  doc.Undo(true); Check(doc.Dirty,"redo moves away from saved revision");
  doc.Save(file); Check(!doc.Dirty,"save new revision");
  doc.Undo(); Check(doc.Dirty,"undo before saved revision is dirty");
  doc.Undo(true); Check(!doc.Dirty,"redo returns to saved revision");
  doc.Undo(); doc.Change(() => doc.Comment(0,new Point(50,500),"Branch after undo"));
  Check(doc.Dirty,"branch does not collide with saved revision identity");
  var beforeNewFont = doc.Bytes().Length;
  doc.Change(() => doc.AddText(0,new Point(50,450),"Font reused after undo",14,Colors.Black));
  Check(doc.Bytes().Length-beforeNewFont < 50000,"font reused after document reload");
  using (var reloaded = new PdfDocument(doc.Bytes()))
  {
   var before = reloaded.Bytes().Length;
   reloaded.Change(() => reloaded.AddText(0,new Point(50,420),"Font reused after reopen",14,Colors.Black));
   Check(reloaded.Bytes().Length-before < 50000,"font reused after reopen");
   Check(reloaded.Text(0).Contains("Font reused after reopen"),"reused font remains valid");
   reloaded.Render(0,0.5);
   reloaded.Change(() => reloaded.Blank(1));
   reloaded.Change(() => reloaded.Delete(0));
   reloaded.Change(() => reloaded.AddText(0,new Point(50,750),"Font valid after source page deletion",14,Colors.Black));
   using var deletedOwner = new PdfDocument(reloaded.Bytes());
   Check(deletedOwner.Text(0).Contains("Font valid after source page deletion"),"borrowed font survives source page deletion");
  }
  for (int rotation = 0; rotation < 4; rotation++)
  {
   var display = doc.ScreenGlyphs(0,800,1000);
   var g = display.First(p => p.Glyph.Text == "F");
   var expected = new Rect(doc.ToScreen(0,g.Glyph.Bounds.TopLeft,800,1000),doc.ToScreen(0,g.Glyph.Bounds.BottomRight,800,1000));
   Check((g.Screen.TopLeft-expected.TopLeft).Length < 2 && (g.Screen.BottomRight-expected.BottomRight).Length < 2,"batched glyph coordinates match native transform at rotation " + rotation);
   doc.Change(() => doc.Rotate(0));
  }

  var root = Path.Combine(folder,"recovery-" + Guid.NewGuid().ToString("N"));
  using var active = new RecoveryStore(root);
  active.Checkpoint(doc);
  var checkpointFile = Directory.EnumerateFiles(root,"*.recovery",SearchOption.AllDirectories).Single();
  var originalCheckpoint = File.ReadAllBytes(checkpointFile);
  var attributes = File.GetAttributes(checkpointFile);
  doc.Change(() => doc.Rotate(0));
  var writeFailed = false;
  try
  {
   File.SetAttributes(checkpointFile,attributes | FileAttributes.ReadOnly);
   try { active.Checkpoint(doc); } catch (UnauthorizedAccessException) { writeFailed = true; } catch (IOException) { writeFailed = true; }
  }
  finally { File.SetAttributes(checkpointFile,attributes); }
  Check(writeFailed && File.ReadAllBytes(checkpointFile).SequenceEqual(originalCheckpoint),"failed replacement preserves previous checkpoint");
  Check(active.NeedsCheckpoint(doc),"failed checkpoint can be retried");
  active.Checkpoint(doc); Check(!active.NeedsCheckpoint(doc),"checkpoint retry succeeds");
  using (var observer = new RecoveryStore(root))
  using (var batch = observer.FindAbandoned()) Check(batch.Entries.Count == 0,"live recovery sessions are skipped");
  active.Forget(doc);

  var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
  if (string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath),"dotnet",StringComparison.OrdinalIgnoreCase)) start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory,"PolyPDF.dll"));
  start.ArgumentList.Add("--recovery-crash-test"); start.ArgumentList.Add(root);
  using (var process = Process.Start(start)!)
  {
   Check(process.WaitForExit(20000),"crash fixture completed");
   Check(process.ExitCode == 17,"fixture exited without recovery cleanup");
  }
  using (var batch = active.FindAbandoned())
  {
   Check(batch.Entries.Count == 1,"crashed session discovered");
   var entry = batch.Entries[0];
   Check(!System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(entry.FilePath)).Contains("%PDF"),"recovery is not plaintext PDF");
   var snapshot = RecoveryStore.Read(entry);
   using var restored = PdfDocument.Recover(snapshot);
   Check(restored.Dirty && restored.FilePath == null && restored.CurrentPage == 1,"recovered document is unsaved copy on correct page");
   Check(restored.Text(1).Contains("Recover this unsaved edit"),"crash recovery retains edits");
   active.Checkpoint(restored); batch.Delete(entry);
   Check(!File.Exists(entry.FilePath),"old recovery copy removed after replacement committed");
   restored.Save(Path.Combine(folder,"recovered.pdf")); active.Forget(restored);
   Check(!active.NeedsCheckpoint(restored),"saved recovery is removed");
  }
  using (var orphan = new RecoveryStore(root)) orphan.Checkpoint(doc);
  using (var batch = active.FindAbandoned())
  {
   var entry = batch.Entries.Single(); File.WriteAllBytes(entry.FilePath,[1,2,3]);
   var rejected = false; try { RecoveryStore.Read(entry); } catch (System.Security.Cryptography.CryptographicException) { rejected = true; }
   Check(rejected && File.Exists(entry.FilePath),"damaged recovery is retained, not silently deleted");
   batch.Delete(entry);
  }
  File.WriteAllText(Path.Combine(folder,"regressions.txt"),$"PASS: saved revision undo/redo and branching; font reuse ({firstSize} -> {repeatedSize} bytes for nine insertions), font reuse after undo/reopen and source-page deletion, batched coordinates, live session isolation, actual process-crash recovery, encrypted checkpoints, failed-write preservation and retry, saved-copy cleanup, damaged-copy retention.");
 }

 public static void CreateCrashedSession(string root)
 {
  // Deliberately leave the live lease and snapshots without normal Dispose.
  var store = new RecoveryStore(root);
  var doc = PdfDocument.Create();
  doc.Change(() => { doc.Blank(1); doc.AddText(1,new Point(50,750),"Recover this unsaved edit",16,Colors.Black); });
  doc.CurrentPage = 1; store.Checkpoint(doc);
  Environment.Exit(17);
 }
}
