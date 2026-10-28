using Grpc.Core;
using Microsoft.AspNetCore.SignalR;
using Raft.Grpc;
using RaftDashboard.Hubs;
using RaftDashboard.Services;

int nodesCount = args.Length > 0 ? int.Parse(args[0]) : 8;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(builder.Configuration["GatewayUrl"] ?? "http://localhost:5100");

string nodeHost = builder.Configuration["NodeHost"] ?? "localhost";
List<string> nodeAddresses = new();
for (int i = 1; i <= nodesCount; i++)
{
    nodeAddresses.Add($"http://{nodeHost}:500{i}");
}

int workDelay = int.TryParse(builder.Configuration["WorkDelay"], out int configuredDelay) ? configuredDelay : 800;

builder.Services.AddSingleton(new NodeClients(nodeAddresses));
builder.Services.AddSingleton(new ClusterSpeed(workDelay));
builder.Services.AddSignalR();
builder.Services.AddHostedService(provider => new ClusterPoller(
    provider.GetRequiredService<NodeClients>(),
    provider.GetRequiredService<IHubContext<ClusterHub>>(),
    provider.GetRequiredService<ClusterSpeed>()));

var app = builder.Build();

string indexFile = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "index.html");
if (File.Exists(indexFile))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapFallbackToFile("index.html");
}

app.MapHub<ClusterHub>("/hub/cluster");
app.MapPost("/api/nodes/{index:int}/pause", (int index, NodeClients clients) => SetPausedAsync(clients, index, true));
app.MapPost("/api/nodes/{index:int}/resume", (int index, NodeClients clients) => SetPausedAsync(clients, index, false));
app.MapPost("/api/speed/{delayMs:int}", (int delayMs, NodeClients clients, ClusterSpeed speed) => SetSpeedAsync(clients, speed, delayMs));
app.Run();

static async Task<IResult> SetPausedAsync(NodeClients clients, int index, bool paused)
{
    var client = clients.ByIndex(index);
    if (client == null)
    {
        return Results.NotFound();
    }

    try
    {
        var response = await client.SetPausedAsync(
            new SetPausedRequest { Paused = paused }, deadline: DateTime.UtcNow.AddSeconds(1));
        return Results.Ok(new { paused = response.Paused });
    }
    catch (RpcException)
    {
        return Results.Problem(statusCode: 502, title: "Node unreachable");
    }
}

static async Task<IResult> SetSpeedAsync(NodeClients clients, ClusterSpeed speed, int delayMs)
{
    speed.DelayMs = Math.Clamp(delayMs, 10, 3000);
    var calls = clients.Addresses.Select(async address =>
    {
        try
        {
            await clients.For(address).SetSpeedAsync(
                new SetSpeedRequest { DelayMs = speed.DelayMs }, deadline: DateTime.UtcNow.AddSeconds(1));
        }
        catch (RpcException)
        {
        }
    });
    await Task.WhenAll(calls);
    return Results.Ok(new { delayMs = speed.DelayMs });
}
