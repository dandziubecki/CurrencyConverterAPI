using WireMock.Server;
using WireMock.Settings;

namespace CurrencyConverterAPI.Tests;

public class WireMockServerFixture : IDisposable
{
    public WireMockServer Server { get; }

    public WireMockServerFixture()
    {
        Server = WireMockServer.Start(new WireMockServerSettings
        {
            Port = 0,
            StartAdminInterface = false
        });
    }

    public void Dispose()
    {
        Server?.Stop();
        Server?.Dispose();
    }
}
