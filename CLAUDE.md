# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

Verified on .NET SDK 10.0.113:

- Build: `dotnet build Raft.sln`
- All tests: `dotnet test` (xUnit, 25 tests, takes ~40 s because of the integration tests)
- Fast tests only: `dotnet test --filter "Category!=Integration"` (21 unit tests, under a second)
- Integration tests only: `dotnet test --filter "Category=Integration"` (4 tests; each starts an in-process cluster and binds localhost ports in the 7101-7404 range)
- Single test class: `dotnet test --filter "FullyQualifiedName~ConsensusServiceTests"`
- Single test method: `dotnet test --filter "FullyQualifiedName=RaftServer.Tests.Services.ConsensusServiceTests.ProposeNumber_WhenNotLeader_Ignore"`
- Run the cluster: `dotnet run --project RaftServer` (8 nodes) or `dotnet run --project RaftServer -- 3` (3 nodes; a bare `3` without `--` is forwarded as well)
- Containerised: `docker compose up --build`

No linter or formatter is configured — there is no `.editorconfig`, `Directory.Build.props` or analyzer package. `Raft.sln.DotSettings.user` is a per-user Rider file, not a project-wide convention.

Startup side effects: the process deletes and recreates `primes.txt` in the content root (`Program.cs:4`), i.e. `RaftServer/primes.txt` when using `dotnet run`. The file is gitignored. Ports 5001..500N must be free. A leader elected inside `dotnet test` appends to `primes.txt` in the test `bin` directory, which is harmless.

## Architecture

### One process is the whole cluster

`Program.cs:12-18` builds N `WebApplication` instances inside a single process through `NodeHost.Build` (`RaftServer/Hosting/NodeHost.cs`), each with its own DI container and its own `RaftNode` singleton (`NodeHost.cs:23`). A node's identity is its URL, `http://localhost:500{i}` (`Program.cs:9`), and its peer list is simply every other URL (`NodeHost.cs:13-20`). There are no per-node config files and no service discovery. The integration tests build their nodes with the same `NodeHost.Build` (`RaftServer.Tests/Cluster/TestCluster.cs`), so test clusters run the production wiring.

Consequence: `compose.yaml` starts **one container that contains the entire cluster**. Scaling it with `replicas` would not add nodes, it would start additional independent clusters. The Dockerfile's `EXPOSE 8080/8081` is inert because `UseUrls` (`NodeHost.cs:22`) overrides it; no ports are published and `primes.txt` stays inside the container.

### Three concurrent writers per node

Each node runs two `BackgroundService`s — `RaftEngine` (consensus) and `MathEngine` (prime search) — plus the gRPC handlers in `ConsensusService`. All three mutate the same `RaftNode` instance, which is the only coordination point. Locking is per-field and not uniform: `lock (_node)` (`ConsensusService.cs:57`), `lock (OfficialPrimes)` (`ConsensusService.cs:27`, `VoteCounter.cs:15`, `ReliableProposer.cs:44`), `lock (pendingNumber)` (`ConsensusService.cs:40`, `Leader.cs:17`), while some reads of the same collections are unlocked (`Leader.cs:27`, `MathEngine.cs:25`). Assume any `RaftNode` field can change underneath you.

### Role transitions (State pattern)

`RaftEngine.ExecuteAsync` (`RaftEngine.cs:26-37`) swaps the `IRaftState` object whenever `RaftNode._currentState` no longer matches the current one, then calls `Execute` in a tight loop. The role is set from three directions: by the role behaviours themselves (`Follower.cs:18`, `Candidate.cs:66`, `Candidate.cs:71`), by incoming RPCs (`ConsensusService.cs:23`, `ConsensusService.cs:65`), and by a leader that sees a newer term in an ack and steps down (`TermGuard.cs`, called from `Leader.cs:39` and `Leader.cs:83`).

The election timeout is not a timer: a follower awaits `Task.Delay(2000-4000, token)` (`Follower.cs:11-15`) and an incoming heartbeat cancels that token through `RaftNode.CancelTokenTimeout()` (`RaftNode.cs:21`, called from `ConsensusService.cs:21`). A heartbeat with a stale term is rejected without resetting the timeout (`ConsensusService.cs:18-19`). `Timer/RandomTime.cs` is dead code and says so in its own comment.

**Debugging note**: `RaftEngine.cs:39-47` catches *every* exception thrown by a role's `Execute`, logs the role, exception type and message, and sleeps 2 s. A recurring `... failed: ...` line in the console is a role crashing on every iteration, not a slow cluster.

### Prime commit path (the part that spans the most files)

