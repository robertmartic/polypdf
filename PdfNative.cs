using System.Runtime.InteropServices;

namespace PolyPDF;
internal static class PdfNative
{
 const string Dll = "pdfium.dll";
 internal const uint SaveNoIncremental = 2, SaveRemoveSecurity = 4;
 [DllImport(Dll)] internal static extern void FPDF_InitLibrary();
 [DllImport(Dll)] internal static extern IntPtr FPDF_CreateNewDocument();
 [DllImport(Dll)] internal static extern IntPtr FPDF_LoadMemDocument64(IntPtr data, nuint size, [MarshalAs(UnmanagedType.LPStr)] string? password);
 [DllImport(Dll)] internal static extern uint FPDF_GetLastError();
 [DllImport(Dll)] internal static extern void FPDF_CloseDocument(IntPtr doc);
 [DllImport(Dll)] internal static extern int FPDF_GetPageCount(IntPtr doc);
 [DllImport(Dll)] internal static extern uint FPDF_GetDocPermissions(IntPtr doc);
 [DllImport(Dll)] internal static extern int FPDF_GetSecurityHandlerRevision(IntPtr doc);
 [DllImport(Dll)] internal static extern int FPDF_GetSignatureCount(IntPtr doc);
 [DllImport(Dll)] internal static extern IntPtr FPDF_LoadPage(IntPtr doc, int index);
 [DllImport(Dll)] internal static extern void FPDF_ClosePage(IntPtr page);
 [DllImport(Dll)] internal static extern float FPDF_GetPageWidthF(IntPtr page);
 [DllImport(Dll)] internal static extern float FPDF_GetPageHeightF(IntPtr page);
 [DllImport(Dll)] internal static extern IntPtr FPDFBitmap_Create(int width, int height, int alpha);
 [DllImport(Dll)] internal static extern IntPtr FPDFBitmap_GetBuffer(IntPtr bitmap);
 [DllImport(Dll)] internal static extern int FPDFBitmap_GetStride(IntPtr bitmap);
 [DllImport(Dll)] internal static extern void FPDFBitmap_FillRect(IntPtr bitmap, int left, int top, int width, int height, uint color);
 [DllImport(Dll)] internal static extern void FPDFBitmap_Destroy(IntPtr bitmap);
 [DllImport(Dll)] internal static extern void FPDF_RenderPageBitmap(IntPtr bitmap, IntPtr page, int x, int y, int w, int h, int rotate, int flags);
 [DllImport(Dll)] internal static extern int FPDF_DeviceToPage(IntPtr page, int x, int y, int w, int h, int rotate, int dx, int dy, out double px, out double py);
 [DllImport(Dll)] internal static extern int FPDF_PageToDevice(IntPtr page, int x, int y, int w, int h, int rotate, double px, double py, out int dx, out int dy);
 [DllImport(Dll)] internal static extern IntPtr FPDFPage_New(IntPtr doc, int index, double width, double height);
 [DllImport(Dll)] internal static extern void FPDFPage_Delete(IntPtr doc, int index);
 [DllImport(Dll)] internal static extern int FPDFPage_GetRotation(IntPtr page);
 [DllImport(Dll)] internal static extern void FPDFPage_SetRotation(IntPtr page, int rotate);
 [DllImport(Dll)] internal static extern int FPDFPage_GenerateContent(IntPtr page);
 [DllImport(Dll)] internal static extern int FPDF_MovePages(IntPtr doc, int[] indices, uint length, int dest);
 [DllImport(Dll)] internal static extern int FPDF_ImportPagesByIndex(IntPtr dest, IntPtr src, int[] indices, uint count, int at);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] internal delegate int WriteBlock(IntPtr self, IntPtr data, uint size);
 [StructLayout(LayoutKind.Sequential)] internal struct FileWrite { public int Version; public IntPtr Callback; }
 [DllImport(Dll)] internal static extern int FPDF_SaveAsCopy(IntPtr doc, ref FileWrite writer, uint flags);
 [DllImport(Dll)] internal static extern IntPtr FPDFText_LoadPage(IntPtr page);
 [DllImport(Dll)] internal static extern void FPDFText_ClosePage(IntPtr text);
 [DllImport(Dll)] internal static extern int FPDFText_CountChars(IntPtr text);
 [DllImport(Dll)] internal static extern int FPDFText_GetText(IntPtr text, int start, int count, [Out] byte[] buffer);
 [DllImport(Dll)] internal static extern uint FPDFText_GetUnicode(IntPtr text, int index);
 [DllImport(Dll)] internal static extern int FPDFText_GetCharBox(IntPtr text, int index, out double left, out double right, out double bottom, out double top);
 [DllImport(Dll)] internal static extern int FPDFText_GetCharIndexAtPos(IntPtr text, double x, double y, double xt, double yt);
 [DllImport(Dll)] internal static extern IntPtr FPDFText_GetTextObject(IntPtr text, int index);
 [DllImport(Dll)] internal static extern uint FPDFTextObj_GetText(IntPtr obj, IntPtr text, [Out] byte[]? buffer, uint length);
 [DllImport(Dll)] internal static extern IntPtr FPDFText_LoadFont(IntPtr doc, byte[] data, uint length, int type, int cid);
 [DllImport(Dll)] internal static extern void FPDFFont_Close(IntPtr font);
 [DllImport(Dll)] internal static extern IntPtr FPDFPageObj_CreateTextObj(IntPtr doc, IntPtr font, float size);
 [DllImport(Dll)] internal static extern int FPDFText_SetText(IntPtr obj, [MarshalAs(UnmanagedType.LPWStr)] string text);
 [DllImport(Dll)] internal static extern int FPDFTextObj_GetFontSize(IntPtr obj, out float size);
 [DllImport(Dll)] internal static extern int FPDFPageObj_GetFillColor(IntPtr obj, out uint r, out uint g, out uint b, out uint a);
 [DllImport(Dll)] internal static extern void FPDFPage_InsertObject(IntPtr page, IntPtr obj);
 [DllImport(Dll)] internal static extern int FPDFPage_RemoveObject(IntPtr page, IntPtr obj);
 [DllImport(Dll)] internal static extern void FPDFPageObj_Destroy(IntPtr obj);
 [DllImport(Dll)] internal static extern int FPDFPage_CountObjects(IntPtr page);
 [DllImport(Dll)] internal static extern IntPtr FPDFPage_GetObject(IntPtr page, int index);
 [DllImport(Dll)] internal static extern int FPDFPageObj_GetType(IntPtr obj);
 [DllImport(Dll)] internal static extern int FPDFPageObj_GetBounds(IntPtr obj, out float left, out float bottom, out float right, out float top);
 [DllImport(Dll)] internal static extern IntPtr FPDFTextObj_GetFont(IntPtr obj);
 [DllImport(Dll)] internal static extern int FPDFPageObj_CountMarks(IntPtr obj);
 [DllImport(Dll)] internal static extern IntPtr FPDFPageObj_GetMark(IntPtr obj, uint index);
 [DllImport(Dll)] internal static extern IntPtr FPDFPageObj_AddMark(IntPtr obj, [MarshalAs(UnmanagedType.LPStr)] string name);
 [DllImport(Dll)] internal static extern int FPDFPageObjMark_GetName(IntPtr mark, [Out] byte[]? buffer, uint length, out uint required);
 [StructLayout(LayoutKind.Sequential)] internal struct Matrix { public float A,B,C,D,E,F; }
 [DllImport(Dll)] internal static extern int FPDFPageObj_GetMatrix(IntPtr obj, out Matrix matrix);
 [DllImport(Dll)] internal static extern int FPDFPageObj_SetMatrix(IntPtr obj, ref Matrix matrix);
 [DllImport(Dll)] internal static extern int FPDFPageObj_SetFillColor(IntPtr obj, uint r, uint g, uint b, uint a);
 [DllImport(Dll)] internal static extern int FPDFPageObj_SetStrokeColor(IntPtr obj, uint r, uint g, uint b, uint a);
 [DllImport(Dll)] internal static extern int FPDFPageObj_SetStrokeWidth(IntPtr obj, float width);
 [DllImport(Dll)] internal static extern void FPDFPageObj_Transform(IntPtr obj, double a, double b, double c, double d, double e, double f);
 [DllImport(Dll)] internal static extern IntPtr FPDFPageObj_CreateNewPath(float x, float y);
 [DllImport(Dll)] internal static extern int FPDFPath_LineTo(IntPtr path, float x, float y);
 [DllImport(Dll)] internal static extern int FPDFPath_SetDrawMode(IntPtr path, int fill, int stroke);
 [StructLayout(LayoutKind.Sequential)] internal struct PdfRect { public float Left, Top, Right, Bottom; }
 [StructLayout(LayoutKind.Sequential)] internal struct Quad { public float X1,Y1,X2,Y2,X3,Y3,X4,Y4; }
 [DllImport(Dll)] internal static extern IntPtr FPDFPage_CreateAnnot(IntPtr page, int type);
 [DllImport(Dll)] internal static extern int FPDFPage_GetAnnotCount(IntPtr page);
 [DllImport(Dll)] internal static extern IntPtr FPDFPage_GetAnnot(IntPtr page, int index);
 [DllImport(Dll)] internal static extern uint FPDFAnnot_GetStringValue(IntPtr annot, [MarshalAs(UnmanagedType.LPStr)] string key, [Out] byte[]? buffer, uint length);
 [DllImport(Dll)] internal static extern void FPDFPage_CloseAnnot(IntPtr annot);
 [DllImport(Dll)] internal static extern int FPDFAnnot_SetRect(IntPtr annot, ref PdfRect rect);
 [DllImport(Dll)] internal static extern int FPDFAnnot_SetColor(IntPtr annot, int type, uint r, uint g, uint b, uint a);
 [DllImport(Dll)] internal static extern int FPDFAnnot_AppendAttachmentPoints(IntPtr annot, ref Quad quad);
 [DllImport(Dll)] internal static extern int FPDFAnnot_SetStringValue(IntPtr annot, [MarshalAs(UnmanagedType.LPStr)] string key, [MarshalAs(UnmanagedType.LPWStr)] string value);
}
