using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace PolyPDF;

internal static class UpdateRunner
{
 internal static Process Start(PreparedUpdate prepared)
 {
  var start = new ProcessStartInfo(prepared.HelperPath) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(prepared.HelperPath)! };
  start.ArgumentList.Add("--apply-update"); start.ArgumentList.Add(prepared.JobPath);
  return Process.Start(start) ?? throw new IOException("The update helper could not start.");
 }
 internal static async Task RunAsync(string jobPath)
 {
  try
  {
   using var settings = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(UpdateModel.DataRoot,"settings.json")));
   if (settings.RootElement.TryGetProperty("Dark",out var dark)) ThemeManager.SetDark(dark.GetBoolean());
  }
  catch { }
  var panel = new StackPanel { Margin = new Thickness(24) };
  var status = new TextBlock { Text = "Waiting for PolyPDF to close…", TextWrapping = TextWrapping.Wrap };
  panel.Children.Add(status);
  var progressBar = new ProgressBar { IsIndeterminate = true, Height = 6, Margin = new Thickness(0,16,0,0) };
  progressBar.SetResourceReference(ProgressBar.ForegroundProperty,"AccentBrush");
  panel.Children.Add(progressBar);
  var window = new ThemedDialog { Title = "Updating PolyPDF", Width = 500, Height = 190, WindowStartupLocation = WindowStartupLocation.CenterScreen, Content = panel };
  var finished = false; window.Closing += (_,e) => e.Cancel = !finished; window.Show();
  UpdateJob? job = null; string nextVersion = ""; var installed = false;
  try
  {
   job = UpdateModel.Read<UpdateJob>(jobPath);
   using var storageGuard = UpdateStorage.Lock(UpdateModel.DataRoot);
   UpdateStorage.Mark(Path.GetDirectoryName(jobPath)!,job.InstallDir,"applying");
   if (!UpdateService.IsInstalled(job.InstallDir)) throw new IOException("The registered installation changed. No files were replaced.");
   nextVersion = UpdateModel.Read<UpdateManifest>(job.ManifestPath).Version;
   try { using var parent = Process.GetProcessById(job.ParentPid); using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60)); await parent.WaitForExitAsync(timeout.Token); }
   catch (ArgumentException) { }
   catch (OperationCanceledException) { throw new IOException("PolyPDF did not close in time. Close all PolyPDF windows, then check for updates and try again."); }
   using var updateLock = new Mutex(true,@"Local\PolyPDF.Update",out var created);
   if (!created) throw new IOException("Another update is already running.");
   if (Mutex.TryOpenExisting(@"Local\PolyPDF.Running",out var other)) { other.Dispose(); throw new IOException("Another PolyPDF window is open. Close it and try updating again."); }
   UpdateModel.VerifyHash(job.MaintenancePath,job.MaintenanceHash);
   using var uninstall = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\PolyPDF",true) ?? throw new IOException("The installed app registration is missing.");
   var oldVersion = uninstall.GetValue("DisplayVersion") as string;
   var oldSize = uninstall.GetValue("EstimatedSize");
   var progress = new Progress<string>(text => status.Text = text);
   string backup;
   try
   {
    backup = await Task.Run(() => UpdateTransaction.Apply(job,root =>
    {
     var start = new ProcessStartInfo(job.MaintenancePath) { UseShellExecute = false, CreateNoWindow = true };
     start.Arguments = "/S /D=" + root;
     using var maintenance = Process.Start(start) ?? throw new IOException("Installation registration could not start."); maintenance.WaitForExit();
     if (maintenance.ExitCode != 0 || (string?)uninstall.GetValue("DisplayVersion") != nextVersion) throw new IOException("Installation registration failed; restoring the previous version.");
    },text => ((IProgress<string>)progress).Report(text)));
   }
   catch
   {
    if (oldVersion != null) uninstall.SetValue("DisplayVersion",oldVersion);
    if (oldSize != null) uninstall.SetValue("EstimatedSize",oldSize,RegistryValueKind.DWord);
    throw;
   }
   installed = true;
   status.Text = "Update installed. Restarting PolyPDF…";
   UpdateModel.Write(job.ResultPath,new UpdateResult(true,job.FromVersion,nextVersion,job.Delta,job.DownloadBytes,job.FullBytes,"Update installed successfully.",backup));
   UpdateStorage.Mark(Path.GetDirectoryName(jobPath)!,job.InstallDir,"completed");
   // Closing this handle allows the verified app to restart; setup sees it while replacing files.
   updateLock.Dispose();
   Process.Start(new ProcessStartInfo(Path.Combine(job.InstallDir,"PolyPDF.exe")) { UseShellExecute = true, WorkingDirectory = job.InstallDir });
  }
  catch (Exception ex)
  {
   if (job != null && !installed)
   {
    try { UpdateModel.Write(job.ResultPath,new UpdateResult(false,job.FromVersion,nextVersion,job.Delta,job.DownloadBytes,job.FullBytes,UpdateMessages.Failure(ex))); } catch { }
   }
   if (job != null)
   {
    try { UpdateStorage.Mark(Path.GetDirectoryName(jobPath)!,job.InstallDir,installed ? "completed" : "failed"); } catch { }
   }
   var message = installed ? $"PolyPDF {nextVersion} was installed, but automatic restart or status recording could not finish. Open PolyPDF using your existing shortcut.\n\n" + UpdateMessages.Failure(ex) :
    "The update could not finish. Open PolyPDF using your existing shortcut and try again. If it cannot open, use the official installer to restore it.\n\n" + UpdateMessages.Failure(ex);
   ThemedMessageBox.Show(window,message,"PolyPDF update",MessageBoxButton.OK,MessageBoxImage.Warning);
  }
  finally { finished = true; window.Close(); }
 }
}
