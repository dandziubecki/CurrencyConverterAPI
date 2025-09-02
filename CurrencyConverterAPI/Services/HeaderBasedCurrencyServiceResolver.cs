using Microsoft.Extensions.Primitives;

namespace CurrencyConverterAPI.Services;

public interface IHeaderBasedCurrencyConverterFactory
{
    ICurrencyService ResolveService();
}

public class HeaderBasedCurrencyConverterFactory : IHeaderBasedCurrencyConverterFactory
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceProvider  _serviceProvider;
    private readonly ILogger<HeaderBasedCurrencyConverterFactory> _logger;
    private const string DefaultProvider = nameof(FrankfurterApiCurrencyService);

    public HeaderBasedCurrencyConverterFactory(
        IHttpContextAccessor httpContextAccessor,
        ILogger<HeaderBasedCurrencyConverterFactory> logger, 
        IServiceProvider serviceProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public ICurrencyService ResolveService()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            _logger.LogWarning("HttpContext is null, using default provider: {DefaultProvider}", DefaultProvider);
            return _serviceProvider.GetRequiredKeyedService<ICurrencyService>(DefaultProvider);
        }

        var providerName = GetProviderNameFromHeader(httpContext);
        
        try
        {
            var service = _serviceProvider.GetRequiredKeyedService<ICurrencyService>(providerName);
            _logger.LogInformation("Resolved currency service: {ProviderName}", providerName);
            return service;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Provider '{ProviderName}' not supported, falling back to default: {DefaultProvider}", providerName, DefaultProvider);
            return _serviceProvider.GetRequiredKeyedService<ICurrencyService>(DefaultProvider);
        }
    }

    private string GetProviderNameFromHeader(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue(ApiConstants.Headers.CurrencyProvider, out StringValues headerValues))
        {
            var providerName = headerValues.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(providerName))
            {
                _logger.LogInformation("Found provider in header: {ProviderName}", providerName);
                return providerName;
            }
        }

        _logger.LogInformation("No provider specified in header '{HeaderName}', using default: {DefaultProvider}", 
            ApiConstants.Headers.CurrencyProvider, DefaultProvider);
        return DefaultProvider;
    }
}
