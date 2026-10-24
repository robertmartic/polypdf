using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace PolyPDF;

internal static class ThemeManager
{
 public static bool IsDark { get; private set; }
 public static void SetDark(bool dark)
 {
  IsDark = dark;
  var colors = dark ? new[] { "#171B24","#222835","#11151D","#E8EDF7","#A6B1C5","#343E50" } : new[] { "#F5F6FA","#FFFFFF","#E8EBF1","#202738","#637086","#DEE3EC" };
  var names = new[] { "WindowBg","PanelBg","CanvasBg","ForegroundBrush","MutedBrush","LineBrush" };
  for (int i = 0; i < names.Length; i++) Application.Current.Resources[names[i]] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i]));
  foreach (Window window in Application.Current.Windows) ApplyCaption(window);
 }

 public static void Attach(Window window) => window.SourceInitialized += (_,_) => ApplyCaption(window);
 static void ApplyCaption(Window window)
 {
  var handle = new WindowInteropHelper(window).Handle;
  if (handle == IntPtr.Zero) return;
  var dark = IsDark ? 1 : 0;
  DwmSetWindowAttribute(handle,20,ref dark,sizeof(int));
  // Caption/text colours are supported on Windows 11. Unsupported attributes
  // return HRESULT, so older Windows keeps its native caption behaviour.
  var background = (SolidColorBrush)Application.Current.FindResource(window is ThemedDialog ? "PanelBg" : "WindowBg");
  var foreground = (SolidColorBrush)Application.Current.FindResource("ForegroundBrush");
  var caption = ColorRef(background.Color); var text = ColorRef(foreground.Color);
  DwmSetWindowAttribute(handle,35,ref caption,sizeof(int));
  DwmSetWindowAttribute(handle,36,ref text,sizeof(int));
 }
 static int ColorRef(Color color) => color.R | color.G << 8 | color.B << 16;
 [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
}

// Derived code-created windows do not automatically pick up an implicit Window
// style. Use an explicit shared style and live resource references for every dialog.
internal class ThemedDialog : Window
{
 public ThemedDialog()
 {
  SetResourceReference(StyleProperty,"AppWindowStyle");
  SetResourceReference(BackgroundProperty,"PanelBg");
  SetResourceReference(ForegroundProperty,"ForegroundBrush");
  ShowInTaskbar = false;
  ThemeManager.Attach(this);
  SourceInitialized += (_,_) => { if (Icon == null && Owner?.Icon != null) Icon = Owner.Icon; };
 }
}
