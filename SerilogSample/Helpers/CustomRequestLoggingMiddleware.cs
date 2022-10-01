using Microsoft.AspNetCore.Http.Features;
using Serilog.AspNetCore;
using Serilog.Events;
using Serilog.Extensions.Hosting;
using Serilog.Parsing;
using Serilog;
using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace SerilogSample.Helpers
{
    public class CustomRequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;

        private readonly DiagnosticContext _diagnosticContext;

        private readonly MessageTemplate _messageTemplate;

        private readonly Action<IDiagnosticContext, HttpContext> _enrichDiagnosticContext;

        private readonly Func<HttpContext, double, Exception, LogEventLevel> _getLevel;

        private readonly Serilog.ILogger _logger;

        private readonly bool _includeQueryInRequestPath;

        private static readonly LogEventProperty[] NoProperties = new LogEventProperty[0];

        public CustomRequestLoggingMiddleware(RequestDelegate next, DiagnosticContext diagnosticContext, RequestLoggingOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            _next = next ?? throw new ArgumentNullException("next");
            _diagnosticContext = diagnosticContext ?? throw new ArgumentNullException("diagnosticContext");
            _getLevel = options.GetLevel;
            _enrichDiagnosticContext = options.EnrichDiagnosticContext; //options.MessageTemplate
            _messageTemplate = new MessageTemplateParser().Parse("HTTP {RequestMethod} {RequestPath} {Body} responded {StatusCode} in {Elapsed:0.0000}");
            _logger = options.Logger?.ForContext<CustomRequestLoggingMiddleware>();
            _includeQueryInRequestPath = options.IncludeQueryInRequestPath;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            if (httpContext == null)
            {
                throw new ArgumentNullException("httpContext");
            }

            long start = Stopwatch.GetTimestamp();
            DiagnosticContextCollector collector = _diagnosticContext.BeginCollection();
            try
            {
                string requestBody = await ReadBodyFromRequest(httpContext.Request);
                await _next(httpContext);
                double elapsedMilliseconds = GetElapsedMilliseconds(start, Stopwatch.GetTimestamp());
                int statusCode = httpContext.Response.StatusCode;
                LogCompletion(httpContext, collector, statusCode, elapsedMilliseconds, null, requestBody);
            }
            catch (Exception ex) when (LogCompletion(httpContext, collector, 500, GetElapsedMilliseconds(start, Stopwatch.GetTimestamp()), ex))
            {
            }
            finally
            {
                collector.Dispose();
            }
        }

        private bool LogCompletion(HttpContext httpContext, DiagnosticContextCollector collector, int statusCode, double elapsedMs, Exception ex, string body = null)
        {
            Serilog.ILogger logger = _logger ?? Log.ForContext<CustomRequestLoggingMiddleware>();
            LogEventLevel level = _getLevel(httpContext, elapsedMs, ex);
            if (!logger.IsEnabled(level))
            {
                return false;
            }

            _enrichDiagnosticContext?.Invoke(_diagnosticContext, httpContext);
            if (!collector.TryComplete(out var properties, out var exception))
            {
                properties = NoProperties;
            }

            IEnumerable<LogEventProperty> properties2 = properties.Concat(new LogEventProperty[5]
            {
                new LogEventProperty("RequestMethod", new ScalarValue(httpContext.Request.Method)),
                new LogEventProperty("RequestPath", new ScalarValue(GetPath(httpContext, _includeQueryInRequestPath))),
                new LogEventProperty("StatusCode", new ScalarValue(statusCode)),
                new LogEventProperty("Elapsed", new ScalarValue(elapsedMs)),
                new LogEventProperty("Body", new ScalarValue(body))
            });
            LogEvent logEvent = new LogEvent(DateTimeOffset.Now, level, ex ?? exception, _messageTemplate, properties2);
            logger.Write(logEvent);
            return false;
        }

        private static double GetElapsedMilliseconds(long start, long stop)
        {
            return (double)((stop - start) * 1000) / (double)Stopwatch.Frequency;
        }

        private static string GetPath(HttpContext httpContext, bool includeQueryInRequestPath)
        {
            string text = ((!includeQueryInRequestPath) ? httpContext.Features.Get<IHttpRequestFeature>()?.Path : httpContext.Features.Get<IHttpRequestFeature>()?.RawTarget);
            if (string.IsNullOrEmpty(text))
            {
                text = httpContext.Request.Path.ToString();
            }

            return text;
        }

        private async Task<string> ReadBodyFromRequest(HttpRequest request)
        {
            // Ensure the request's body can be read multiple times 
            // (for the next middlewares in the pipeline).
            request.EnableBuffering();
            using var streamReader = new StreamReader(request.Body, leaveOpen: true);
            var requestBody = await streamReader.ReadToEndAsync();
            // Reset the request's body stream position for 
            // next middleware in the pipeline.
            request.Body.Position = 0;
            return requestBody;
        }
    }
}
