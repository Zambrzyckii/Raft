using Grpc.Core;
using Grpc.Core.Interceptors;
using Raft.Models;

namespace Raft.Services;

public class ChaosInterceptor : Interceptor
{
    private readonly RaftNode _node;

    public ChaosInterceptor(RaftNode node)
    {
        _node = node;
    }

    public override Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        if (_node.paused)
        {
            throw new RpcException(new Status(StatusCode.Unavailable, "Node is paused"));
        }

        return continuation(request, context);
    }
}
