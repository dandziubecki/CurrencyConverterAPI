using System.Security.Claims;
using System.Text;
using CurrencyConverterAPI.Models;
using CurrencyConverterAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace CurrencyConverterAPI;

public static class CurrencyConverterApi
{
    public static void AddApi(this WebApplication webApplication, IConfiguration appConfiguration)
    {
        webApplication.MapGet("/rates/latest", async ([FromQuery] string baseCurrency,
                IHeaderBasedCurrencyConverterFactory resolver) =>
            {
                if (string.IsNullOrWhiteSpace(baseCurrency))
                {
                    return Results.BadRequest("Base currency cannot be empty.");
                }

                var currencyService = resolver.ResolveService();
                var result = await currencyService.GetLatestRatesAsync(baseCurrency.ToUpper());
                return result is not null
                    ? Results.Ok(result)
                    : Results.NotFound($"Rates for base currency '{baseCurrency}' not found.");
            })
            .WithName("GetLatestRates")
            .WithOpenApi()
            .RequireAuthorization(ApiConstants.Authentication.UserPolicy)
            .RequireRateLimiting(ApiConstants.RateLimiting.FixedPolicy);

        webApplication.MapGet("/convert", async ([FromQuery] string from, [FromQuery] string to,
                [FromQuery] decimal amount,
                IHeaderBasedCurrencyConverterFactory resolver) =>
            {
                if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to) || amount <= 0)
                {
                    return Results.BadRequest(
                        "'from', 'to' and 'amount' parameters are required and amount must be positive.");
                }

                var currencyService = resolver.ResolveService();
                if (currencyService.IsCurrencyExcluded(from) || currencyService.IsCurrencyExcluded(to))
                {
                    return Results.BadRequest("Currency conversion involving TRY, PLN, THB, or MXN is not supported.");
                }

                var result = await currencyService.ConvertCurrencyAsync(from.ToUpper(), to.ToUpper(), amount);
                return result is not null
                    ? Results.Ok(result)
                    : Results.NotFound("Could not perform currency conversion.");
            })
            .WithName("ConvertCurrency")
            .WithOpenApi()
            .RequireAuthorization(ApiConstants.Authentication.UserPolicy)
            .RequireRateLimiting(ApiConstants.RateLimiting.FixedPolicy);

        webApplication.MapGet("/rates/historical", async (
                [FromQuery] string baseCurrency,
                [FromQuery] DateOnly startDate,
                [FromQuery] DateOnly endDate,
                [FromQuery] int page,
                [FromQuery] int pageSize,
                IHeaderBasedCurrencyConverterFactory resolver) =>
            {
                if (string.IsNullOrWhiteSpace(baseCurrency) || startDate == default || endDate == default ||
                    page <= 0 || pageSize <= 0)
                {
                    return Results.BadRequest(
                        "All parameters (baseCurrency, startDate, endDate, page, pageSize) are required and must be valid.");
                }

                if (startDate > endDate)
                {
                    return Results.BadRequest("startDate cannot be after endDate.");
                }

                var currencyService = resolver.ResolveService();
                var result =
                    await currencyService.GetHistoricalRatesAsync(baseCurrency.ToUpper(), startDate, endDate, page,
                        pageSize);
                return result is not null
                    ? Results.Ok(result)
                    : Results.NotFound("Could not retrieve historical rates.");
            })
            .WithName("GetHistoricalRates")
            .WithOpenApi()
            .RequireAuthorization(ApiConstants.Authentication.AdminOnlyPolicy)
            .RequireRateLimiting(ApiConstants.RateLimiting.FixedPolicy);

        webApplication.MapPost("/token", (UserLogin user) =>
        {
            if (user.Username == "admin" && user.Password == "password")
            {
                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Username),
                    new Claim(ClaimTypes.Role, ApiConstants.Authentication.AdminRole)
                };
                return Results.Ok(GenerateToken(claims));
            }

            if (user.Username == "user" && user.Password == "password")
            {
                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Username),
                    new Claim(ClaimTypes.Role, ApiConstants.Authentication.UserRole)
                };
                return Results.Ok(GenerateToken(claims));
            }

            return Results.Unauthorized();

            string GenerateToken(IEnumerable<Claim> claims)
            {
                var issuer = appConfiguration[ApiConstants.Configuration.JwtIssuer];
                var audience = appConfiguration[ApiConstants.Configuration.JwtAudience];
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(appConfiguration[ApiConstants.Configuration.JwtKey]!));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
                    issuer: issuer,
                    audience: audience,
                    claims: claims,
                    expires: DateTime.Now.AddMinutes(30),
                    signingCredentials: creds
                );
                return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
            }
        }).AllowAnonymous();
    }
}
