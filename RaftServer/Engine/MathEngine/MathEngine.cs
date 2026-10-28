using Grpc.Net.Client;
using Raft.Grpc;
using Raft.Models;
using Raft.Algorithm;
namespace Raft.Engine.MathEngine;

public class MathEngine : BackgroundService
{
    private readonly RaftNode _context;
    private int _currentNumber = 1;

    public MathEngine(RaftNode context) { _context = context; }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_context.paused && _context._currentState == Role.Follower && !string.IsNullOrEmpty(_context.currentLeader))
            {
                if (DynamicWorkDivider.IsMyTurn(_context, _currentNumber))
                {
                    var stoper = System.Diagnostics.Stopwatch.StartNew();
                    bool isPrime = IsPrime.CheckPrime(_currentNumber);
                    
                    if (isPrime && !_context.OfficialPrimes.Exists(p => p.PrimeNumber == _currentNumber))
                    {
                        _context.AddEvent("found", _currentNumber);
                        try
                        {
                            await ReliableProposer.SendAndWaitForAckAsync(_context, _currentNumber, stoper.Elapsed.ToString(), stoppingToken);
                        }
                        catch (TaskCanceledException) {}
                    }
                }

                _currentNumber++;
                await Task.Delay(_context.workDelay, stoppingToken);
            }
            else
            {
                await Task.Delay(500, stoppingToken);
            }
        }
    }
}