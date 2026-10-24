using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using FilePath = System.IO.Path;
using MessageBox = PolyPDF.ThemedMessageBox;

namespace PolyPDF;

public partial class MainWindow : Window
{
 enum Tool { Select, Text, Edit, Highlight, Draw, Comment, MoveText }
 Tool tool;
 PdfDocument? Doc => (Documents.SelectedItem as TabItem)?.Tag as PdfDocument;
 double zoom = 1;
 int pageCount;
 bool refreshing, dark, busy, dragging, closing, allowClose, pendingRender;
 PageFitMode fitMode = PageFitMode.Height;
 readonly bool previewMode;
 Point start;
 Matrix deviceToPdf = Matrix.Identity;
 readonly List<Point> stroke = [];
 readonly HashSet<PdfDocument> acknowledgedSignatures = [];
 Rectangle? selection;
 Polyline? ink;
 List<(PdfDocument.Glyph Glyph, Rect Screen)> screenGlyphs = [];
 string selectedText = "";
 int lastSearchPage = -1, lastSearchOffset = -1;
 string lastQuery = "";
 readonly string settingsPath = FilePath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PolyPDF", "settings.json");
 List<string> recent = [];
 sealed record Settings(bool Dark, List<string> Recent);
 readonly DispatcherTimer renderTimer = new() { Interval = TimeSpan.FromMilliseconds(150) };
 readonly DispatcherTimer recoveryTimer = new() { Interval = TimeSpan.FromSeconds(30) };
 readonly DispatcherTimer recoveryDebounce = new() { Interval = TimeSpan.FromSeconds(3) };
 RecoveryStore? recovery;
 bool recoveryWarningShown;
 AvailableUpdate? availableUpdate;
 PreparedUpdate? pendingUpdate;
 bool checkingUpdates;

