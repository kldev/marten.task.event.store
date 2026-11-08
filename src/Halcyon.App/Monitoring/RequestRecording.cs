using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace Halcyon.App.Monitoring;

/// <summary>
/// Records every HTTP answer - success or error - into the database queue (ADR-0011). First in the pipeline, so it
/// sees the status the client got: after the exception handler turned an exception into 500 and after the status-code
/// pages answered 404. Without the query string: it can carry personal data (a return address, a search).
/// </summary>
public sealed class RequestRecording(
    RequestDelegate next,
    MonitoringQueues queues,
    IOptions<MonitoringOptions> options,
    TimeProvider time
)
{
    public async Task InvokeAsync(HttpContext http)
    {
        var path = http.Request.Path.Value ?? "/";
        if (Ignored(path))
        {
            await next(http);
            return;
        }

        var at = time.GetUtcNow();
        var started = Stopwatch.GetTimestamp();
        var failed = false;
        try
        {
            await next(http);
        }
        catch
        {
            // Without an exception handler (Development) the client gets 500.
            failed = true;
            throw;
        }
        finally
        {
            queues.Requests.Enqueue(
                new RequestRecord(
                    at,
                    http.Request.Method,
                    path.Length > 1_000 ? path[..1_000] : path,
                    failed ? StatusCodes.Status500InternalServerError : http.Response.StatusCode,
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                    http.User.Identity?.IsAuthenticated == true ? http.User.Identity.Name : null,
                    Activity.Current?.TraceId.ToHexString()
                )
            );
        }
    }

    private bool Ignored(string path)
    {
        foreach (var prefix in options.Value.IgnoredPaths)
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}
