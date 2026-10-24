using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;

namespace PolyPDF;

internal static class PrintService
{
 public static Task PrintAsync(PdfDocument document, string fullName, byte[] ticket, Size size)
 {
  var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
  var thread = new Thread(() =>
  {
   try
   {
    var split = fullName.LastIndexOf('\\');
    using PrintServer server = fullName.StartsWith("\\\\", StringComparison.Ordinal)
     ? new PrintServer(fullName[..split]) : new LocalPrintServer();
    using var queue = new PrintQueue(server, fullName.StartsWith("\\\\", StringComparison.Ordinal) ? fullName[(split+1)..] : fullName);
    using var stream = new MemoryStream(ticket);
    var dialog = new PrintDialog { PrintQueue = queue, PrintTicket = new PrintTicket(stream) };
    dialog.PrintDocument(new PdfPaginator(document,size),document.Name);
    completion.SetResult();
   }
   catch (Exception ex) { completion.SetException(ex); }
  }) { IsBackground = true, Name = "PolyPDF print worker" };
  thread.SetApartmentState(ApartmentState.STA);
  thread.Start();
  return completion.Task;
 }
}
