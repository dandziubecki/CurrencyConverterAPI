using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using CurrencyConverterAPI;
using CurrencyConverterAPI.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;
using System.IdentityModel.Tokens.Jwt;

namespace CurrencyConverterAPI.Tests;

public class CurrencyApiIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IClassFixture<WireMockServerFixture>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private readonly WireMockServerFixture _wireMockFixture;
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public CurrencyApiIntegrationTests(CustomWebApplicationFactory factory, WireMockServerFixture wireMockFixture)
    {
        _factory = factory;
        _wireMockFixture = wireMockFixture;
        _client = _factory.WithWireMockServer(_wireMockFixture.Server).CreateClient();
        _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new DateOnlyJsonConverter() }
        };

        var token = GenerateAdminToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static string GenerateAdminToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(CustomWebApplicationFactory.TestSecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-admin"),
            new Claim(ClaimTypes.Role, ApiConstants.Authentication.AdminRole)
        };

        var token = new JwtSecurityToken(
            issuer: CustomWebApplicationFactory.TestIssuer,
            audience: CustomWebApplicationFactory.TestAudience,
            claims: claims,
            expires: DateTime.Now.AddMinutes(30),
            signingCredentials: creds
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task GetLatestRates_ShouldReturnOkResult_WithValidRequest()
    {
        var responseContent = new { amount = 1.0, @base = "USD", date = "2024-01-01", rates = new { EUR = 0.9 } };
        _wireMockFixture.Server
            .Given(Request.Create().WithPath("/latest").WithParam("from", "USD").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(responseContent)));

        var response = await _client.GetAsync("/rates/latest?baseCurrency=USD");

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<LatestRatesResponse>(content, _jsonSerializerOptions);
        Assert.NotNull(result);
        Assert.Equal("USD", result.Base);
        Assert.Contains("EUR", result.Rates.Keys);
    }

    public async Task GetLatestRates_ShouldReturnBadRequest_WhenBaseCurrencyIsMissing()
    {
        var response = await _client.GetAsync("/rates/latest?baseCurrency=");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Convert_ShouldReturnOkResult_WithValidRequest()
    {
        var responseContent = new { amount = 100.0, @base = "USD", date = "2024-01-01", rates = new { EUR = 90.0 } };
        _wireMockFixture.Server
            .Given(Request.Create().WithPath("/latest").WithParam("amount", "100").WithParam("from", "USD").WithParam("to", "EUR").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(responseContent)));
        
        var response = await _client.GetAsync("/convert?from=USD&to=EUR&amount=100");

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ConversionResponse>(content, _jsonSerializerOptions);
        Assert.NotNull(result);
        Assert.Equal(100, result.Amount);
        Assert.Equal("USD", result.Base);
        Assert.Equal((decimal)90.0, result.Rates["EUR"]);
    }

    [Theory]
    [InlineData("?from=USD&to=EUR&amount=0")]
    [InlineData("?from=USD&to=&amount=100")]
    [InlineData("?from=&to=EUR&amount=100")]
    public async Task Convert_ShouldReturnBadRequest_WithInvalidParameters(string queryString)
    {
        var response = await _client.GetAsync($"/convert{queryString}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Convert_ShouldReturnBadRequest_ForExcludedCurrency()
    {
        var response = await _client.GetAsync("/convert?from=USD&to=TRY&amount=100");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetHistoricalRates_ShouldReturnOkResult_WithValidRequest()
    {
        var responseContent = new
        {
            amount = 1.0,
            @base = "USD",
            start_date = "2023-01-01",
            end_date = "2023-01-02",
            rates = new Dictionary<string, object>
            {
                { "2023-01-01", new { EUR = 0.91 } },
                { "2023-01-02", new { EUR = 0.92 } }
            }
        };
        _wireMockFixture.Server
            .Given(Request.Create().WithPath("/2023-01-01..2023-01-02").WithParam("from", "USD").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(responseContent)));

        var response = await _client.GetAsync("/rates/historical?baseCurrency=USD&startDate=2023-01-01&endDate=2023-01-02&page=1&pageSize=10");

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<PaginatedHistoricalRatesResponse>(content, _jsonSerializerOptions);
        Assert.NotNull(result);
        Assert.Equal("USD", result.Base);
        Assert.Equal(2, result.Rates.Count);
    }

    [Fact]
    public async Task GetHistoricalRates_ShouldReturnBadRequest_WhenStartDateIsAfterEndDate()
    {
        var response = await _client.GetAsync("/rates/historical?baseCurrency=USD&startDate=2023-01-02&endDate=2023-01-01&page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetToken_AdminLogin_ShouldReturnOk_WithValidToken()
    {
        var client = _factory.CreateClient(); // Create a client without the default auth header
        var userLogin = new { Username = "admin", Password = "password" };
        var content = new StringContent(JsonSerializer.Serialize(userLogin), Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/token", content);

        response.EnsureSuccessStatusCode();
        var tokenString = (await response.Content.ReadAsStringAsync()).Replace("\"", string.Empty);;
        Assert.False(string.IsNullOrEmpty(tokenString));

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenString);

        Assert.Equal("admin", token.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal(ApiConstants.Authentication.AdminRole, token.Claims.First(c => c.Type == ClaimTypes.Role).Value);
    }

    [Fact]
    public async Task GetToken_UserLogin_ShouldReturnOk_WithValidToken()
    {
        var client = _factory.CreateClient(); 
        var userLogin = new { Username = "user", Password = "password" };
        var content = new StringContent(JsonSerializer.Serialize(userLogin), Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/token", content);

        response.EnsureSuccessStatusCode();
        var tokenString = (await response.Content.ReadAsStringAsync()).Replace("\"", string.Empty);
        Assert.False(string.IsNullOrEmpty(tokenString));

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenString);

        Assert.Equal("user", token.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal(ApiConstants.Authentication.UserRole, token.Claims.First(c => c.Type == ClaimTypes.Role).Value);
    }

    [Theory]
    [InlineData("admin", "wrongpassword")]
    [InlineData("wronguser", "password")]
    [InlineData("", "")]
    public async Task GetToken_InvalidCredentials_ShouldReturnUnauthorized(string username, string password)
    {
        var client = _factory.CreateClient(); // Create a client without the default auth header
        var userLogin = new { Username = username, Password = password };
        var content = new StringContent(JsonSerializer.Serialize(userLogin), Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/token", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private WireMock.Server.WireMockServer? _wireMockServer;
    public const string TestIssuer = "TestIssuer";
    public const string TestAudience = "TestAudience";
    public const string TestSecretKey = "MySuperSecretKeyForTestingEnvironment123!";
    public CustomWebApplicationFactory WithWireMockServer(WireMock.Server.WireMockServer server)
    {
        _wireMockServer = server;
        return this;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ApiConstants.Configuration.JwtKey] = TestSecretKey,
                [ApiConstants.Configuration.JwtIssuer] = TestIssuer,
                [ApiConstants.Configuration.JwtAudience] = TestAudience,
                [ApiConstants.Configuration.FrankfurterApiBaseUrl] = _wireMockServer?.Url
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddHttpClient(ApiConstants.HttpClients.Frankfurter, client =>
            {
                client.BaseAddress = new Uri(_wireMockServer?.Url);
            });
        });
    }
}

public class DateOnlyJsonConverter : JsonConverter<DateOnly>
{
    private const string Format = "yyyy-MM-dd";

    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return DateOnly.ParseExact(reader.GetString()!, Format);
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(Format));
    }
}
