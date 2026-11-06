using Grpc.Net.Client;
using Raft.Grpc;

namespace RaftDashboard.Services;

public class NodeClients
{
    private readonly List<string> _addresses;
    private readonly Dictionary<string, RaftMonitoring.RaftMonitoringClient> _clients = new();

    public NodeClients(List<string> addresses)
    {
        _addresses = addresses;
        foreach (var address in addresses)
        {
            _clients[address] = new RaftMonitoring.RaftMonitoringClient(GrpcChannel.ForAddress(address));
        }
    }

    public IReadOnlyList<string> Addresses => _addresses;

    public RaftMonitoring.RaftMonitoringClient For(string address) => _clients[address];

    public RaftMonitoring.RaftMonitoringClient? ByIndex(int index) =>
        index >= 1 && index <= _addresses.Count ? _clients[_addresses[index - 1]] : null;
}
