using System.Windows;
using System.Windows.Controls;

namespace PolyPDF;

internal sealed class AboutDialog : ThemedDialog
{
 internal TabControl Tabs { get; }
 internal ScrollViewer ChangelogScroll { get; }
 internal bool CheckForUpdatesRequested { get; private set; }
 public AboutDialog(Window owner,bool showChangelog = false)
 {
  Owner = owner; Title = "About PolyPDF"; Width = 660; Height = 560;
  MinWidth = 480; MinHeight = 360; WindowStartupLocation = WindowStartupLocation.CenterOwner;
  var root = new Grid { Margin = new Thickness(24) };
  root.Resources.Add(typeof(System.Windows.Controls.Primitives.ScrollBar),FindResource("DialogScrollBar"));
  root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
  root.RowDefinitions.Add(new RowDefinition());
  root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
  var header = new StackPanel { Margin = new Thickness(0,0,0,18) };
  header.Children.Add(new TextBlock { Text = "PolyPDF", FontSize = 28, FontWeight = FontWeights.SemiBold });
  var version = new TextBlock { Text = "Version " + AppInfo.Version, Margin = new Thickness(0,4,0,0) };
  version.SetResourceReference(TextBlock.ForegroundProperty,"MutedBrush"); header.Children.Add(version); root.Children.Add(header);
  Tabs = new TabControl { Style = (Style)FindResource("DialogTabs") };
  Grid.SetRow(Tabs,1); root.Children.Add(Tabs);
  var overview = new TextBlock
  {
   Text = "An open-source PDF workspace for Windows.\n\nRead, annotate and organise PDF documents.\nLocal processing. No account or subscription.\n\nEncrypted recovery checkpoints help protect unsaved work.\n\nExisting text editing depends on the PDF font and structure. OCR requires an installed Windows language pack.\n\nBuilt with C# / WPF, PDFium and Windows OCR.\nMIT license; third-party notices are included with the app.",
   TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2,0,12,8)
  };
  Tabs.Items.Add(new TabItem { Header = "Overview", Content = new ScrollViewer { Content = overview, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled } });
  var history = new StackPanel { Margin = new Thickness(2,0,16,0) };
  foreach (var release in ReleaseNotes.All)
  {
   var title = new TextBlock { Text = "Version " + release.Version, FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,0,0,10) };
   history.Children.Add(title);
   foreach (var change in release.Changes) history.Children.Add(new TextBlock { Text = "• " + change, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,8) });
   var divider = new Border { BorderThickness = new Thickness(0,0,0,1), Margin = new Thickness(0,8,0,20) };
   divider.SetResourceReference(Border.BorderBrushProperty,"LineBrush"); history.Children.Add(divider);
  }
  ChangelogScroll = new ScrollViewer { Content = history, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
  Tabs.Items.Add(new TabItem { Header = "Changelog", Content = ChangelogScroll });
  var updates = new StackPanel();
  var automatic = new CheckBox { Content = "Check for updates when PolyPDF starts", IsChecked = UpdateModel.Preferences.AutomaticChecks, Margin = new Thickness(0,0,0,16) };
  automatic.Click += (_,_) => UpdateModel.Preferences = UpdateModel.Preferences with { AutomaticChecks = automatic.IsChecked == true };
  updates.Children.Add(automatic);
  updates.Children.Add(new TextBlock { Text = "Updates are downloaded from the official GitHub Releases. Installation starts only after you choose Update now. Your documents and preferences are kept.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,16) });
  var check = new Button { Content = "Check for updates", HorizontalAlignment = HorizontalAlignment.Left };
  check.Click += (_,_) => { CheckForUpdatesRequested = true; DialogResult = true; }; updates.Children.Add(check);
  var storage = new Button { Content = "Manage update storage", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0,12,0,0) };
  storage.Click += (_,_) => new UpdateStorageDialog(this).ShowDialog(); updates.Children.Add(storage);
  try
  {
   var pendingResult = System.IO.Path.Combine(UpdateModel.CacheRoot,"pending-result.json");
   var lastResult = System.IO.File.Exists(pendingResult) ? pendingResult : System.IO.Path.Combine(UpdateModel.CacheRoot,"last-result.json");
   var result = UpdateModel.Read<UpdateResult>(lastResult);
   if (result.Success)
   {
    var saved = result.FullBytes > 0 ? Math.Clamp(100d*(1d-(double)result.DownloadBytes/result.FullBytes),0,100) : 0;
    updates.Children.Add(new TextBlock { Text = $"Last update: {result.FromVersion} → {result.ToVersion}\nDownloaded {result.DownloadBytes/1048576d:0.00} MB" +
     (result.Delta ? $" using changed files.\nFull update: {result.FullBytes/1048576d:0.00} MB · Saved {saved:0.0}%" : " using the full package."),
     TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,24,0,0) });
   }
   else updates.Children.Add(new TextBlock { Text = "Last update did not finish.\n" + result.Message + "\nCheck for updates to try again.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,24,0,0) });
  }
  catch { }
  Tabs.Items.Add(new TabItem { Header = "Updates", Content = new ScrollViewer { Content = updates, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
  Tabs.SelectedIndex = showChangelog ? 1 : 0;
  var close = new Button { Content = "Close", IsCancel = true, IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 80, Margin = new Thickness(0,18,0,0) };
  Grid.SetRow(close,2); root.Children.Add(close); Content = root;
 }
}
