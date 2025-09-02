using CurrencyConverterAPI.Models;
using CurrencyConverterAPI.Services;

namespace CurrencyConverterAPI.Tests.Services;

public class FrankfurterApiCurrencyService : ICurrencyService
{
    public Task<LatestRatesResponse?> GetLatestRatesAsync(string baseCurrency)
    {
        throw new NotImplementedException();
    }

    public Task<ConversionResponse?> ConvertCurrencyAsync(string from, string to, decimal amount)
    {
        throw new NotImplementedException();
    }

    public Task<PaginatedHistoricalRatesResponse?> GetHistoricalRatesAsync(string baseCurrency, DateOnly startDate, DateOnly endDate, int page, int pageSize)
    {
        throw new NotImplementedException();
    }

    public bool IsCurrencyExcluded(string currency)
    {
        throw new NotImplementedException();
    }
}