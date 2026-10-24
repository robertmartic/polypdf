using System.IO;
using System.IO.Compression;

namespace PolyPDF;

internal static class UpdateTransaction
{
 internal static void CopyTree(string source,string target)
 {
  if (File.GetAttributes(source).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Linked installation folders cannot be updated automatically.");
  Directory.CreateDirectory(target);
  foreach (var file in Directory.EnumerateFiles(source))
  {
   if (File.GetAttributes(file).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Linked files cannot be updated automatically.");
   File.Copy(file,Path.Combine(target,Path.GetFileName(file)),true);
  }
  foreach (var directory in Directory.EnumerateDirectories(source)) CopyTree(directory,Path.Combine(target,Path.GetFileName(directory)));
 }
 internal static void ExtractPayload(string zipPath,string target,UpdateManifest manifest)
 {
  var allowed = manifest.Files.ToDictionary(f => f.Path,StringComparer.OrdinalIgnoreCase);
  var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  using var zip = ZipFile.OpenRead(zipPath);
  foreach (var entry in zip.Entries)
  {
   var name = entry.FullName.Replace('\\','/');
   if (!name.StartsWith("PolyPDF/",StringComparison.Ordinal)) throw new InvalidDataException("Unexpected update archive layout.");
   name = name[8..];
   if (name.Length == 0 || name.EndsWith('/')) { if (name.Length > 0) UpdateModel.SafePath(target,name.TrimEnd('/')); continue; }
   var path = UpdateModel.SafePath(target,name);
   if (!seen.Add(name)) throw new InvalidDataException("Duplicate update archive entry.");
   if (name == UpdateModel.ManifestName) continue;
   if (!allowed.TryGetValue(name,out var file) || entry.Length != file.Size || entry.Length > 1024L*1024*1024) throw new InvalidDataException("Unexpected update file.");
   Directory.CreateDirectory(Path.GetDirectoryName(path)!);
   entry.ExtractToFile(path,true); UpdateModel.VerifyHash(path,file.Sha256);
  }
 }
 internal static string Apply(UpdateJob job,Action<string> maintain,Action<string>? stageChanged = null)
 {
  UpdateModel.VerifyHash(job.ManifestPath,job.ManifestHash);
  UpdateModel.VerifyHash(job.PayloadPath,job.PayloadHash);
  var next = UpdateModel.Read<UpdateManifest>(job.ManifestPath); UpdateModel.Validate(next);
  var root = Path.GetFullPath(job.InstallDir).TrimEnd(Path.DirectorySeparatorChar);
  var old = UpdateModel.Read<UpdateManifest>(Path.Combine(root,UpdateModel.ManifestName)); UpdateModel.Validate(old);
  if (old.Version != job.FromVersion || UpdateModel.ParseVersion(next.Version) <= UpdateModel.ParseVersion(old.Version)) throw new InvalidDataException("The installed version changed or this update is not newer.");
  if (job.Delta && (next.DeltaFrom != old.Version || !UpdateModel.Matches(root,old))) throw new InvalidDataException("The differential update does not match this installation. Use the full update.");
  var parent = Directory.GetParent(root)?.FullName ?? throw new IOException("Invalid installation directory.");
  UpdateStorage.EnsureSpace([(root,checked(UpdateStorage.TreeSize(root) + next.Files.Sum(f => f.Size)))]);
  var token = Guid.NewGuid().ToString("N");
  var stage = Path.Combine(parent,".PolyPDF-stage-" + token);
  var backup = Path.Combine(parent,".PolyPDF-backup-" + token);
  var failed = Path.Combine(parent,".PolyPDF-failed-" + token);
  var swapped = false; var movedOld = false;
  try
  {
   stageChanged?.Invoke("Preparing the updated installation…");
   CopyTree(root,stage);
   foreach (var file in old.Files.Where(f => !next.Files.Any(n => n.Path.Equals(f.Path,StringComparison.OrdinalIgnoreCase)))) File.Delete(UpdateModel.SafePath(stage,file.Path));
   ExtractPayload(job.PayloadPath,stage,next);
   File.Copy(job.ManifestPath,Path.Combine(stage,UpdateModel.ManifestName),true);
   if (!UpdateModel.Matches(stage,next)) throw new InvalidDataException("The prepared installation did not pass verification.");
   stageChanged?.Invoke("Installing the verified update…");
   Directory.Move(root,backup); movedOld = true;
   Directory.Move(stage,root); swapped = true;
   maintain(root);
   if (!UpdateModel.Matches(root,next)) throw new InvalidDataException("The installed update did not pass verification.");
   // Housekeeping cannot turn a committed installation into failure.
   try { UpdateStorage.Mark(backup,root,"backup"); } catch { }
   return backup;
  }
  catch
  {
   if (swapped) Directory.Move(root,failed);
   if (movedOld) Directory.Move(backup,root);
   // Failed staged copies are retained for diagnosis; original user files remain in root.
   throw;
  }
 }
}
