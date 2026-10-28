using Microsoft.AspNetCore.SignalR;
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

builder.Services.AddSignalR();
builder.Services.AddHostedService(provider => new ClusterPoller(
    nodeAddresses,
    provider.GetRequiredService<IHubContext<ClusterHub>>()));

var app = builder.Build();
app.MapHub<ClusterHub>("/hub/cluster");
app.Run();
