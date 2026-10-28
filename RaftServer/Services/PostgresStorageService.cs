using Npgsql;
using Raft.Models;

namespace Raft.Services;

public class PostgresStorageService
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresStorageService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task EnsureTableAsync()
    {
        await using var command = _dataSource.CreateCommand(@"
            CREATE TABLE IF NOT EXISTS primes (
                id          SERIAL PRIMARY KEY,
                number      INTEGER NOT NULL UNIQUE,
                found_by    TEXT,
                search_time TEXT,
                created_at  TIMESTAMPTZ DEFAULT now()
            );");
        await command.ExecuteNonQueryAsync();
    }

    public async Task SaveBatchAsync(IReadOnlyList<LogEntry> entries)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var batch = new NpgsqlBatch(connection, transaction);

        foreach (var entry in entries)
        {
            var insert = new NpgsqlBatchCommand("INSERT INTO primes (number) VALUES ($1) ON CONFLICT (number) DO NOTHING");
            insert.Parameters.Add(new NpgsqlParameter { Value = entry.PrimeNumber });
            batch.BatchCommands.Add(insert);
        }

        await batch.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
    }
}
