using CurrencyConverterAPI.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace CurrencyConverterAPI.Services;

public class FrankfurterApiCurrencyService : ICurrencyService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FrankfurterApiCurrencyService> _logger;
    private readonly HashSet<string> _excludedCurrencies = new(StringComparer.OrdinalIgnoreCase) { "TRY", "PLN", "THB", "MXN" };
    private readonly IMemoryCache _cache;

    public FrankfurterApiCurrencyService(IHttpClientFactory httpClientFactory, ILogger<FrankfurterApiCurrencyService> logger, IMemoryCache cache)
    {
        _httpClient = httpClientFactory.CreateClient(ApiConstants.HttpClients.Frankfurter);
        _logger = logger;
        _cache = cache;
    }

    public bool IsCurrencyExcluded(string currency) => _excludedCurrencies.Contains(currency);

    public async Task<ConversionResponse?> ConvertCurrencyAsync(string from, string to, decimal amount)
    {
        var cacheKey = $"convert_{from}_{to}_{amount}";
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            _logger.LogInformation("Cache miss for {CacheKey}. Fetching from API.", cacheKey);

            try
            {
                var response = await _httpClient.GetAsync($"latest?amount={amount}&from={from}&to={to}");
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                var frankfurterResponse = JsonSerializer.Deserialize<FrankfurterLatestResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (frankfurterResponse is null) return null;

                return new ConversionResponse(frankfurterResponse.Amount, frankfurterResponse.Base, frankfurterResponse.Date, frankfurterResponse.Rates);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error converting currency from {From} to {To}", from, to);
                return null;
            }
        });
    }

    public async Task<PaginatedHistoricalRatesResponse?> GetHistoricalRatesAsync(string baseCurrency, DateOnly startDate, DateOnly endDate, int page, int pageSize)
    {
        var cacheKey = $"historical_{baseCurrency}_{startDate:yyyy-MM-dd}_{endDate:yyyy-MM-dd}";

        var frankfurterResponse = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1);
            _logger.LogInformation("Cache miss for {CacheKey}. Fetching from API.", cacheKey);
            try
            {
                var response = await _httpClient.GetAsync($"{startDate:yyyy-MM-dd}..{endDate:yyyy-MM-dd}?from={baseCurrency}");
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<FrankfurterHistoricalResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error getting historical rates for {BaseCurrency}", baseCurrency);
                return null;
            }
        });

        if (frankfurterResponse is null) return null;

        var filteredRates = frankfurterResponse.Rates
            .Select(kvp => new
            {
                Date = kvp.Key,
                Rates = kvp.Value.Where(r => !_excludedCurrencies.Contains(r.Key)).ToDictionary(r => r.Key, r => r.Value)
            })
            .ToDictionary(x => x.Date, x => x.Rates);

        var totalItems = filteredRates.Count;
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var paginatedRates = filteredRates
            .OrderBy(kvp => kvp.Key)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        return new PaginatedHistoricalRatesResponse(
            frankfurterResponse.Base,
            frankfurterResponse.StartDate,
            frankfurterResponse.EndDate,
            page,
            pageSize,
            totalItems,
            totalPages,
            paginatedRates
        );
    }

    public string ProviderName => nameof(FrankfurterApiCurrencyService);

    public async Task<LatestRatesResponse?> GetLatestRatesAsync(string baseCurrency)
    {
        var cacheKey = $"latest_{baseCurrency}";
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            _logger.LogInformation("Cache miss for {CacheKey}. Fetching from API.", cacheKey);

            try
            {
                var response = await _httpClient.GetAsync($"latest?from={baseCurrency}");
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                var frankfurterResponse = JsonSerializer.Deserialize<FrankfurterLatestResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (frankfurterResponse is null) return null;

                var filteredRates = frankfurterResponse.Rates
                    .Where(kvp => !_excludedCurrencies.Contains(kvp.Key))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                return new LatestRatesResponse(frankfurterResponse.Base, frankfurterResponse.Date, filteredRates);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error getting latest rates for {BaseCurrency}", baseCurrency);
                return null;
            }
        });
    }
}
