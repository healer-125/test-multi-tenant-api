using Microsoft.EntityFrameworkCore;
using Serilog;
using MultiTenantApi.Data;
using MultiTenantApi.Middleware;
using MultiTenantApi.Seed;
using MultiTenantApi.Services;

// -------------------------------------------------------------------------
// Bootstrap Serilog from configuration before the host is built so that
// start-up errors are captured.
// -------------------------------------------------------------------------
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // -------------------------------------------------------------------------
    // Serilog
    // -------------------------------------------------------------------------
    builder.Host.UseSerilog((ctx, services, config) =>
        config.ReadFrom.Configuration(ctx.Configuration)
              .ReadFrom.Services(services)
              .Enrich.FromLogContext());

    // -------------------------------------------------------------------------
    // Resolve the data directory relative to the content root
    // -------------------------------------------------------------------------
    var contentRoot  = builder.Environment.ContentRootPath;
    var dataDir      = Path.GetFullPath(
        Path.Combine(contentRoot, builder.Configuration["DataDirectory"] ?? "../data"));
    var metadataDb   = builder.Configuration["MetadataDb"] ?? "metadata.db";
    var metadataPath = Path.Combine(dataDir, metadataDb);

    Directory.CreateDirectory(dataDir);

    // -------------------------------------------------------------------------
    // EF Core — metadata store
    // -------------------------------------------------------------------------
    builder.Services.AddDbContext<MetadataDbContext>(options =>
        options.UseSqlite($"Data Source={metadataPath}"));

    // -------------------------------------------------------------------------
    // Application services
    // -------------------------------------------------------------------------
    builder.Services.AddScoped<ITenantService,       TenantService>();
    builder.Services.AddScoped<IDynamicQueryService, DynamicQueryService>();
    builder.Services.AddScoped<IColumnMappingService, ColumnMappingService>();
    builder.Services.AddScoped<IForeignKeyService,   ForeignKeyService>();
    builder.Services.AddScoped<IExportService,       ExportService>();

    // -------------------------------------------------------------------------
    // MVC + static files
    // -------------------------------------------------------------------------
    builder.Services.AddControllers()
        .AddJsonOptions(o =>
        {
            // Keep property names as-is (PascalCase to match C# models)
            o.JsonSerializerOptions.PropertyNamingPolicy = null;
        });

    // -------------------------------------------------------------------------
    // CORS — open for local dev front-end
    // -------------------------------------------------------------------------
    builder.Services.AddCors(options =>
        options.AddDefaultPolicy(policy =>
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

    // -------------------------------------------------------------------------
    // Build
    // -------------------------------------------------------------------------
    var app = builder.Build();

    // -------------------------------------------------------------------------
    // Seeding (Development only)
    // -------------------------------------------------------------------------
    if (app.Environment.IsDevelopment())
    {
        await DatabaseSeeder.SeedAsync(app.Services, dataDir);
        Log.Information("Database seeding complete. Data directory: {DataDir}", dataDir);
    }

    // -------------------------------------------------------------------------
    // Middleware pipeline
    // -------------------------------------------------------------------------
    app.UseMiddleware<ErrorHandlingMiddleware>();
    app.UseSerilogRequestLogging();
    app.UseCors();
    app.UseStaticFiles();
    app.UseRouting();
    app.MapControllers();

    // Serve index.html for root requests (SPA-style fallback)
    app.MapFallbackToFile("index.html");

    Log.Information("MultiTenantApi starting on {Urls}", string.Join(", ", app.Urls));
    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application start-up failed");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
