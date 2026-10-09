using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using TrainHub.Application.Common.Security;
using TrainHub.Application.Interfaces;
using TrainHub.Infrastructure.Services;

namespace TrainHub.Infrastructure.Authentication;

/// <summary>
/// Extension methods for configuring JWT authentication and token generation services.
/// </summary>
public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Bind & Configure JwtSettings ────────────────────────────
        var jwtSection = configuration.GetSection(JwtSettings.SectionName);
        if (!jwtSection.Exists())
        {
            jwtSection = configuration.GetSection("Jwt");
        }

        services.Configure<JwtSettings>(jwtSection);

        var jwtSettings = jwtSection.Get<JwtSettings>() ?? new JwtSettings();

        // ── Register JWT Token Generator ────────────────────────────
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

        // ── Configure JWT Bearer Authentication ──────────────────────
        var secret = jwtSettings.Secret;
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException(
                $"JWT Secret key is missing. Please configure a valid secret under '{JwtSettings.SectionName}:Secret'.");
        }

        var keyBytes = Encoding.UTF8.GetBytes(secret);
        if (keyBytes.Length < 32)
        {
            throw new InvalidOperationException(
                "JWT Secret key must be at least 32 characters (256 bits) for HMAC-SHA256 signing.");
        }

        var signingKey = new SymmetricSecurityKey(keyBytes);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = jwtSettings.RequireHttpsMetadata;
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = !string.IsNullOrWhiteSpace(jwtSettings.Issuer),
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = !string.IsNullOrWhiteSpace(jwtSettings.Audience),
                ValidAudience = jwtSettings.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                RoleClaimType = ClaimTypes.Role,
                NameClaimType = ClaimTypes.NameIdentifier
            };

            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    if (context.Exception is SecurityTokenExpiredException)
                    {
                        context.Response.Headers.Append("Token-Expired", "true");
                    }
                    return Task.CompletedTask;
                },
                OnChallenge = async context =>
                {
                    context.HandleResponse();
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/problem+json";

                    var problemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status401Unauthorized,
                        Title = "Unauthorized",
                        Detail = string.IsNullOrWhiteSpace(context.ErrorDescription)
                            ? "You are not authorized to access this resource. A valid Bearer token is required."
                            : context.ErrorDescription,
                        Instance = context.Request.Path,
                        Extensions =
                        {
                            ["traceId"] = context.HttpContext.TraceIdentifier
                        }
                    };

                    var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                    await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails, jsonOptions));
                },
                OnForbidden = async context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/problem+json";

                    var problemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status403Forbidden,
                        Title = "Forbidden",
                        Detail = "You do not have permission to access this resource.",
                        Instance = context.Request.Path,
                        Extensions =
                        {
                            ["traceId"] = context.HttpContext.TraceIdentifier
                        }
                    };

                    var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                    await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails, jsonOptions));
                }
            };
        });

        return services;
    }
}
