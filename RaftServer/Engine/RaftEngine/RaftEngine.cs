using Grpc.Net.Client;
using Raft.Grpc;
using Raft.Interfaces;
using Raft.Models;
using Raft.RoleBehaviours;
using Raft.RoleBehaviours.Leader;
using Raft.Services;

namespace Raft.Engine.RaftEngine;

public class RaftEngine : BackgroundService
{
    private readonly RaftNode _context;
    private IRaftState _state;

    public RaftEngine(RaftNode context)
    {
        _context = context;
        _state = new Follower();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_context._currentState == Role.Follower && _state is not Follower)
            {
                _state = new Follower();
            }
            else if (_context._currentState == Role.Candidate && _state is not Candidate)
            {
                _state = new Candidate();
            }
            else if (_context._currentState == Role.Leader && _state is not Leader)
            {
                _state = new Leader();
            }

            try
            {
                await _state.Execute(_context);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[{_context.id}] {_state.GetType().Name} failed: {exception.GetType().Name}: {exception.Message}");
                await Task.Delay(2000);
            }
        }
    }
}