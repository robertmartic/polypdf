using System.IO;
using System.Windows;
using MessageBox = PolyPDF.ThemedMessageBox;

namespace PolyPDF;
public partial class App : Application
{
 Mutex? runningMarker;
 protected override void OnExit(ExitEventArgs e)
 {
  runningMarker?.Dispose();
  base.OnExit(e);
 }
 protected override async void OnStartup(StartupEventArgs e)
 {
  base.OnStartup(e);
  DispatcherUnhandledException += (_, args) => { MessageBox.Show(args.Exception.Message, "PolyPDF", MessageBoxButton.OK, MessageBoxImage.Error); args.Handled = true; };
  try
  {
   if (e.Args.Length == 2 && e.Args[0] == "--apply-update") { await UpdateRunner.RunAsync(e.Args[1]); Shutdown(); return; }
   if (e.Args.Length == 2 && e.Args[0] == "--update-interruption-test") { UpdateTests.PauseAfterReplacement(e.Args[1]); Shutdown(); return; }
   PdfWorker.Initialize();
   if (e.Args.Length == 2 && e.Args[0] == "--recovery-crash-test") { RegressionTests.CreateCrashedSession(e.Args[1]); return; }
   if (e.Args.Contains("--self-test")) { SmokeTests.Run(); Shutdown(Environment.ExitCode); return; }
   if (e.Args.Contains("--ui-test"))
   {
    var preview=new MainWindow(true) { ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=0 }; MainWindow=preview;
    preview.ContentRendered+=async (_,_)=>{
     var folder=Path.Combine(AppContext.BaseDirectory,"test-results");Directory.CreateDirectory(folder);
     File.Delete(Path.Combine(folder,"ui-result.txt"));File.Delete(Path.Combine(folder,"ui-error.txt"));
     try { await preview.CapturePreviewAsync(Path.Combine(folder,"welcome.png"));await preview.OpenFileAsync(Path.Combine(folder,"verification.pdf"));await preview.VerifyDefaultFitAsync(folder);await preview.CapturePreviewAsync(Path.Combine(folder,"workspace.png"));await preview.CapturePreviewAsync(Path.Combine(folder,"dark.png"),true);await preview.CapturePreviewAsync(Path.Combine(folder,"annotate.png"),true,"Annotate");preview.Width=980;preview.Height=650;await preview.CapturePreviewAsync(Path.Combine(folder,"compact.png"),false,"Pages");
      await preview.VerifyUiRegressionsAsync();
       DialogThemeTests.Run(preview,folder);
       UpdateUiTests.Run(preview,folder);
       await preview.VerifyVersion022Async(folder);
       await preview.VerifyTextMoveUiAsync(folder);
      using var sample=new PdfDocument(File.ReadAllBytes(Path.Combine(folder,"verification.pdf")));
      var image=await PdfWorker.Run(() => sample.Render(0,2));
      if (!e.Args.Contains("--skip-ocr")) { var ocr=await OcrService.Recognize(image,"en-US");File.WriteAllText(Path.Combine(folder,"ocr.txt"),ocr); }
      else File.WriteAllText(Path.Combine(folder,"ocr.txt"),"SKIPPED: OCR is tested separately on a Windows desktop with the language pack installed.");
      File.WriteAllText(Path.Combine(folder,"ui-result.txt"),"PASS: annotation mode resets, masked password field, UI remains responsive during PDF work, version label, light/dark layouts." + (e.Args.Contains("--skip-ocr") ? " OCR skipped by explicit CI option." : " English OCR passed."));
     } catch(Exception ex){File.WriteAllText(Path.Combine(folder,"ui-error.txt"),ex.ToString());Environment.ExitCode=1;}
     Shutdown(Environment.ExitCode);
    };preview.Show();return;
   }
   // Keep a named handle alive so setup never replaces files while an app is open.
   // This does not prevent multiple PolyPDF instances.
   runningMarker = new Mutex(false,@"Local\PolyPDF.Running");
   if (Mutex.TryOpenExisting(@"Local\PolyPDF.Setup",out var setupMarker) || Mutex.TryOpenExisting(@"Local\PolyPDF.Update",out setupMarker))
   {
    setupMarker.Dispose();
    runningMarker.Dispose(); runningMarker = null;
    MessageBox.Show("PolyPDF setup is running. Wait for it to finish, then open PolyPDF again.","PolyPDF");
    Shutdown(); return;
   }
   var window = new MainWindow(); MainWindow = window; window.Show();
   await window.OfferRecoveryAsync();
   foreach (var path in e.Args.Where(File.Exists)) await window.OpenFileAsync(path);
   await window.StartUpdateChecksAsync();
  }
  catch (Exception ex) { MessageBox.Show("PolyPDF could not start. Keep pdfium.dll beside PolyPDF.exe.\n\n" + ex.Message, "PolyPDF"); Shutdown(1); }
 }
}
