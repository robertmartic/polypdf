using System.IO;
using System.Text.RegularExpressions;

namespace PolyPDF;

internal sealed record UpdateStorageMarker(int Format,string InstallDir,string State,DateTime CreatedUtc);
internal sealed record UpdateStorageItem(string Path,long Bytes,bool Eligible,string Reason);
internal sealed record UpdateStorageSummary(IReadOnlyList<UpdateStorageItem> Items)
{
 internal long TotalBytes => Items.Sum(i => i.Bytes);
 internal long EligibleBytes => Items.Where(i => i.Eligible).Sum(i => i.Bytes);
}

internal static class UpdateStorage
{
 internal const string MarkerName = ".polypdf-update-storage.json";
 const long Reserve = 64L*1024*1024;
 internal static string Size(long bytes) => $"{bytes/1048576d:0.0} MB";
 internal static FileStream Lock(string dataRoot)
 {
  Directory.CreateDirectory(dataRoot);
  try { return new FileStream(Path.Combine(dataRoot,"update-storage.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None); }
  catch (IOException) { throw new IOException("Another update or cleanup is running. Wait for it to finish and try again."); }
 }
 internal static void Mark(string folder,string installDir,string state,DateTime? created = null)
 {
  var path = Path.Combine(folder,MarkerName);
  var date = created ?? (File.Exists(path) ? UpdateModel.Read<UpdateStorageMarker>(path).CreatedUtc : DateTime.UtcNow);
  UpdateModel.Write(path,new UpdateStorageMarker(1,Path.GetFullPath(installDir).TrimEnd(Path.DirectorySeparatorChar),state,date));
 }
 internal static long TreeSize(string root)
 {
  if (File.GetAttributes(root).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Linked update folders are not supported.");
  long bytes = 0;
  foreach (var file in Directory.EnumerateFiles(root))
  {
   if (File.GetAttributes(file).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Linked update files are not supported.");
   bytes = checked(bytes + new FileInfo(file).Length);
  }
  foreach (var folder in Directory.EnumerateDirectories(root)) bytes = checked(bytes + TreeSize(folder));
  return bytes;
 }
 internal static void EnsureSpace(IEnumerable<(string Path,long Bytes)> requirements,Func<string,long>? available = null)
 {
  foreach (var drive in requirements.GroupBy(r => Path.GetPathRoot(Path.GetFullPath(r.Path))!,StringComparer.OrdinalIgnoreCase))
  {
   var required = checked(drive.Sum(r => r.Bytes) + Reserve);
   var free = available?.Invoke(drive.Key) ?? new DriveInfo(drive.Key).AvailableFreeSpace;
   if (free < required) throw new IOException($"Not enough free space on {drive.Key}. This update needs at least {Size(required)} free; {Size(free)} is available. Free some space and try again.");
  }
 }
 static bool DirectChild(string path,string parent) => string.Equals(Directory.GetParent(Path.GetFullPath(path))?.FullName,Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase);
 internal static UpdateStorageSummary Inspect(string installDir,string cacheRoot,DateTime now)
 {
  installDir = Path.GetFullPath(installDir).TrimEnd(Path.DirectorySeparatorChar);
  var parent = Directory.GetParent(installDir)?.FullName ?? throw new IOException("Invalid installation folder.");
  var items = new List<UpdateStorageItem>();
  var backups = new List<(string Path,DateTime Created,long Bytes,bool Safe)>();
  foreach (var folder in Directory.Exists(parent) ? Directory.EnumerateDirectories(parent,".PolyPDF-backup-*") : [])
  {
   try
   {
    if (!Regex.IsMatch(Path.GetFileName(folder),@"^\.PolyPDF-backup-[a-f0-9]{32}$") || !DirectChild(folder,parent)) continue;
    var bytes = TreeSize(folder);
    var marker = UpdateModel.Read<UpdateStorageMarker>(Path.Combine(folder,MarkerName));
    if (marker.Format != 1 || marker.State != "backup" || !string.Equals(marker.InstallDir,installDir,StringComparison.OrdinalIgnoreCase)) continue;
    var manifest = UpdateModel.Read<UpdateManifest>(Path.Combine(folder,UpdateModel.ManifestName)); UpdateModel.Validate(manifest);
    var owned = manifest.Files.Select(f => f.Path).Append(UpdateModel.ManifestName).Append("Uninstall.exe").Append("polypdf-installation.ini").Append(MarkerName).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var safe = Directory.EnumerateFiles(folder,"*",SearchOption.AllDirectories).All(f => owned.Contains(Path.GetRelativePath(folder,f).Replace('\\','/'))) && UpdateModel.Matches(folder,manifest);
    backups.Add((folder,marker.CreatedUtc,bytes,safe));
   }
   catch (IOException) { }
   catch (InvalidDataException) { }
   catch (System.Text.Json.JsonException) { }
   catch (UnauthorizedAccessException) { }
  }
  var newest = backups.Where(b => b.Safe).OrderByDescending(b => b.Created).FirstOrDefault().Path;
  var protectedBackups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  foreach (var name in new[] { "pending-result.json","last-result.json" })
  {
   try { var result = UpdateModel.Read<UpdateResult>(Path.Combine(cacheRoot,name)); if (result.Success && result.BackupPath != null) protectedBackups.Add(Path.GetFullPath(result.BackupPath)); }
   catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException or ArgumentException or UnauthorizedAccessException) { }
  }
  foreach (var backup in backups)
  {
   var retained = backup.Path == newest || protectedBackups.Contains(backup.Path);
   var eligible = backup.Safe && !retained && now-backup.Created >= TimeSpan.FromDays(30);
   items.Add(new(backup.Path,backup.Bytes,eligible,!backup.Safe ? "Kept: contains additional or modified files" : retained ? "Kept: latest recovery backup" : eligible ? "Old recovery backup" : "Kept: backup is less than 30 days old"));
  }
  foreach (var folder in Directory.Exists(cacheRoot) ? Directory.EnumerateDirectories(cacheRoot,"job-*") : [])
  {
   try
   {
    if (!Regex.IsMatch(Path.GetFileName(folder),@"^job-[a-f0-9]{32}$") || !DirectChild(folder,cacheRoot)) continue;
    var bytes = TreeSize(folder);
    var marker = UpdateModel.Read<UpdateStorageMarker>(Path.Combine(folder,MarkerName));
    if (marker.Format != 1 || !string.Equals(marker.InstallDir,installDir,StringComparison.OrdinalIgnoreCase)) continue;
    var ended = marker.State is "completed" or "failed" or "abandoned";
    var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { MarkerName,"job.json",UpdateModel.ManifestName,"payload.zip","maintenance.exe",UpdateModel.ManifestName+".part","payload.zip.part","maintenance.exe.part" };
    var helperManifest = Path.Combine(folder,"helper",UpdateModel.ManifestName);
    if (File.Exists(helperManifest))
    {
     var helper = UpdateModel.Read<UpdateManifest>(helperManifest); UpdateModel.Validate(helper);
     foreach (var file in helper.Files) allowed.Add("helper/"+file.Path);
     allowed.Add("helper/"+UpdateModel.ManifestName);
    }
    var safe = Directory.EnumerateFiles(folder,"*",SearchOption.AllDirectories).All(f => allowed.Contains(Path.GetRelativePath(folder,f).Replace('\\','/')));
    var eligible = safe && ended && now-marker.CreatedUtc >= TimeSpan.FromDays(7);
    items.Add(new(folder,bytes,eligible,!safe ? "Kept: contains additional files" : eligible ? "Old update download and helper" : ended ? "Kept: update data is less than 7 days old" : "Kept: prepared or active update"));
   }
   catch (IOException) { }
   catch (InvalidDataException) { }
   catch (System.Text.Json.JsonException) { }
   catch (UnauthorizedAccessException) { }
  }
  return new(items);
 }
 internal static long Cleanup(string installDir,string cacheRoot,string dataRoot,DateTime now)
 {
  using var guard = Lock(dataRoot);
  long removed = 0;
  // Recompute candidates while holding the same lock used by preparation/apply.
  foreach (var item in Inspect(installDir,cacheRoot,now).Items.Where(i => i.Eligible))
  {
   // TreeSize rejects links before any recursive operation; never follow junctions.
   TreeSize(item.Path);
   Directory.Delete(item.Path,true); removed += item.Bytes;
  }
  return removed;
 }
 internal static void Abandon(PreparedUpdate prepared)
 {
  try
  {
   using var guard = Lock(UpdateModel.DataRoot);
   var folder = Path.GetDirectoryName(prepared.JobPath)!;
   if (DirectChild(folder,UpdateModel.CacheRoot)) Mark(folder,AppContext.BaseDirectory,"abandoned");
  }
  catch { /* A retained prepared copy is safe; cleanup must never break closing. */ }
 }
}
