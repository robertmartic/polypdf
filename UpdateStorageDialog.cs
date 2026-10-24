using System.Windows;
using System.Windows.Controls;

namespace PolyPDF;

internal sealed class UpdateStorageDialog : ThemedDialog
{
 readonly TextBlock summary = new() { TextWrapping = TextWrapping.Wrap };
 readonly TextBlock details = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,16,12,0) };
 readonly Button clean = new() { Content = "Clean up old update files", IsEnabled = false };
 readonly Button refresh = new() { Content = "Refresh" };
 readonly Button close = new() { Content = "Close", IsCancel = true, MinWidth = 80 };
 readonly string installDir;
 bool working,closeWhenFinished;
 internal UpdateStorageDialog(Window owner,string? root = null)
 {
  Owner = owner; installDir = root ?? AppContext.BaseDirectory;
  Title = "Update storage"; Width = 620; Height = 470; MinWidth = 480; MinHeight = 360; WindowStartupLocation = WindowStartupLocation.CenterOwner;
  var grid = new Grid { Margin = new Thickness(24) };
  grid.Resources.Add(typeof(System.Windows.Controls.Primitives.ScrollBar),FindResource("DialogScrollBar"));
  grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); grid.RowDefinitions.Add(new RowDefinition()); grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
  grid.Children.Add(summary);
  var scroll = new ScrollViewer { Content = details, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Grid.SetRow(scroll,1); grid.Children.Add(scroll);
  var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,16,0,0) };
  buttons.Children.Add(clean); buttons.Children.Add(refresh); buttons.Children.Add(close); Grid.SetRow(buttons,2); grid.Children.Add(buttons);
  clean.Click += async (_,_) => await RefreshAsync(true);
  refresh.Click += async (_,_) => await RefreshAsync(false);
  close.Click += (_,_) => Close();
  Loaded += async (_,_) => await RefreshAsync(false);
  Closing += (_,e) => { if (working) { closeWhenFinished = true; e.Cancel = true; } };
  Content = grid;
 }
 async Task RefreshAsync(bool remove)
 {
  working = true; clean.IsEnabled = refresh.IsEnabled = close.IsEnabled = false;
  summary.Text = remove ? "Cleaning up old update files…" : "Checking update storage…";
  try
  {
   var removed = remove ? await Task.Run(() => UpdateStorage.Cleanup(installDir,UpdateModel.CacheRoot,UpdateModel.DataRoot,DateTime.UtcNow)) : 0;
   var result = await Task.Run(() => UpdateStorage.Inspect(installDir,UpdateModel.CacheRoot,DateTime.UtcNow));
   summary.Text = (remove ? $"Freed {UpdateStorage.Size(removed)}.\n\n" : "") + $"Update downloads and backups: {UpdateStorage.Size(result.TotalBytes)}\nAvailable to clean: {UpdateStorage.Size(result.EligibleBytes)}";
   details.Text = "Completed or cancelled downloads are kept for 7 days. Older backups are kept for 30 days, and the latest recovery backup is always retained. Prepared updates and backups containing additional files are kept.\n\n" +
    (result.Items.Count == 0 ? "No managed update files found. Older update folders are left untouched." : string.Join("\n",result.Items.GroupBy(i => i.Reason).Select(g => $"{g.Key}: {g.Count()} · {UpdateStorage.Size(g.Sum(i => i.Bytes))}")));
   clean.IsEnabled = result.Items.Any(i => i.Eligible);
  }
  catch (Exception ex) { summary.Text = UpdateMessages.Failure(ex); }
  finally { working = false; refresh.IsEnabled = close.IsEnabled = true; if (closeWhenFinished) Close(); }
 }
}
