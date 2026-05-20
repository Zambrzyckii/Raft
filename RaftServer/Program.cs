using Raft.Engine.RaftEngine;
using Raft.Engine.MathEngine;
using Raft.Models;
using Raft.Services;

int nodesCount = args.Length > 0 ? int.Parse(args[0]) : 8;
File.Delete("primes.txt");

List<string> allAdress = new();
for (int i = 1; i <= nodesCount; i++)
{
    allAdress.Add($"http://localhost:500{i}");
}

List<Task> workingServers = new();
foreach (var adress in allAdress)
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
    workingServers.Add(app.RunAsync());
}
await Task.WhenAll(workingServers);