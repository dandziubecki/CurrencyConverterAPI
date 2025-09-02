using System.Text.Json.Serialization;

namespace CurrencyConverterAPI.Models;

public record FrankfurterLatestResponse(
    decimal Amount,
    string Base,
    DateOnly Date,
    Dictionary<string, decimal> Rates
);

public record FrankfurterHistoricalResponse(
    decimal Amount,
    string Base,
    [property: JsonPropertyName("start_date")]
    DateOnly StartDate,
    [property: JsonPropertyName("end_date")]
    DateOnly EndDate,
    Dictionary<string, Dictionary<string, decimal>> Rates
);

public record LatestRatesResponse(
    string Base,
    DateOnly Date,
    Dictionary<string, decimal> Rates
);

public record ConversionResponse(
    decimal Amount,
    string Base,
    DateOnly Date,
    Dictionary<string, decimal> Rates
);

public record PaginatedHistoricalRatesResponse(
    string Base,
    DateOnly StartDate,
    DateOnly EndDate,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    Dictionary<string, Dictionary<string, decimal>> Rates
);
