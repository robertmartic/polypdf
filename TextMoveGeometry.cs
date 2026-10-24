using System.Windows;
using System.Windows.Media;

namespace PolyPDF;

internal static class TextMoveGeometry
{
 internal static Rect ToScreen(Rect bounds,Matrix deviceToPdf)
 {
  if (!deviceToPdf.HasInverse) throw new InvalidOperationException("The page transform cannot be inverted.");
  deviceToPdf.Invert(); return Rect.Transform(bounds,deviceToPdf);
 }
 internal static Vector Clamp(Rect bounds,Vector offset,Size page)
 {
  // Oversized blocks can slide between either edge, but never disappear fully.
  var x1 = -bounds.Left; var x2 = page.Width-bounds.Right;
  var y1 = -bounds.Top; var y2 = page.Height-bounds.Bottom;
  return new(Math.Clamp(offset.X,Math.Min(x1,x2),Math.Max(x1,x2)),Math.Clamp(offset.Y,Math.Min(y1,y2),Math.Max(y1,y2)));
 }
 internal static Vector ToPdfOffset(Point start,Vector offset,Matrix deviceToPdf) => deviceToPdf.Transform(start+offset)-deviceToPdf.Transform(start);
}
