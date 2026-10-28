using Raft.Engine.MathEngine;
using Raft.Engine.RaftEngine;
using Raft.Engine.StorageEngine;
using Raft.Models;
using Raft.Services;

namespace Raft.Hosting;

public static class NodeHost
{
    public static WebApplication Build(string adress, List<string> allAdress, string[] args, PostgresStorageService? storage = null, string? bindHost = null)
    {
        RaftNode myAdress = new RaftNode { id = adress };
        myAdress.otherCandidates.Clear();
        foreach (var candidate in allAdress)
        {
            if (candidate != adress)
            {
                myAdress.otherCandidates.Add(candidate);
            }
        }
        var builder = WebApplication.CreateBuilder(args);
        int port = new Uri(adress).Port;
        builder.WebHost.UseUrls(bindHost == null ? adress : $"http://{bindHost}:{port}");
        builder.Services.AddSingleton(myAdress);
        if (storage != null)
        {
            builder.Services.AddSingleton(storage);
            builder.Services.AddHostedService<StorageEngine>();
        }
        builder.Services.AddGrpc().AddServiceOptions<ConsensusService>(options =>
        {
            options.Interceptors.Add<ChaosInterceptor>();
        });
        builder.Services.AddHostedService<RaftEngine>();
        builder.Services.AddHostedService<MathEngine>();
        var app = builder.Build();
        app.MapGrpcService<ConsensusService>();
        app.MapGrpcService<MonitoringService>();
        return app;
    }
}
