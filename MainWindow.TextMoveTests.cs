using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FilePath = System.IO.Path;

namespace PolyPDF;

public partial class MainWindow
{
 internal async Task VerifyTextMoveUiAsync(string folder)
 {
  void Check(bool condition,string message) { if (!condition) throw new Exception("Text move UI regression: "+message); }
  void StopRender() { renderTimer.Stop(); pendingRender = false; }
  static Point Center(Rect bounds) => new(bounds.X+bounds.Width/2,bounds.Y+bounds.Height/2);
  var path = FilePath.Combine(folder,"text-move-ui.pdf");
  var document = await PdfWorker.Run(() =>
  {
   var d = new PdfDocument(TextMoveTests.OriginalFixture());
   d.Change(() => { d.AddText(0,new Point(60,600),"Move this text\nSecond line",20,Colors.Blue); d.AddText(0,new Point(60,400),"Keep this block",16,Colors.Red); d.Blank(1); });
   d.Save(path); return d;
  });
  await AddDocumentAsync(document); StopRender();
  ShowTools(AnnotateTools); MoveTextClick(this,new RoutedEventArgs());
  UpdateLayout(); await RenderPageAsync(); StopRender();
  Check(MoveTextButton.IsChecked == true && addedTextTargets.Count == 2,"Move text action and cached supported blocks");
  var initial = addedTextTargets[0]; var revision = document.RevisionId;
  BeginTextMove(Center(initial.Screen));
  Check(selectedAddedText != null && addedTextFrame != null,"click shows a selection frame");
  await CommitTextMoveAsync(Center(initial.Screen));
  Check(document.RevisionId == revision && !document.Dirty,"click without dragging makes no edit");
  BeginTextMove(Center(initial.Screen));
  Check(movingAddedText,"page mouse capture");
  var requested = new Vector(36,24); PreviewTextMove(Center(initial.Screen)+requested);
  Check(addedTextOffset == requested && document.RevisionId == revision,"preview moves only the frame");
  await CommitTextMoveAsync(Center(initial.Screen)+requested); StopRender();
  var moved = addedTextTargets[0];
  Check(document.Dirty && document.RevisionId != revision && selectedAddedText != null,"drop commits one move and keeps selection");
  Check((Center(moved.Screen)-Center(initial.Screen)-requested).Length < 1,$"screen drop position: {initial.Screen} -> {moved.Screen}; requested {requested}; zoom {zoom}");
  await UndoAsync(false); StopRender();
  Check(!document.Dirty && (Center(addedTextTargets[0].Screen)-Center(initial.Screen)).Length < 1,"UI undo position and saved state");
  await UndoAsync(true); StopRender();
  Check(document.Dirty && (Center(addedTextTargets[0].Screen)-Center(moved.Screen)).Length < 1,"UI redo position");
  foreach (var darkTheme in new[] { false,true })
  {
   SetPreviewTheme(darkTheme); BeginTextMove(Center(addedTextTargets[0].Screen)); CancelTextMove(); UpdateLayout();
   await Dispatcher.InvokeAsync(() => {},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
   StopRender();
   Check(addedTextFrame != null && addedTextFrame.Stroke is SolidColorBrush brush && brush.Color.A > 0 && MoveTextButton.IsChecked == true,"visible frame and active action in both themes");
   DialogThemeTests.Save(this,FilePath.Combine(folder,$"move-text-{(darkTheme ? "dark" : "light")}.png"));
  }
  var beforeCancel = document.RevisionId;
  BeginTextMove(Center(addedTextTargets[0].Screen)); PreviewTextMove(Center(addedTextTargets[0].Screen)+new Vector(20,20));
  PageOverlay.ReleaseMouseCapture();
  Check(!movingAddedText && document.RevisionId == beforeCancel,"lost capture cancels without editing");
  BeginTextMove(Center(addedTextTargets[0].Screen)); PreviewTextMove(Center(addedTextTargets[0].Screen)+new Vector(20,20));
  SetTool(Tool.Select,"Drag to select text. Ctrl+F to search.");
  Check(!movingAddedText && selectedAddedText == null && MoveTextButton.IsChecked == false && document.RevisionId == beforeCancel,"Escape/tool-change action cancels pending move");
  MoveTextClick(this,new RoutedEventArgs());
  var pixelWidth = (int)PageImage.Width; var pixelHeight = (int)PageImage.Height;
  var originalPoint = await PdfWorker.Run(() => document.ToScreen(0,new Point(90,755),pixelWidth,pixelHeight));
  BeginTextMove(originalPoint); Check(selectedAddedText == null,"unmarked original text cannot be selected");
  var old = addedTextTargets[0]; BeginTextMove(Center(old.Screen));
  Zoom(1.2); StopRender(); await RenderPageAsync(); StopRender();
  Check(!movingAddedText && selectedAddedText != null && addedTextFrame != null,"zoom cancels drag and refreshes selection frame");
  await ExecuteAsync("Rotating test…",() => ChangeAsync(d => d.Rotate(0))); StopRender();
  var rotated = addedTextTargets[0]; var rotateOffset = new Vector(25,15);
  BeginTextMove(Center(rotated.Screen)); await CommitTextMoveAsync(Center(rotated.Screen)+rotateOffset); StopRender();
  Check((Center(addedTextTargets[0].Screen)-Center(rotated.Screen)-rotateOffset).Length < 1,"rotated page drag direction");
  BeginTextMove(Center(addedTextTargets[0].Screen)); await NavigateAsync(1); StopRender();
  Check(!movingAddedText && selectedAddedText == null,"page navigation clears selection and pending drag");
  await NavigateAsync(0); StopRender();
  var savedBounds = addedTextTargets[0].Target.Bounds;
  await SaveDocumentAsync(document); await CloseDocumentAsync((TabItem)Documents.SelectedItem); StopRender();
  await OpenFileAsync(path); StopRender();
  Check(addedTextTargets.Count == 2 && Math.Abs(addedTextTargets[0].Target.Bounds.X-savedBounds.X) < 0.03,"UI save/reopen retains position and editable identity");
  ShowTools(AnnotateTools); MoveTextClick(this,new RoutedEventArgs()); BeginTextMove(Center(addedTextTargets[0].Screen));
  Check(selectedAddedText != null && addedTextFrame != null,"reopened text can be selected");
  CancelTextMove(); await CloseDocumentAsync((TabItem)Documents.SelectedItem); StopRender();
  File.WriteAllText(FilePath.Combine(folder,"text-move-ui-tests.txt"),"PASS: real WPF selection frames and active mode in both themes; click without edit; mouse capture; preview without mutation; committed drop; Undo/Redo; lost-capture and tool-change cancellation; original text excluded; zoom and page changes; rotated drag; save/reopen and reselect.");
 }
}
