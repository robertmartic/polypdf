using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PolyPDF;

internal sealed record RecoverySnapshot(int Format, string Name, string? OriginalPath, int Page,
 bool SourceEncrypted, uint Permissions, DateTimeOffset WrittenAt, byte[] Pdf);
internal sealed record RecoveryEntry(string FilePath);

// The lease distinguishes a crashed session from another running PolyPDF process.
// Entire snapshots (including filenames) are encrypted for the current Windows user.
internal sealed class RecoveryStore : IDisposable
{
 static readonly byte[] Entropy = Encoding.UTF8.GetBytes("PolyPDF.Recovery.v1");
 readonly string root, session;
 readonly FileStream lease;
 readonly Dictionary<Guid,(long Revision,int Page)> written = [];
 public static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PolyPDF", "Recovery");

 public RecoveryStore(string? directory = null)
 {
  root = Path.GetFullPath(directory ?? DefaultRoot);
  session = Path.Combine(root, Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(session);
  lease = new FileStream(Path.Combine(session,"lease"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
 }

 public bool NeedsCheckpoint(PdfDocument doc) => doc.Dirty
  ? !written.TryGetValue(doc.RecoveryId, out var state) || state != (doc.RevisionId, doc.CurrentPage)
  : written.ContainsKey(doc.RecoveryId);

 public void Checkpoint(PdfDocument document)
 {
  if (!document.Dirty) { Forget(document); return; }
  if (!NeedsCheckpoint(document)) return;
  var state = (document.RevisionId, document.CurrentPage);
  var snapshot = document.RecoverySnapshot();
  var plaintext = JsonSerializer.SerializeToUtf8Bytes(snapshot);
  byte[] encrypted;
  try { encrypted = ProtectedData.Protect(plaintext, Entropy, DataProtectionScope.CurrentUser); }
  finally { CryptographicOperations.ZeroMemory(plaintext); }
  var target = SnapshotPath(document);
  var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
  try
  {
   using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
   { file.Write(encrypted); file.Flush(true); }
   File.Move(temp, target, true);
   written[document.RecoveryId] = state;
  }
  finally { if (File.Exists(temp)) File.Delete(temp); }
 }

 string SnapshotPath(PdfDocument doc) => Path.Combine(session, doc.RecoveryId.ToString("N") + ".recovery");
 public void Forget(PdfDocument doc) { File.Delete(SnapshotPath(doc)); written.Remove(doc.RecoveryId); }

 public RecoveryBatch FindAbandoned()
 {
  var result = new RecoveryBatch();
  foreach (var directory in Directory.EnumerateDirectories(root))
  {
   if (directory == session || !Guid.TryParseExact(Path.GetFileName(directory),"N",out _)) continue;
   FileStream? otherLease = null;
   try
   {
    otherLease = new FileStream(Path.Combine(directory,"lease"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    var files = Directory.EnumerateFiles(directory,"*.recovery")
     .Where(file => Guid.TryParseExact(Path.GetFileNameWithoutExtension(file),"N",out _)).ToArray();
    result.Add(directory, otherLease, files.Select(f => new RecoveryEntry(f)));
    otherLease = null;
   }
   catch (IOException) { /* A live session holds the lease. */ }
   catch (UnauthorizedAccessException) { /* Leave inaccessible sessions intact. */ }
   finally { otherLease?.Dispose(); }
  }
  return result;
 }

 public static RecoverySnapshot Read(RecoveryEntry entry)
 {
  var encrypted = File.ReadAllBytes(entry.FilePath);
  var plaintext = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
  try
  {
   var snapshot = JsonSerializer.Deserialize<RecoverySnapshot>(plaintext);
   if (snapshot is null || snapshot.Format != 1 || snapshot.Pdf is null || snapshot.Pdf.Length < 5 || string.IsNullOrWhiteSpace(snapshot.Name))
    throw new InvalidDataException("This recovery copy is incomplete or unsupported.");
   return snapshot;
  }
  finally { CryptographicOperations.ZeroMemory(plaintext); }
 }

 public void Dispose() { lease.Dispose(); CleanupEmpty(session); }
 internal static void CleanupEmpty(string directory)
 {
  try
  {
   // Never recursively delete a session; unknown or unfinished files are retained.
   if (Directory.EnumerateFileSystemEntries(directory).Any(p => Path.GetFileName(p) != "lease")) return;
   File.Delete(Path.Combine(directory,"lease"));
   Directory.Delete(directory);
  }
  catch (IOException) { }
  catch (UnauthorizedAccessException) { }
 }
}

internal sealed class RecoveryBatch : IDisposable
{
 readonly List<(string Directory,FileStream Lease)> leases = [];
 public List<RecoveryEntry> Entries { get; } = [];
 internal void Add(string directory, FileStream lease, IEnumerable<RecoveryEntry> entries)
 { leases.Add((directory,lease)); Entries.AddRange(entries); }
 public void Delete(RecoveryEntry entry)
 {
  if (!Entries.Contains(entry)) throw new InvalidOperationException("Unknown recovery entry.");
  File.Delete(entry.FilePath);
 }
 public void Dispose() { foreach (var item in leases) { item.Lease.Dispose(); RecoveryStore.CleanupEmpty(item.Directory); } }
}
