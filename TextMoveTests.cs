using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using static PolyPDF.PdfNative;

namespace PolyPDF;

internal static class TextMoveTests
{
 static void Check(bool value,string message) { if (!value) throw new Exception("Text move regression: "+message); }
 internal static byte[] OriginalFixture(bool legacy = false) => PdfWorker.Invoke(() =>
 {
  var doc = FPDF_CreateNewDocument(); var page = FPDFPage_New(doc,0,595.28,841.89);
  var bytes = File.ReadAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"segoeui.ttf"));
  var font = FPDFText_LoadFont(doc,bytes,(uint)bytes.Length,2,1);
  try
  {
   foreach (var y in legacy ? new[] { 750d,700d } : new[] { 750d })
   {
    var obj = FPDFPageObj_CreateTextObj(doc,font,18);
    if (obj == IntPtr.Zero || FPDFText_SetText(obj,"Original text") == 0) throw new Exception("Fixture creation failed.");
    if (legacy) FPDFPageObj_AddMark(obj,"PolyPDF.Font.SegoeUI");
    FPDFPageObj_Transform(obj,1,0,0,1,60,y); FPDFPage_InsertObject(page,obj);
   }
   if (FPDFPage_GenerateContent(page) == 0) throw new Exception("Fixture content failed.");
   using var stream = new MemoryStream();
   WriteBlock callback = (_,data,length) => { var buffer = new byte[length]; Marshal.Copy(data,buffer,0,buffer.Length); stream.Write(buffer); return 1; };
   var writer = new FileWrite { Version = 1,Callback = Marshal.GetFunctionPointerForDelegate(callback) };
   if (FPDF_SaveAsCopy(doc,ref writer,SaveNoIncremental) == 0) throw new Exception("Fixture save failed.");
   GC.KeepAlive(callback); return stream.ToArray();
  }
  finally { FPDF_ClosePage(page); FPDFFont_Close(font); FPDF_CloseDocument(doc); }
 });
 static void Near(Rect actual,Rect expected,string message)
 {
  Check(Math.Abs(actual.X-expected.X) < 0.03 && Math.Abs(actual.Y-expected.Y) < 0.03 && Math.Abs(actual.Width-expected.Width) < 0.03 && Math.Abs(actual.Height-expected.Height) < 0.03,message);
 }
 internal static void Run(string folder)
 {
  using var doc = new PdfDocument(OriginalFixture());
  Check(doc.AddedTextTargets(0).Count == 0,"original external text is not movable");
  doc.Change(() => doc.AddText(0,new Point(60,600),"Move this text\n\nSecond line · Čć Žž",18,Colors.Blue));
  doc.Change(() => doc.AddText(0,new Point(60,400),"Other block",16,Colors.Red));
  var blocks = doc.AddedTextTargets(0); Check(blocks.Count == 2 && blocks[0].Indices.Length == 2,"multiline insertion grouped, separate insertion independent");
  var target = blocks[0]; var other = blocks[1]; var text = doc.Text(0);
  var path = Path.Combine(folder,"text-move-roundtrip.pdf"); doc.Save(path);
  var offset = new Vector(42,-35); doc.Change(() => doc.MoveAddedText(target,offset));
  var expected = target.Bounds; expected.Offset(offset); Near(doc.AddedTextTargets(0)[0].Bounds,expected,"movement preserves block size and spacing");
  Near(doc.AddedTextTargets(0)[1].Bounds,other.Bounds,"other insertion remains in place");
  Check(doc.Text(0) == text && doc.Dirty,"contents and dirty flag after move");
  Check(doc.Undo() && !doc.Dirty,"undo returns to saved revision"); Near(doc.AddedTextTargets(0)[0].Bounds,target.Bounds,"undo position");
  Check(doc.Undo(true) && doc.Dirty,"redo marks document dirty"); Near(doc.AddedTextTargets(0)[0].Bounds,expected,"redo position");
  var before = doc.RevisionId; var staleRejected = false;
  try { doc.Change(() => doc.MoveAddedText(target,new Vector(1,1))); } catch (InvalidOperationException) { staleRejected = true; }
  Check(staleRejected && doc.RevisionId == before,"stale selection rejected without a new undo revision"); Near(doc.AddedTextTargets(0)[0].Bounds,expected,"failed move rollback");
  doc.Save(path);
  using (var reopened = new PdfDocument(File.ReadAllBytes(path)))
  {
   Check(reopened.AddedTextTargets(0).Count == 2 && reopened.AddedTextTargets(0)[0].Indices.Length == 2,"saved block identity survives reopen");
   Near(reopened.AddedTextTargets(0)[0].Bounds,expected,"saved position");
   var foreignRejected = false;
   try { reopened.Change(() => reopened.MoveAddedText(doc.AddedTextTargets(0)[0],new Vector(1,1))); } catch (InvalidOperationException) { foreignRejected = true; }
   Check(foreignRejected,"selection cannot affect another document");
   var reopenedTarget = reopened.AddedTextTargets(0)[0]; reopened.Change(() => reopened.MoveAddedText(reopenedTarget,new Vector(10,12)));
   expected.Offset(10,12); Near(reopened.AddedTextTargets(0)[0].Bounds,expected,"reopened insertion can move again");
  }
  using (var legacy = new PdfDocument(OriginalFixture(true)))
  {
   Check(legacy.AddedTextTargets(0).Count == 2 && legacy.AddedTextTargets(0).All(t => t.Indices.Length == 1),"legacy insertions stay independent");
   var line = legacy.AddedTextTargets(0)[0]; var remaining = legacy.AddedTextTargets(0)[1].Bounds;
   legacy.Change(() => legacy.MoveAddedText(line,new Vector(20,0))); Near(legacy.AddedTextTargets(0)[1].Bounds,remaining,"legacy move affects one line");
  }
  using (var spaced = PdfDocument.Create())
  {
   spaced.Change(() => spaced.AddText(0,new Point(60,600),"First\n   \nLast",18,Colors.Blue));
   var block = spaced.AddedTextTargets(0).Single(); Check(block.Indices.Length == 3,"space-only line stays part of its insertion");
   spaced.Change(() => spaced.MoveAddedText(block,new Vector(15,-10)));
   var expectedBounds = block.Bounds; expectedBounds.Offset(15,-10); Near(spaced.AddedTextTargets(0).Single().Bounds,expectedBounds,"space-containing block moves together");
  }
  var protectedSnapshot = doc.RecoverySnapshot() with { Permissions = 4 };
  using (var protectedDoc = PdfDocument.Recover(protectedSnapshot))
  {
   var refused = false;
   try { protectedDoc.Change(() => protectedDoc.MoveAddedText(protectedDoc.AddedTextTargets(0)[0],new Vector(10,10))); } catch (InvalidOperationException) { refused = true; }
   Check(refused,"document editing restrictions still apply");
  }
  foreach (var rotation in Enumerable.Range(0,4))
  {
   using var rotated = PdfDocument.Create();
   for (int i = 0; i < rotation; i++) rotated.Change(() => rotated.Rotate(0));
   rotated.Change(() => rotated.AddText(0,new Point(150,350),"Rotated block\nSecond line",16,Colors.Blue));
   foreach (var scale in new[] { 0.5,1d,2d })
   {
    var size = rotated.Size(0); var width = (int)(size.Width*scale); var height = (int)(size.Height*scale);
    var initial = rotated.AddedTextTargets(0)[0]; var center = new Point(initial.Bounds.X+initial.Bounds.Width/2,initial.Bounds.Y+initial.Bounds.Height/2);
    var screen = rotated.ToScreen(0,center,width,height); var desired = new Vector(18,22);
    var pdfOffset = rotated.ToPdf(0,screen+desired,width,height)-rotated.ToPdf(0,screen,width,height);
    rotated.Change(() => rotated.MoveAddedText(initial,pdfOffset));
    var moved = rotated.AddedTextTargets(0)[0]; var movedCenter = new Point(moved.Bounds.X+moved.Bounds.Width/2,moved.Bounds.Y+moved.Bounds.Height/2);
    Check((rotated.ToScreen(0,movedCenter,width,height)-screen-desired).Length < 2,"screen direction at every rotation and zoom");
   }
   using var saved = new PdfDocument(rotated.Bytes()); Near(saved.AddedTextTargets(0)[0].Bounds,rotated.AddedTextTargets(0)[0].Bounds,"rotated saved bounds");
  }
  Check(TextMoveGeometry.Clamp(new Rect(20,30,80,40),new Vector(-100,500),new Size(200,150)) == new Vector(-20,80),"visible-page clamp");
  Check(TextMoveGeometry.Clamp(new Rect(0,0,300,250),new Vector(-500,500),new Size(200,150)) == new Vector(-100,0),"oversized block clamp");
  File.WriteAllText(Path.Combine(folder,"text-move-tests.txt"),"PASS: unmarked original text excluded; multiline grouping; independent/legacy insertions; translation without text changes; stale/cross-document selection rejection; rollback; Undo/Redo and saved state; save/reopen; all four page rotations at three zoom levels; page-edge and oversized-block clamps.");
 }
}
