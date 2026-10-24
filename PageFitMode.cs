using System.Windows;

namespace PolyPDF;

internal enum PageFitMode { Manual, Width, Height }
internal static class PageFitting
{
 public static double Scale(Size page, Size available, PageFitMode mode, double manualZoom)
 {
  if (page.Width <= 0 || page.Height <= 0) throw new InvalidOperationException("The page has invalid dimensions.");
  var scale = mode switch
  {
   PageFitMode.Width => Math.Max(1,available.Width) / page.Width,
   PageFitMode.Height => Math.Max(1,available.Height) / page.Height,
   _ => manualZoom
  };
  return Math.Min(Math.Max(0.001,scale),6000 / Math.Max(page.Width,page.Height));
 }
}
