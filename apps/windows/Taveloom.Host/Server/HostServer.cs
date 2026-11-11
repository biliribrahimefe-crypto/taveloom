using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Taveloom.Host.Server;

/// <summary>Kestrel host serving a single WSS endpoint (/ws) over TLS with the pinned host certificate.</summary>
public sealed class HostServer : IAsyncDisposable
{
    private WebApplication? _app;
    public int Port { get; private set; }
    public bool IsRunning => _app is not null;

    public async Task StartAsync(HostContext ctx, X509Certificate2 cert, int port, bool lan)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(k =>
        {
            k.AddServerHeader = false;
            k.Listen(lan ? IPAddress.Any : IPAddress.Loopback, port, lo => lo.UseHttps(new HttpsConnectionAdapterOptions
            {
                ServerCertificate = cert,
                SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                ClientCertificateMode = ClientCertificateMode.NoCertificate,
            }));
        });

        var app = builder.Build();
        app.Use(async (http, next) =>
        {
            if (!NetworkGuard.IsLocalNetwork(http.Connection.RemoteIpAddress)) { http.Response.StatusCode = StatusCodes.Status403Forbidden; return; }
            await next();
        });
        app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(15), KeepAliveTimeout = TimeSpan.FromSeconds(30) });
        app.Map("/ws", async http =>
        {
            if (!http.WebSockets.IsWebSocketRequest) { http.Response.StatusCode = StatusCodes.Status400BadRequest; return; }
            using var ws = await http.WebSockets.AcceptWebSocketAsync();
            await new ClientSession(ctx, ws, http.Connection.RemoteIpAddress).RunAsync(http.RequestAborted);
        });

        await app.StartAsync();
        var addr = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        Port = new Uri(addr.Replace("[::]", "localhost").Replace("0.0.0.0", "localhost")).Port;
        _app = app;
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is null) return;
        var app = _app; _app = null;
        await app.StopAsync(TimeSpan.FromSeconds(2));
        await app.DisposeAsync();
    }
}
