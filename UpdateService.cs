using System.IO;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Win32;

namespace PolyPDF;

internal sealed class UpdateService : IDisposable
{
 readonly HttpClient client;
 internal UpdateService(HttpMessageHandler? handler = null)
 {
  client = handler == null ? new HttpClient() : new HttpClient(handler);
  client.Timeout = TimeSpan.FromMinutes(10);
  client.DefaultRequestHeaders.UserAgent.ParseAdd("PolyPDF/" + AppInfo.Version);
  client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
 }
 internal static bool IsInstalled(string root)
 {
  try
  {
   var registered = Registry.CurrentUser.OpenSubKey(@"Software\PolyPDF")?.GetValue("InstallDir") as string;
   return registered != null && Path.GetFullPath(registered).TrimEnd('\\').Equals(Path.GetFullPath(root).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase) &&
    File.ReadAllLines(Path.Combine(root,"polypdf-installation.ini")).Any(l => l.Trim() == "AppId=PolyPDF");
  }
  catch { return false; }
 }
 internal async Task<AvailableUpdate?> CheckAsync(CancellationToken cancellation = default)
 {
  using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation); timeout.CancelAfter(TimeSpan.FromSeconds(25));
  using var response = await client.GetAsync("https://api.github.com/repos/robertmartic/polypdf/releases/latest",timeout.Token);
  response.EnsureSuccessStatusCode();
  var bytes = await response.Content.ReadAsByteArrayAsync(timeout.Token);
  if (bytes.Length > 4*1024*1024) throw new InvalidDataException("Oversized release metadata.");
  using var document = JsonDocument.Parse(bytes); var root = document.RootElement;
  if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return null;
  var version = UpdateModel.ParseVersion(root.GetProperty("tag_name").GetString()!).ToString(3);
  if (UpdateModel.ParseVersion(version) <= UpdateModel.ParseVersion(AppInfo.Version)) return null;
  var assets = new List<ReleaseAsset>();
  foreach (var item in root.GetProperty("assets").EnumerateArray())
  {
   var name = item.GetProperty("name").GetString()!;
   var url = item.GetProperty("browser_download_url").GetString()!;
   var digest = item.TryGetProperty("digest",out var d) ? d.GetString() : null;
   if (digest?.StartsWith("sha256:",StringComparison.Ordinal) != true) continue;
   var asset = new ReleaseAsset(name,url,item.GetProperty("size").GetInt64(),digest[7..]);
   ValidateAsset(asset,version); assets.Add(asset);
  }
  return new(version,root.TryGetProperty("body",out var body) ? body.GetString() ?? "" : "",assets);
 }
 internal static void ValidateAsset(ReleaseAsset asset,string version)
 {
  if (Path.GetFileName(asset.Name) != asset.Name || asset.Name.Contains('\\') || asset.Size is < 1 or > 2147483648 ||
   asset.Sha256.Length != 64 || !asset.Sha256.All(Uri.IsHexDigit) ||
   asset.Url != $"https://github.com/robertmartic/polypdf/releases/download/v{version}/{asset.Name}") throw new InvalidDataException("Unexpected release asset or download source.");
 }
 async Task DownloadAsync(ReleaseAsset asset,string version,string destination,IProgress<(long Done,long Total,string Status)>? progress,CancellationToken cancellation)
 {
  ValidateAsset(asset,version);
  var temporary = destination + ".part";
  try
  {
   using var response = await client.GetAsync(asset.Url,HttpCompletionOption.ResponseHeadersRead,cancellation); response.EnsureSuccessStatusCode();
   if (response.RequestMessage?.RequestUri?.Scheme != "https") throw new InvalidDataException("An update download was redirected to an insecure connection.");
   await using (var source = await response.Content.ReadAsStreamAsync(cancellation))
   await using (var output = File.Create(temporary))
   {
    var buffer = new byte[128*1024]; long total = 0; int count;
    while ((count = await source.ReadAsync(buffer,cancellation)) != 0)
    {
     total += count; if (total > asset.Size) throw new InvalidDataException("The download is larger than expected.");
     await output.WriteAsync(buffer.AsMemory(0,count),cancellation);
     var label = asset.Name.EndsWith("manifest.json") ? "Downloading update information…" : asset.Name.EndsWith("maintenance.exe") ? "Downloading the installer…" : asset.Name.EndsWith("delta.zip") ? "Downloading changed files…" : "Downloading the full update…";
     progress?.Report((total,asset.Size,label));
    }
    if (total != asset.Size) throw new IOException("The download was interrupted or incomplete.");
   }
   progress?.Report((asset.Size,asset.Size,"Verifying the download…"));
   await Task.Run(() => UpdateModel.VerifyHash(temporary,asset.Sha256),cancellation);
   File.Move(temporary,destination,true);
  }
  finally { if (File.Exists(temporary)) File.Delete(temporary); }
 }
 internal async Task<PreparedUpdate> PrepareAsync(AvailableUpdate release,IProgress<(long Done,long Total,string Status)>? progress,CancellationToken cancellation)
 {
  var root = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
  if (!IsInstalled(root)) throw new InvalidOperationException("Install PolyPDF using its conventional installer before using in-app updates.");
  using var storageGuard = UpdateStorage.Lock(UpdateModel.DataRoot);
  UpdateStorage.EnsureSpace([(UpdateModel.CacheRoot,4L*1024*1024)]);
  var folder = Path.Combine(UpdateModel.CacheRoot,"job-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
  UpdateStorage.Mark(folder,root,"preparing");
  try
  {
  ReleaseAsset Asset(string name) => release.Assets.SingleOrDefault(a => a.Name == name) ?? throw new InvalidDataException("This release is missing a required update asset: " + name);
  var manifestAsset = Asset($"PolyPDF-{release.Version}-manifest.json");
  if (manifestAsset.Size > 4*1024*1024) throw new InvalidDataException("Oversized update manifest.");
  var manifestPath = Path.Combine(folder,UpdateModel.ManifestName);
  await DownloadAsync(manifestAsset,release.Version,manifestPath,progress,cancellation);
  var manifest = UpdateModel.Read<UpdateManifest>(manifestPath); UpdateModel.Validate(manifest);
  if (manifest.Version != release.Version) throw new InvalidDataException("Release and manifest versions differ.");
  var full = Asset($"PolyPDF-{release.Version}-win-x64.zip");
  var maintenance = Asset($"PolyPDF-{release.Version}-maintenance.exe");
  var current = UpdateModel.Read<UpdateManifest>(Path.Combine(root,UpdateModel.ManifestName)); UpdateModel.Validate(current);
  if (current.Version != AppInfo.Version) throw new InvalidDataException("Installed update metadata does not match the running application.");
  progress?.Report((0,1,"Checking the installed files…"));
  var deltaAsset = release.Assets.SingleOrDefault(a => a.Name == $"PolyPDF-{current.Version}-to-{release.Version}-delta.zip");
  var delta = manifest.DeltaFrom == current.Version && deltaAsset != null && await Task.Run(() => UpdateModel.Matches(root,current),cancellation);
  var payload = delta ? deltaAsset! : full;
  var helperBytes = current.Files.Sum(f => f.Size);
  var installedBytes = await Task.Run(() => UpdateStorage.TreeSize(root),cancellation);
  UpdateStorage.EnsureSpace([(folder,checked(payload.Size + maintenance.Size + helperBytes)),
   (root,checked(installedBytes + manifest.Files.Sum(f => f.Size)))]);
  var payloadPath = Path.Combine(folder,"payload.zip");
  await DownloadAsync(payload,release.Version,payloadPath,progress,cancellation);
  var maintenancePath = Path.Combine(folder,"maintenance.exe");
  await DownloadAsync(maintenance,release.Version,maintenancePath,progress,cancellation);
  progress?.Report((0,1,"Preparing a separate update helper…"));
  var helper = Path.Combine(folder,"helper"); Directory.CreateDirectory(helper);
  UpdateModel.Write(Path.Combine(helper,UpdateModel.ManifestName),current);
  await Task.Run(() =>
  {
   foreach (var file in current.Files)
   {
    cancellation.ThrowIfCancellationRequested();
    var destination = UpdateModel.SafePath(helper,file.Path); Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
    File.Copy(UpdateModel.SafePath(root,file.Path),destination,true);
   }
  },cancellation);
  var job = new UpdateJob(root,current.Version,manifestPath,manifestAsset.Sha256,payloadPath,payload.Sha256,
   maintenancePath,maintenance.Sha256,delta,Environment.ProcessId,Path.Combine(UpdateModel.CacheRoot,"pending-result.json"),
   manifestAsset.Size + payload.Size + maintenance.Size,full.Size + manifestAsset.Size + maintenance.Size);
  var jobPath = Path.Combine(folder,"job.json"); UpdateModel.Write(jobPath,job);
  UpdateStorage.Mark(folder,root,"ready");
  progress?.Report((1,1,delta ? "Differential update verified and ready." : "Full update verified and ready."));
  return new(jobPath,Path.Combine(helper,"PolyPDF.exe"));
  }
  catch
  {
   try { UpdateStorage.Mark(folder,root,"abandoned"); } catch { }
   throw;
  }
 }
 public void Dispose() => client.Dispose();
}
