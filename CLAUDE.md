# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

Verified on .NET SDK 10.0.113:

- Build: `dotnet build Raft.sln`
- All tests: `dotnet test` (xUnit, 27 tests, takes ~40 s because of the integration tests)
- Fast tests only: `dotnet test --filter "Category!=Integration"` (23 unit tests, under a second)
- Integration tests only: `dotnet test --filter "Category=Integration"` (4 tests; each starts an in-process cluster and binds localhost ports in the 7101-7404 range)
- Single test class: `dotnet test --filter "FullyQualifiedName~ConsensusServiceTests"`
- Single test method: `dotnet test --filter "FullyQualifiedName=RaftServer.Tests.Services.ConsensusServiceTests.ProposeNumber_WhenNotLeader_Ignore"`
- Run the cluster: copy `.env.example` to `.env` once and `docker compose up -d postgres` first, then `dotnet run --project RaftServer` (8 nodes) or `dotnet run --project RaftServer -- 3` (3 nodes; a bare `3` without `--` is forwarded as well)
- Containerised: `docker compose up --build`

No linter or formatter is configured — there is no `.editorconfig`, `Directory.Build.props` or analyzer package. `Raft.sln.DotSettings.user` is a per-user Rider file, not a project-wide convention.

Startup side effects: before any node starts, the process connects to PostgreSQL and runs `CREATE TABLE IF NOT EXISTS primes` (`Program.cs:15-17`), so `dotnet run` fails fast when the database is down. Start it with `docker compose up -d postgres` (host port 5434). The connection string lives in `.env` in the repo root (gitignored, copy `.env.example`), loaded by `DotEnv.Load` (`RaftServer/Hosting/DotEnv.cs`) into the configuration before `AddEnvironmentVariables`, so real environment variables still win. The container image has no `.env`, compose assembles `ConnectionStrings__Primes` for it from the same file on the host. Ports 5001..500N must be free. Test clusters build their nodes without a storage instance, so `dotnet test` needs no database.

## Architecture

### One process is the whole cluster

`Program.cs:25-30` builds N `WebApplication` instances inside a single process through `NodeHost.Build` (`RaftServer/Hosting/NodeHost.cs`), each with its own DI container and its own `RaftNode` singleton (`NodeHost.cs:24`). A node's identity is its URL, `http://localhost:500{i}` (`Program.cs:22`), and its peer list is simply every other URL (`NodeHost.cs:14-21`). All nodes share one `PostgresStorageService` over one `NpgsqlDataSource` created in `Program.cs:15-16` and passed into `NodeHost.Build` as an optional parameter. There are no per-node config files and no service discovery. The integration tests build their nodes with the same `NodeHost.Build` (`RaftServer.Tests/Cluster/TestCluster.cs`) but omit the storage argument, so test clusters run the production wiring minus persistence.

Consequence: `compose.yaml` starts **one container that contains the entire cluster** plus a `postgres` service. Scaling the raft service with `replicas` would not add nodes, it would start additional independent clusters. The Dockerfile's `EXPOSE 8080/8081` is inert because `UseUrls` (`NodeHost.cs:23`) overrides it; the only published port is PostgreSQL's 5434, which is also the only way to inspect the results from the host.

### Three concurrent writers per node

Each node runs up to three `BackgroundService`s — `RaftEngine` (consensus), `MathEngine` (prime search) and `StorageEngine` (PostgreSQL batch writer, registered only when `NodeHost.Build` received a storage instance, so never in tests) — plus the gRPC handlers in `ConsensusService`. All of them share the same `RaftNode` instance, which is the only coordination point. Locking is per-field and not uniform: `lock (_node)` (`ConsensusService.cs:57`), `lock (OfficialPrimes)` (`ConsensusService.cs:27`, `VoteCounter.cs:14`, `ReliableProposer.cs:43`, `StorageEngine.cs:28`), `lock (pendingNumber)` (`ConsensusService.cs:40`, `Leader.cs:15`), while some reads of the same collections are unlocked (`Leader.cs:25`, `MathEngine.cs:25`). Assume any `RaftNode` field can change underneath you.

### Role transitions (State pattern)

`RaftEngine.ExecuteAsync` (`RaftEngine.cs:26-37`) swaps the `IRaftState` object whenever `RaftNode._currentState` no longer matches the current one, then calls `Execute` in a tight loop. The role is set from three directions: by the role behaviours themselves (`Follower.cs:18`, `Candidate.cs:66`, `Candidate.cs:71`), by incoming RPCs (`ConsensusService.cs:23`, `ConsensusService.cs:65`), and by a leader that sees a newer term in an ack and steps down and ends the turn (`TermGuard.cs`, called from `Leader.cs:37` and `Leader.cs:84`).

The election timeout is not a timer: a follower awaits `Task.Delay(2000-4000, token)` (`Follower.cs:11-15`) and an incoming heartbeat cancels that token through `RaftNode.CancelTokenTimeout()` (`RaftNode.cs:20`, called from `ConsensusService.cs:21`). A heartbeat with a stale term is rejected without resetting the timeout (`ConsensusService.cs:18-19`). `Timer/RandomTime.cs` is dead code and says so in its own comment.

**Debugging note**: `RaftEngine.cs:39-47` catches *every* exception thrown by a role's `Execute`, logs the role, exception type and message, and sleeps 2 s. A recurring `... failed: ...` line in the console is a role crashing on every iteration, not a slow cluster.

### Prime commit path (the part that spans the most files)

