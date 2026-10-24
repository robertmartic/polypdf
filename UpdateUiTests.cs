using System.IO;
using System.Net.Http;

namespace PolyPDF;

internal static class UpdateUiTests
{
 internal static void Run(MainWindow owner,string folder)
 {
  foreach (var dark in new[] { false,true })
  {
   owner.SetPreviewTheme(dark);
   var attempts = 0; Exception? failure = null;
   var prepared = new PreparedUpdate("test-job.json","test-helper.exe");
   UpdateDialog? dialog = null;
   dialog = new UpdateDialog(owner,new("0.4.5","What's new\nClearer updates and storage cleanup.",[]),true,async token =>
   {
    attempts++;
    await Task.Yield();
    if (attempts == 1) throw new HttpRequestException("simulated offline");
    if (attempts == 2) { dialog!.CancelForTest(); token.ThrowIfCancellationRequested(); }
    return prepared;
   }) { ShowActivated = false,WindowStartupLocation = System.Windows.WindowStartupLocation.Manual,Left = -19000,Top = 0 };
   dialog.Loaded += async (_,_) =>
   {
    try
    {
     await dialog.AttemptForTest();
     if (!dialog.CanRetryForTest() || !dialog.StatusForTest.Text.Contains("internet connection")) throw new Exception("No actionable network error or retry.");
     await dialog.AttemptForTest();
     if (!dialog.CanRetryForTest() || !dialog.StatusForTest.Text.Contains("cancelled")) throw new Exception("Cancellation did not enable a fresh attempt.");
     await dialog.AttemptForTest();
    }
    catch (Exception ex) { failure = ex; dialog.Close(); }
   };
   var result = dialog.ShowDialog();
   if (failure != null) throw new Exception("Update dialog regression",failure);
   if (result != true || dialog.Prepared != prepared || attempts != 3) throw new Exception("Retry did not complete preparation.");
  }
  File.WriteAllText(Path.Combine(folder,"update-ui-tests.txt"),"PASS: real update dialogs in both themes; offline error, retry, cancellation and a successful subsequent preparation with a fresh cancellation token.");
 }
}
