using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PolyPDF;

internal static class DialogThemeTests
{
 public static void Run(MainWindow owner, string folder)
 {
  foreach (var dark in new[] { false,true })
  {
   owner.SetPreviewTheme(dark);
   var suffix = dark ? "dark" : "light";
   var cases = new[]
   {
    ("ocr", "Recognise page text", "Language code: en-US (English) or hr-HR (Croatian). Requires the matching Windows OCR language pack.", "en-US", false, false),
    ("add-text", "Add text", "Text is placed at the selected position. Choose font, size and colour in the toolbar.", "Example text", true, false),
    ("edit-text", "Edit text block", "The existing font and position are kept. Longer text may overlap neighbouring content.", "Example text", false, false),
    ("comment", "Add comment", "This note will be saved as a PDF comment.", "Review this paragraph.", true, false),
    ("comments", "Page comments", "Comments on page 1", "1. Review this paragraph.", true, true),
    ("ocr-result", "Recognised text", "Review the result before copying; recognition may contain errors.", "Example recognised text", true, true),
    ("extract", "Extract pages", "Enter pages or ranges, for example: 1,3,5-7", "1,3,5-7", false, false)
   };
   foreach (var item in cases)
   {
    var dialog = new TextDialog(owner,item.Item2,item.Item3,item.Item4,item.Item5,item.Item6);
    Modal(owner,dialog,() =>
    {
     VerifyTheme(dialog); owner.VerifyDisabledSidebar();
     Save(dialog,Path.Combine(folder,$"dialog-{item.Item1}-{suffix}.png"));
     if (item.Item1 == "ocr") Save(owner,Path.Combine(folder,$"disabled-workspace-{suffix}.png"));
    });
   }
   Modal(owner,new PasswordDialog(owner),() => {} ,Path.Combine(folder,$"dialog-password-{suffix}.png"));
   var about = new AboutDialog(owner);
   Modal(owner,about,() =>
   {
    if (about.Tabs.Items.Count != 3 || about.Tabs.SelectedIndex != 0) throw new Exception("Missing About tabs.");
    Save(about,Path.Combine(folder,$"about-overview-{suffix}.png"));
    about.Tabs.SelectedIndex = 1; about.UpdateLayout();
    if (ReleaseNotes.All.Count < 1 || !ReleaseNotes.All.Any(r => r.Version == AppInfo.Version && r.Changes.Count > 0)) throw new Exception("Missing embedded release notes.");
    if (about.ChangelogScroll.ScrollableHeight <= 0 || about.ChangelogScroll.ViewportHeight <= 0) throw new Exception("Changelog is not scrollable.");
    var scrollbar = (System.Windows.Controls.Primitives.ScrollBar)about.ChangelogScroll.Template.FindName("PART_VerticalScrollBar",about.ChangelogScroll);
    if (scrollbar.Style != Application.Current.FindResource("DialogScrollBar")) throw new Exception("Changelog scrollbar is not themed.");
    Save(about,Path.Combine(folder,$"about-changelog-{suffix}.png"));
    about.ChangelogScroll.ScrollToEnd(); about.UpdateLayout();
    if (about.ChangelogScroll.VerticalOffset <= 0) throw new Exception("Cannot scroll release history.");
    about.Tabs.SelectedIndex = 2; about.UpdateLayout();
    Save(about,Path.Combine(folder,$"about-updates-{suffix}.png"));
   });
   var update = new UpdateDialog(owner,new AvailableUpdate("0.4.1","Version 0.4.1\nA small verification update.",[]),true);
   Modal(owner,update,() => {},Path.Combine(folder,$"dialog-update-{suffix}.png"));
   Modal(owner,new UpdateStorageDialog(owner),() => {},Path.Combine(folder,$"dialog-update-storage-{suffix}.png"));
   foreach (var item in new[]
   {
    ("about", "About PolyPDF", AppInfo.DisplayName + "\nAn open-source PDF workspace for Windows.", MessageBoxButton.OK, MessageBoxImage.Information),
    ("warning", "Recovery unavailable", "The recovery copy could not be updated. Save your work manually.", MessageBoxButton.OK, MessageBoxImage.Warning),
    ("confirm", "Delete page", "Delete page 1? You can undo this change.", MessageBoxButton.YesNo, MessageBoxImage.Question),
    ("unsaved", "Unsaved changes", "Save changes to Example.pdf?", MessageBoxButton.YesNoCancel, MessageBoxImage.Question),
    ("recovery", "Recover unsaved documents", "Found recovery copies from an interrupted session.\n\nYes: recover documents as new, unsaved copies.\nNo: permanently discard these recovery copies.\nCancel: keep them for the next start.", MessageBoxButton.YesNoCancel, MessageBoxImage.Question)
   })
   {
    var dialog = new MessageDialog(owner,item.Item3,item.Item2,item.Item4,item.Item5);
    Modal(owner,dialog,() => {},Path.Combine(folder,$"dialog-{item.Item1}-{suffix}.png"));
    var expected = item.Item4 == MessageBoxButton.YesNo ? MessageBoxResult.No : item.Item4 == MessageBoxButton.YesNoCancel ? MessageBoxResult.Cancel : MessageBoxResult.OK;
    if (dialog.Result != expected) throw new Exception("Closing a message changed its result.");
   }
  }
  // Check dynamic resource references on a dialog that is already open.
  owner.SetPreviewTheme(true);
  var live = new TextDialog(owner,"Theme switch","This open dialog must follow theme changes.","en-US");
  Modal(owner,live,() => { owner.SetPreviewTheme(false); VerifyTheme(live); owner.SetPreviewTheme(true); VerifyTheme(live); });
  foreach (var expected in new[] { MessageBoxResult.Yes,MessageBoxResult.No,MessageBoxResult.Cancel })
  {
   var dialog = new MessageDialog(owner,"Save changes?","Unsaved changes",MessageBoxButton.YesNoCancel,MessageBoxImage.Question);
   Modal(owner,dialog,() => dialog.Choices.Single(b => b.Content.ToString() == expected.ToString()).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)),close:false);
   if (dialog.Result != expected) throw new Exception("A message button returned the wrong result.");
  }
  File.WriteAllText(Path.Combine(folder,"dialog-theme-tests.txt"),"PASS: real modal OCR/text/comment/extract/password and app message dialogs in both themes; disabled-owner sidebar; native caption colours where available; live theme switching; Yes/No/Cancel results and close behaviour.");
 }

 static void Modal(MainWindow owner, ThemedDialog dialog, Action inspect, string? image = null, bool close = true)
 {
  Exception? failure = null;
  dialog.ShowActivated = false; dialog.WindowStartupLocation = WindowStartupLocation.Manual; dialog.Left = -18000; dialog.Top = 0;
  dialog.ContentRendered += (_,_) => dialog.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(() =>
  {
   try { VerifyTheme(dialog); owner.VerifyDisabledSidebar(); if (image != null) Save(dialog,image); inspect(); }
   catch (Exception ex) { failure = ex; }
   finally { if (close || failure != null) dialog.Close(); }
  }));
  owner.SetPreviewDialogBusy(true);
  try { dialog.ShowDialog(); }
  finally { owner.SetPreviewDialogBusy(false); }
  if (failure != null) throw new Exception($"Dialog theme failure: {dialog.Title}",failure);
 }

 static void VerifyTheme(ThemedDialog dialog)
 {
  var background = ((SolidColorBrush)Application.Current.FindResource("PanelBg")).Color;
  var foreground = ((SolidColorBrush)Application.Current.FindResource("ForegroundBrush")).Color;
  if (dialog.Background is not SolidColorBrush bg || bg.Color != background || dialog.Foreground is not SolidColorBrush fg || fg.Color != foreground)
   throw new Exception("Dialog colours do not match the selected theme.");
  var handle = new WindowInteropHelper(dialog).Handle;
  if (DwmGetWindowAttribute(handle,35,out var caption,sizeof(int)) == 0 && caption != (background.R | background.G << 8 | background.B << 16))
   throw new Exception("The native caption colour does not match the dialog.");
 }
 internal static void Save(Window window, string path)
 {
  window.UpdateLayout();
  var root = (FrameworkElement)window.Content;
  var width = (int)Math.Ceiling(root.ActualWidth + root.Margin.Left + root.Margin.Right);
  var height = (int)Math.Ceiling(root.ActualHeight + root.Margin.Top + root.Margin.Bottom);
  var bitmap = new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);
  var backdrop = new DrawingVisual();
  using (var drawing = backdrop.RenderOpen()) drawing.DrawRectangle(window.Background,null,new Rect(0,0,width,height));
  bitmap.Render(backdrop); bitmap.Render(root);
  var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
  using var file = File.Create(path); encoder.Save(file);
 }
 [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd,int attribute,out int value,int size);
}
