using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace PolyPDF;

internal sealed class UpdateDialog : ThemedDialog
{
 internal PreparedUpdate? Prepared { get; private set; }
 CancellationTokenSource? cancellation;
 bool downloading;
 internal Func<Task> AttemptForTest { get; }
 internal Action CancelForTest => () => cancellation?.Cancel();
 internal Func<bool> CanRetryForTest { get; }
 internal TextBlock StatusForTest { get; }
 internal UpdateDialog(Window owner,AvailableUpdate update,bool installed,
  Func<CancellationToken,Task<PreparedUpdate>>? prepareForTest = null)
 {
  Owner = owner; Title = "PolyPDF update available"; Width = 650; Height = 540; MinWidth = 480; MinHeight = 360; WindowStartupLocation = WindowStartupLocation.CenterOwner;
  var root = new Grid { Margin = new Thickness(24) };
  root.Resources.Add(typeof(System.Windows.Controls.Primitives.ScrollBar),FindResource("DialogScrollBar"));
  foreach (var height in new[] { GridLength.Auto,new GridLength(1,GridUnitType.Star),GridLength.Auto,GridLength.Auto }) root.RowDefinitions.Add(new RowDefinition { Height = height });
  var heading = new StackPanel { Margin = new Thickness(0,0,0,16) };
  heading.Children.Add(new TextBlock { Text = "PolyPDF " + update.Version + " is available", FontSize = 22, FontWeight = FontWeights.SemiBold });
  heading.Children.Add(new TextBlock { Text = "Installed version: " + AppInfo.Version, Margin = new Thickness(0,6,0,0) }); root.Children.Add(heading);
  var notes = Regex.Replace(update.Notes,@"(?m)^#+\s*","").Replace("**","").Replace("`","");
  var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = new TextBlock { Text = notes, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,12,0) } };
  Grid.SetRow(scroll,1); root.Children.Add(scroll);
  var details = new StackPanel { Margin = new Thickness(0,16,0,8) };
  var status = new TextBlock { Text = installed ? "Only changed files are downloaded when a matching differential package is available. Unsaved documents are checked before restarting." : "This is a portable copy. Install PolyPDF using the conventional installer to enable in-app updates.", TextWrapping = TextWrapping.Wrap };
  StatusForTest = status;
  var progressBar = new ProgressBar { Height = 6, Minimum = 0, Maximum = 100, Margin = new Thickness(0,12,0,0), Visibility = Visibility.Collapsed };
  progressBar.SetResourceReference(ProgressBar.ForegroundProperty,"AccentBrush"); progressBar.SetResourceReference(ProgressBar.BackgroundProperty,"WindowBg");
  details.Children.Add(status); details.Children.Add(progressBar); Grid.SetRow(details,2); root.Children.Add(details);
  var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
  var later = new Button { Content = "Later", MinWidth = 85 };
  var install = new Button { Content = "Update now", MinWidth = 120, IsEnabled = installed };
  later.Click += (_,_) => { if (downloading) cancellation?.Cancel(); else DialogResult = false; };
  CanRetryForTest = () => install.IsEnabled && install.Content.ToString() == "Retry";
  async Task Attempt()
  {
   cancellation?.Dispose(); cancellation = new CancellationTokenSource();
   downloading = true; install.IsEnabled = false; later.Content = "Cancel download"; progressBar.Visibility = Visibility.Visible;
   try
   {
    using var service = new UpdateService();
    var progress = new Progress<(long Done,long Total,string Status)>(p =>
    {
     status.Text = p.Status + (p.Total > 1024 ? $"\n{p.Done/1048576d:0.0} / {p.Total/1048576d:0.0} MB" : "");
     progressBar.IsIndeterminate = p.Total <= 1;
     progressBar.Value = p.Total > 1 ? 100d*p.Done/p.Total : 0;
    });
    Prepared = prepareForTest != null ? await prepareForTest(cancellation.Token) : await service.PrepareAsync(update,progress,cancellation.Token);
    status.Text = "Update ready. Save any unsaved work before PolyPDF restarts.";
    downloading = false; DialogResult = true;
   }
   catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { status.Text = "Download cancelled. Your installed version is unchanged. Choose Retry to download again."; }
   catch (Exception ex) { status.Text = UpdateMessages.Failure(ex) + "\n\nYour installed version is unchanged."; }
   finally { downloading = false; later.Content = "Close"; install.Content = "Retry"; install.IsEnabled = installed; progressBar.Visibility = Visibility.Collapsed; }
  }
  AttemptForTest = Attempt;
  install.Click += async (_,_) => await Attempt();
  Closing += (_,e) => { if (downloading) { cancellation?.Cancel(); e.Cancel = true; } };
  Closed += (_,_) => cancellation?.Dispose();
  buttons.Children.Add(later); buttons.Children.Add(install); Grid.SetRow(buttons,3); root.Children.Add(buttons); Content = root;
 }
}
