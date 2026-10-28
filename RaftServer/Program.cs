using Npgsql;
using Raft.Hosting;
using Raft.Services;

int nodesCount = args.Length > 0 ? int.Parse(args[0]) : 8;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddInMemoryCollection(DotEnv.Load())
    .AddEnvironmentVariables()
    .Build();

string connectionString = config.GetConnectionString("Primes")
    ?? throw new InvalidOperationException("Connection string Primes not found, copy .env.example to .env");
await using var dataSource = NpgsqlDataSource.Create(connectionString);
var storage = new PostgresStorageService(dataSource);
await storage.EnsureTableAsync();

string? bindHost = config["BindHost"];

List<string> allAdress = new();
for (int i = 1; i <= nodesCount; i++)
{
    allAdress.Add($"http://localhost:500{i}");
}

List<Task> workingServers = new();
foreach (var adress in allAdress)
{
    var app = NodeHost.Build(adress, allAdress, args, storage, bindHost);
    workingServers.Add(app.RunAsync());
}
await Task.WhenAll(workingServers);
