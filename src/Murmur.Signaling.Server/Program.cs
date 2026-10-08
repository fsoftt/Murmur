using Murmur.Signaling.Server;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SignalingOptions>(builder.Configuration.GetSection(SignalingOptions.SectionName));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<SignalingOptions>>().Value);
builder.Services.AddSingleton<ITopicHub, InMemoryTopicHub>();
builder.Services.AddSingleton<AddressLimiter>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<SignalingSession>();

var app = builder.Build();
var options = app.Services.GetRequiredService<SignalingOptions>();

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = options.KeepAliveInterval,
    KeepAliveTimeout = options.KeepAliveTimeout,
});

app.MapGet("/healthz", () => Results.Text("ok"));

app.Map("/ws", async (HttpContext context, AddressLimiter limiter, SignalingSession session) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        return Results.BadRequest();
    }

    var address = context.Connection.RemoteIpAddress;
    if (!limiter.TryAcquire(address))
    {
        return Results.StatusCode(StatusCodes.Status429TooManyRequests);
    }

    try
    {
        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        await session.RunAsync(socket, context.RequestAborted);
    }
    finally
    {
        limiter.Release(address);
    }

    return Results.Empty;
});

app.Run();

/// <summary>Entry point; public for integration tests.</summary>
public partial class Program;
