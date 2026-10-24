using System.IO;

namespace PolyPDF;

internal sealed record ReleaseNote(string Version, IReadOnlyList<string> Changes);
internal static class ReleaseNotes
{
 public static IReadOnlyList<ReleaseNote> All { get; } = Read();
 static IReadOnlyList<ReleaseNote> Read()
 {
  using var stream = typeof(ReleaseNotes).Assembly.GetManifestResourceStream("PolyPDF.CHANGELOG.md")
   ?? throw new InvalidOperationException("The application release notes are missing.");
  using var reader = new StreamReader(stream);
  var releases = new List<ReleaseNote>();
  string? version = null; var changes = new List<string>();
  while (reader.ReadLine() is {} line)
  {
   if (line.StartsWith("## ",StringComparison.Ordinal))
   {
    if (version != null) releases.Add(new ReleaseNote(version,changes.ToArray()));
    version = line[3..].Trim(); changes.Clear();
   }
   else if (version != null && line.StartsWith("- ",StringComparison.Ordinal)) changes.Add(line[2..]);
  }
  if (version != null) releases.Add(new ReleaseNote(version,changes.ToArray()));
  return releases.AsReadOnly();
 }
}
