using System.Diagnostics.CodeAnalysis;
using CurrencyConverterAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using CurrencyConverterAPI;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("Starting up CurrencyConverterAPI");

try
{

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            In = ParameterLocation.Header,
            Description = "Please enter a valid token",
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            BearerFormat = "JWT",
            Scheme = "Bearer"
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

    builder.Services.AddMemoryCache();

    var retryPolicy = HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

    var circuitBreakerPolicy = HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));

    builder.Services.AddHttpClient(ApiConstants.HttpClients.Frankfurter, client =>
    {
        client.BaseAddress = new Uri(builder.Configuration[ApiConstants.Configuration.FrankfurterApiBaseUrl]
            ?? throw new InvalidOperationException("FrankfurterApi:BaseUrl configuration is missing."));
    })
    .AddPolicyHandler(retryPolicy)
    .AddPolicyHandler(circuitBreakerPolicy);

    builder.Services.AddHttpContextAccessor();
    
    builder.Services.AddKeyedScoped<ICurrencyService, DummyExchangeRateApiService>(nameof(DummyExchangeRateApiService));
    builder.Services.AddKeyedScoped<ICurrencyService, FrankfurterApiCurrencyService>(nameof(FrankfurterApiCurrencyService));
    
    
    builder.Services.AddScoped<IHeaderBasedCurrencyConverterFactory, HeaderBasedCurrencyConverterFactory>();

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(builder.Environment.ApplicationName))
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddConsoleExporter());

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration[ApiConstants.Configuration.JwtIssuer],
            ValidAudience = builder.Configuration[ApiConstants.Configuration.JwtAudience],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration[ApiConstants.Configuration.JwtKey]
                ?? throw new InvalidOperationException("JWT Key not configured.")))
        };
    });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy(ApiConstants.Authentication.AdminOnlyPolicy, policy => policy.RequireRole(ApiConstants.Authentication.AdminRole));
        options.AddPolicy(ApiConstants.Authentication.UserPolicy, policy => policy.RequireRole(ApiConstants.Authentication.UserRole, ApiConstants.Authentication.AdminRole));
    });

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.AddFixedWindowLimiter(ApiConstants.RateLimiting.FixedPolicy, opt =>
        {
            opt.PermitLimit = builder.Configuration.GetValue<int>(ApiConstants.Configuration.RateLimitingPermitLimit);
            opt.Window = TimeSpan.FromSeconds(builder.Configuration.GetValue<int>(ApiConstants.Configuration.RateLimitingWindow));
            opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            opt.QueueLimit = builder.Configuration.GetValue<int>(ApiConstants.Configuration.RateLimitingQueueLimit);
        });
    });
    
    var app = builder.Build();
    
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("ClientIP", httpContext.Connection.RemoteIpAddress?.ToString());
            var clientId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
            diagnosticContext.Set("ClientId", clientId);
        };
    });
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();

    app.UseAuthentication();
    app.UseAuthorization();

    app.UseRateLimiter();
    
    app.AddApi(app.Configuration);
    
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

[ExcludeFromCodeCoverage]
public partial class Program { }
