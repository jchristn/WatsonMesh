# Watson Mesh Networking

[![][nuget-img]][nuget]

[nuget]:     https://www.nuget.org/packages/WatsonMesh/
[nuget-img]: https://badge.fury.io/nu/Object.svg

A simple C# mesh networking library using TCP (with or without SSL) with integrated framing for reliable transmission and receipt of data amongst multiple nodes.

## What is Watson Mesh?
Watson Mesh is a simple library for mesh networking.  Instantiate the ```WatsonMesh``` class after defining the mesh network settings in ```MeshSettings``` and the local server configuration in ```Peer```.  Then, add ```Peer``` objects representing the other nodes in the network.  Each ```WatsonMesh``` node runs a local server and some n number of clients based on the number of configured peers.  By default, a node will attempt to reconnect to each of the configured peers should the connection become severed.  

The network is considered 'healthy' from a node's perspective if it has established outbound connections to each of its defined peers.  Thus, the health of the network is determined by each node from its own viewpoint.  The state of inbound connections from other nodes is not considered.

Send a message to a peer using ```Send(string ip, int port, byte[] data)``` or broadcast to all connected peers using ```Broadcast(byte[] data)```.

Under the hood, ```WatsonMesh``` relies on ```WatsonTcp``` (see https://github.com/jchristn/WatsonTcp).

## New in This Version
- Initial release

## Roadmap
The main gap in this release is the lack of a state machine to manage sharing of configuration and authentication amongst nodes.  However, authentication is less necessary when using SSL with certificate files.

## Test App
A test project is included which will help you understand the library.  Multiple instances can be started, assuming each server is started on a different port.

## Example
The following example shows a simple example without SSL.  Make sure you start instances for mesh nodes running on ports 8000, 8001, and 8002.  You can use multiple instances of the ```Test``` project to see a more complete example. 
```
using WatsonMesh;

// initialize
MeshSettings settings = new MeshSettings(); // use defaults
Peer self = new Peer("127.0.0.1", 8000, false);
WatsonMesh mesh = new WatsonMesh(settings, self);

// define callback methods
mesh.PeerConnected = PeerConnected;
mesh.PeerDisconnected = PeerDisconnected;
mesh.MessageReceived = MessageReceived;

// add and remove peers (remote servers in the mesh network)
mesh.Add("127.0.0.1", 8001, false);
mesh.Add("127.0.0.1", 8002, false);
mesh.Remove("127.0.0.1", 8003);

// send messages
byte[] data = Encoding.UTF8.GetBytes("Hello from Watson Mesh!");
mesh.Send("127.0.0.1", 8001, data);
mesh.Broadcast(data);

// callbacks
bool PeerConnected(Peer peer) {
    Console.WriteLine("Peer " + peer.ToString() + " connected!");
    return true;
}
bool PeerDisconnected(Peer peer) {
    Console.WriteLine("Peer " + peer.ToString() + " disconnected!");
    return true;
}
bool MessageReceived(Peer peer, byte[] data) {
    Console.WriteLine(peer.ToString() + " says: " + Encoding.UTF8.GetString(data));
    return true;
}
```

## Running under Mono
The project works well in Mono environments to the extent that we have tested it. It is recommended that when running under Mono, you execute the containing EXE using --server and after using the Mono Ahead-of-Time Compiler (AOT).  Note that TLS 1.2 is hard-coded, which may need to be downgraded to TLS in Mono environments.

NOTE: Windows accepts '0.0.0.0' as an IP address representing any interface.  On Mac and Linux you must be specified ('127.0.0.1' is also acceptable, but '0.0.0.0' is NOT).
```
mono --aot=nrgctx-trampolines=8096,nimt-trampolines=8096,ntrampolines=4048 --server myapp.exe
mono --server myapp.exe
```
 
## Version History
Release content from previous versions will be shown here.
v1.0.x
- Initial release
