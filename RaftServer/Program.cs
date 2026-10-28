using Npgsql;
using Raft.Hosting;
using Raft.Services;

int nodesCount = args.Length > 0 ? int.Parse(args[0]) : 8;
File.Delete("primes.txt");

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

await using var dataSource = NpgsqlDataSource.Create(config.GetConnectionString("Primes")!);
var storage = new PostgresStorageService(dataSource);
await storage.EnsureTableAsync();

List<string> allAdress = new();
for (int i = 1; i <= nodesCount; i++)
{
    allAdress.Add($"http://localhost:500{i}");
}

List<Task> workingServers = new();
foreach (var adress in allAdress)
{
    var app = NodeHost.Build(adress, allAdress, args, storage);
    workingServers.Add(app.RunAsync());
}
await Task.WhenAll(workingServers);
