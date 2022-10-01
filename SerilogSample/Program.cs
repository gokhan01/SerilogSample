using Azure.Core;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.MSSqlServer;
using SerilogSample.Data;
using SerilogSample.Helpers;
using System.Collections.ObjectModel;
using System.Data;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var columnOptions = new ColumnOptions
{
    AdditionalColumns = new Collection<SqlColumn>
    {
        new SqlColumn
            {ColumnName = "EnvironmentName",  DataType = SqlDbType.VarChar, DataLength = 64},

        new SqlColumn
            {ColumnName = "ProductName", DataType = SqlDbType.NVarChar, DataLength = 32},

        new SqlColumn
            {ColumnName = "ThreadId", DataType = SqlDbType.Int},

        new SqlColumn
            {ColumnName = "Body", DataType = SqlDbType.NVarChar, DataLength = -1}
    }
};
columnOptions.Store.Remove(StandardColumn.Properties);
columnOptions.Store.Remove(StandardColumn.MessageTemplate);

Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithEnvironmentName()
            .Enrich.WithThreadId()
            //.Destructure.ByTransforming<HttpRequest>(r => new { Body = r.Body, Method = r.Method })
            .WriteTo.Console()
            .WriteTo.MSSqlServer(
                connectionString: builder.Configuration.GetConnectionString("SerilogSampleContext"),
                sinkOptions: new MSSqlServerSinkOptions { SchemaName = "Logs", TableName = "LogEvents", AutoCreateSqlTable = true },
                columnOptions: columnOptions)
            //.Destructure.With(new RequestDestructuringPolicy())
            .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddDbContext<SerilogSampleContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SerilogSampleContext") ?? throw new InvalidOperationException("Connection string 'SerilogSampleContext' not found.")));

// Add services to the container.
builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseCustomSerilogRequestLogging();
//app.UseSerilogRequestLogging(options =>
//{
//    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
//    {
//        //var body = httpContext.Request.Body;

//        ////This line allows us to set the reader for the request back at the beginning of its stream.
//        //httpContext.Request.EnableBuffering();

//        ////We now need to read the request stream.  First, we create a new byte[] with the same length as the request stream...
//        //var buffer = new byte[Convert.ToInt32(httpContext.Request.ContentLength)];

//        ////...Then we copy the entire request stream into the new buffer.
//        //await httpContext.Request.Body.ReadAsync(buffer, 0, buffer.Length);

//        ////We convert the byte[] into a string using UTF8 encoding...
//        //var bodyAsText = Encoding.UTF8.GetString(buffer);

//        ////..and finally, assign the read body back to the request body, which is allowed because of EnableRewind()
//        //httpContext.Request.Body = body;

//        // Ensure the request's body can be read multiple times 
//        // (for the next middlewares in the pipeline).

//        HttpRequest request = httpContext.Request;
//        request.EnableBuffering();
//        using var streamReader = new StreamReader(request.Body, leaveOpen: true);
//        var requestBody = streamReader.ReadToEndAsync().GetAwaiter().GetResult();
//        // Reset the request's body stream position for 
//        // next middleware in the pipeline.
//        request.Body.Position = 0;

//        diagnosticContext.Set("Body", requestBody);
//    };
//    //options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} {Body} responded {StatusCode} in {Elapsed:0.0000}";
//});

//app.RequestResponseLoggingMiddleware();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
