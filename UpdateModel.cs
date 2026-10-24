using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace PolyPDF;

internal sealed record UpdateFile(string Path, long Size, string Sha256);
internal sealed record UpdateManifest(int Format, string Version, List<UpdateFile> Files, string? DeltaFrom = null);
internal sealed record ReleaseAsset(string Name, string Url, long Size, string Sha256);
internal sealed record AvailableUpdate(string Version, string Notes, IReadOnlyList<ReleaseAsset> Assets);
internal sealed record PreparedUpdate(string JobPath, string HelperPath);
internal sealed record UpdateJob(string InstallDir, string FromVersion, string ManifestPath, string ManifestHash,
 string PayloadPath, string PayloadHash, string MaintenancePath, string MaintenanceHash, bool Delta,
 int ParentPid, string ResultPath, long DownloadBytes, long FullBytes);
internal sealed record UpdateResult(bool Success, string FromVersion, string ToVersion, bool Delta,
 long DownloadBytes, long FullBytes, string Message, string? BackupPath = null);
internal sealed record UpdatePreferences(bool AutomaticChecks = true, string? LastShownVersion = null);

internal static class UpdateModel
{
 internal static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
 internal static string DataRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PolyPDF");
 internal static string CacheRoot => Path.Combine(DataRoot,"Updates");
 internal const string ManifestName = "update-manifest.json";
 internal static string Hash(string path) { using var file = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(file)).ToLowerInvariant(); }
 internal static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllText(path),Json) ?? throw new InvalidDataException("Empty update metadata.");
 internal static void Write<T>(string path,T value)
 {
  Directory.CreateDirectory(Path.GetDirectoryName(path)!);
  var temporary = path + ".tmp";
  File.WriteAllText(temporary,JsonSerializer.Serialize(value,Json)); File.Move(temporary,path,true);
 }
 internal static UpdatePreferences Preferences
 {
  get { try { return Read<UpdatePreferences>(Path.Combine(DataRoot,"updates.json")); } catch { return new(); } }
  set => Write(Path.Combine(DataRoot,"updates.json"),value);
 }
 internal static Version ParseVersion(string value)
 {
  if (!Version.TryParse(value.TrimStart('v'),out var result) || result.Major < 0 || result.Build < 0 || result.Revision > 0)
   throw new InvalidDataException("Invalid release version.");
  return new Version(result.Major,result.Minor,result.Build);
 }
 internal static string SafePath(string root,string relative)
 {
  if (string.IsNullOrWhiteSpace(relative) || relative.Length > 220 || relative.Contains('\\') || relative.Contains(':') || relative.StartsWith('/') ||
      relative.Split('/').Any(p => p is "" or "." or ".." || p.EndsWith('.') || p.EndsWith(' ') || p.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
       System.Text.RegularExpressions.Regex.IsMatch(p.Split('.')[0],@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$",System.Text.RegularExpressions.RegexOptions.IgnoreCase)))
   throw new InvalidDataException("Unsafe update filename.");
  var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
  var result = Path.GetFullPath(Path.Combine(fullRoot,relative.Replace('/',Path.DirectorySeparatorChar)));
  if (!result.StartsWith(fullRoot,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update filename escapes its folder.");
  return result;
 }
 internal static void Validate(UpdateManifest manifest)
 {
  if (manifest.Format != 1 || manifest.Files == null || manifest.Files.Count is < 1 or > 2000) throw new InvalidDataException("Unsupported update manifest.");
  ParseVersion(manifest.Version);
  if (manifest.DeltaFrom != null) ParseVersion(manifest.DeltaFrom);
  var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  long size = 0;
  foreach (var file in manifest.Files)
  {
   SafePath(Path.GetTempPath(),file.Path);
   if (!names.Add(file.Path) || file.Path.Equals(ManifestName,StringComparison.OrdinalIgnoreCase) ||
       file.Path.Equals("Uninstall.exe",StringComparison.OrdinalIgnoreCase) || file.Path.Equals("polypdf-installation.ini",StringComparison.OrdinalIgnoreCase) ||
       file.Size < 0 || file.Size > 1024L*1024*1024 || file.Sha256.Length != 64 || !file.Sha256.All(Uri.IsHexDigit)) throw new InvalidDataException("Invalid update file metadata.");
   size = checked(size + file.Size);
  }
  if (size > 2L*1024*1024*1024 || !names.Contains("PolyPDF.exe") || !names.Contains("PolyPDF.dll")) throw new InvalidDataException("Incomplete or oversized update.");
 }
 internal static bool Matches(string root,UpdateManifest manifest)
 {
  Validate(manifest);
  return manifest.Files.All(f => File.Exists(SafePath(root,f.Path)) && new FileInfo(SafePath(root,f.Path)).Length == f.Size &&
   !File.GetAttributes(SafePath(root,f.Path)).HasFlag(FileAttributes.ReparsePoint) && Hash(SafePath(root,f.Path)).Equals(f.Sha256,StringComparison.OrdinalIgnoreCase));
 }
 internal static void VerifyHash(string path,string hash)
 {
  if (hash.Length != 64 || !hash.All(Uri.IsHexDigit) || !Hash(path).Equals(hash,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The update failed its SHA-256 integrity check.");
 }
}
