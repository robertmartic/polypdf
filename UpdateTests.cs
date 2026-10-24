using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Diagnostics;

namespace PolyPDF;

internal static class UpdateTests
{
 static void Check(bool condition,string message) { if (!condition) throw new Exception("Update regression: " + message); }
 static void Reject(Action action,string message)
 {
  try { action(); } catch (IOException) { return; } catch (InvalidDataException) { return; } catch (ArgumentException) { return; }
  throw new Exception("Update regression accepted " + message);
 }
 sealed record Fixture(string Root,UpdateJob Job,UpdateManifest Old,UpdateManifest Next,string FullZip);
 static Fixture Create(string folder)
 {
  var root = Path.Combine(folder,"install"); var payload = Path.Combine(folder,"next"); Directory.CreateDirectory(root); Directory.CreateDirectory(payload);
  File.WriteAllText(Path.Combine(root,"PolyPDF.exe"),"unchanged executable example");
  File.WriteAllText(Path.Combine(root,"PolyPDF.dll"),"old library"); File.WriteAllText(Path.Combine(root,"obsolete.txt"),"old owned file");
  File.WriteAllText(Path.Combine(root,"personal.txt"),"user-owned file to preserve");
  File.WriteAllBytes(Path.Combine(root,"runtime.bin"),new byte[128*1024]);
  UpdateFile Entry(string directory,string name) => new(name,new FileInfo(Path.Combine(directory,name)).Length,UpdateModel.Hash(Path.Combine(directory,name)));
  var old = new UpdateManifest(1,"0.4.0",[Entry(root,"PolyPDF.exe"),Entry(root,"PolyPDF.dll"),Entry(root,"obsolete.txt"),Entry(root,"runtime.bin")]);
  UpdateModel.Write(Path.Combine(root,UpdateModel.ManifestName),old);
  File.Copy(Path.Combine(root,"PolyPDF.exe"),Path.Combine(payload,"PolyPDF.exe"));
  File.Copy(Path.Combine(root,"runtime.bin"),Path.Combine(payload,"runtime.bin"));
  File.WriteAllText(Path.Combine(payload,"PolyPDF.dll"),"new library"); File.WriteAllText(Path.Combine(payload,"added.txt"),"new file");
  var next = new UpdateManifest(1,"0.4.1",[Entry(payload,"PolyPDF.exe"),Entry(payload,"PolyPDF.dll"),Entry(payload,"runtime.bin"),Entry(payload,"added.txt")],"0.4.0");
  var manifestPath = Path.Combine(folder,"next.json"); UpdateModel.Write(manifestPath,next);
  var deltaPath = Path.Combine(folder,"delta.zip"); var fullPath = Path.Combine(folder,"full.zip");
  using (var delta = ZipFile.Open(deltaPath,ZipArchiveMode.Create))
   foreach (var file in next.Files.Where(f => !old.Files.Any(o => o.Path == f.Path && o.Sha256 == f.Sha256))) delta.CreateEntryFromFile(Path.Combine(payload,file.Path),"PolyPDF/" + file.Path);
  using (var full = ZipFile.Open(fullPath,ZipArchiveMode.Create))
   foreach (var file in next.Files) full.CreateEntryFromFile(Path.Combine(payload,file.Path),"PolyPDF/" + file.Path);
  var job = new UpdateJob(root,"0.4.0",manifestPath,UpdateModel.Hash(manifestPath),deltaPath,UpdateModel.Hash(deltaPath),"unused","unused",true,0,Path.Combine(folder,"result.json"),1,2);
  return new(root,job,old,next,fullPath);
 }
 internal static void Run(string parent)
 {
  var folder = Path.Combine(parent,"update-regressions-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
  Fixture Case(string name) { var path = Path.Combine(folder,name); Directory.CreateDirectory(path); return Create(path); }
  var success = Case("success"); var maintained = false;
  var backup = UpdateTransaction.Apply(success.Job,_ => maintained = true);
  Check(maintained && UpdateModel.Matches(success.Root,success.Next) && UpdateModel.Matches(backup,success.Old),"successful update and backup");
  Check(File.ReadAllText(Path.Combine(success.Root,"personal.txt")) == "user-owned file to preserve" && !File.Exists(Path.Combine(success.Root,"obsolete.txt")),"foreign-file preservation and obsolete-file removal");
  var rollback = Case("rollback");
  Reject(() => UpdateTransaction.Apply(rollback.Job,_ => throw new IOException("simulated registration failure")),"maintenance failure");
  Check(UpdateModel.Matches(rollback.Root,rollback.Old) && !File.Exists(Path.Combine(rollback.Root,"added.txt")),"rollback after folder replacement");
  var mismatch = Case("modified-base"); File.WriteAllText(Path.Combine(mismatch.Root,"PolyPDF.dll"),"modified installed library");
  Reject(() => UpdateTransaction.Apply(mismatch.Job,_ => {}),"modified delta base");
  var fullJob = mismatch.Job with { Delta = false, PayloadPath = mismatch.FullZip, PayloadHash = UpdateModel.Hash(mismatch.FullZip) };
  UpdateTransaction.Apply(fullJob,_ => {}); Check(UpdateModel.Matches(mismatch.Root,mismatch.Next),"full-package repair fallback");
  var hash = Case("hash");
  Reject(() => UpdateTransaction.Apply(hash.Job with { PayloadHash = new string('0',64) },_ => {}),"corrupt payload hash");
  Check(UpdateModel.Matches(hash.Root,hash.Old),"corrupt download left original intact");
  var archive = Case("traversal");
  var evil = Path.Combine(folder,"traversal.zip");
  using (var zip = ZipFile.Open(evil,ZipArchiveMode.Create)) { using var writer = new StreamWriter(zip.CreateEntry("PolyPDF/../escaped.txt").Open()); writer.Write("invalid"); }
  Reject(() => UpdateTransaction.Apply(archive.Job with { PayloadPath = evil, PayloadHash = UpdateModel.Hash(evil) },_ => {}),"archive traversal");
  Check(UpdateModel.Matches(archive.Root,archive.Old) && !File.Exists(Path.Combine(folder,"escaped.txt")),"archive traversal left original intact");
  var locked = Case("locked");
  using (var fileLock = new FileStream(Path.Combine(locked.Root,"PolyPDF.dll"),FileMode.Open,FileAccess.Read,FileShare.None))
   Reject(() => UpdateTransaction.Apply(locked.Job,_ => {}),"locked installed file");
  Check(UpdateModel.Matches(locked.Root,locked.Old),"locked-file update left original intact");
  InterruptedUpdate(Case("interrupted"));
  Reject(() => UpdateModel.Validate(success.Next with { Files = [..success.Next.Files,success.Next.Files[0] with { Path = "polypdf.EXE" }] }),"case-duplicate manifest");
  foreach (var path in new[] { "../bad", "/bad", "C:/bad", "x/../bad", "x\\bad", "x:stream", "x./bad" }) Reject(() => UpdateModel.SafePath(folder,path),"unsafe path " + path);
  Check(UpdateModel.ParseVersion("0.10.0") > UpdateModel.ParseVersion("0.9.0"),"numeric version ordering");
  Reject(() => UpdateModel.ParseVersion("0.4.1-beta"),"prerelease version");
  Reject(() => UpdateService.ValidateAsset(new("bad.zip","https://example.com/bad.zip",10,new string('0',64)),"0.4.1"),"untrusted download origin");
  NetworkChecks().GetAwaiter().GetResult();
  UpdateStorageTests.Run(parent);
  File.WriteAllText(Path.Combine(parent,"update-tests.txt"),"PASS: verified file-delta apply, unchanged runtime reuse, owned-file deletion, foreign-file preservation, original backup, rollback after registration failure, forced helper termination and isolated backup restoration, modified-base rejection/full fallback, corrupt hashes, unsafe archive paths, file locks, duplicate paths, numeric versions, official download origins, public metadata and cancellation.");
 }
 internal static void PauseAfterReplacement(string jobPath)
 {
  var job = UpdateModel.Read<UpdateJob>(jobPath);
  UpdateTransaction.Apply(job,_ =>
  {
   File.WriteAllText(job.ResultPath+".interrupted","replacement reached; test can terminate this helper");
   Thread.Sleep(Timeout.Infinite);
  });
 }
 static void InterruptedUpdate(Fixture fixture)
 {
  var jobPath = Path.Combine(Path.GetDirectoryName(fixture.Job.ManifestPath)!,"interruption-job.json"); UpdateModel.Write(jobPath,fixture.Job);
  var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false,CreateNoWindow = true };
  start.ArgumentList.Add("--update-interruption-test"); start.ArgumentList.Add(jobPath);
  using var process = Process.Start(start) ?? throw new Exception("Could not start interruption test helper.");
  var reached = fixture.Job.ResultPath+".interrupted";
  try
  {
   var timeout = Stopwatch.StartNew();
   while (!File.Exists(reached) && !process.HasExited && timeout.Elapsed < TimeSpan.FromSeconds(20)) Thread.Sleep(50);
   Check(File.Exists(reached),"helper reached directory replacement");
  }
  finally { if (!process.HasExited) process.Kill(); process.WaitForExit(5000); }
  var backup = Directory.GetDirectories(Path.GetDirectoryName(fixture.Root)!,".PolyPDF-backup-*").Single();
  Check(UpdateModel.Matches(backup,fixture.Old) && !File.Exists(fixture.Job.ResultPath),"forced termination preserves backup and writes no success result");
  Directory.Move(fixture.Root,fixture.Root+"-interrupted"); Directory.Move(backup,fixture.Root);
  Check(UpdateModel.Matches(fixture.Root,fixture.Old) && File.ReadAllText(Path.Combine(fixture.Root,"personal.txt")) == "user-owned file to preserve","isolated interrupted-installation recovery");
 }
 sealed class Handler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> respond) : HttpMessageHandler
 { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation) => respond(request,cancellation); }
 static async Task NetworkChecks()
 {
  var version = UpdateModel.ParseVersion(AppInfo.Version); var next = new Version(version.Major,version.Minor,version.Build+1).ToString(3);
  using var service = new UpdateService(new Handler((request,_) =>
  {
   Check(request.Method == HttpMethod.Get && request.Headers.Authorization == null && request.RequestUri!.Host == "api.github.com","public metadata request");
   var body = JsonSerializer.Serialize(new { draft=false,prerelease=false,tag_name="v" + next,body="Release notes",assets=Array.Empty<object>() });
   return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
  }));
  Check((await service.CheckAsync())?.Version == next,"new-version metadata");
  using var cancelled = new UpdateService(new Handler((_,ct) => Task.FromCanceled<HttpResponseMessage>(ct)));
  using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
  try { await cancelled.CheckAsync(cancellation.Token); throw new Exception("Cancellation was ignored."); } catch (OperationCanceledException) { }
 }
}
