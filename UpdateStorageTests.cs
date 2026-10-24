using System.IO;
using System.Net;
using System.Net.Http;

namespace PolyPDF;

internal static class UpdateStorageTests
{
 static void Check(bool condition,string message) { if (!condition) throw new Exception("Update storage regression: "+message); }
 internal static void Run(string parent)
 {
  var folder = Path.Combine(parent,"storage-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
  var root = Path.Combine(folder,"install"); Directory.CreateDirectory(root);
  var cache = Path.Combine(folder,"data","Updates"); Directory.CreateDirectory(cache);
  var data = Path.GetDirectoryName(cache)!; var now = DateTime.UtcNow;
  string Job(string state,int days,bool foreign = false)
  {
   var path = Path.Combine(cache,"job-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path);
   File.WriteAllText(Path.Combine(path,"payload.zip"),"test download");
   UpdateStorage.Mark(path,root,state,now.AddDays(-days));
   if (foreign) File.WriteAllText(Path.Combine(path,"personal.pdf"),"preserve");
   return path;
  }
  string Backup(int days,bool foreign = false)
  {
   var path = Path.Combine(folder,".PolyPDF-backup-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path);
   File.WriteAllText(Path.Combine(path,"PolyPDF.exe"),"old exe"); File.WriteAllText(Path.Combine(path,"PolyPDF.dll"),"old dll");
   var manifest = new UpdateManifest(1,"0.4.2",new[] { "PolyPDF.exe","PolyPDF.dll" }.Select(n => new UpdateFile(n,new FileInfo(Path.Combine(path,n)).Length,UpdateModel.Hash(Path.Combine(path,n)))).ToList());
   UpdateModel.Write(Path.Combine(path,UpdateModel.ManifestName),manifest);
   UpdateStorage.Mark(path,root,"backup",now.AddDays(-days));
   if (foreign) File.WriteAllText(Path.Combine(path,"personal.pdf"),"preserve");
   return path;
  }
  var expired = Job("completed",9); var cancelled = Job("abandoned",9); var failed = Job("failed",9);
  UpdateStorage.Mark(cancelled,root+Path.DirectorySeparatorChar,"abandoned",now.AddDays(-9));
  var recent = Job("completed",1); var ready = Job("ready",50); var active = Job("applying",50); var foreignJob = Job("completed",50,true);
  var old = Backup(80); var newest = Backup(45); var foreignBackup = Backup(90,true);
  var unmarked = Path.Combine(cache,"job-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(unmarked); File.WriteAllText(Path.Combine(unmarked,"personal.pdf"),"preserve");
  var otherRoot = Job("completed",50); UpdateStorage.Mark(otherRoot,Path.Combine(folder,"other-install"),"completed",now.AddDays(-50));
  var invalid = Job("completed",50); File.WriteAllText(Path.Combine(invalid,UpdateStorage.MarkerName),"invalid JSON");
  var summary = UpdateStorage.Inspect(root,cache,now);
  Check(summary.EligibleBytes > 0 && summary.TotalBytes > summary.EligibleBytes,"storage totals and retention");
  using (UpdateStorage.Lock(data))
  {
   var refused = false; try { UpdateStorage.Cleanup(root,cache,data,now); } catch (IOException) { refused = true; }
   Check(refused && Directory.Exists(expired),"concurrent operation blocks cleanup");
  }
  var removed = UpdateStorage.Cleanup(root,cache,data,now);
  Check(removed == summary.EligibleBytes,"cleanup reports actual eligible bytes");
  foreach (var path in new[] { expired,cancelled,failed,old }) Check(!Directory.Exists(path),"eligible folder removed");
  foreach (var path in new[] { root,recent,ready,active,foreignJob,newest,foreignBackup,unmarked,otherRoot,invalid }) Check(Directory.Exists(path),"protected folder retained");
  Check(File.ReadAllText(Path.Combine(foreignBackup,"personal.pdf")) == "preserve","foreign content preserved");
  var insufficient = false;
  try { UpdateStorage.EnsureSpace([(root,100),(cache,200)],_ => 64L*1024*1024+299); } catch (IOException) { insufficient = true; }
  Check(insufficient,"same-volume requirements are combined");
  UpdateStorage.EnsureSpace([(root,100),(cache,200)],_ => 64L*1024*1024+300);
  Check(UpdateMessages.Failure(new HttpRequestException("technical socket detail")).Contains("internet connection"),"offline message");
  Check(UpdateMessages.Failure(new HttpRequestException("rate limit",null,HttpStatusCode.TooManyRequests)).Contains("limiting"),"rate limit message");
  Check(UpdateMessages.Failure(new InvalidDataException()).Contains("verified"),"verification failure message");
  File.WriteAllText(Path.Combine(parent,"update-storage-tests.txt"),"PASS: cleanup retention, latest backup, active/prepared jobs, foreign files, unmarked/malformed folders, other installations, concurrent lock, reclaimed bytes, combined-volume disk requirements, offline/rate-limit/integrity messages.");
 }
}
