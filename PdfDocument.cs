using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static PolyPDF.PdfNative;

namespace PolyPDF;
public sealed partial class PdfDocument : IDisposable
{
 IntPtr handle, memory;
 uint permissions = uint.MaxValue;
 readonly List<IntPtr> fonts = [];
 readonly List<IntPtr> fontOwnerPages = [];
 readonly Dictionary<string,IntPtr> fontCache = new();
 sealed record Revision(byte[] Bytes, long Id, int Page);
 readonly LinkedList<Revision> undo = new(), redo = new();
 long currentRevision, nextRevision, savedRevision;
 string? recoveredName;
 const long HistoryLimit = 96L * 1024 * 1024;
 public string? FilePath { get; private set; }
 public string Name => FilePath is null ? recoveredName ?? "Untitled.pdf" : Path.GetFileName(FilePath);
 public bool Dirty => currentRevision != savedRevision;
 public long RevisionId => currentRevision;
 public Guid RecoveryId { get; } = Guid.NewGuid();
 public bool SourceEncrypted { get; private set; }
 public int CurrentPage { get; set; }
 public int Count => PdfWorker.Invoke(() => FPDF_GetPageCount(handle));
 public bool CanEdit => (permissions & 8) != 0;
 public bool HasSignatures => PdfWorker.Invoke(() => FPDF_GetSignatureCount(handle) > 0);
 public bool CanPrint => (permissions & 4) != 0;
 public bool CanCopy => (permissions & 16) != 0;
 public PdfDocument(byte[] data, string? path = null, string? password = null) { PdfWorker.Invoke(() => { Load(data, password); FilePath = path; SourceEncrypted=FPDF_GetSecurityHandlerRevision(handle)>=0; permissions=SourceEncrypted?FPDF_GetDocPermissions(handle):uint.MaxValue; }); }
 PdfDocument(IntPtr doc) { handle = doc; }
 public static PdfDocument Create() => PdfWorker.Invoke(CreateCore);
 static PdfDocument CreateCore()
 {
  var doc = new PdfDocument(FPDF_CreateNewDocument());
  var p = FPDFPage_New(doc.handle, 0, 595.28, 841.89); FPDF_ClosePage(p); doc.currentRevision = doc.nextRevision = 1; return doc;
 }
 void Load(byte[] bytes, string? password = null)
 {
  var data = Marshal.AllocHGlobal(bytes.Length);
  Marshal.Copy(bytes, 0, data, bytes.Length);
  var doc = FPDF_LoadMemDocument64(data, (nuint)bytes.Length, password);
  if (doc == IntPtr.Zero) { Marshal.FreeHGlobal(data); throw new InvalidOperationException(FPDF_GetLastError() == 4 ? "This PDF needs a valid password." : "The PDF could not be opened. It may be damaged or unsupported."); }
  Release(); handle = doc; memory = data;
 }
 void Release()
 {
  foreach (var page in fontOwnerPages) FPDF_ClosePage(page); fontOwnerPages.Clear();
  foreach (var f in fonts) FPDFFont_Close(f); fonts.Clear(); fontCache.Clear();
  if (handle != IntPtr.Zero) FPDF_CloseDocument(handle);
  if (memory != IntPtr.Zero) Marshal.FreeHGlobal(memory);
  handle = memory = IntPtr.Zero;
 }
 public void Dispose() => PdfWorker.Invoke(Release);
 T Page<T>(int index, Func<IntPtr,T> action)
 {
  ObjectDisposedException.ThrowIf(handle == IntPtr.Zero, this);
  var p = FPDF_LoadPage(handle, index); if (p == IntPtr.Zero) throw new InvalidOperationException("This page could not be loaded.");
  try { return action(p); } finally { FPDF_ClosePage(p); }
 }
 public byte[] Bytes() => PdfWorker.Invoke(BytesCore);
 byte[] BytesCore()
 {
  ObjectDisposedException.ThrowIf(handle == IntPtr.Zero, this);
  using var stream = new MemoryStream(); Exception? failure = null;
  WriteBlock callback = (_, ptr, length) => { try { var data = new byte[checked((int)length)]; Marshal.Copy(ptr, data, 0, data.Length); stream.Write(data); return 1; } catch(Exception ex) { failure = ex; return 0; } };
  var writer = new FileWrite { Version = 1, Callback = Marshal.GetFunctionPointerForDelegate(callback) };
  var ok = FPDF_SaveAsCopy(handle, ref writer, SaveNoIncremental | SaveRemoveSecurity); GC.KeepAlive(callback);
  if (ok == 0 || failure != null) throw new IOException("The PDF could not be saved.", failure);
  return stream.ToArray();
 }
 public void Save(string path) => PdfWorker.Invoke(() => SaveCore(path));
 void SaveCore(string path)
 {
  var bytes = Bytes();
  // Validate the complete result before replacing the destination.
  using (var check = new PdfDocument(bytes)) if (check.Count != Count) throw new IOException("Saved page count did not match.");
  var absolute = Path.GetFullPath(path);
  var temp = absolute + "." + Guid.NewGuid().ToString("N") + ".tmp";
  try { File.WriteAllBytes(temp, bytes); File.Move(temp, absolute, true); }
  finally { if (File.Exists(temp)) File.Delete(temp); }
  FilePath = absolute; savedRevision = currentRevision; SourceEncrypted = false;
 }
 public void Change(Action action) => PdfWorker.Invoke(() => ChangeCore(action));
 void ChangeCore(Action action)
 {
  if (!CanEdit) throw new InvalidOperationException("This document does not allow editing.");
  var before = new Revision(Bytes(), currentRevision, CurrentPage);
  try { action(); undo.AddLast(before); Trim(undo); redo.Clear(); currentRevision = ++nextRevision; }
  catch { Load(before.Bytes); CurrentPage = before.Page; throw; }
 }
 static void Trim(LinkedList<Revision> history) { while (history.Count > 1 && (history.Count > 25 || history.Sum(b => (long)b.Bytes.Length) > HistoryLimit)) history.RemoveFirst(); }
 public bool Undo(bool forward = false) => PdfWorker.Invoke(() => UndoCore(forward));
 bool UndoCore(bool forward)
 {
  var source = forward ? redo : undo; var dest = forward ? undo : redo;
  if (source.Last is null) return false;
  var now = new Revision(Bytes(), currentRevision, CurrentPage); var target = source.Last.Value; Load(target.Bytes); source.RemoveLast(); dest.AddLast(now); Trim(dest);
  CurrentPage = Math.Clamp(target.Page, 0, Count - 1); currentRevision = target.Id; return true;
 }
 public Size Size(int index) => PdfWorker.Invoke(() => Page(index, p => new Size(FPDF_GetPageWidthF(p), FPDF_GetPageHeightF(p))));
 public BitmapSource Render(int index, double scale = 1) => PdfWorker.Invoke(() => RenderCore(index, scale));
 BitmapSource RenderCore(int index, double scale)
 {
  return Page(index, p => {
   scale=Math.Min(scale,6000/Math.Max(FPDF_GetPageWidthF(p),FPDF_GetPageHeightF(p)));
   var w = Math.Max((int)(FPDF_GetPageWidthF(p) * scale), 1); var h = Math.Max((int)(FPDF_GetPageHeightF(p) * scale), 1);
   var bitmap = FPDFBitmap_Create(w,h,0); if (bitmap == IntPtr.Zero) throw new InvalidOperationException("Not enough memory to render this page.");
   try { FPDFBitmap_FillRect(bitmap,0,0,w,h,0xFFFFFFFF); FPDF_RenderPageBitmap(bitmap,p,0,0,w,h,0,1 | 2);
    var stride = FPDFBitmap_GetStride(bitmap); var data = new byte[stride*h]; Marshal.Copy(FPDFBitmap_GetBuffer(bitmap),data,0,data.Length);
    var result = BitmapSource.Create(w,h,96,96,PixelFormats.Bgr32,null,data,stride); result.Freeze(); return result;
   } finally { FPDFBitmap_Destroy(bitmap); }
  });
 }
 public Point ToPdf(int index, Point point, int width, int height) => PdfWorker.Invoke(() => Page(index,p => { FPDF_DeviceToPage(p,0,0,width,height,0,(int)point.X,(int)point.Y,out var x,out var y); return new Point(x,y); }));
 public Point ToScreen(int index, Point point, int width, int height) => PdfWorker.Invoke(() => Page(index,p => { FPDF_PageToDevice(p,0,0,width,height,0,point.X,point.Y,out var x,out var y); return new Point(x,y); }));
 public string Text(int index) => PdfWorker.Invoke(() => TextCore(index));
 string TextCore(int index) => Page(index,p => {
  var t = FPDFText_LoadPage(p); try { var n = FPDFText_CountChars(t); if(n <= 0) return ""; var buf = new byte[(n+1)*2]; var read = FPDFText_GetText(t,0,n,buf); return Encoding.Unicode.GetString(buf,0,Math.Max(0,read-1)*2); } finally { FPDFText_ClosePage(t); }
 });
 public record Glyph(string Text, Rect Bounds);
 public List<Glyph> Glyphs(int index) => PdfWorker.Invoke(() => GlyphsCore(index));
 List<Glyph> GlyphsCore(int index) => Page(index,p => {
  var t = FPDFText_LoadPage(p); var result = new List<Glyph>();
  try { for(int i=0;i<FPDFText_CountChars(t);i++) { var ch = FPDFText_GetUnicode(t,i); if(FPDFText_GetCharBox(t,i,out var l,out var r,out var b,out var top) != 0 && ch > 0 && ch <= 0x10FFFF) result.Add(new Glyph(char.ConvertFromUtf32((int)ch),new Rect(l,b,Math.Max(0,r-l),Math.Max(0,top-b)))); } return result; }
  finally { FPDFText_ClosePage(t); }
 });
 IntPtr Font(string family)
 {
  if (fontCache.TryGetValue(family, out var cached)) return cached;
  var tag = FontTag(family);
  // Marks survive save, undo and recovery. Only reuse full fonts embedded by us,
  // never an arbitrary subset font already present in an external PDF.
  for (int pageIndex = 0; pageIndex < Count; pageIndex++)
  {
   var ownerPage = FPDF_LoadPage(handle, pageIndex);
   if (ownerPage == IntPtr.Zero) continue;
   IntPtr existing = IntPtr.Zero;
   try {
    var page = ownerPage;
    for (int i = 0; i < FPDFPage_CountObjects(page); i++)
    {
     var obj = FPDFPage_GetObject(page, i);
     for (uint j = 0; j < FPDFPageObj_CountMarks(obj); j++)
     {
      var mark = FPDFPageObj_GetMark(obj, j);
      if (FPDFPageObjMark_GetName(mark, null, 0, out var length) == 0) continue;
      var name = new byte[length];
      if (FPDFPageObjMark_GetName(mark, name, length, out _) != 0 && Encoding.Unicode.GetString(name).TrimEnd('\0') == tag)
       { existing = FPDFTextObj_GetFont(obj); break; }
     }
     if (existing != IntPtr.Zero) break;
    }
   }
   finally { if (existing == IntPtr.Zero) FPDF_ClosePage(ownerPage); }
   if (existing != IntPtr.Zero) { fontOwnerPages.Add(ownerPage); fontCache[family] = existing; return existing; }
  }
  var file = family switch { "Arial" => "arial.ttf", "Times New Roman" => "times.ttf", _ => "segoeui.ttf" };
  var bytes = File.ReadAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),file));
  var f = FPDFText_LoadFont(handle,bytes,(uint)bytes.Length,2,1); if(f == IntPtr.Zero) throw new InvalidOperationException("The selected font could not be loaded."); fonts.Add(f); fontCache[family] = f; return f;
 }
 static string FontTag(string family) => "PolyPDF.Font." + family.Replace(" ", "");
 public void AddText(int index, Point at, string text, float size, Color color, string family = "Segoe UI") => PdfWorker.Invoke(() => AddTextCore(index, at, text, size, color, family));
 void AddTextCore(int index, Point at, string text, float size, Color color, string family) => Page(index,p => {
  var font = Font(family); int line=0;var angle=FPDFPage_GetRotation(p)*Math.PI/2;var cos=Math.Cos(angle);var sin=Math.Sin(angle);
  var block = TextBlockPrefix + Guid.NewGuid().ToString("N");
  foreach(var value in text.Replace("\r", "").Split('\n')) { if(value.Length == 0) { line++; continue; } var obj = FPDFPageObj_CreateTextObj(handle,font,size);
   if(obj == IntPtr.Zero) throw new InvalidOperationException("Text could not be created.");
   if(FPDFText_SetText(obj,value)==0) { FPDFPageObj_Destroy(obj); throw new InvalidOperationException("This text is not supported by the selected font."); }
   if (FPDFPageObj_AddMark(obj, FontTag(family)) == IntPtr.Zero || FPDFPageObj_AddMark(obj, block) == IntPtr.Zero)
   { FPDFPageObj_Destroy(obj); throw new InvalidOperationException("The text block could not be identified safely."); }
   FPDFPageObj_SetFillColor(obj,color.R,color.G,color.B,255); FPDFPageObj_Transform(obj,cos,sin,-sin,cos,at.X+sin*line*size*1.3,at.Y-cos*line*size*1.3); FPDFPage_InsertObject(p,obj); line++;
  } Generate(p); return true;
 });
 public record TextTarget(int ObjectIndex, string Text, float Size);
 public TextTarget? HitText(int index, Point at) => PdfWorker.Invoke(() => HitTextCore(index, at));
 TextTarget? HitTextCore(int index, Point at) => Page<TextTarget?>(index,p => {
  var t = FPDFText_LoadPage(p); try {
   var i = FPDFText_GetCharIndexAtPos(t,at.X,at.Y,3,3); if(i<0) return null; var obj=FPDFText_GetTextObject(t,i);
   var objectIndex = -1; for(int j=0;j<FPDFPage_CountObjects(p);j++) if(FPDFPage_GetObject(p,j)==obj) { objectIndex=j; break; }
   if(objectIndex<0) return null;
   var length=FPDFTextObj_GetText(obj,t,null,0); var buffer = new byte[length]; FPDFTextObj_GetText(obj,t,buffer,length); FPDFTextObj_GetFontSize(obj,out var size);
   return new TextTarget(objectIndex,Encoding.Unicode.GetString(buffer).TrimEnd('\0'),size);
  } finally { FPDFText_ClosePage(t); }
 });
 public void ReplaceText(int index, TextTarget target, string value) => PdfWorker.Invoke(() => ReplaceTextCore(index, target, value));
 void ReplaceTextCore(int index, TextTarget target, string value) => Page(index,p => {
  var obj=FPDFPage_GetObject(p,target.ObjectIndex);
  // Reject nested or transformed text: replacing it could alter its placement.
  if(FPDFPageObj_GetMatrix(obj,out var matrix)==0 || Math.Abs(matrix.B)>0.001 || Math.Abs(matrix.C)>0.001) throw new InvalidOperationException("Editing rotated or skewed text is not supported yet.");
  if(FPDFText_SetText(obj,value)==0) throw new InvalidOperationException("The existing font cannot represent this replacement. Use Add text instead.");
  Generate(p);
  // Detect missing glyphs / unsupported embedded font mappings and roll back the transaction.
  var t=FPDFText_LoadPage(p); try { var n=FPDFTextObj_GetText(obj,t,null,0); var buf=new byte[n]; FPDFTextObj_GetText(obj,t,buf,n); if(Encoding.Unicode.GetString(buf).TrimEnd('\0') != value) throw new InvalidOperationException("This embedded font cannot safely represent the replacement. No changes were applied."); } finally { FPDFText_ClosePage(t); }
  return true;
 });
 public void Highlight(int index, IEnumerable<Rect> boxes) => PdfWorker.Invoke(() => HighlightCore(index, boxes));
 void HighlightCore(int index, IEnumerable<Rect> boxes) => Page(index,p => {
  var a=FPDFPage_CreateAnnot(p,9); if(a==IntPtr.Zero) throw new InvalidOperationException("Highlight could not be created.");
  try { FPDFAnnot_SetColor(a,0,255,218,50,110);
   foreach(var b in boxes) { var q=new Quad { X1=(float)b.Left,Y1=(float)b.Bottom,X2=(float)b.Right,Y2=(float)b.Bottom,X3=(float)b.Left,Y3=(float)b.Top,X4=(float)b.Right,Y4=(float)b.Top }; if(FPDFAnnot_AppendAttachmentPoints(a,ref q)==0) throw new InvalidOperationException("Highlight could not be saved."); }
  } finally { FPDFPage_CloseAnnot(a); } return true;
 });
 public void Draw(int index, IReadOnlyList<Point> points, Color color) => PdfWorker.Invoke(() => DrawCore(index, points, color));
 void DrawCore(int index, IReadOnlyList<Point> points, Color color) => Page(index,p => {
  if(points.Count<2) return false; var path=FPDFPageObj_CreateNewPath((float)points[0].X,(float)points[0].Y);
  foreach(var pt in points.Skip(1)) FPDFPath_LineTo(path,(float)pt.X,(float)pt.Y);
  FPDFPageObj_SetStrokeColor(path,color.R,color.G,color.B,255); FPDFPageObj_SetStrokeWidth(path,1.6f); FPDFPath_SetDrawMode(path,0,1); FPDFPage_InsertObject(p,path); Generate(p); return true;
 });
 public void Comment(int index, Point at, string text) => PdfWorker.Invoke(() => CommentCore(index, at, text));
 void CommentCore(int index, Point at, string text) => Page(index,p => {
  var a=FPDFPage_CreateAnnot(p,1); if(a==IntPtr.Zero) throw new InvalidOperationException("Comment could not be created.");
  try { var rect=new PdfRect { Left=(float)at.X,Right=(float)at.X+22,Bottom=(float)at.Y,Top=(float)at.Y+22 }; FPDFAnnot_SetRect(a,ref rect); FPDFAnnot_SetColor(a,0,255,190,40,255); FPDFAnnot_SetStringValue(a,"Contents",text); FPDFAnnot_SetStringValue(a,"T","PolyPDF"); } finally { FPDFPage_CloseAnnot(a); } return true;
 });
 public List<string> Comments(int index) => PdfWorker.Invoke(() => CommentsCore(index));
 List<string> CommentsCore(int index) => Page(index,p => {
  var result=new List<string>();for(int i=0;i<FPDFPage_GetAnnotCount(p);i++){var a=FPDFPage_GetAnnot(p,i);try{var n=FPDFAnnot_GetStringValue(a,"Contents",null,0);if(n>2){var buffer=new byte[n];FPDFAnnot_GetStringValue(a,"Contents",buffer,n);result.Add(Encoding.Unicode.GetString(buffer).TrimEnd('\0'));}}finally{FPDFPage_CloseAnnot(a);}}return result;
 });
 public void Rotate(int index) => PdfWorker.Invoke(() => Page(index,p => { FPDFPage_SetRotation(p,(FPDFPage_GetRotation(p)+1)%4); return true; }));
 public void Blank(int at) => PdfWorker.Invoke(() => { var p=FPDFPage_New(handle,at,595.28,841.89); if(p==IntPtr.Zero) throw new InvalidOperationException("Page could not be created."); FPDF_ClosePage(p); });
 public void Delete(int index) => PdfWorker.Invoke(() => { if(Count<=1) throw new InvalidOperationException("Keep at least one page in the document."); FPDFPage_Delete(handle,index); });
 public void Move(int from,int to) => PdfWorker.Invoke(() => { if(FPDF_MovePages(handle,[from],1,to)==0) throw new InvalidOperationException("Page could not be moved."); });
 public void Merge(PdfDocument other) => PdfWorker.Invoke(() => { var indices=Enumerable.Range(0,other.Count).ToArray(); if(FPDF_ImportPagesByIndex(handle,other.handle,indices,(uint)indices.Length,Count)==0) throw new InvalidOperationException("These documents could not be merged."); });
 public void Extract(int[] indices,string path) => PdfWorker.Invoke(() => ExtractCore(indices, path));
 void ExtractCore(int[] indices,string path)
 {
  using var doc=new PdfDocument(FPDF_CreateNewDocument());
  if(FPDF_ImportPagesByIndex(doc.handle,handle,indices,(uint)indices.Length,0)==0) throw new InvalidOperationException("Pages could not be extracted."); doc.Save(path);
 }
 static void Generate(IntPtr p) { if(FPDFPage_GenerateContent(p)==0) throw new InvalidOperationException("The page content could not be updated."); }
 internal RecoverySnapshot RecoverySnapshot() => PdfWorker.Invoke(() => new RecoverySnapshot(1, Name, FilePath, CurrentPage, SourceEncrypted, permissions, DateTimeOffset.UtcNow, Bytes()));
 internal static PdfDocument Recover(RecoverySnapshot snapshot) => PdfWorker.Invoke(() => {
  var document = new PdfDocument(snapshot.Pdf);
  document.recoveredName = Path.GetFileNameWithoutExtension(snapshot.Name) + " (recovered).pdf";
  document.CurrentPage = Math.Clamp(snapshot.Page, 0, document.Count - 1);
  document.currentRevision = document.nextRevision = 1;
  document.permissions = snapshot.Permissions;
  document.SourceEncrypted = snapshot.SourceEncrypted;
  return document;
 });
 // Compute the page's affine transform once for a whole selection/search result.
 public List<(Glyph Glyph, Rect Screen)> ScreenGlyphs(int index, int width, int height) => PdfWorker.Invoke(() => {
  var glyphs = GlyphsCore(index);
  return Page(index, p => {
   FPDF_DeviceToPage(p,0,0,width,height,0,0,0,out var originX,out var originY);
   FPDF_DeviceToPage(p,0,0,width,height,0,width,0,out var axisX,out var axisY);
   FPDF_DeviceToPage(p,0,0,width,height,0,0,height,out var verticalX,out var verticalY);
   var vx = new Vector((axisX-originX)/width,(axisY-originY)/width);
   var vy = new Vector((verticalX-originX)/height,(verticalY-originY)/height);
   var determinant = vx.X*vy.Y-vx.Y*vy.X;
   Point Screen(Point point) { var x=point.X-originX;var y=point.Y-originY;return new Point((x*vy.Y-y*vy.X)/determinant,(vx.X*y-vx.Y*x)/determinant); }
   return glyphs.Select(g => (g,new Rect(Screen(g.Bounds.TopLeft),Screen(g.Bounds.BottomRight)))).ToList();
  });
 });
}
