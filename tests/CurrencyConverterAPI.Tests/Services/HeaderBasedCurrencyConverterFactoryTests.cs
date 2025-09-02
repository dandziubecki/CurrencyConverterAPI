using System;
using CurrencyConverterAPI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CurrencyConverterAPI.Tests.Services;

public class HeaderBasedCurrencyConverterFactoryTests
{
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly HeaderBasedCurrencyConverterFactory _sut;

    public HeaderBasedCurrencyConverterFactoryTests()
    {
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        Mock<ILogger<HeaderBasedCurrencyConverterFactory>> mockLogger = new();
        var services = new ServiceCollection();
        services.AddKeyedSingleton<ICurrencyService>("FrankfurterApiCurrencyService", new FrankfurterApiCurrencyService());
        services.AddKeyedSingleton<ICurrencyService>("TestProvider", new TestProvider());
        IServiceProvider serviceProvider = services.BuildServiceProvider();

        _sut = new HeaderBasedCurrencyConverterFactory(
            _mockHttpContextAccessor.Object,
            mockLogger.Object,
            serviceProvider);
    }

    [Fact]
    public void ResolveService_ShouldReturnProviderFromHeader_WhenHeaderIsValid()
    {
        const string providerName = "TestProvider";
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Currency-Provider"] = providerName;
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var result = _sut.ResolveService();

        Assert.Equal(result.GetType(), typeof(TestProvider));
        
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ResolveService_ShouldReturnDefaultProvider_WhenHeaderIsMissingOrEmpty(string headerValue)
    {
        var httpContext = new DefaultHttpContext();
        if (headerValue is not null)
        {
            httpContext.Request.Headers["X-Currency-Provider"] = headerValue;
        }
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var result = _sut.ResolveService();

        Assert.Equal(result.GetType(), typeof(FrankfurterApiCurrencyService));
    }

    [Fact]
    public void ResolveService_ShouldReturnDefaultProvider_WhenProviderInHeaderIsUnsupported()
    {
        const string providerName = "UnsupportedProvider";
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Currency-Provider"] = providerName;
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var result = _sut.ResolveService();

        Assert.Equal(result.GetType(), typeof(FrankfurterApiCurrencyService));
    }

    [Fact]
    public void ResolveService_ShouldReturnDefaultProvider_WhenHttpContextIsNull()
    {
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext)null);

        var result = _sut.ResolveService();

        Assert.Equal(result.GetType(), typeof(FrankfurterApiCurrencyService));
    }
}
