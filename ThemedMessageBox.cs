using System.Windows;
using System.Windows.Controls;

namespace PolyPDF;

internal sealed class MessageDialog : ThemedDialog
{
 public MessageBoxResult Result { get; private set; }
 internal IReadOnlyList<Button> Choices { get; }

 public MessageDialog(Window? owner, string text, string title, MessageBoxButton buttons, MessageBoxImage image)
 {
  if (owner?.IsVisible == true) Owner = owner;
  Title = title; Width = 530; MinWidth = 400; SizeToContent = SizeToContent.Height;
  ResizeMode = ResizeMode.NoResize;
  WindowStartupLocation = Owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
  var grid = new Grid { Margin = new Thickness(24) };
  grid.RowDefinitions.Add(new RowDefinition()); grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
  var body = new DockPanel { LastChildFill = true };
  if (image != MessageBoxImage.None)
  {
   var symbol = new TextBlock { Text = image == MessageBoxImage.Question ? "?" : image == MessageBoxImage.Information ? "i" : "!", FontSize = 26, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,0,18,0) };
   symbol.SetResourceReference(TextBlock.ForegroundProperty,"AccentBrush");
   body.Children.Add(symbol);
  }
  var scroll = new ScrollViewer { MaxHeight = 420, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
  scroll.Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,3,0,4) };
  body.Children.Add(scroll); grid.Children.Add(body);
  var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,20,0,0) };
  Grid.SetRow(footer,1); grid.Children.Add(footer);
  var choices = buttons switch
  {
   MessageBoxButton.YesNoCancel => new[] { MessageBoxResult.Yes,MessageBoxResult.No,MessageBoxResult.Cancel },
   MessageBoxButton.YesNo => new[] { MessageBoxResult.Yes,MessageBoxResult.No },
   MessageBoxButton.OKCancel => new[] { MessageBoxResult.OK,MessageBoxResult.Cancel },
   _ => new[] { MessageBoxResult.OK }
  };
  Result = choices[^1];
  var controls = new List<Button>();
  foreach (var choice in choices)
  {
   var button = new Button { Content = choice.ToString(), MinWidth = 72, IsDefault = choice == choices[0], IsCancel = choice == Result };
   if (choice == MessageBoxResult.OK || choice == MessageBoxResult.Yes) button.Style = (Style)FindResource("Primary");
   button.Click += (_,_) => { Result = choice; DialogResult = true; };
   footer.Children.Add(button); controls.Add(button);
  }
  Choices = controls; Content = grid;
 }
}

internal static class ThemedMessageBox
{
 public static MessageBoxResult Show(Window owner, string text, string title,
  MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None)
 {
  var dialog = new MessageDialog(owner,text,title,buttons,image);
  dialog.ShowDialog(); return dialog.Result;
 }
 public static MessageBoxResult Show(string text, string title,
  MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None)
 {
  var dialog = new MessageDialog(Application.Current.MainWindow,text,title,buttons,image);
  dialog.ShowDialog(); return dialog.Result;
 }
}
