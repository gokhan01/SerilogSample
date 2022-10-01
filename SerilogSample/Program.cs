using Microsoft.AspNetCore.HttpLogging;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.MSSqlServer;
using SerilogSample.Data;
using System.Collections.ObjectModel;
using System.Data;

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
            {ColumnName = "ThreadId", DataType = SqlDbType.Int}
    }
};
columnOptions.Store.Remove(StandardColumn.Properties);
columnOptions.Store.Remove(StandardColumn.MessageTemplate);

Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithEnvironmentName()
            .Enrich.WithThreadId()
            .WriteTo.Console()
            .WriteTo.MSSqlServer(
                connectionString: builder.Configuration.GetConnectionString("SerilogSampleContext"),
                sinkOptions: new MSSqlServerSinkOptions { SchemaName = "Logs", TableName = "LogEvents", AutoCreateSqlTable = true },
                columnOptions: columnOptions
            ).CreateLogger();

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

app.UseSerilogRequestLogging();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
