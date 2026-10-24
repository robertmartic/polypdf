using System.Text;
using System.Windows;
using static PolyPDF.PdfNative;

namespace PolyPDF;

public sealed partial class PdfDocument
{
 const string TextBlockPrefix = "PolyPDF.TextBlock.";
 internal sealed record AddedTextTarget(Guid DocumentId,long Revision,int Page,string Key,int[] Indices,Rect Bounds);
 static List<string> ObjectMarks(IntPtr obj)
 {
  var names = new List<string>();
  for (uint i = 0; i < FPDFPageObj_CountMarks(obj); i++)
  {
   var mark = FPDFPageObj_GetMark(obj,i);
   if (FPDFPageObjMark_GetName(mark,null,0,out var length) == 0 || length is < 2 or > 1024 || length%2 != 0) continue;
   var bytes = new byte[length];
   if (FPDFPageObjMark_GetName(mark,bytes,length,out _) != 0) names.Add(Encoding.Unicode.GetString(bytes).TrimEnd('\0'));
  }
  return names;
 }
 internal List<AddedTextTarget> AddedTextTargets(int index) => PdfWorker.Invoke(() => Page(index,p => AddedTextTargetsCore(index,p)));
 List<AddedTextTarget> AddedTextTargetsCore(int index,IntPtr page)
 {
  var groups = new Dictionary<string,(List<int> Indices,Rect Bounds)>();
  var unsupportedGroups = new HashSet<string>();
  for (int i = 0; i < FPDFPage_CountObjects(page); i++)
  {
   var obj = FPDFPage_GetObject(page,i);
   if (FPDFPageObj_GetType(obj) != 1) continue;
   var marks = ObjectMarks(obj);
   var blockMarks = marks.Where(m => m.StartsWith(TextBlockPrefix,StringComparison.Ordinal)).Distinct().ToArray();
   if (blockMarks.Length > 1 || (blockMarks.Length == 1 && !Guid.TryParseExact(blockMarks[0][TextBlockPrefix.Length..],"N",out _))) continue;
   var key = blockMarks.Length == 1 ? blockMarks[0] : "Legacy:" + i;
   if (!new[] { "Segoe UI","Arial","Times New Roman" }.Any(f => marks.Contains(FontTag(f)))) { unsupportedGroups.Add(key); continue; }
   if (FPDFPageObj_GetMatrix(obj,out var matrix) == 0 ||
       !new[] { matrix.A,matrix.B,matrix.C,matrix.D,matrix.E,matrix.F }.All(float.IsFinite) ||
       Math.Abs((double)matrix.A*matrix.D-(double)matrix.B*matrix.C) < 1e-10 ||
       FPDFPageObj_GetBounds(obj,out var left,out var bottom,out var right,out var top) == 0 ||
       !new[] { left,bottom,right,top }.All(float.IsFinite) || right < left || top < bottom) { unsupportedGroups.Add(key); continue; }
   // Legacy insertions have independently marked lines. Never guess adjacency.
   var bounds = new Rect(left,bottom,right-left,top-bottom);
   if (groups.TryGetValue(key,out var group)) { group.Indices.Add(i); group.Bounds.Union(bounds); groups[key] = group; }
   else groups.Add(key,([i],bounds));
  }
  return groups.Where(g => !unsupportedGroups.Contains(g.Key) && g.Value.Bounds.Width > 0 && g.Value.Bounds.Height > 0)
   .Select(g => new AddedTextTarget(RecoveryId,currentRevision,index,g.Key,g.Value.Indices.ToArray(),g.Value.Bounds)).OrderBy(t => t.Indices.Max()).ToList();
 }
 internal void MoveAddedText(AddedTextTarget target,Vector offset) => PdfWorker.Invoke(() =>
 {
  if (!CanEdit) throw new InvalidOperationException("This document does not allow editing.");
  if (target.DocumentId != RecoveryId || target.Revision != currentRevision || target.Page < 0 || target.Page >= Count)
   throw new InvalidOperationException("The text selection is no longer current. Select the text again.");
  if (!double.IsFinite(offset.X) || !double.IsFinite(offset.Y)) throw new InvalidOperationException("The text position is invalid.");
  Page(target.Page,p =>
  {
   var current = AddedTextTargetsCore(target.Page,p).SingleOrDefault(t => t.Key == target.Key && t.Indices.SequenceEqual(target.Indices));
   if (current == null || current.Bounds != target.Bounds) throw new InvalidOperationException("This text block cannot be moved. Select supported text added in PolyPDF.");
   foreach (var index in current.Indices)
   {
    var obj = FPDFPage_GetObject(p,index);
    if (FPDFPageObj_GetMatrix(obj,out var matrix) == 0) throw new InvalidOperationException("The text position could not be read.");
    matrix.E = (float)(matrix.E+offset.X); matrix.F = (float)(matrix.F+offset.Y);
    if (!float.IsFinite(matrix.E) || !float.IsFinite(matrix.F) || FPDFPageObj_SetMatrix(obj,ref matrix) == 0)
     throw new InvalidOperationException("The text could not be moved.");
   }
   Generate(p); return true;
  });
 });
}
