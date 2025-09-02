using System.Diagnostics.CodeAnalysis;
using CurrencyConverterAPI.Models;

namespace CurrencyConverterAPI.Services;

[ExcludeFromCodeCoverage]
public class DummyExchangeRateApiService : ICurrencyService
{
    public Task<ConversionResponse?> ConvertCurrencyAsync(string from, string to, decimal amount)
    {
        var convertedAmount = amount * 1.2m; // Dummy conversion rate
        var rates = new Dictionary<string, decimal> { { to, convertedAmount } };
        var response = new ConversionResponse(amount, from, DateOnly.FromDateTime(DateTime.UtcNow), rates);
        return Task.FromResult<ConversionResponse?>(response);
    }

    public Task<PaginatedHistoricalRatesResponse?> GetHistoricalRatesAsync(string baseCurrency, DateOnly startDate, DateOnly endDate, int page, int pageSize)
    {
        throw new NotImplementedException("This is a dummy provider and does not implement historical rates.");
    }

    public Task<LatestRatesResponse?> GetLatestRatesAsync(string baseCurrency)
    {
        throw new NotImplementedException("This is a dummy provider and does not implement latest rates.");
    }

    public bool IsCurrencyExcluded(string currency)
    {
        return false;
    }
}
