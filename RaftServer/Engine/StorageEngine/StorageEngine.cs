using Raft.Models;
using Raft.Services;

namespace Raft.Engine.StorageEngine;

public class StorageEngine : BackgroundService
{
    private const int BatchSize = 50;
    private readonly RaftNode _context;
    private readonly PostgresStorageService _storage;
    private int _savedCount;

    public StorageEngine(RaftNode context, PostgresStorageService storage)
    {
        _context = context;
        _storage = storage;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_context._currentState == Role.Leader)
                {
                    List<LogEntry> toSave;
                    lock (_context.OfficialPrimes)
                    {
                        toSave = _context.OfficialPrimes.Skip(_savedCount).Take(BatchSize).ToList();
                    }

                    if (toSave.Count >= BatchSize)
                    {
                        await _storage.SaveBatchAsync(toSave);
                        _savedCount += toSave.Count;
                        Console.WriteLine($"[{_context.id}] Saved {toSave.Count} primes, total {_savedCount}");
                    }
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[{_context.id}] Storage error: {exception.Message}");
            }

            await Task.Delay(500, stoppingToken);
        }
    }
}
