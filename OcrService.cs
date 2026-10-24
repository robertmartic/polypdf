using System.IO;
using System.Windows.Media.Imaging;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace PolyPDF;
internal static class OcrService
{
 public static async Task<string> Recognize(BitmapSource image, string language)
 {
  var engine=OcrEngine.TryCreateFromLanguage(new Language(language));
  if(engine is null) throw new InvalidOperationException("The Windows OCR language pack for " + language + " is not installed. Add it in Windows Settings > Time & language > Language & region > Language options, then try again.");
  var encoder=new PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image)); using var memory=new MemoryStream(); encoder.Save(memory);
  using var stream=new InMemoryRandomAccessStream();
  using(var writer=new DataWriter(stream.GetOutputStreamAt(0))) { writer.WriteBytes(memory.ToArray()); await writer.StoreAsync(); await writer.FlushAsync(); }
  stream.Seek(0); var decoder=await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream); using var bitmap=await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied);
  var result=await engine.RecognizeAsync(bitmap); return string.Join(Environment.NewLine,result.Lines.Select(l=>l.Text));
 }
}
