using System.Windows;
using System.Windows.Controls;

namespace PolyPDF;

internal sealed class PasswordDialog : ThemedDialog
{
 readonly PasswordBox input = new() { Padding = new Thickness(9), Margin = new Thickness(0,14,0,14) };
 public PasswordDialog(Window owner)
 {
  Owner = owner; Title = "Password required"; Width = 440; Height = 220;
  ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
  var panel = new StackPanel { Margin = new Thickness(22) };
  panel.Children.Add(new TextBlock { Text = "Enter the PDF password. The password is not saved.", TextWrapping = TextWrapping.Wrap });
  panel.Children.Add(input);
  var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
  var cancel = new Button { Content = "Cancel", IsCancel = true };
  var open = new Button { Content = "Open", IsDefault = true, Style = (Style)FindResource("Primary") };
  open.Click += (_,_) => DialogResult = true;
  buttons.Children.Add(cancel); buttons.Children.Add(open); panel.Children.Add(buttons); Content = panel;
  Loaded += (_,_) => input.Focus();
 }
 public static string? Ask(Window owner)
 {
  var dialog = new PasswordDialog(owner);
  try { return dialog.ShowDialog() == true ? dialog.input.Password : null; }
  finally { dialog.input.Clear(); }
 }
}