1. `MathEngine.cs:18` — only a follower that already knows its leader computes; each node walks its own `_currentNumber` counter.
2. `DynamicWorkDivider.IsMyTurn` (`DynamicWorkDivider.cs:9-21`) — sharding by `number % followerCount == myIndex` over the sorted peer list with the current leader removed, so work rebalances by itself after every leader change.
3. `IsPrime.CheckPrime` (`Algorithm/IsPrime.cs`) — 6k±1 trial division.
4. `ReliableProposer.SendAndWaitForAckAsync` (`ReliableProposer.cs:15-50`) — resends `ProposeNumber` every 200 ms, re-resolving `context.currentLeader` on each retry (so a leader change mid-proposal is followed), until the number appears in the node's own `OfficialPrimes`; that local arrival (delivered later by `AppendEntries`) *is* the acknowledgement.
5. Leader side: `ConsensusService.ProposeNumber` (`ConsensusService.cs:35-53`) deduplicates against the log and the queue, then enqueues into `pendingNumber`; a non-leader answers `IsFine = false`.
6. `Leader.Execute` (`Leader.cs:11-57`) peeks one queued item, replicates it with `ProposeNumbers.SendProposeNumbers`, steps down and returns if an ack carries a newer term (`TermGuard.cs`), counts non-null `IsDone` acks and requires a majority in `VoteCounter.cs:8-22`, appends a `LogEntry` stamped with `currentTerm` (`VoteCounter.cs:18`), and only then dequeues.
7. With an empty queue the leader sends heartbeats instead (`Leader.cs:58-88`), and paces every iteration with `Task.Delay(50)` (`Leader.cs:90`).
8. Persistence is asynchronous and decoupled from consensus: the leader's `StorageEngine` snapshots `OfficialPrimes` under its lock every 500 ms and, once at least 50 new entries accumulated, writes exactly 50 of them with `INSERT ... ON CONFLICT (number) DO NOTHING` in one transaction (`StorageEngine.cs:25-38`, `PostgresStorageService.cs:29-46`). `_savedCount` advances only after a successful write, so a failed batch is retried; a new leader resends its whole log from index 0 in chunks of 50, which the unique index absorbs; the tail below 50 entries is never written. `found_by` and `search_time` travel from the proposal through `LogEntry` (`VoteCounter.cs:18`) and are NULL on entries a node learned over `AppendEntries`, because `AppendRequest` does not carry them and `ON CONFLICT DO NOTHING` keeps the first written row. `FileStorageService` has no callers anymore and remains as dead code.

Replication is set-based, not index-based: a follower appends any number it has not seen from `AppendEntries` (`ConsensusService.cs:25-31`); there is no log matching or backtracking. A request whose `LeaderTerm` is lower than the node's term is denied with `IsDone = false` and the node's own term in `AppendResponse.Term` (`ConsensusService.cs:18-19`). The election restriction is approximated by comparing log count and last term (`ConsensusService.cs:68-75`).

### gRPC layer

The contract lives in `Protos/RaftConsensus.proto`, generated for both client and server (`RaftServer.csproj:12`) into namespace `Raft.Grpc`. `AppendResponse` carries the responder's term (`RaftConsensus.proto:31`), which is what `TermGuard` reads. Plaintext HTTP/2 is forced in `appsettings.json:9-13`; gRPC will not work without it. The test host copies that `appsettings.json` next to the test binaries, which is why in-process test clusters get HTTP/2 without extra configuration.

Channel reuse is deliberate (socket exhaustion was a real problem in this project) but partial: `Leader._channelCache` (`Leader.cs:10`, per Leader instance, so it resets on every role change) and the static `ReliableProposer._channels` (`ReliableProposer.cs:10`) cache channels, while `Candidate.FarmVoteResponses` (`Candidate.cs:48`) still creates a fresh channel per peer per campaign.

## Conventions

- `RootNamespace` is `Raft` (`RaftServer.csproj:8`), so folders map to `Raft.*`, not `RaftServer.*` — `RaftServer/Services/` → `namespace Raft.Services`. Test namespaces are `RaftServer.Tests.*`.
- Model members are camelCase, some with a leading underscore (`RaftNode.currentTerm`, `RaftNode._currentState`), and role behaviours name their parameter `_context`. Unusual for C#, but match it.
- Role logic longer than a few lines is extracted into a static helper class next to the role (`RoleBehaviours/Leader/ProposeNumbers.cs`, `VoteCounter.cs`, `GetSignal.cs`, `TermGuard.cs`).
- Remote calls go through `*Safe` helpers that swallow the exception and return `null` (`GetSignal.cs:9-16`, `Candidate.cs:27-37`). A `null` means a dead peer, and callers count only non-null responses (`Candidate.cs:22`, `Leader.cs:45`).
- Tests: xUnit, `Method_Scenario_Expectation` naming; `ConsensusService` is exercised directly with `null!` as the `ServerCallContext` (`ConsensusServiceTests.cs:15`).
- Integration tests (`RaftServer.Tests/Cluster/`) carry `[Trait("Category", "Integration")]`, share one xUnit collection so clusters never run in parallel, use a fixed port range per test, and wait on conditions with a timeout (`TestCluster.WaitUntilAsync`) instead of fixed delays.
- Code, comments and commit messages are written in English.

## README vs code

`README.md` describes 8 independent processes; in reality it is one process hosting 8 in-process Kestrel servers, and one Docker container. It also states that gRPC channels are created once and cached — true for the leader and proposer paths, not for the election path (`Candidate.cs:48`).
