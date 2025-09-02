using System.Net;
using System.Text.Json;
using CurrencyConverterAPI.Models;
using CurrencyConverterAPI.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Moq;
using Xunit;

namespace CurrencyConverterAPI.Tests.Services;

public class FrankfurterApiCurrencyServiceTests : IClassFixture<WireMockServerFixture>
{
    private readonly CurrencyConverterAPI.Services.FrankfurterApiCurrencyService _sut;
    private readonly WireMockServer _wireMockServer;

    public FrankfurterApiCurrencyServiceTests(WireMockServerFixture wireMockFixture)
    {
        _wireMockServer = wireMockFixture.Server;
        Mock<ILogger<CurrencyConverterAPI.Services.FrankfurterApiCurrencyService>> mockLogger = new();
        IMemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions());

        var httpClient = new HttpClient()
        {
            BaseAddress = new Uri(_wireMockServer.Url!)
        };
        
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        mockHttpClientFactory.Setup(_ => _.CreateClient("Frankfurter")).Returns(httpClient);
        var httpClientFactory = mockHttpClientFactory.Object;

        _sut = new CurrencyConverterAPI.Services.FrankfurterApiCurrencyService(httpClientFactory, mockLogger.Object, memoryCache);
    }

    [Theory]
    [InlineData("TRY", true)]
    [InlineData("PLN", true)]
    [InlineData("USD", false)]
    [InlineData("eur", false)]
    public void IsCurrencyExcluded_ShouldReturnCorrectValue(string currency, bool expected)
    {
        var result = _sut.IsCurrencyExcluded(currency);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task ConvertCurrencyAsync_ShouldReturnConversionResponse_WhenApiCallIsSuccessful()
    {
        var from = "USD";
        var to = "EUR";
        var amount = 100m;
        var expectedResponse = new FrankfurterLatestResponse(amount, from, DateOnly.FromDateTime(DateTime.Today), new Dictionary<string, decimal> { { to, 92.5m } });

        _wireMockServer.Reset();
        _wireMockServer
            .Given(Request.Create().WithPath("/latest").WithParam("amount", amount.ToString()).WithParam("from", from).WithParam("to", to).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(expectedResponse)));

        var result = await _sut.ConvertCurrencyAsync(from, to, amount);

        Assert.NotNull(result);
        Assert.Equal(amount, result.Amount);
        Assert.Equal(from, result.Base);
        Assert.Equal(92.5m, result.Rates[to]);
    }

    [Fact]
    public async Task ConvertCurrencyAsync_ShouldReturnNull_WhenApiCallFails()
    {
        _wireMockServer.Reset();
        _wireMockServer
            .Given(Request.Create().WithPath("/latest").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(500));

        var result = await _sut.ConvertCurrencyAsync("USD", "EUR", 100);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetLatestRatesAsync_ShouldReturnLatestRatesAndFilterExcludedCurrencies()
    {
        var baseCurrency = "USD";
        var rates = new Dictionary<string, decimal>
        {
            { "EUR", 0.9m },
            { "GBP", 0.8m },
            { "TRY", 25.0m },
            { "PLN", 4.0m }
        };
        var frankfurterResponse = new FrankfurterLatestResponse(1, baseCurrency, DateOnly.FromDateTime(DateTime.Today), rates);

        _wireMockServer.Reset();
        _wireMockServer
            .Given(Request.Create().WithPath("/latest").WithParam("from", baseCurrency).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(frankfurterResponse)));

        var result = await _sut.GetLatestRatesAsync(baseCurrency);

        Assert.NotNull(result);
        Assert.Equal(baseCurrency, result.Base);
        Assert.Equal(2, result.Rates.Count);
        Assert.Contains("EUR", result.Rates.Keys);
        Assert.Contains("GBP", result.Rates.Keys);
        Assert.DoesNotContain("TRY", result.Rates.Keys);
        Assert.DoesNotContain("PLN", result.Rates.Keys);
    }

    [Fact]
    public async Task GetHistoricalRatesAsync_ShouldReturnPaginatedAndFilteredRates()
    {
        var baseCurrency = "USD";
        var startDate = new DateOnly(2023, 1, 1);
        var endDate = new DateOnly(2023, 1, 5);
        var rates = new Dictionary<string, Dictionary<string, decimal>>
        {
            ["2023-01-01"] = new() { { "EUR", 0.91m }, { "TRY", 25.1m } },
            ["2023-01-02"] = new() { { "EUR", 0.92m }, { "PLN", 4.1m } },
            ["2023-01-03"] = new() { { "EUR", 0.93m }, { "GBP", 0.81m } },
            ["2023-01-04"] = new() { { "EUR", 0.94m }, { "THB", 34.1m } },
            ["2023-01-05"] = new() { { "EUR", 0.95m }, { "MXN", 17.1m } },
        };
        var frankfurterResponse = new FrankfurterHistoricalResponse(1, baseCurrency, startDate, endDate, rates);

        _wireMockServer.Reset();
        _wireMockServer
            .Given(Request.Create().WithPath($"/{startDate:yyyy-MM-dd}..{endDate:yyyy-MM-dd}").WithParam("from", baseCurrency).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(frankfurterResponse)));

        var result = await _sut.GetHistoricalRatesAsync(baseCurrency, startDate, endDate, 2, 2);

        Assert.NotNull(result);
        Assert.Equal(baseCurrency, result.Base);
        Assert.Equal(5, result.TotalItems);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(2, result.Rates.Count);
        Assert.True(result.Rates.ContainsKey("2023-01-03"));
        Assert.Equal(2, result.Rates["2023-01-03"].Count);
        Assert.DoesNotContain("TRY", result.Rates.SelectMany(r => r.Value.Keys));
    }
    
    [Fact]
    public async Task ConvertCurrencyAsync_ShouldUseCacheOnSecondCall()
    {
        var from = "USD";
        var to = "EUR";
        var amount = 100m;
        var expectedResponse = new FrankfurterLatestResponse(amount, from, DateOnly.FromDateTime(DateTime.Today), new Dictionary<string, decimal> { { to, 92.5m } });

        _wireMockServer.Reset();
        _wireMockServer
            .Given(Request.Create().WithPath("/latest").WithParam("amount", amount.ToString()).WithParam("from", from).WithParam("to", to).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(expectedResponse)));

        var result1 = await _sut.ConvertCurrencyAsync(from, to, amount);
        var result2 = await _sut.ConvertCurrencyAsync(from, to, amount);

        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal(result1.Rates[to], result2.Rates[to]);
        
        Assert.Single(_wireMockServer.LogEntries);
    }
}
