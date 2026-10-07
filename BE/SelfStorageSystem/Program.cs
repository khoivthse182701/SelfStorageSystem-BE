using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Infrastructure.Extensions;
using SelfStorageSystem.Middlewares;

var builder = WebApplication.CreateBuilder(args);

// Hide server technology banner from HTTP response headers
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
});


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
        else
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

// Register Infrastructure Services
builder.Services.AddInfrastructureServices(builder.Configuration);

// Configure Rate Limiting (Anti-spam / Brute-force protection)
var rateLimitSettings = builder.Configuration
    .GetSection(RateLimitingSettings.SectionName)
    .Get<RateLimitingSettings>() ?? new RateLimitingSettings();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        var response = ApiResponse.Fail("Too many requests received in a short period. Please try again later.");
        await context.HttpContext.Response.WriteAsJsonAsync(response, cancellationToken: token);
    };

    // Keep strict limiter specifically for Auth endpoints (Login, Register, OTP anti-spam)
    options.AddPolicy(RateLimitingSettings.AuthPolicyName, httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rateLimitSettings.AuthPermitLimit > 0 ? rateLimitSettings.AuthPermitLimit : 10,
            Window = TimeSpan.FromSeconds(rateLimitSettings.AuthWindowSeconds > 0 ? rateLimitSettings.AuthWindowSeconds : 60),
            QueueLimit = 0
        });
    });
});

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

app.UseStaticFiles();
app.UseCors(CorsSettings.PolicyName);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program
{
}