 public MainWindow(bool preview = false)
 {
  previewMode = preview;
  InitializeComponent();
  ThemeManager.Attach(this);
  SoftwareVersion.Text = $"v{AppInfo.Version}";
  Title = AppInfo.DisplayName;
  try
  {
   if (!previewMode && File.Exists(settingsPath))
   {
    var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(settingsPath));
    if (settings != null) { recent = settings.Recent.Where(File.Exists).Take(8).ToList(); if (settings.Dark) ToggleTheme(); }
   }
  }
  catch { }
  RecentFiles.ItemsSource = recent;
  Toolbar.IsEnabled = SaveButton.IsEnabled = false;
  UpdateFitControls();
  PreviewKeyDown += Shortcuts;
  PageScroll.SizeChanged += (_, _) => { if (fitMode != PageFitMode.Manual && Doc != null) ScheduleRender(); };
  renderTimer.Tick += async (_, _) =>
  {
   renderTimer.Stop();
   if (busy) { pendingRender = true; return; }
   await ExecuteAsync("Rendering page…", RenderPageAsync);
  };
  recoveryDebounce.Tick += async (_, _) => { recoveryDebounce.Stop(); await CheckpointAsync(); };
  recoveryTimer.Tick += async (_, _) => await CheckpointAsync();
  if (!previewMode) recoveryTimer.Start();
 }

 async Task<bool> ExecuteAsync(string message, Func<Task> action)
 {
  if (busy) return false;
  SetBusy(true);
  Status.Text = message;
  try { await action(); return true; }
  catch (Exception ex) { Report(ex); return false; }
  finally
  {
   SetBusy(false);
   if (pendingRender && !closing) { pendingRender = false; ScheduleRender(); }
  }
 }
 void Report(Exception ex)
 {
  MessageBox.Show(this, ex.Message, "PolyPDF", MessageBoxButton.OK, MessageBoxImage.Warning);
  Status.Text = "The operation was not completed.";
 }
 void SetBusy(bool value)
 {
  if (value) CancelTextMove();
  busy = value;
  FileActions.IsEnabled = Documents.IsEnabled = Workspace.IsEnabled = !value;
  Toolbar.IsEnabled = !value && Doc != null;
  SaveButton.IsEnabled = !value && Doc != null;
  UpdateFitControls();
  Mouse.OverrideCursor = value ? Cursors.AppStarting : null;
 }
 IEnumerable<PdfDocument> OpenDocuments => Documents.Items.Cast<TabItem>().Select(t => (PdfDocument)t.Tag);

 async void OpenClick(object sender, RoutedEventArgs e)
 {
  if (busy) return;
  var picker = new OpenFileDialog { Filter = "PDF documents (*.pdf)|*.pdf", Multiselect = true };
  if (picker.ShowDialog(this) == true) foreach (var path in picker.FileNames) await OpenFileAsync(path);
 }
 public Task OpenFileAsync(string path) => ExecuteAsync("Opening PDF…", async () =>
 {
  path = FilePath.GetFullPath(path);
  foreach (TabItem tab in Documents.Items)
   if (tab.Tag is PdfDocument open && string.Equals(open.FilePath, path, StringComparison.OrdinalIgnoreCase))
   { Documents.SelectedItem = tab; fitMode = PageFitMode.Height; ResetSearch(); SetTool(Tool.Select,"Drag to select text. Ctrl+F to search."); await RefreshDocumentAsync(); return; }
  var bytes = await File.ReadAllBytesAsync(path);
  PdfDocument document;
  try { document = await PdfWorker.Run(() => new PdfDocument(bytes, path)); }
  catch (InvalidOperationException ex) when (ex.Message.Contains("password"))
  {
   var password = PasswordDialog.Ask(this);
   if (password is null) return;
   document = await PdfWorker.Run(() => new PdfDocument(bytes, path, password));
  }
  var count = await PdfWorker.Run(() => document.Count);
  if (count == 0) { await PdfWorker.Run(document.Dispose); throw new InvalidOperationException("This PDF has no pages."); }
  await AddDocumentAsync(document);
  recent.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
  recent.Insert(0, path); recent = recent.Take(8).ToList(); RecentFiles.ItemsSource = recent; SaveSettings();
  if (await PdfWorker.Run(() => document.HasSignatures)) Status.Text = "Signed PDF · Saving edits changes the signed content.";
 });

 async Task AddDocumentAsync(PdfDocument doc)
 {
  var tab = new TabItem { Tag = doc };
  var header = new StackPanel { Orientation = Orientation.Horizontal };
  header.Children.Add(new TextBlock { Text = doc.Name, VerticalAlignment = VerticalAlignment.Center });
  var close = new Button { Content = "×", Padding = new Thickness(7,0,7,1), Margin = new Thickness(12,0,0,0), ToolTip = "Close document" };
  close.Click += async (_, e) => { e.Handled = true; await ExecuteAsync("Closing document…", () => CloseDocumentAsync(tab)); };
  header.Children.Add(close); tab.Header = header;
  refreshing = true;
  try { Documents.Items.Add(tab); Documents.SelectedItem = tab; }
  finally { refreshing = false; }
  fitMode = PageFitMode.Height;
  ResetSearch(); SetTool(Tool.Select, "Drag to select text. Ctrl+F to search.");
  await RefreshDocumentAsync(); ScheduleRecovery();
 }

 void UpdateTitle()
 {
  foreach (TabItem tab in Documents.Items)
  {
   var doc = (PdfDocument)tab.Tag;
   ((TextBlock)((StackPanel)tab.Header).Children[0]).Text = doc.Name + (doc.Dirty ? " •" : "");
  }
  Title = Doc is {} selected ? selected.Name + (selected.Dirty ? " *" : "") + " — " + AppInfo.DisplayName : AppInfo.DisplayName;
 }
 async void DocumentChanged(object sender, SelectionChangedEventArgs e)
 {
  if (!ReferenceEquals(e.Source, Documents) || refreshing || busy) return;
  await ExecuteAsync("Opening document tab…", async () =>
  { fitMode = PageFitMode.Height; ResetSearch(); SetTool(Tool.Select, "Drag to select text. Ctrl+F to search."); await RefreshDocumentAsync(); });
 }

 // WPF requests previews only for realised list items. Images are rendered on the
 // PDF worker and frozen before they reach the UI thread.
 sealed class PagePreview(PdfDocument document, int index) : INotifyPropertyChanged
 {
  BitmapSource? preview;
  bool requested;
  public string Label => (index + 1).ToString();
  public BitmapSource? Preview { get { if (!requested) { requested = true; _ = LoadAsync(); } return preview; } }
  public event PropertyChangedEventHandler? PropertyChanged;
  async Task LoadAsync()
  {
   try
   {
    preview = await PdfWorker.Run(() => document.Render(index, 108 / document.Size(index).Width));
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Preview)));
   }
   catch (ObjectDisposedException) { }
   catch (InvalidOperationException) { }
  }
 }

 async Task RefreshDocumentAsync()
 {
  refreshing = true;
  try
  {
   var doc = Doc;
   Workspace.Visibility = doc == null ? Visibility.Collapsed : Visibility.Visible;
   Welcome.Visibility = doc == null ? Visibility.Visible : Visibility.Collapsed;
   UpdateTitle();
   if (doc == null)
   {
    CancelTextMove(true); addedTextTargets.Clear();
    pageCount = 0; Thumbnails.ItemsSource = null; PageImage.Source = null; PageOverlay.Children.Clear();
    screenGlyphs.Clear(); selectedText = ""; PageNumberBox.Text = "0"; PageCountLabel.Text = "/ 0";
    Status.Text = "Ready · Local processing"; return;
   }
   pageCount = await PdfWorker.Run(() => doc.Count);
   doc.CurrentPage = Math.Clamp(doc.CurrentPage, 0, pageCount - 1);
   Thumbnails.ItemsSource = Enumerable.Range(0, pageCount).Select(i => new PagePreview(doc, i)).ToList();
   Thumbnails.SelectedIndex = doc.CurrentPage;
   PageCountLabel.Text = "/ " + pageCount;
   await RenderPageAsync();
  }
  finally { refreshing = false; }
 }
 async Task RenderPageAsync()
 {
  if (Doc is not {} doc) return;
  CancelTextMove();
  var previousSelection = selectedAddedText is {} previous && previous.DocumentId == doc.RecoveryId && previous.Page == doc.CurrentPage && previous.Revision == doc.RevisionId ? previous : null;
  var page = doc.CurrentPage;
  // Opening the first document makes a collapsed workspace visible. Arrange it
  // before measuring; an unarranged viewport otherwise produces a near-zero zoom.
  UpdateLayout();
  if (PageScroll.ActualWidth <= 56 + SystemParameters.VerticalScrollBarWidth + 2 ||
      PageScroll.ActualHeight <= 56 + SystemParameters.HorizontalScrollBarHeight + 2) return;
  // Reserve the page's 28-DIP margin and scrollbar room on each axis so fitting
  // does not oscillate when a scrollbar appears/disappears.
  var available = new Size(Math.Max(1,PageScroll.ActualWidth-56-SystemParameters.VerticalScrollBarWidth-2),Math.Max(1,PageScroll.ActualHeight-56-SystemParameters.HorizontalScrollBarHeight-2));
  var requestedZoom = zoom; var mode = fitMode;
  var result = await PdfWorker.Run(() =>
  {
   var size = doc.Size(page);
   var scale = PageFitting.Scale(size,available,mode,requestedZoom);
   var bitmap = doc.Render(page, scale);
   var w = bitmap.PixelWidth; var h = bitmap.PixelHeight;
   var origin = doc.ToPdf(page, new Point(0,0), w,h);
   var x = doc.ToPdf(page, new Point(w,0), w,h); var y = doc.ToPdf(page, new Point(0,h), w,h);
   var transform = new Matrix((x.X-origin.X)/w, (x.Y-origin.Y)/w, (y.X-origin.X)/h, (y.Y-origin.Y)/h, origin.X, origin.Y);
   return (Bitmap: bitmap, Glyphs: doc.ScreenGlyphs(page,w,h), Transform: transform, Scale: scale,AddedText: doc.AddedTextTargets(page));
  });
  if (Doc != doc || doc.CurrentPage != page) return;
  zoom = result.Scale; deviceToPdf = result.Transform; screenGlyphs = result.Glyphs;
  PageImage.Source = result.Bitmap;
  PageImage.Width = PageOverlay.Width = result.Bitmap.PixelWidth; PageImage.Height = PageOverlay.Height = result.Bitmap.PixelHeight;
  PageOverlay.Children.Clear(); selectedText = "";
  addedTextTargets = result.AddedText.Select(t => (t,TextMoveGeometry.ToScreen(t.Bounds,deviceToPdf))).ToList();
  var currentSelection = addedTextTargets.FirstOrDefault(t => previousSelection != null && t.Target.Key == previousSelection.Key && t.Target.Indices.SequenceEqual(previousSelection.Indices));
  selectedAddedText = currentSelection.Target; addedTextBounds = currentSelection.Screen; DrawAddedTextFrame();
  PageOverlay.Cursor = ToolCursor();
  PageNumberBox.Text = (page+1).ToString(); ZoomLabel.Text = $"{zoom:P0}";
  UpdateFitControls();
  Status.Text = doc.Name + " · " + pageCount + " pages";
 }
 void ScheduleRender() { renderTimer.Stop(); renderTimer.Start(); }
 async void PageChanged(object sender, SelectionChangedEventArgs e)
 {
  if (refreshing || busy || Thumbnails.SelectedIndex < 0 || Doc == null) return;
  var index = Thumbnails.SelectedIndex;
  await ExecuteAsync("Rendering page…", () => NavigateAsync(index));
 }
 async Task NavigateAsync(int index)
 {
  if (Doc is not {} doc) return;
  doc.CurrentPage = Math.Clamp(index,0,pageCount-1);
  refreshing = true;
  try { Thumbnails.SelectedIndex = doc.CurrentPage; Thumbnails.ScrollIntoView(Thumbnails.SelectedItem); }
  finally { refreshing = false; }
  await RenderPageAsync();
 }
 async void NewClick(object s, RoutedEventArgs e) => await ExecuteAsync("Creating PDF…", async () => await AddDocumentAsync(await PdfWorker.Run(PdfDocument.Create)));

 async Task<bool> SaveDocumentAsync(PdfDocument doc, bool choose = false)
 {
  if (doc.SourceEncrypted && MessageBox.Show(this, "The saved PDF will not have password protection. Save an unencrypted copy?", "Password protection", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return false;
  string? path = doc.FilePath;
  if (choose || path is null)
  {
   var picker = new SaveFileDialog { Filter = "PDF document (*.pdf)|*.pdf", FileName = doc.Name, DefaultExt = ".pdf", AddExtension = true };
   if (picker.ShowDialog(this) != true) return false;
   path = picker.FileName;
  }
  if (OpenDocuments.Any(other => other != doc && string.Equals(other.FilePath,path,StringComparison.OrdinalIgnoreCase)))
   throw new InvalidOperationException("That file is open in another tab. Choose a different filename.");
  await PdfWorker.Run(() => doc.Save(path));
  await ForgetRecoveryAsync(doc);
  UpdateTitle(); Status.Text = "Saved · " + path; return true;
 }
 async void SaveClick(object s, RoutedEventArgs e) => await ExecuteAsync("Saving PDF…", async () => { if (Doc is {} doc) await SaveDocumentAsync(doc); });
 async void SaveAsClick(object s, RoutedEventArgs e) => await ExecuteAsync("Saving PDF…", async () => { if (Doc is {} doc) await SaveDocumentAsync(doc,true); });
 async Task<bool> MayCloseAsync(PdfDocument doc)
 {
  if (!doc.Dirty) return true;
  var answer = MessageBox.Show(this, "Save changes to " + doc.Name + "?", "Unsaved changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
  return answer == MessageBoxResult.No || answer == MessageBoxResult.Yes && await SaveDocumentAsync(doc);
 }
 async Task CloseDocumentAsync(TabItem tab)
 {
  var doc = (PdfDocument)tab.Tag;
  if (!await MayCloseAsync(doc)) return;
  await ForgetRecoveryAsync(doc);
  refreshing = true; try { Documents.Items.Remove(tab); } finally { refreshing = false; }
  acknowledgedSignatures.Remove(doc);
  await PdfWorker.Run(doc.Dispose);
  ResetSearch(); SetTool(Tool.Select, "Drag to select text. Ctrl+F to search."); await RefreshDocumentAsync();
 }
 async void WindowClosing(object? sender, CancelEventArgs e)
 {
  if (allowClose) return;
  if (previewMode) { renderTimer.Stop(); foreach (var doc in OpenDocuments) doc.Dispose(); return; }
  e.Cancel = true;
  if (busy || closing) { Status.Text = "Please wait for the current operation before closing."; return; }
  closing = true;
  var accepted = false;
  await ExecuteAsync("Closing workspace…", async () =>
  {
   foreach (var doc in OpenDocuments) if (!await MayCloseAsync(doc)) return;
     if (pendingUpdate != null) UpdateRunner.Start(pendingUpdate);
   recoveryTimer.Stop(); recoveryDebounce.Stop(); renderTimer.Stop(); SaveSettings();
   foreach (var doc in OpenDocuments) { await ForgetRecoveryAsync(doc); await PdfWorker.Run(doc.Dispose); }
   if (recovery != null) { await Task.Run(recovery.Dispose); recovery = null; }
   accepted = true;
  });
  closing = false;
   if (accepted) { allowClose = true; Close(); }
   else { if (pendingUpdate != null) UpdateStorage.Abandon(pendingUpdate); pendingUpdate = null; }
 }

 async Task ChangeAsync(Action<PdfDocument> action)
 {
  if (Doc is not {} doc) return;
  if (!acknowledgedSignatures.Contains(doc) && await PdfWorker.Run(() => doc.HasSignatures))
  {
   if (MessageBox.Show(this,"Editing this document changes its digitally signed content. Continue?","Signed document",MessageBoxButton.YesNo,MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
   acknowledgedSignatures.Add(doc);
  }
  await PdfWorker.Run(() => doc.Change(() => action(doc)));
  ResetSearch(); await RefreshDocumentAsync(); ScheduleRecovery();
 }
 async void UndoClick(object s, RoutedEventArgs e) => await UndoAsync(false);
 async void RedoClick(object s, RoutedEventArgs e) => await UndoAsync(true);
 async Task UndoAsync(bool redo) => await ExecuteAsync(redo ? "Redoing…" : "Undoing…", async () =>
 { if (Doc is {} doc && await PdfWorker.Run(() => doc.Undo(redo))) { ResetSearch(); await RefreshDocumentAsync(); ScheduleRecovery(); } });
 void ShowTools(FrameworkElement panel)
 {
  // Leaving annotation tools always returns to a non-editing mode.
  if (panel != AnnotateTools) SetTool(Tool.Select,"Drag to select text. Ctrl+F to search.");
  ViewTools.Visibility = AnnotateTools.Visibility = PageTools.Visibility = Visibility.Collapsed; panel.Visibility = Visibility.Visible;
  ViewTab.IsChecked = panel == ViewTools;
  AnnotateTab.IsChecked = panel == AnnotateTools;
  PagesTab.IsChecked = panel == PageTools;
 }
 void ViewToolsClick(object s, RoutedEventArgs e) { if (ViewTools != null) ShowTools(ViewTools); }
 void AnnotateToolsClick(object s, RoutedEventArgs e) { if (AnnotateTools != null) ShowTools(AnnotateTools); }
 void PageToolsClick(object s, RoutedEventArgs e) { if (PageTools != null) ShowTools(PageTools); }
 Cursor ToolCursor() => tool == Tool.MoveText ? Cursors.SizeAll : tool is Tool.Select or Tool.Highlight or Tool.Text ? Cursors.IBeam : Cursors.Cross;
 void SetTool(Tool value, string hint)
 {
  var hadTextSelection = tool == Tool.MoveText && selectedAddedText != null;
  var cancelledMove = movingAddedText;
  CancelTextMove(true);
  dragging = false; PageOverlay.ReleaseMouseCapture(); tool = value; ToolHint.Text = hint;
  if (MoveTextButton != null) MoveTextButton.IsChecked = value == Tool.MoveText;
  PageOverlay.Cursor = ToolCursor(); PageOverlay.Children.Clear(); selectedText = "";
  if (hadTextSelection) Status.Text = cancelledMove ? "Move cancelled. The document is unchanged." : "Text selection cleared.";
 }
 void SelectClick(object s, RoutedEventArgs e) => SetTool(Tool.Select,"Drag over text to select, then Ctrl+C to copy.");
 void AddTextClick(object s, RoutedEventArgs e) => SetTool(Tool.Text,"Click the page to add text at that position.");
 void EditTextClick(object s, RoutedEventArgs e) => SetTool(Tool.Edit,"Click an existing text block. Embedded fonts may limit replacement.");
 void HighlightClick(object s, RoutedEventArgs e) => SetTool(Tool.Highlight,"Drag over existing text to highlight it.");
 void DrawClick(object s, RoutedEventArgs e) => SetTool(Tool.Draw,"Drag on the page to draw. Use Undo to remove the stroke.");
 void CommentClick(object s, RoutedEventArgs e) => SetTool(Tool.Comment,"Click the page to place a comment.");
 Color InkColor => ColorChoice.SelectedIndex switch { 1=>Colors.Black, 2=>Color.FromRgb(210,50,60), 3=>Color.FromRgb(25,145,85), _=>Color.FromRgb(55,95,215) };
 Point PdfPoint(Point point) => deviceToPdf.Transform(point);

 async void PageMouseDown(object s, MouseButtonEventArgs e)
 {
  if (busy || Doc is not {} doc) return;
  Keyboard.Focus(PageOverlay); start = e.GetPosition(PageOverlay);
  if (tool == Tool.MoveText) { BeginTextMove(start); e.Handled = true; return; }
  if (tool == Tool.Select && !doc.CanCopy) { Report(new InvalidOperationException("This PDF does not allow copying text.")); return; }
  if (tool is Tool.Text or Tool.Edit or Tool.Comment)
  {
   var at = PdfPoint(start); var color = InkColor; var page = doc.CurrentPage;
   await ExecuteAsync("Editing PDF…", async () =>
   {
    if (tool == Tool.Text)
    {
     if (!float.TryParse(FontSizeBox.Text,out var size) || size < 4 || size > 200) throw new InvalidOperationException("Choose a text size between 4 and 200.");
     var family = ((ComboBoxItem)FontChoice.SelectedItem).Content.ToString()!;
     var text = TextDialog.Ask(this,"Add text","Text is placed at the selected position. Choose font, size and colour in the toolbar.","",true);
     if (!string.IsNullOrWhiteSpace(text)) await ChangeAsync(d => d.AddText(page,at,text,size,color,family));
    }
    else if (tool == Tool.Edit)
    {
     var target = await PdfWorker.Run(() => doc.HitText(page,at));
     if (target == null) throw new InvalidOperationException("No editable text block here. Scans and text inside nested forms cannot be edited with this tool.");
     var text = TextDialog.Ask(this,"Edit text block","The existing font and position are kept. Longer text may overlap neighbouring content.",target.Text);
     if (text != null && text != target.Text) await ChangeAsync(d => d.ReplaceText(page,target,text));
    }
    else
    {
     var text = TextDialog.Ask(this,"Add comment","This note will be saved as a PDF comment.","",true);
     if (!string.IsNullOrWhiteSpace(text)) await ChangeAsync(d => d.Comment(page,at,text));
    }
   });
   return;
  }
  PageOverlay.Children.Clear(); selectedText = ""; dragging = true; PageOverlay.CaptureMouse();
  if (tool == Tool.Draw)
  {
   stroke.Clear(); stroke.Add(start);
   ink = new Polyline { Stroke = new SolidColorBrush(InkColor), StrokeThickness = 2, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
   ink.Points.Add(start); PageOverlay.Children.Add(ink);
  }
  else
  {
   selection = new Rectangle { Fill = new SolidColorBrush(Color.FromArgb(48,65,105,225)), Stroke = new SolidColorBrush(Color.FromArgb(130,65,105,225)), StrokeThickness = 1 };
   PageOverlay.Children.Add(selection); Canvas.SetLeft(selection,start.X); Canvas.SetTop(selection,start.Y);
  }
 }
 void PageMouseMove(object s, MouseEventArgs e)
 {
  if (!dragging) return; var at = e.GetPosition(PageOverlay);
  if (tool == Tool.MoveText) { PreviewTextMove(at); return; }
  if (tool == Tool.Draw) { stroke.Add(at); ink?.Points.Add(at); }
  else if (selection != null)
  { Canvas.SetLeft(selection,Math.Min(at.X,start.X)); Canvas.SetTop(selection,Math.Min(at.Y,start.Y)); selection.Width = Math.Abs(at.X-start.X); selection.Height = Math.Abs(at.Y-start.Y); }
 }
 async void PageMouseUp(object s, MouseButtonEventArgs e)
 {
  if (!dragging || Doc is not {} doc) return;
  if (tool == Tool.MoveText) { e.Handled = true; await CommitTextMoveAsync(e.GetPosition(PageOverlay)); return; }
  dragging = false; PageOverlay.ReleaseMouseCapture(); var end = e.GetPosition(PageOverlay);
  if (tool == Tool.Draw)
  {
   var points = stroke.Select(PdfPoint).ToList(); var color = InkColor; var page = doc.CurrentPage;
   if (points.Count > 1) await ExecuteAsync("Saving stroke…", () => ChangeAsync(d => d.Draw(page,points,color)));
   return;
  }
  var region = new Rect(start,end);
  var selected = screenGlyphs.Where(g => region.Contains(new Point(g.Screen.X+g.Screen.Width/2,g.Screen.Y+g.Screen.Height/2))).ToList();
  selectedText = string.Concat(selected.Select(g => g.Glyph.Text));
  if (tool == Tool.Highlight)
  {
   if (selected.Count > 0)
   {
    var boxes = selected.Select(g => g.Glyph.Bounds).ToList(); var page = doc.CurrentPage;
    await ExecuteAsync("Highlighting text…", () => ChangeAsync(d => d.Highlight(page,boxes)));
   }
   else { PageOverlay.Children.Clear(); Status.Text = "No text selected. OCR can read scanned pages."; }
  }
  else { DrawBoxes(selected.Select(g => g.Screen),Color.FromArgb(70,65,105,225)); Status.Text = selected.Count + " characters selected · Ctrl+C to copy"; }
 }
 void DrawBoxes(IEnumerable<Rect> boxes, Color color)
 {
  PageOverlay.Children.Clear();
  var geometry = new GeometryGroup();
  foreach (var rect in boxes) geometry.Children.Add(new RectangleGeometry(new Rect(rect.X,rect.Y,Math.Max(1,rect.Width),Math.Max(1,rect.Height))));
  geometry.Freeze();
  PageOverlay.Children.Add(new System.Windows.Shapes.Path { Data = geometry, Fill = new SolidColorBrush(color), IsHitTestVisible = false });
 }
 async void CopyClick(object s, RoutedEventArgs e) => await ExecuteAsync("Reading page text…", async () =>
 {
  if (Doc is not {} doc) return;
  if (!doc.CanCopy) throw new InvalidOperationException("This PDF does not allow copying text.");
  var page = doc.CurrentPage; var text = await PdfWorker.Run(() => doc.Text(page));
  if (text.Length > 0) { Clipboard.SetText(text); Status.Text = "Page text copied."; } else Status.Text = "No selectable text. Try OCR page.";
 });
 async void CommentsClick(object s, RoutedEventArgs e) => await ExecuteAsync("Reading comments…", async () =>
 {
  if (Doc is not {} doc) return;
  var page = doc.CurrentPage; var comments = await PdfWorker.Run(() => doc.Comments(page));
  new TextDialog(this,"Page comments","Comments on page " + (page+1),comments.Count == 0 ? "No comments on this page." : string.Join("\n\n",comments.Select((c,i) => (i+1) + ". " + c)),true,true).ShowDialog();
 });
 void ResetSearch() { lastSearchPage = lastSearchOffset = -1; lastQuery = ""; }
 async void SearchClick(object s, RoutedEventArgs e) => await ExecuteAsync("Searching PDF…", async () =>
 {
  if (Doc is not {} doc || string.IsNullOrWhiteSpace(SearchBox.Text)) return;
  if (!doc.CanCopy) throw new InvalidOperationException("This PDF does not allow text extraction.");
  var query = SearchBox.Text; var first = doc.CurrentPage; var count = pageCount;
  var previousPage = lastSearchPage; var previousOffset = lastSearchOffset; var previousQuery = lastQuery;
  var match = await PdfWorker.Run<(int Page,int Offset)?>(() =>
  {
   for (int n = 0; n <= count; n++)
   {
    var page = (first+n)%count; var text = string.Concat(doc.Glyphs(page).Select(g => g.Text));
    var from = n == 0 && page == previousPage && query == previousQuery ? previousOffset+1 : 0;
    var found = text.IndexOf(query,Math.Clamp(from,0,text.Length),StringComparison.OrdinalIgnoreCase);
    if (found >= 0) return (page,found);
   }
   return null;
  });
  if (match is not {} result) { Status.Text = "No matches for “" + query + "”."; return; }
  await NavigateAsync(result.Page);
  var rectangles = new List<Rect>(); int charOffset = 0;
  foreach (var glyph in screenGlyphs)
  {
   if (charOffset >= result.Offset && charOffset < result.Offset+query.Length) rectangles.Add(glyph.Screen);
   charOffset += glyph.Glyph.Text.Length;
  }
  DrawBoxes(rectangles,Color.FromArgb(120,255,205,45));
  if (rectangles.Count > 0) PageScroll.ScrollToVerticalOffset(Math.Max(0,rectangles[0].Y-160));
  lastSearchPage = result.Page; lastSearchOffset = result.Offset; lastQuery = query;
  Status.Text = "Found on page " + (result.Page+1);
 });
 void SearchKeyDown(object s, KeyEventArgs e) { if (e.Key == Key.Enter) { SearchClick(s,e); e.Handled = true; } }
 async void OcrClick(object s, RoutedEventArgs e) => await ExecuteAsync("Recognising page text locally…", async () =>
 {
  if (Doc is not {} doc) return;
  if (!doc.CanCopy) throw new InvalidOperationException("This PDF does not allow text extraction.");
  var language = TextDialog.Ask(this,"Recognise page text","Language code: en-US (English) or hr-HR (Croatian). Requires the matching Windows OCR language pack.","en-US");
  if (language is null) return;
  var page = doc.CurrentPage;
  var image = await PdfWorker.Run(() => { var size = doc.Size(page); return doc.Render(page,Math.Min(2.5,2500/Math.Max(size.Width,size.Height))); });
  var text = await OcrService.Recognize(image,language.Trim());
  new TextDialog(this,"Recognised text","Review the result before copying; recognition may contain errors.",text.Length > 0 ? text : "No text detected.",true,true).ShowDialog();
  Status.Text = "OCR complete · No document changes";
 });
 async void RotateClick(object s, RoutedEventArgs e) => await ExecuteAsync("Rotating page…", () => ChangeAsync(d => d.Rotate(d.CurrentPage)));
 async void BlankClick(object s, RoutedEventArgs e) => await ExecuteAsync("Adding page…", () => ChangeAsync(d => { d.Blank(d.CurrentPage+1); d.CurrentPage++; }));
 async void DeleteClick(object s, RoutedEventArgs e) => await ExecuteAsync("Deleting page…", async () =>
 { if (Doc != null && MessageBox.Show(this,"Delete page " + (Doc.CurrentPage+1) + "? You can undo this change.","Delete page",MessageBoxButton.YesNo) == MessageBoxResult.Yes) await ChangeAsync(d => d.Delete(d.CurrentPage)); });
 async void MoveLeftClick(object s, RoutedEventArgs e) => await ExecuteAsync("Moving page…", async () =>
 { if (Doc is {} doc && doc.CurrentPage > 0) await ChangeAsync(d => { d.Move(d.CurrentPage,d.CurrentPage-1); d.CurrentPage--; }); });
 async void MoveRightClick(object s, RoutedEventArgs e) => await ExecuteAsync("Moving page…", async () =>
 { if (Doc is {} doc && doc.CurrentPage < pageCount-1) await ChangeAsync(d => { d.Move(d.CurrentPage,d.CurrentPage+1); d.CurrentPage++; }); });
 async void MergeClick(object s, RoutedEventArgs e) => await ExecuteAsync("Merging PDFs…", async () =>
 {
  if (Doc == null) return;
  var pick = new OpenFileDialog { Filter = "PDF documents (*.pdf)|*.pdf", Multiselect = true };
  if (pick.ShowDialog(this) != true) return;
  var sources = new List<PdfDocument>();
  try
  {
   foreach (var path in pick.FileNames)
   {
    var bytes = await File.ReadAllBytesAsync(path); var source = await PdfWorker.Run(() => new PdfDocument(bytes));
    sources.Add(source); if (!source.CanCopy) throw new InvalidOperationException("A source PDF does not allow extracting pages.");
   }
   await ChangeAsync(d => { foreach (var source in sources) d.Merge(source); });
  }
  finally { foreach (var source in sources) await PdfWorker.Run(source.Dispose); }
 });
 internal static int[] ParsePages(string value, int count)
 {
  var pages = new List<int>();
  foreach (var token in value.Split(',',StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
  {
   var parts = token.Split('-');
   if (parts.Length > 2 || !int.TryParse(parts[0],out var a)) throw new FormatException("Use page numbers such as 1,3,5-7.");
   var b = a;
   if (parts.Length == 2 && !int.TryParse(parts[1],out b)) throw new FormatException("Invalid page range.");
   if (a < 1 || b < a || b > count) throw new FormatException("Page range is outside this document.");
   for (int i = a; i <= b; i++) if (!pages.Contains(i-1)) pages.Add(i-1);
  }
  if (pages.Count == 0) throw new FormatException("Choose at least one page.");
  return pages.ToArray();
 }
 async void ExtractClick(object s, RoutedEventArgs e) => await ExecuteAsync("Extracting pages…", async () =>
 {
  if (Doc is not {} doc) return;
  if (!doc.CanCopy) throw new InvalidOperationException("This PDF does not allow extracting pages.");
  var range = TextDialog.Ask(this,"Extract pages","Enter pages or ranges, for example: 1,3,5-7",(doc.CurrentPage+1).ToString());
  if (range is null) return;
  var pages = ParsePages(range,pageCount);
  var pick = new SaveFileDialog { Filter = "PDF document (*.pdf)|*.pdf", FileName = "Extracted pages.pdf" };
  if (pick.ShowDialog(this) != true) return;
  if (OpenDocuments.Any(d => string.Equals(d.FilePath,pick.FileName,StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Choose a new filename rather than an open document.");
  await PdfWorker.Run(() => doc.Extract(pages,pick.FileName)); Status.Text = "Extracted " + pages.Length + " pages.";
 });
 async void ExportClick(object s, RoutedEventArgs e) => await ExecuteAsync("Exporting page…", async () =>
 {
  if (Doc is not {} doc) return;
  if (!doc.CanCopy) throw new InvalidOperationException("This PDF does not allow exporting content.");
  var page = doc.CurrentPage; var pick = new SaveFileDialog { Filter = "PNG image (*.png)|*.png", FileName = "Page " + (page+1) + ".png" };
  if (pick.ShowDialog(this) != true) return;
  var image = await PdfWorker.Run(() => doc.Render(page,2));
  await Task.Run(() => { var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using var output = File.Create(pick.FileName); encoder.Save(output); });
  Status.Text = "Page exported as PNG.";
 });
 async void PrintClick(object s, RoutedEventArgs e) => await ExecuteAsync("Preparing print…", async () =>
 {
  if (Doc is not {} doc) return;
  if (!doc.CanPrint) throw new InvalidOperationException("This PDF does not allow printing.");
  var dialog = new PrintDialog(); if (dialog.ShowDialog() != true) return;
  var size = new Size(dialog.PrintableAreaWidth,dialog.PrintableAreaHeight);
  using var ticketStream = dialog.PrintTicket.GetXmlStream(); using var bytes = new MemoryStream(); ticketStream.CopyTo(bytes);
  await PrintService.PrintAsync(doc,dialog.PrintQueue.FullName,bytes.ToArray(),size);
  Status.Text = "Document sent to printer.";
 });
 void Zoom(double factor) { if (busy) return; fitMode = PageFitMode.Manual; zoom = Math.Clamp(zoom*factor,0.25,4); UpdateFitControls(); ScheduleRender(); }
 void ZoomInClick(object s, RoutedEventArgs e) => Zoom(1.2);
 void ZoomOutClick(object s, RoutedEventArgs e) => Zoom(1/1.2);
 void FitClick(object s, RoutedEventArgs e) => SelectFit(PageFitMode.Width);
 void FitHeightClick(object s, RoutedEventArgs e) => SelectFit(PageFitMode.Height);
 void SelectFit(PageFitMode mode) { if (busy || Doc == null) return; fitMode = mode; UpdateFitControls(); PageScroll.ScrollToHome(); ScheduleRender(); }
 void UpdateFitControls()
 {
  FitWidthButton.IsChecked = fitMode == PageFitMode.Width;
  FitHeightButton.IsChecked = fitMode == PageFitMode.Height;
  FitWidthButton.IsEnabled = FitHeightButton.IsEnabled = !busy && Doc != null;
 }
 void SidebarClick(object s, RoutedEventArgs e) { SidebarColumn.Width = new GridLength(SidebarColumn.Width.Value == 0 ? 172 : 0); if (fitMode != PageFitMode.Manual) ScheduleRender(); }
 async void PreviousClick(object s, RoutedEventArgs e) => await ExecuteAsync("Rendering page…", () => NavigateAsync((Doc?.CurrentPage ?? 0)-1));
 async void NextClick(object s, RoutedEventArgs e) => await ExecuteAsync("Rendering page…", () => NavigateAsync((Doc?.CurrentPage ?? 0)+1));
 async void PageNumberKeyDown(object s, KeyEventArgs e) { if (e.Key == Key.Enter && int.TryParse(PageNumberBox.Text,out var page)) { e.Handled = true; await ExecuteAsync("Rendering page…", () => NavigateAsync(page-1)); } }
 async void PageMouseWheel(object s, MouseWheelEventArgs e)
 {
  if (busy) { e.Handled = true; return; }
  if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { Zoom(e.Delta > 0 ? 1.1 : 1/1.1); e.Handled = true; return; }
  if (Doc is not {} doc) return;
  if (e.Delta < 0 && PageScroll.VerticalOffset >= PageScroll.ScrollableHeight-1 && doc.CurrentPage < pageCount-1)
  { e.Handled = true; await ExecuteAsync("Rendering page…", async () => { await NavigateAsync(doc.CurrentPage+1); PageScroll.ScrollToTop(); }); }
  else if (e.Delta > 0 && PageScroll.VerticalOffset <= 0 && doc.CurrentPage > 0)
  { e.Handled = true; await ExecuteAsync("Rendering page…", async () => { await NavigateAsync(doc.CurrentPage-1); PageScroll.ScrollToBottom(); }); }
 }
 async void FilesDropped(object s, DragEventArgs e)
 {
  if (busy) return;
  if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
   foreach (var file in files.Where(f => string.Equals(FilePath.GetExtension(f),".pdf",StringComparison.OrdinalIgnoreCase))) await OpenFileAsync(file);
 }
 async void RecentDoubleClick(object s, MouseButtonEventArgs e) { if (RecentFiles.SelectedItem is string path) await OpenFileAsync(path); }
 void Shortcuts(object s, KeyEventArgs e)
 {
  if (busy) return;
  if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
  {
   switch (e.Key)
   {
    case Key.O: OpenClick(s,e); break;
    case Key.S: if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) SaveAsClick(s,e); else SaveClick(s,e); break;
    case Key.F: ShowTools(ViewTools); SearchBox.Focus(); SearchBox.SelectAll(); break;
    case Key.P: PrintClick(s,e); break;
    case Key.W: if (Documents.SelectedItem is TabItem tab) _ = ExecuteAsync("Closing document…", () => CloseDocumentAsync(tab)); break;
    case Key.Z: if (Keyboard.FocusedElement is TextBox or PasswordBox) return; UndoClick(s,e); break;
    case Key.Y: if (Keyboard.FocusedElement is TextBox or PasswordBox) return; RedoClick(s,e); break;
    case Key.C:
     if (Keyboard.FocusedElement is TextBox or PasswordBox) return;
     if (selectedText.Length > 0 && Doc?.CanCopy == true) { try { Clipboard.SetText(selectedText); } catch (Exception ex) { Report(ex); } }
     break;
    default: return;
   }
   e.Handled = true;
  }
  else if (e.Key == Key.Escape) SetTool(Tool.Select,"Drag to select text. Ctrl+F to search.");
  else if (Keyboard.FocusedElement is not TextBox && Doc is {} doc)
  { if (e.Key == Key.PageDown) { NextClick(s,e); e.Handled = true; } else if (e.Key == Key.PageUp) { PreviousClick(s,e); e.Handled = true; } }
 }
 void ThemeClick(object s, RoutedEventArgs e) { ToggleTheme(); SaveSettings(); }
 void ToggleTheme()
 {
  dark = !dark;
  ThemeManager.SetDark(dark);
 }
 void SaveSettings()
 {
  if (previewMode) return;
  try { Directory.CreateDirectory(FilePath.GetDirectoryName(settingsPath)!); File.WriteAllText(settingsPath,JsonSerializer.Serialize(new Settings(dark,recent))); }
  catch { Status.Text = "Preferences could not be saved."; }
 }
 async void AboutClick(object s, RoutedEventArgs e)
 {
  var dialog = new AboutDialog(this); dialog.ShowDialog();
  if (dialog.CheckForUpdatesRequested) await CheckUpdatesAsync(true);
 }
 async void UpdateNoticeClick(object s,RoutedEventArgs e)
 {
  if (availableUpdate == null) await CheckUpdatesAsync(true); else OfferUpdate(availableUpdate);
 }
 void OfferUpdate(AvailableUpdate update)
 {
  if (busy || closing) return;
  var dialog = new UpdateDialog(this,update,UpdateService.IsInstalled(AppContext.BaseDirectory));
  if (dialog.ShowDialog() == true && dialog.Prepared != null) { pendingUpdate = dialog.Prepared; Close(); }
 }
 internal async Task CheckUpdatesAsync(bool manual)
 {
  if (previewMode || checkingUpdates || closing) return;
  checkingUpdates = true;
  var previousStatus = Status.Text;
  if (manual) Status.Text = "Checking for updates…";
  try
  {
   using var service = new UpdateService(); var update = await service.CheckAsync();
   if (closing) return;
   availableUpdate = update;
   UpdateNotice.Visibility = update == null ? Visibility.Collapsed : Visibility.Visible;
   if (update != null) { UpdateNotice.Content = "Update to " + update.Version; OfferUpdate(update); }
   else if (manual) MessageBox.Show(this,"PolyPDF is up to date.","PolyPDF updates");
  }
  catch (Exception ex) { if (manual) MessageBox.Show(this,UpdateMessages.Failure(ex),"PolyPDF updates",MessageBoxButton.OK,MessageBoxImage.Warning); }
  finally { checkingUpdates = false; if (manual && Status.Text == "Checking for updates…") Status.Text = previousStatus; }
 }
 internal async Task StartUpdateChecksAsync()
 {
  if (previewMode) return;
  var resultPath = FilePath.Combine(UpdateModel.CacheRoot,"pending-result.json");
  if (File.Exists(resultPath))
  {
   try
   {
    var result = UpdateModel.Read<UpdateResult>(resultPath);
    if (result.Success && result.ToVersion == AppInfo.Version && UpdateModel.Preferences.LastShownVersion != AppInfo.Version)
    {
     var changelog = new AboutDialog(this,true) { Title = "PolyPDF updated to " + AppInfo.Version };
     changelog.ShowDialog(); UpdateModel.Preferences = UpdateModel.Preferences with { LastShownVersion = AppInfo.Version };
    }
    else if (!result.Success) MessageBox.Show(this,"The last update did not finish.\n\n" + result.Message,"PolyPDF updates",MessageBoxButton.OK,MessageBoxImage.Warning);
    File.Move(resultPath,FilePath.Combine(UpdateModel.CacheRoot,"last-result.json"),true);
   }
   catch (Exception ex) { Status.Text = "Update status could not be read: " + ex.Message; }
  }
  if (UpdateModel.Preferences.AutomaticChecks) await CheckUpdatesAsync(false);
 }

 void ScheduleRecovery() { if (!previewMode) { recoveryDebounce.Stop(); recoveryDebounce.Start(); } }
 async Task CheckpointAsync()
 {
  if (previewMode || busy || closing || recovery == null) return;
  var docs = OpenDocuments.Where(recovery.NeedsCheckpoint).ToArray();
  if (docs.Length == 0) return;
  await ExecuteAsync("Saving recovery copies…", async () =>
  {
   try
   {
    await Task.Run(() => { foreach (var doc in docs) recovery.Checkpoint(doc); });
    recoveryWarningShown = false; Status.Text = "Recovery copies updated · " + DateTime.Now.ToShortTimeString();
   }
   catch (Exception ex)
   {
    Status.Text = "Recovery unavailable · Save your changes manually.";
    if (!recoveryWarningShown) { recoveryWarningShown = true; MessageBox.Show(this,"The recovery copy could not be updated. Save your work manually.\n\n" + ex.Message,"Recovery unavailable",MessageBoxButton.OK,MessageBoxImage.Warning); }
   }
  });
 }
 async Task ForgetRecoveryAsync(PdfDocument doc)
 {
  if (recovery == null) return;
  try { await Task.Run(() => recovery.Forget(doc)); }
  catch (Exception) { Status.Text = "Recovery cleanup could not finish. An older copy may be offered at the next start."; }
 }
 public async Task OfferRecoveryAsync()
 {
  if (previewMode) return;
  await ExecuteAsync("Checking recovery copies…", async () =>
  {
   try { recovery = await Task.Run(() => new RecoveryStore()); }
   catch (Exception ex) { Status.Text = "Recovery unavailable · Save manually."; MessageBox.Show(this,"Automatic recovery is unavailable.\n\n" + ex.Message,"PolyPDF",MessageBoxButton.OK,MessageBoxImage.Warning); return; }
   using var batch = await Task.Run(recovery.FindAbandoned);
   if (batch.Entries.Count == 0) { Status.Text = "Ready · Recovery enabled"; return; }
   var answer = MessageBox.Show(this,$"Found {batch.Entries.Count} recovery copies from an interrupted session.\n\nYes: recover documents as new, unsaved copies.\nNo: permanently discard these recovery copies.\nCancel: keep them for the next start.","Recover unsaved documents",MessageBoxButton.YesNoCancel,MessageBoxImage.Question);
   if (answer == MessageBoxResult.Cancel) return;
   var errors = new List<string>();
   foreach (var entry in batch.Entries)
   {
    try
    {
     if (answer == MessageBoxResult.No) { await Task.Run(() => batch.Delete(entry)); continue; }
     var snapshot = await Task.Run(() => RecoveryStore.Read(entry));
     var document = await PdfWorker.Run(() => PdfDocument.Recover(snapshot));
     try
     {
      // Commit the replacement recovery copy before removing the old one.
      await Task.Run(() => recovery.Checkpoint(document));
      await AddDocumentAsync(document);
     }
     catch { if (!OpenDocuments.Contains(document)) await PdfWorker.Run(document.Dispose); throw; }
     await Task.Run(() => batch.Delete(entry));
    }
    catch (Exception ex) { errors.Add(ex.Message); }
   }
   if (errors.Count > 0) MessageBox.Show(this,"Some recovery copies could not be processed and were kept for another attempt.\n\n" + string.Join("\n",errors.Distinct()),"Recovery incomplete",MessageBoxButton.OK,MessageBoxImage.Warning);
   else Status.Text = answer == MessageBoxResult.Yes ? "Documents recovered · Save as to choose a destination" : "Recovery copies discarded.";
  });
 }

 internal async Task CapturePreviewAsync(string path, bool darkTheme = false, string tools = "View")
 {
  if (darkTheme != dark) ToggleTheme();
  ShowTools(tools == "Annotate" ? AnnotateTools : tools == "Pages" ? PageTools : ViewTools);
  UpdateLayout(); if (Doc != null) { await RenderPageAsync(); UpdateLayout(); }
  var root = (FrameworkElement)Content;
  var bitmap = new RenderTargetBitmap((int)root.ActualWidth,(int)root.ActualHeight,96,96,PixelFormats.Pbgra32);
  bitmap.Render(root); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
  using var output = File.Create(path); encoder.Save(output);
 }
 internal async Task VerifyUiRegressionsAsync()
 {
  if (SoftwareVersion.Text != $"v{AppInfo.Version}") throw new Exception("Incorrect version label.");
  var password = new PasswordDialog(this);
  var fields = ((StackPanel)password.Content).Children;
  if (fields.OfType<PasswordBox>().Count() != 1 || fields.OfType<TextBox>().Any()) throw new Exception("Password entry is not masked.");
  password.Close();
  DrawClick(this,new RoutedEventArgs()); ViewToolsClick(this,new RoutedEventArgs());
  if (tool != Tool.Select) throw new Exception("View left an annotation tool active.");
  DrawClick(this,new RoutedEventArgs()); PageToolsClick(this,new RoutedEventArgs());
  if (tool != Tool.Select) throw new Exception("Pages left an annotation tool active.");
  var timerTicks = 0;
  var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
  timer.Tick += (_,_) => timerTicks++;
  timer.Start();
  await ExecuteAsync("Testing background work…", async () => await PdfWorker.Run(() => Thread.Sleep(250)));
  timer.Stop();
  if (timerTicks < 2) throw new Exception("UI dispatcher did not stay responsive during PDF work.");
 }
 internal async Task VerifyDefaultFitAsync(string folder)
 {
  void AssertInitialFit()
  {
   if (fitMode != PageFitMode.Height || FitHeightButton.IsChecked != true ||
       PageImage.Source == null || zoom < 0.01 || PageImage.Height < 20 ||
       PageImage.Height + 56 > PageScroll.ActualHeight + 1)
    throw new Exception("Opening a PDF did not immediately render a visible page in Fit Height.");
  }
  // Check before CapturePreviewAsync, which performs its own layout and render.
  AssertInitialFit();
  var originalTab = (TabItem)Documents.SelectedItem;
  Zoom(1.2); renderTimer.Stop(); pendingRender = false;
  var secondPath = FilePath.Combine(folder,"second-open.pdf");
  File.Copy(((PdfDocument)originalTab.Tag).FilePath!,secondPath,true);
  await OpenFileAsync(secondPath);
  AssertInitialFit();
  await CloseDocumentAsync((TabItem)Documents.SelectedItem);
  await RefreshDocumentAsync();
  AssertInitialFit();
  Zoom(1.2); renderTimer.Stop(); pendingRender = false;
  await OpenFileAsync(((PdfDocument)originalTab.Tag).FilePath!);
  AssertInitialFit();
  File.WriteAllText(FilePath.Combine(folder,"default-fit-tests.txt"),"PASS: first open before preview rendering, new document after manual zoom, tab restoration, reopening an existing PDF: visible Fit Height.");
 }
 internal async Task VerifyVersion022Async(string folder)
 {
  void StopRender() { renderTimer.Stop(); pendingRender = false; }
  void HeightFits()
  {
   if (fitMode != PageFitMode.Height || FitHeightButton.IsChecked != true || FitWidthButton.IsChecked == true || PageImage.Height + 56 > PageScroll.ActualHeight + 1)
    throw new Exception("Fit Height did not fit the available height.");
  }
  var originalWidth = Width; var originalHeight = Height;
  FitHeightClick(this,new RoutedEventArgs()); StopRender(); await RenderPageAsync(); UpdateLayout(); HeightFits();
  var initialZoom = zoom;
  Width += 100; Height += 120; UpdateLayout(); StopRender(); await RenderPageAsync(); UpdateLayout(); HeightFits();
  if (zoom <= initialZoom) throw new Exception("Fit Height did not follow window resize.");
  var testDocument = Doc!;
  await ChangeAsync(d => d.Rotate(d.CurrentPage)); UpdateLayout(); HeightFits();
  await PdfWorker.Run(() => testDocument.Undo()); await RefreshDocumentAsync(); UpdateLayout(); HeightFits();
  await NavigateAsync(1); UpdateLayout(); HeightFits(); await NavigateAsync(0);
  Zoom(1.2); StopRender(); await RenderPageAsync(); var manualZoom = zoom;
  Height -= 60; UpdateLayout(); StopRender(); await RenderPageAsync();
  if (fitMode != PageFitMode.Manual || FitHeightButton.IsChecked == true || FitWidthButton.IsChecked == true || zoom != manualZoom)
   throw new Exception("Manual zoom did not leave fitting mode.");
  FitClick(this,new RoutedEventArgs()); StopRender(); await RenderPageAsync(); UpdateLayout();
  if (FitWidthButton.IsChecked != true || PageImage.Width + 56 > PageScroll.ActualWidth + 1) throw new Exception("Fit Width regression.");
  FitHeightClick(this,new RoutedEventArgs()); StopRender(); await RenderPageAsync();
  foreach (var darkTheme in new[] { false,true })
   foreach (var group in new[] { "View","Annotate","Pages" })
   {
    await CapturePreviewAsync(System.IO.Path.Combine(folder,$"fit-height-{group.ToLowerInvariant()}-{(darkTheme ? "dark" : "light")}.png"),darkTheme,group);
    StopRender(); HeightFits();
    var selected = group == "View" ? ViewTab : group == "Annotate" ? AnnotateTab : PagesTab;
    if (new[] { ViewTab,AnnotateTab,PagesTab }.Count(t => t.IsChecked == true) != 1 || selected.IsChecked != true)
     throw new Exception("Incorrect active toolbar group.");
    selected.ApplyTemplate();
    var border = (Border)selected.Template.FindName("SelectionBody",selected);
    if (border.Background is not SolidColorBrush brush || brush.Color.A == 0 || border.BorderThickness.Bottom == 0)
     throw new Exception("Missing selected toolbar indicator.");
   }
  Width = originalWidth; Height = originalHeight; UpdateLayout(); FitClick(this,new RoutedEventArgs()); StopRender(); await RenderPageAsync(); StopRender();
  File.WriteAllText(System.IO.Path.Combine(folder,"version-022-tests.txt"),"PASS: Fit Height on resize, rotation and page navigation; manual zoom exits fitting; Fit Width; active toolbar indicators in both themes.");
 }
 internal void SetPreviewTheme(bool value)
 {
  renderTimer.Stop(); pendingRender = false;
  if (value != dark) ToggleTheme();
  UpdateLayout();
 }
 internal void VerifyDisabledSidebar()
 {
  Thumbnails.ApplyTemplate();
  var surface = (Border)Thumbnails.Template.FindName("ListSurface",Thumbnails);
  var actual = (SolidColorBrush)surface.Background;
  var expected = (SolidColorBrush)FindResource("PanelBg");
  if (Thumbnails.IsEnabled || actual.Color != expected.Color)
   throw new Exception("The disabled sidebar did not retain the app theme.");
 }
 internal void SetPreviewDialogBusy(bool value) => SetBusy(value);
}

internal sealed class PdfPaginator(PdfDocument doc, Size size) : DocumentPaginator
{
 public override bool IsPageCountValid => true;
 public override int PageCount => doc.Count;
 public override Size PageSize { get; set; } = size;
 public override IDocumentPaginatorSource Source => null!;
 public override DocumentPage GetPage(int pageNumber)
 {
  if (pageNumber < 0 || pageNumber >= PageCount) return DocumentPage.Missing;
  var bitmap = doc.Render(pageNumber,2); var visual = new DrawingVisual();
  using (var dc = visual.RenderOpen())
  {
   var scale = Math.Min((PageSize.Width-32)/bitmap.PixelWidth,(PageSize.Height-32)/bitmap.PixelHeight);
   var w = bitmap.PixelWidth*scale; var h = bitmap.PixelHeight*scale;
   dc.DrawImage(bitmap,new Rect((PageSize.Width-w)/2,(PageSize.Height-h)/2,w,h));
  }
  return new DocumentPage(visual,PageSize,new Rect(PageSize),new Rect(PageSize));
 }
}
