using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Filters;
using SelfStorageSystem.Infrastructure.Extensions;
using SelfStorageSystem.Middlewares;
using System.Diagnostics;
using System.Text;

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

var seedRequested = args.Contains("--seed", StringComparer.OrdinalIgnoreCase);
var configArgs = args.Where(a => !string.Equals(a, "--seed", StringComparison.OrdinalIgnoreCase)).ToArray();

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = configArgs,
    ContentRootPath = contentRoot
});

builder.Configuration
    .SetBasePath(contentRoot)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

if (configArgs.Length > 0)
{
    builder.Configuration.AddCommandLine(configArgs);
}

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

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
builder.Services.AddControllers(options =>
{
    options.Filters.Add<FluentValidationFilter>();
    options.Filters.Add<TraceIdResultFilter>();
});
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(kvp => kvp.Value?.Errors.Count > 0)
            .ToDictionary(
                kvp => string.IsNullOrWhiteSpace(kvp.Key)
                    ? "_"
                    : char.ToLowerInvariant(kvp.Key[0]) + kvp.Key[1..],
                kvp => kvp.Value!.Errors
                    .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Invalid input." : e.ErrorMessage)
                    .Distinct()
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);

        var traceId = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
        var payload = ApiResponse.Fail("One or more validation errors occurred.", "Validation.Failed", errors, traceId);
        return new ObjectResult(payload) { StatusCode = StatusCodes.Status422UnprocessableEntity };
    };
});
builder.Services.AddValidatorsFromAssemblyContaining<SelfStorageSystem.Application.DTOs.GoogleUserInfo>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSignalR();
builder.Services.AddScoped<SelfStorageSystem.Application.Interfaces.IRealtimeNotifier, SelfStorageSystem.Services.RealtimeNotifier>();
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
            policy.SetIsOriginAllowed(origin =>
                  {
                      if (corsSettings.AllowedOrigins.Any(o => string.Equals(o.TrimEnd('/'), origin.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)))
                          return true;
                      if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                      {
                          return uri.Host.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase)
                              || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                              || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase);
                      }
                      return false;
                  })
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
builder.Services.PostConfigure<MediaSettings>(opts =>
{
    if (string.IsNullOrWhiteSpace(opts.WebRootPath))
    {
        opts.WebRootPath = wwwRootPath;
    }
});

// Register JWT Authentication & Authorization
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<Microsoft.Extensions.Options.IOptions<JwtSettings>>((options, jwtOptions) =>
    {
        var jwtSettings = jwtOptions.Value
            ?? throw new InvalidOperationException("Configuration 'JwtSettings' is not defined in appsettings.json.");

        if (string.IsNullOrWhiteSpace(jwtSettings.Secret) || jwtSettings.Secret.Length < 32)
        {
            throw new InvalidOperationException("JwtSettings:Secret must be configured and at least 32 characters long.");
        }

        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
            ValidateIssuer = !string.IsNullOrWhiteSpace(jwtSettings.Issuer),
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = !string.IsNullOrWhiteSpace(jwtSettings.Audience),
            ValidAudience = jwtSettings.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },
            OnChallenge = async context =>
            {
                if (context.HttpContext.Request.Path.StartsWithSegments("/hubs")) return;
                context.HandleResponse();
                if (context.Response.HasStarted) return;
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                var traceId = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
                await context.Response.WriteAsJsonAsync(
                    ApiResponse.Fail("Authentication is required.", "Auth.Unauthorized", null, traceId));
            },
            OnForbidden = async context =>
            {
                if (context.HttpContext.Request.Path.StartsWithSegments("/hubs")) return;
                if (context.Response.HasStarted) return;
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                var traceId = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
                await context.Response.WriteAsJsonAsync(
                    ApiResponse.Fail("You do not have permission to access this resource.", "Auth.Forbidden", null, traceId));
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Global exception handler to avoid leaking stack traces, exception messages, or internal errors
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        var exFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var logger = context.RequestServices.GetService<ILogger<Program>>();
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        logger?.LogError(exFeature?.Error, "Unhandled exception in request pipeline [TraceId: {TraceId}]: {Message}", traceId, exFeature?.Error?.Message);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        var response = ApiResponse.Fail(
            "An internal system error occurred. Please contact support or try again later.",
            "System.InternalError",
            null,
            traceId);
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
app.MapHub<SelfStorageSystem.Hubs.NotificationHub>("/hubs/notifications");

if (seedRequested)
{
    await app.Services.SeedDatabaseAsync();
    Console.WriteLine("Database seeding completed (--seed).");
    return;
}

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
