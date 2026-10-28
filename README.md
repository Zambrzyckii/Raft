# Distributed Math Cluster - Raft Consensus



## Project overview

The project is fully distributed computing system designed for asynchronous prime number searching. It was designed with high availability and fault tolerance in mind. Instead of relying on central server, the cluster utilizes an implementation of the Raft consensus algorithm to elect a Leader, synchronize data, and maintain a consistent state across all active instances. The cluster survives a crash of a single node, which is covered by integration tests in `RaftServer.Tests/Cluster`. The conscious simplifications relative to the Raft paper are listed in Known limitations below.

## Technologies

* C#
* gRPC
* PostgreSQL
* SignalR
* BackgroundServices and state machine
* Docker, Docker Compose

## Challanges solved and Architecture

* **Raft Algorithm** - Nodes in cluster dynamically change their roles (Follower -> Candidate -> Leader) depending on the network state. State transitions are implemented using the State design patter, which eliminates complex conditional statements and encapsulates node behaviour logic based on its current roles
* **Dynamic shrading** - The cluster does not use static configuration files to assign task pools. The ranges of numbers to check is dynamically divided among nodes using modulo operation, takin into account the number of currently active followers. After a Leader change the system automatically rebalances the work allocation. A crashed follower keeps its share of numbers (see Known limitations)
* **Network Optimization and Socket Exhaustion problem** -
Initial, intesive creation of gRPC channels inside computational loops led to the exhaustion of TCP ports at the OS level. the issue was resolved by implementing connection pooling pattern. Network channels are created once, cached in RAM and reused to send asynchronous requests
* **Race conditions** - The multi thread nature of the nodes created risk of race conditions and memory corruption. The code was secured with locks on shared collections and deduplication mechanisms. Among other things the double voting problem (voting twice in the same term by follower/candidate) was solved by implementing a thread-safe state machine that "seals" the cast vote. Some reads of the shared collections still happen outside those locks (see Known limitations)

## Known limitations

The implementation simplifies the Raft paper on purpose. These are conscious trade-offs.

* **No commit index** - A follower appends a number as soon as it arrives in `AppendEntries`, before the Leader has counted the acknowledgements, so a follower can hold an entry that was never committed by a majority. Replication is set based, there is no log matching and no backtracking
* **Static work pool** - `DynamicWorkDivider` shards numbers over the full peer list with only the current Leader removed. Numbers assigned to a crashed follower are not recomputed by anyone. With three nodes and one of them dead, the surviving follower can even end up with a shard that contains no primes at all (only even numbers)
* **Votes carry no term** - `AppendResponse` returns the responder term and a stale Leader steps down (`TermGuard`), but `VoteResponse` carries no term, so a Candidate learns about newer terms only from incoming RPCs
* **No persistence of the Raft state** - Term, vote and log live in memory. A restarted process starts the search from scratch and re-commits the same numbers. The PostgreSQL registry survives restarts, duplicates are absorbed by `ON CONFLICT DO NOTHING`
* **Occasional extra elections** - A healthy cluster can sometimes hold an election even though the Leader is alive, observed with eight nodes. The committed log is not affected by it

## How to run the project
1. Install docker 
2. Clone repo ``` git clone https://gitlab.com/ZambrzyckiBartosz/raft.git ```
3. Enter folder 
4. Copy the configuration template ``` cp .env.example .env ```
5. Enter ``` sudo docker-compose up --build ``` or ```sudo docker compose up --build ```
* The system will automatically spin up 8 independent processes, conduct an election campaign, elect a Leader, and staqrt distributed computations, saving the resulting registry to PostgreSQL in batches of 50 (table `primes`, reachable from the host with `psql -h localhost -p 5434 -U raft -d raft`).
* The `dashboard` service polls every node's `GetStatus` over gRPC and streams the live cluster state (roles, terms, log sizes) over SignalR at `http://localhost:5100/hub/cluster`.