![alt tag](https://github.com/jchristn/watsonmesh/blob/master/assets/watson.ico)

# Watson Mesh Networking

[![][nuget-img]][nuget]

[nuget]:     https://www.nuget.org/packages/WatsonMesh/
[nuget-img]: https://badge.fury.io/nu/Object.svg

A simple C# mesh networking library using TCP (with or without SSL) with integrated framing for reliable transmission and receipt of data amongst multiple nodes.

## New in v2.0.x

- Breaking changes!  Task-based callbacks
- Simplified constructors and APIs
- Dependency updates to better support graceful handling of disconnects
- Bugfixes

## What is Watson Mesh?

Watson Mesh is a simple library for mesh networking.  Instantiate the ```WatsonMesh``` class after defining the mesh network settings in ```MeshSettings``` and the local server configuration in ```Peer```.  Then, add ```Peer``` objects representing the other nodes in the network.  Each ```WatsonMesh``` node runs a local server and one client for each defined ```Peer```.  By default, a node will attempt to reconnect to each of the configured peers should the connection become severed.  

The network ```IsHealthy``` from a node's perspective if it has established outbound connections to each of its defined peers.  Thus, the health of the network is determined by each node from its own viewpoint.  The state of inbound connections from other nodes is not considered, but rather its ability to reach out to its peers.
  
Under the hood, ```WatsonMesh``` relies on ```WatsonTcp``` (see https://github.com/jchristn/WatsonTcp) for framing and reliable delivery.

## Test App

Refer to the ```Test``` project for a full working example.  Multiple instances can be started with each server listening on a different port.  Then add each node as peers on each of the nodes you start.

## Example

The following example shows a simple example using byte arrays and without SSL.  Make sure you start instances for mesh nodes running on ports 8000, 8001, and 8002.  You can use multiple instances of the ```Test``` project to see a more complete example. 

```
using WatsonMesh; 

// initialize

MeshSettings settings = new MeshSettings(); // use defaults
Peer self = new Peer("127.0.0.1", 8000);
WatsonMesh mesh = new WatsonMesh(settings, self);

// define callbacks and start

mesh.PeerConnected = PeerConnected;
mesh.PeerDisconnected = PeerDisconnected;
mesh.MessageReceived = MessageReceived;
mesh.SyncMessageReceived = SyncMessageReceived;
mesh.Start();

// add peers 

mesh.Add(new Peer("127.0.0.1", 8001));
mesh.Add(new Peer("127.0.0.1", 8002)); 

// implement callbacks

static async Task PeerConnected(Peer peer) 
{
    Console.WriteLine("Peer " + peer.ToString() + " connected!");
}

static async Task PeerDisconnected(Peer peer) 
{
    Console.WriteLine("Peer " + peer.ToString() + " disconnected!");
}

static async Task MessageReceived(Peer peer, long contentLength, Stream stream) 
{
    // read contentLength bytes from stream and process
}

static SyncResponse SyncMessageReceived(Peer peer, long contentLength, Stream stream) 
{
	// read stream into byte[] data
	Console.WriteLine(peer.ToString() + " says: " + Encoding.UTF8.GetString(data));
	Console.Write("How would you like to respond? ");
	byte[] responseBytes = Encoding.UTF8.GetBytes(Console.ReadLine());
    MemoryStream ms = new MemoryStream(responseBytes);
	SyncResponse sr = new SyncResponse(SyncResponseStatus.Success, responseBytes.Length, ms);
	return sr;
}

// send messages

byte[] data = Encoding.UTF8.GetBytes("Hello from Watson Mesh!");
if (!mesh.Send("127.0.0.1", 8001, data)) { // handle errors }
if (!mesh.Broadcast(data)) { // handle errors }
SyncResponse resp = mesh.SendSync("127.0.0.1", 8001, 5000, dataLength, stream);
```

## Running under Mono

The preferred framework for WatsonMesh for multiplatform environments is .NET Core.  However, the project works well in Mono environments with hte .NET Frameworks to the extent that we have tested it. It is recommended that when running under Mono, you execute the containing EXE using --server and after using the Mono Ahead-of-Time Compiler (AOT).  Note that TLS 1.2 is hard-coded, which may need to be downgraded to TLS in Mono environments.

NOTE: Windows accepts '0.0.0.0' as an IP address representing any interface.  On Mac and Linux you must be specified ('127.0.0.1' is also acceptable, but '0.0.0.0' is NOT).
```
mono --aot=nrgctx-trampolines=8096,nimt-trampolines=8096,ntrampolines=4048 --server myapp.exe
mono --server myapp.exe
```
 
## Version History

Refer to CHANGELOG.md for a list of changes by version.
