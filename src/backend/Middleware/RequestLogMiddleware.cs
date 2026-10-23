using System.Diagnostics;

namespace backend.Middleware;

public sealed class RequestLogMiddleware(RequestDelegate next, ILogger<RequestLogMiddleware> logger)
{
    private const string Key = "ffa.correlation_id";
    public static string CorrelationId(HttpContext context)
        => context.Items[Key] as string ?? (string)(context.Items[Key] = Guid.NewGuid().ToString("N"));

    public async Task Invoke(HttpContext context)
    {
        var supplied = context.Request.Headers["X-Correlation-Id"];
        context.Items[Key] = supplied.Count == 1 && Guid.TryParseExact(supplied[0], "N", out var parsed)
            ? parsed.ToString("N") : Guid.NewGuid().ToString("N");
        var correlationId = CorrelationId(context);
        context.Response.Headers["X-Correlation-Id"] = correlationId;
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["TraceId"] = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier
        });
        var started = Stopwatch.GetTimestamp();
        try { await next(context); }
        finally
        {
            logger.LogInformation("HTTP {Method} {Endpoint} completed with {StatusCode} in {ElapsedMs}ms",
                context.Request.Method, context.GetEndpoint()?.DisplayName ?? "unmatched",
                context.RequestAborted.IsCancellationRequested ? 499 : context.Response.StatusCode,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }
}
