using System.IO;
using System.Net;
using System.Net.Http;

namespace PolyPDF;

internal static class UpdateMessages
{
 internal static string Failure(Exception error) => error switch
 {
  OperationCanceledException => "The request timed out. Check your connection and try again.",
  HttpRequestException { StatusCode: HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests } => "GitHub is limiting update requests. Wait a few minutes and try again.",
  HttpRequestException { StatusCode: HttpStatusCode.NotFound } => "The update is not available on GitHub. Try checking for updates again later.",
  HttpRequestException => "Could not connect to GitHub. Check your internet connection and try again.",
  InvalidDataException => "The update package or release information could not be verified. Check for updates again. If the problem continues, use the official GitHub installer.",
  UnauthorizedAccessException => "PolyPDF could not access the update folder. Check folder permissions or use the official installer.",
  IOException => error.Message,
  _ => "The update could not be completed. Try again, or use the official installer if the problem continues."
 };
}
