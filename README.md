# Distributed Math Cluster - Raft Consensus



## Project overview

The project is fully distributed computing system designed for asynchronous prime number searching. It was designed with high availability and fault tolerance in mind. Instead of relying on central server, the cluster utilizes an implementation of the Raft consensus algorithm to elect a Leader, synchronize data, and maintain a consistent state across all active instances.

## Technologies

* C#
* gRPC
* BackgroundServices and state machine
* Docker, Docker Compose

## Challanges solved and Architecture

* **Raft Algorithm** - Nodes in cluster dynamically change their roles (Follower -> Candidate -> Leader) depending on the network state. State transitions are implemented using the State design patter, which eliminates complex conditional statements and encapsulates node behaviour logic based on its current roles
* **Dynamic shrading** - The cluster does not use static configuration files to assign task pools. The ranges of numbers to check is dynamically divided among nodes using modulo operation, takin into account the number of currently active followers. In the event of a node failture or Leader change, the system automatically rebalances the work allocation
* **Network Optimization and Socket Exhaustion problem** -
Initial, intesive creation of gRPC channels inside computational loops led to the exhaustion of TCP ports at the OS level. the issue was resolved by implementing connection pooling pattern. Network channels are created once, cached in RAM and reused to send asynchronous requests
* **Race conditions** - The multi thread nature of the nodes created risk of race conditions and memory corruption. The code was secured with locks on shared collections and deduplication mechanisms. Among other things the double voting problem (voting twice in the same term by follower/candidate) was solved by implementing a thread-safe state machine that "seals" the cast vote

## How to run the project
1. Install docker 
2. Clone repo ``` git clone https://gitlab.com/ZambrzyckiBartosz/raft.git ```
3. Enter folder 
4. Enter ``` docker-compose up --build ```
* The system will automatically spin up 8 independent processes, conduct an election campaign, elect a Leader, and staqrt distributed computations, saving the resulting registry to the ``` prime.txt ``` file.