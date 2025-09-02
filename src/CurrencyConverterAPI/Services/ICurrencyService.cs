using CurrencyConverterAPI.Models;

namespace CurrencyConverterAPI.Services;

public interface ICurrencyService
{
    Task<LatestRatesResponse?> GetLatestRatesAsync(string baseCurrency);
    Task<ConversionResponse?> ConvertCurrencyAsync(string from, string to, decimal amount);
    Task<PaginatedHistoricalRatesResponse?> GetHistoricalRatesAsync(string baseCurrency, DateOnly startDate, DateOnly endDate, int page, int pageSize);
    bool IsCurrencyExcluded(string currency);
}