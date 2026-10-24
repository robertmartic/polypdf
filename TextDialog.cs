using System.Windows;
using System.Windows.Controls;

namespace PolyPDF;
internal sealed class TextDialog : ThemedDialog
{
 readonly TextBox input;
 public string Value => input.Text;
 public TextDialog(Window owner, string title, string description, string initial = "", bool multiline = false, bool readOnly = false)
 {
  Owner=owner; Title=title; Width=multiline?620:460; Height=multiline?460:235; MinWidth=350; MinHeight=200; WindowStartupLocation=WindowStartupLocation.CenterOwner;
  var grid=new Grid { Margin=new Thickness(22) }; grid.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto }); grid.RowDefinitions.Add(new RowDefinition()); grid.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto }); Content=grid;
  var hint=new TextBlock { Text=description, TextWrapping=TextWrapping.Wrap, Margin=new Thickness(3,0,3,12) }; grid.Children.Add(hint);
  input=new TextBox { Text=initial, AcceptsReturn=multiline, TextWrapping=multiline?TextWrapping.Wrap:TextWrapping.NoWrap, VerticalScrollBarVisibility=ScrollBarVisibility.Auto, IsReadOnly=readOnly, VerticalContentAlignment=VerticalAlignment.Top }; Grid.SetRow(input,1); grid.Children.Add(input);
  var buttons=new StackPanel { Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Right, Margin=new Thickness(0,14,0,0) }; Grid.SetRow(buttons,2); grid.Children.Add(buttons);
  if(readOnly) { var copy=new Button { Content="Copy all" }; copy.Click+=(_,_)=>Clipboard.SetText(input.Text); buttons.Children.Add(copy); }
  var cancel=new Button { Content=readOnly?"Close":"Cancel", IsCancel=true }; buttons.Children.Add(cancel);
  if(!readOnly) { var ok=new Button { Content="Apply", IsDefault=!multiline, Style=(Style)FindResource("Primary") }; ok.Click+=(_,_)=>{DialogResult=true;}; buttons.Children.Add(ok); }
  Loaded+=(_,_)=>{input.Focus(); if(!multiline) input.SelectAll();};
 }
 public static string? Ask(Window owner,string title,string description,string initial="",bool multiline=false) { var dialog=new TextDialog(owner,title,description,initial,multiline); return dialog.ShowDialog()==true?dialog.Value:null; }
}
