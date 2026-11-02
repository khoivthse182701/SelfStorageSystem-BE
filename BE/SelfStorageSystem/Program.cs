using Microsoft.OpenApi.Models;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Infrastructure.Extensions;
using SelfStorageSystem.Middlewares;

// Global process exception traps to diagnose silent crashes (exit code -1 / 0xffffffff)
AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine($"[FATAL CRASH] AppDomain.CurrentDomain.UnhandledException: {eventArgs.ExceptionObject}");
    Console.ResetColor();
};

TaskScheduler.UnobservedTaskException += (sender, eventArgs) =>
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine($"[FATAL CRASH] TaskScheduler.UnobservedTaskException: {eventArgs.Exception}");
    Console.ResetColor();
    eventArgs.SetObserved();
};

// Robust content root detection: if current working directory doesn't have appsettings.json,
// fallback to AppContext.BaseDirectory so executing SelfStorageSystem.exe from any folder finds appsettings.json
var contentRoot = File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"))
    ? Directory.GetCurrentDirectory()
    : AppContext.BaseDirectory;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = contentRoot
});

builder.Configuration
    .SetBasePath(contentRoot)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// In .NET 8, prevent an unhandled exception in background worker from crashing the entire host process with exit code -1
builder.Services.Configure<HostOptions>(options =>
{
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
});

// Hide server technology banner from HTTP response headers
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
});

// Ensure wwwroot directory exists so StaticFiles and WebRootPath are properly initialized
var wwwRootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
if (!Directory.Exists(wwwRootPath))
{
    Directory.CreateDirectory(wwwRootPath);
}
builder.Environment.WebRootPath = wwwRootPath;

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Self-Storage Facility Rental and Management System API",
        Version = "v1",
        Description = "Self-Storage Management Backend API (Clean Architecture)"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT Bearer token."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Configure CORS
var corsSettings = builder.Configuration
    .GetSection(CorsSettings.SectionName)
    .Get<CorsSettings>() ?? new CorsSettings();

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsSettings.PolicyName, policy =>
    {
        if (corsSettings.AllowedOrigins.Length > 0)
        {
            policy.WithOrigins(corsSettings.AllowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(origin =>
            {
                if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                {
                    return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                        || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase);
                }
                return false;
            })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
        }
        else
        {
            throw new InvalidOperationException("CORS AllowedOrigins must be configured for non-development environments.");
        }
    });
});

// Register Infrastructure Services
builder.Services.AddInfrastructureServices(builder.Configuration);

var app = builder.Build();

// Global exception handler to avoid leaking stack traces or internal errors
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        var exFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var logger = context.RequestServices.GetService<ILogger<Program>>();
        logger?.LogError(exFeature?.Error, "Unhandled exception in request pipeline: {Message}", exFeature?.Error?.Message);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        var errorMsg = app.Environment.IsDevelopment() && exFeature?.Error != null
            ? (exFeature.Error.InnerException != null ? $"{exFeature.Error.Message} --> {exFeature.Error.InnerException.Message}" : exFeature.Error.Message)
            : "An internal system error occurred. Please contact administrator or try again later.";
        var response = ApiResponse.Fail(errorMsg);
        await context.Response.WriteAsJsonAsync(response);
    });
});

// Map typed domain errors (IHasAppError) to structured JSON with correct HTTP status
app.UseMiddleware<AppExceptionMiddleware>();

// Defensive security response headers
app.UseMiddleware<SecurityHeadersMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Self-Storage System API v1");
    });
    app.MapGet("/", () => Results.Redirect("/swagger"));
}

app.UseCors(CorsSettings.PolicyName);
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

try
{
    Console.WriteLine($"Starting SelfStorageSystem API on environment: {app.Environment.EnvironmentName} (Root: {app.Environment.ContentRootPath})...");
    app.Run();
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine($"[FATAL CRASH] Application host terminated unexpectedly: {ex}");
    Console.ResetColor();
    throw;
}
finally
{
    Console.WriteLine("SelfStorageSystem API stopped.");
}

public partial class Program
{
}
