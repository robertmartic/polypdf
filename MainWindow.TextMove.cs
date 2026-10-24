using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PolyPDF;

public partial class MainWindow
{
 List<(PdfDocument.AddedTextTarget Target,Rect Screen)> addedTextTargets = [];
 PdfDocument.AddedTextTarget? selectedAddedText;
 Rectangle? addedTextFrame;
 Rect addedTextBounds;
 bool movingAddedText;
 Vector addedTextOffset;
 Matrix moveTransform;

 void MoveTextClick(object sender,RoutedEventArgs e) => SetTool(Tool.MoveText,"Click PolyPDF-added text and drag to move. Esc cancels.");
 void DrawAddedTextFrame()
 {
  if (tool != Tool.MoveText) return;
  PageOverlay.Children.Clear(); addedTextFrame = null;
  if (selectedAddedText == null) return;
  var frame = addedTextBounds; frame.Offset(addedTextOffset); frame.Inflate(3,3);
  addedTextFrame = new Rectangle { Width = frame.Width,Height = frame.Height,StrokeThickness = 2,StrokeDashArray = new DoubleCollection { 4,2 },Fill = new SolidColorBrush(Color.FromArgb(18,65,105,225)),IsHitTestVisible = false };
  addedTextFrame.SetResourceReference(Shape.StrokeProperty,"AccentBrush");
  System.Windows.Controls.Canvas.SetLeft(addedTextFrame,frame.Left); System.Windows.Controls.Canvas.SetTop(addedTextFrame,frame.Top);
  PageOverlay.Children.Add(addedTextFrame);
 }
 void CancelTextMove(bool clearSelection = false)
 {
  if (movingAddedText) { movingAddedText = false; dragging = false; PageOverlay.ReleaseMouseCapture(); }
  addedTextOffset = new Vector();
  if (clearSelection) selectedAddedText = null;
  DrawAddedTextFrame();
 }
 void PageLostMouseCapture(object sender,MouseEventArgs e)
 {
  if (!movingAddedText) return;
  CancelTextMove(); Status.Text = "Move cancelled. The document is unchanged.";
 }
 void BeginTextMove(Point at)
 {
  CancelTextMove();
  if (Doc is not {} doc || !doc.CanEdit) { selectedAddedText = null; DrawAddedTextFrame(); Status.Text = "This document does not allow editing."; return; }
  // Reverse drawing order chooses the topmost supported text insertion.
  var hit = addedTextTargets.AsEnumerable().Reverse().FirstOrDefault(t =>
  {
   var bounds = t.Screen; bounds.Inflate(4,4); return bounds.Contains(at);
  });
  selectedAddedText = hit.Target;
  if (selectedAddedText == null) { DrawAddedTextFrame(); Status.Text = "No movable text here. Choose text added in PolyPDF."; return; }
  start = at; addedTextBounds = hit.Screen; moveTransform = deviceToPdf;
  dragging = movingAddedText = PageOverlay.CaptureMouse();
  DrawAddedTextFrame();
  Status.Text = "Added text selected · Drag the frame to move · Esc cancels";
 }
 void PreviewTextMove(Point at)
 {
  if (!movingAddedText) return;
  var requested = at-start;
  if (Math.Abs(requested.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(requested.Y) < SystemParameters.MinimumVerticalDragDistance) addedTextOffset = new Vector();
  else addedTextOffset = TextMoveGeometry.Clamp(addedTextBounds,requested,new Size(PageImage.Width,PageImage.Height));
  DrawAddedTextFrame();
 }
 async Task CommitTextMoveAsync(Point at)
 {
  if (!movingAddedText || selectedAddedText is not {} target || Doc is not {} doc) return;
  PreviewTextMove(at);
  var offset = addedTextOffset;
  movingAddedText = dragging = false; PageOverlay.ReleaseMouseCapture();
  addedTextOffset = new Vector();
  if (offset.Length < 0.01) { DrawAddedTextFrame(); return; }
  var delta = TextMoveGeometry.ToPdfOffset(start,offset,moveTransform);
  var changed = await ExecuteAsync("Moving text…",() => ChangeAsync(d => d.MoveAddedText(target,delta)));
  if (Doc != doc || tool != Tool.MoveText) return;
  var current = addedTextTargets.FirstOrDefault(t => t.Target.Key == target.Key && t.Target.Indices.SequenceEqual(target.Indices));
  selectedAddedText = current.Target; addedTextBounds = current.Screen;
  DrawAddedTextFrame();
  if (changed && doc.RevisionId != target.Revision) Status.Text = "Text moved · Ctrl+Z to undo";
 }
}
