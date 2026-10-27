using Raft.Hosting;

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
    var app = NodeHost.Build(adress, allAdress, args);
    workingServers.Add(app.RunAsync());
}
await Task.WhenAll(workingServers);
