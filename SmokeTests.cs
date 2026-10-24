using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PolyPDF;
internal static class SmokeTests
{
 static void Check(bool value,string message) { if(!value)throw new Exception("FAIL: "+message); }
 public static void Run()
 {
  var folder=Path.Combine(AppContext.BaseDirectory,"test-results");Directory.CreateDirectory(folder);
  File.WriteAllText(Path.Combine(folder,"result.txt"),"RUNNING");
  File.Delete(Path.Combine(folder,"regressions.txt"));
  try {
   using var doc=PdfDocument.Create();
   doc.Change(()=>doc.AddText(0,new Point(55,770),"PolyPDF verification",26,Color.FromRgb(55,95,215)));
   doc.Change(()=>doc.AddText(0,new Point(55,725),"Hello PDF · Čć Žž Šš Đđ",16,Colors.Black));
   Check(doc.Text(0).Contains("Hello PDF"),"added text extraction");
   var glyphs=doc.Glyphs(0);var first=glyphs.First(g=>g.Text=="H");
   var hit=doc.HitText(0,new Point(first.Bounds.X+first.Bounds.Width/2,first.Bounds.Y+first.Bounds.Height/2));
   Check(hit!=null,"text hit test");
   doc.Change(()=>doc.ReplaceText(0,hit!,"Edited PDF · Čć Žž Šš Đđ"));
   Check(doc.Text(0).Contains("Edited PDF"),"replacement text extraction");
   Check(doc.Undo() && doc.Text(0).Contains("Hello PDF"),"undo edit");Check(doc.Undo(true) && doc.Text(0).Contains("Edited PDF"),"redo edit");
   doc.Change(()=>doc.Highlight(0,doc.Glyphs(0).Where(g=>g.Bounds.Y>715&&g.Bounds.Y<745).Select(g=>g.Bounds)));
   doc.Change(()=>doc.Comment(0,new Point(55,655),"Comment saved by PolyPDF."));
   Check(doc.Comments(0).Contains("Comment saved by PolyPDF."),"comment readback");
   doc.Change(()=>doc.Draw(0,[new Point(55,630),new Point(200,635),new Point(320,625)],Colors.Blue));
   doc.Change(()=>{doc.Blank(1);doc.AddText(1,new Point(55,770),"Second page",24,Colors.Black);});
   doc.Change(()=>doc.Move(0,1));Check(doc.Text(1).Contains("PolyPDF"),"move right");
   doc.Change(()=>doc.Move(1,0));Check(doc.Text(0).Contains("PolyPDF"),"move left");
   var original=doc.Size(0);doc.Change(()=>doc.Rotate(0));Check(Math.Abs(doc.Size(0).Width-original.Height)<1,"rotation dimensions");
   var device=new Point(120,160);var point=doc.ToPdf(0,device,800,600);var reverse=doc.ToScreen(0,point,800,600);Check((device-reverse).Length<2,"rotated coordinates");doc.Undo();
   doc.Extract([1],Path.Combine(folder,"extracted.pdf"));using(var other=new PdfDocument(File.ReadAllBytes(Path.Combine(folder,"extracted.pdf")))){Check(other.Count==1&&other.Text(0).Contains("Second page"),"extract");doc.Change(()=>doc.Merge(other));}
   Check(doc.Count==3,"merge count");doc.Change(()=>doc.Delete(2));Check(doc.Count==2,"delete count");
   var before=doc.Text(0);try{doc.Change(()=>{doc.Delete(1);throw new Exception("rollback");});}catch(Exception ex)when(ex.Message=="rollback"){}Check(doc.Count==2&&doc.Text(0)==before,"transaction rollback");
   var path=Path.Combine(folder,"verification.pdf");doc.Save(path);Check(!doc.Dirty,"saved flag");
   using(var reopened=new PdfDocument(File.ReadAllBytes(path))) {Check(reopened.Count==2&&reopened.Text(0).Contains("Edited PDF"),"save and reopen");var image=reopened.Render(0,1);var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(Path.Combine(folder,"verification.png"));enc.Save(file);}
   Check(MainWindow.ParsePages("1,2-3,2",3).SequenceEqual(new[]{0,1,2}),"page ranges");
   bool rejected=false;try{MainWindow.ParsePages("0-5",3);}catch(FormatException){rejected=true;}Check(rejected,"invalid ranges rejected");
   var fixtures=Path.Combine(Environment.CurrentDirectory,"tests","fixtures");
   if(File.Exists(Path.Combine(fixtures,"external.pdf")))
   {
    using var external=new PdfDocument(File.ReadAllBytes(Path.Combine(fixtures,"external.pdf")));
    Check(external.Count==2&&external.Text(0).Contains("Original office text"),"external PDF read");
    var target=external.HitText(0,new Point(60,735));Check(target!=null,"external text hit");
    external.Change(()=>external.ReplaceText(0,target!,"Changed office text"));Check(external.Text(0).Contains("Changed office text"),"external font edit");
    external.Save(Path.Combine(folder,"external-edited.pdf"));
    using var restricted=new PdfDocument(File.ReadAllBytes(Path.Combine(fixtures,"restricted.pdf")),password:"reader");
    Check(restricted.SourceEncrypted&&!restricted.CanEdit&&!restricted.CanCopy&&restricted.CanPrint,"PDF permission enforcement");
    var blocked=false;try{restricted.Change(()=>restricted.Delete(1));}catch(InvalidOperationException){blocked=true;}Check(blocked&&restricted.Count==2,"restricted edit rejected");
    using var passwordDoc=new PdfDocument(File.ReadAllBytes(Path.Combine(fixtures,"password.pdf")),password:"reader");
    Check(passwordDoc.CanEdit&&passwordDoc.SourceEncrypted,"password PDF open");
    passwordDoc.Change(()=>passwordDoc.AddText(0,new Point(50,600),"Password PDF edit",14,Colors.Black));
    passwordDoc.Save(Path.Combine(folder,"unencrypted-copy.pdf"));
    using var copy=new PdfDocument(File.ReadAllBytes(Path.Combine(folder,"unencrypted-copy.pdf")));
    Check(!copy.SourceEncrypted&&copy.Text(0).Contains("Password PDF edit"),"unencrypted copy round trip");
   }
   RegressionTests.Run(folder);
   TextMoveTests.Run(folder);
   UpdateTests.Run(folder);
   File.WriteAllText(Path.Combine(folder,"result.txt"),"PASS: text add/edit, Unicode, undo/redo, highlights, comments, drawing, page order, rotate, coordinate transforms, extract, merge, delete, rollback, save/reopen, rendering, page-range validation. Extended bug-fix and crash-recovery checks: see regressions.txt.");
  }catch(Exception ex){File.WriteAllText(Path.Combine(folder,"result.txt"),ex.ToString());Environment.ExitCode=1;}
 }
}
