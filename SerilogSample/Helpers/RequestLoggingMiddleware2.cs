using Microsoft.Extensions.Options;
using Serilog.AspNetCore;
using System.Text;

namespace SerilogSample.Helpers
{
    public class RequestLoggingMiddleware2
    {
        private readonly RequestDelegate _next;

        public RequestLoggingMiddleware2(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ILogger<RequestLoggingMiddleware2> logger)
        {
            //First, get the incoming request
            string request = await ReadBodyFromRequest(context.Request);

            logger.LogInformation("{Body}", request);

            await _next(context);
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

        //private async Task<string> FormatResponse(HttpResponse response)
        //{
        //    //We need to read the response stream from the beginning...
        //    response.Body.Seek(0, SeekOrigin.Begin);

        //    //...and copy it into a string
        //    string text = await new StreamReader(response.Body).ReadToEndAsync();

        //    //We need to reset the reader for the response so that the client can read it.
        //    response.Body.Seek(0, SeekOrigin.Begin);

        //    //Return the string for the response, including the status code (e.g. 200, 404, 401, etc.)
        //    return $"{response.StatusCode}: {text}";
        //}
    }

    public static class MyCustomMiddlewareExtensions
    {
        public static IApplicationBuilder RequestResponseLoggingMiddleware(
            this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RequestLoggingMiddleware2>();
        }

        public static IApplicationBuilder UseCustomSerilogRequestLogging(this IApplicationBuilder app, Action<RequestLoggingOptions> configureOptions = null)
        {
            if (app == null)
            {
                throw new ArgumentNullException("app");
            }

            RequestLoggingOptions requestLoggingOptions = app.ApplicationServices.GetService<IOptions<RequestLoggingOptions>>()?.Value ?? new RequestLoggingOptions();
            configureOptions?.Invoke(requestLoggingOptions);
            if (requestLoggingOptions.MessageTemplate == null)
            {
                throw new ArgumentException("MessageTemplate cannot be null.");
            }

            if (requestLoggingOptions.GetLevel == null)
            {
                throw new ArgumentException("GetLevel cannot be null.");
            }

            return app.UseMiddleware<CustomRequestLoggingMiddleware>(new object[1] { requestLoggingOptions });
        }
    }
}