1. `MathEngine.cs:18` — only a follower that already knows its leader computes; each node walks its own `_currentNumber` counter.
2. `DynamicWorkDivider.IsMyTurn` (`DynamicWorkDivider.cs:9-21`) — sharding by `number % followerCount == myIndex` over the sorted peer list with the current leader removed, so work rebalances by itself after every leader change.
3. `IsPrime.CheckPrime` (`Algorithm/IsPrime.cs`) — 6k±1 trial division.
4. `ReliableProposer.SendAndWaitForAckAsync` (`ReliableProposer.cs:15-51`) — resends `ProposeNumber` every 200 ms, re-resolving `context.currentLeader` on each retry (so a leader change mid-proposal is followed), until the number appears in the node's own `OfficialPrimes`; that local arrival (delivered later by `AppendEntries`) *is* the acknowledgement.
5. Leader side: `ConsensusService.ProposeNumber` (`ConsensusService.cs:35-53`) deduplicates against the log and the queue, then enqueues into `pendingNumber`; a non-leader answers `IsFine = false`.
6. `Leader.Execute` (`Leader.cs:13-56`) peeks one queued item, replicates it with `ProposeNumbers.SendProposeNumbers`, steps down if an ack carries a newer term (`TermGuard.cs`), counts non-null `IsDone` acks and requires a majority in `VoteCounter.cs:11-25`, appends a `LogEntry` stamped with `currentTerm` (`VoteCounter.cs:19`) plus one line to `primes.txt` (`FileStorageService.cs:9-13`), and only then dequeues.
7. With an empty queue the leader sends heartbeats instead (`Leader.cs:57-84`), and paces every iteration with `Task.Delay(50)` (`Leader.cs:86`).

Replication is set-based, not index-based: a follower appends any number it has not seen from `AppendEntries` (`ConsensusService.cs:25-31`); there is no log matching or backtracking. A request whose `LeaderTerm` is lower than the node's term is denied with `IsDone = false` and the node's own term in `AppendResponse.Term` (`ConsensusService.cs:18-19`). The election restriction is approximated by comparing log count and last term (`ConsensusService.cs:68-75`).

### gRPC layer

The contract lives in `Protos/RaftConsensus.proto`, generated for both client and server (`RaftServer.csproj:12`) into namespace `Raft.Grpc`. `AppendResponse` carries the responder's term (`RaftConsensus.proto:31`), which is what `TermGuard` reads. Plaintext HTTP/2 is forced in `appsettings.json:9-13`; gRPC will not work without it. The test host copies that `appsettings.json` next to the test binaries, which is why in-process test clusters get HTTP/2 without extra configuration.

Channel reuse is deliberate (socket exhaustion was a real problem in this project) but partial: `Leader._channelCache` (`Leader.cs:12`, per Leader instance, so it resets on every role change) and the static `ReliableProposer._channels` (`ReliableProposer.cs:10`) cache channels, while `Candidate.FarmVoteResponses` (`Candidate.cs:48`) still creates a fresh channel per peer per campaign.

## Conventions

- `RootNamespace` is `Raft` (`RaftServer.csproj:8`), so folders map to `Raft.*`, not `RaftServer.*` — `RaftServer/Services/` → `namespace Raft.Services`. Test namespaces are `RaftServer.Tests.*`.
- Model members are camelCase, some with a leading underscore (`RaftNode.currentTerm`, `RaftNode._currentState`), and role behaviours name their parameter `_context`. Unusual for C#, but match it.
- Role logic longer than a few lines is extracted into a static helper class next to the role (`RoleBehaviours/Leader/ProposeNumbers.cs`, `VoteCounter.cs`, `GetSignal.cs`, `TermGuard.cs`).
- Remote calls go through `*Safe` helpers that swallow the exception and return `null` (`GetSignal.cs:9-16`, `Candidate.cs:27-37`). A `null` means a dead peer, and callers count only non-null responses (`Candidate.cs:22`, `Leader.cs:44`).
- Tests: xUnit, `Method_Scenario_Expectation` naming; `ConsensusService` is exercised directly with `null!` as the `ServerCallContext` (`ConsensusServiceTests.cs:15`).
- Integration tests (`RaftServer.Tests/Cluster/`) carry `[Trait("Category", "Integration")]`, share one xUnit collection so clusters never run in parallel, use a fixed port range per test, and wait on conditions with a timeout (`TestCluster.WaitUntilAsync`) instead of fixed delays.
- Code, comments and commit messages are written in English.

## README vs code

`README.md` describes 8 independent processes; in reality it is one process hosting 8 in-process Kestrel servers, and one Docker container. It also states that gRPC channels are created once and cached — true for the leader and proposer paths, not for the election path (`Candidate.cs:48`).
