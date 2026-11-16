using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PointCloudViewer.Core;

namespace PointCloudViewer.Api.ErrorHandling;

/// <summary>
/// Converts exceptions into RFC 7807 problem responses. Expected domain errors map to 4xx codes
/// with their message; anything else becomes a generic 500 without leaking internals.
/// </summary>
public sealed partial class PointCloudExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<PointCloudExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            UnsupportedPointCloudFormatException e => (StatusCodes.Status415UnsupportedMediaType, "Unsupported point cloud format", e.Message),
            InvalidPointCloudException e => (StatusCodes.Status422UnprocessableEntity, "Invalid point cloud", e.Message),
            PointLimitExceededException e => (StatusCodes.Status413PayloadTooLarge, "Too many points", e.Message),
            BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } e => (StatusCodes.Status413PayloadTooLarge, "Request too large", e.Message),
            OperationCanceledException when httpContext.RequestAborted.IsCancellationRequested => (499, "Client closed request", null),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred", null),
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            LogUnhandled(logger, exception, httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            LogRejected(logger, status, exception.Message);
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail },
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Path}")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string method, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Request rejected with {Status}: {Reason}")]
    private static partial void LogRejected(ILogger logger, int status, string reason);
}
