using Raft.Engine.MathEngine;
using Raft.Engine.RaftEngine;
using Raft.Models;
using Raft.Services;

namespace Raft.Hosting;

public static class NodeHost
{
    public static WebApplication Build(string adress, List<string> allAdress, string[] args)
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
        builder.WebHost.UseUrls(adress);
        builder.Services.AddSingleton(myAdress);
        builder.Services.AddGrpc();
        builder.Services.AddHostedService<RaftEngine>();
        builder.Services.AddHostedService<MathEngine>();
        var app = builder.Build();
        app.MapGrpcService<ConsensusService>();
        return app;
    }
}
