using System.Reflection;

namespace PolyPDF;
internal static class AppInfo
{
 public static string Version { get; } = typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
 public static string DisplayName => $"PolyPDF v{Version}";
}
