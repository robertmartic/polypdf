using System.Collections.Concurrent;

namespace PolyPDF;

// Every PDFium call uses one thread; PDFium must never be entered concurrently.
internal sealed class PdfWorker
{
 static readonly PdfWorker Instance = new();
 readonly BlockingCollection<Action> queue = new();
 readonly Thread thread;

 PdfWorker()
 {
  thread = new Thread(() => { foreach (var action in queue.GetConsumingEnumerable()) action(); })
  { IsBackground = true, Name = "PolyPDF PDF worker" };
  thread.Start();
 }

 public static Task<T> Run<T>(Func<T> action)
 {
  if (Thread.CurrentThread == Instance.thread)
  {
   try { return Task.FromResult(action()); } catch (Exception ex) { return Task.FromException<T>(ex); }
  }
  var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
  Instance.queue.Add(() => { try { result.SetResult(action()); } catch (Exception ex) { result.SetException(ex); } });
  return result.Task;
 }

 public static Task Run(Action action) => Run(() => { action(); return true; });
 public static T Invoke<T>(Func<T> action) => Run(action).GetAwaiter().GetResult();
 public static void Invoke(Action action) => Run(action).GetAwaiter().GetResult();
 public static void Initialize() => Invoke(PdfNative.FPDF_InitLibrary);
}
