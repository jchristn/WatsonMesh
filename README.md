![alt tag](https://github.com/jchristn/watsonmesh/blob/master/assets/watson.ico)

# Watson Mesh Networking

[![][nuget-img]][nuget]

[nuget]:     https://www.nuget.org/packages/WatsonMesh/
[nuget-img]: https://badge.fury.io/nu/Object.svg

A simple C# mesh networking library using TCP (with or without SSL) with integrated framing for reliable transmission and receipt of data amongst multiple nodes.

## What is Watson Mesh?

Watson Mesh is a simple library for mesh networking.  Instantiate the ```WatsonMesh``` class after defining the mesh network settings in ```MeshSettings``` and the local server configuration in ```Peer```.  Then, add ```Peer``` objects representing the other nodes in the network.  Each ```WatsonMesh``` node runs a local server and one client for each defined ```Peer```.  By default, a node will attempt to reconnect to each of the configured peers should the connection become severed.  

The network ```IsHealthy``` from a node's perspective if it has established outbound connections to each of its defined peers.  Thus, the health of the network is determined by each node from its own viewpoint.  The state of inbound connections from other nodes is not considered, but rather its ability to reach out to its peers.

Watson Mesh supports use of either byte arrays or streams for sending and receiving data.

- If you prefer to use ```byte[]```, set ```Settings.ReadDataStream = true``` and implement the callbacks ```AsyncMessageReceived``` and ```SyncMessageReceived```
- If you prefer to use ```Stream```, set ```Settings.ReadDataStream = false``` and implement the callbacks ```AsyncStreamReceived``` and ```SyncStreamReceived```

Send an asynchronous message using:

- A byte array: ```SendAsync(string ip, int port, byte[] data)```
- A stream: ```SendAsync(string ip, int port, long contentLength, Stream stream)```

Sending a synchronous message will block until a response is received or the message times out.  To send a synchronous message using:

- A byte array: ```SendSync(string ip, int port, int timeoutMs, byte[] data, out byte[] response)```
- A stream: ```SendSync(string ip, int port, int timeoutMs, long contentLength, Stream stream, out long responseLength, out Stream responseStream)```

You can also send a message to every connected peer.  To do so using:

- A byte array: ```Broadcast(byte[] data)```
- A stream: ```Broadcast(long contentLength, Stream stream)```

Under the hood, ```WatsonMesh``` relies on ```WatsonTcp``` (see https://github.com/jchristn/WatsonTcp).

## New in v1.2.x

- Added support for sync messaging using streams

## Test App

Refer to the ```Test``` project for a full working example.  Multiple instances can be started with each server on a different port.  Then add each node as peers on each of the nodes you start.

## Example

The following example shows a simple example using byte arrays and without SSL.  Make sure you start instances for mesh nodes running on ports 8000, 8001, and 8002.  You can use multiple instances of the ```Test``` project to see a more complete example. 

```
using WatsonMesh;
using System.IO;

// initialize

MeshSettings settings = new MeshSettings(); // use defaults
Peer self = new Peer("127.0.0.1", 8000);
WatsonMesh mesh = new WatsonMesh(settings, self);
mesh.Start();

// define callbacks

mesh.PeerConnected = PeerConnected;
mesh.PeerDisconnected = PeerDisconnected;
mesh.AsyncMessageReceived = AsyncMessageReceived;
mesh.SyncMessageReceived = SyncMessageReceived;

// add peers 

mesh.Add(new Peer("127.0.0.1", 8001));
mesh.Add(new Peer("127.0.0.1", 8002));
mesh.Add(new Peer("127.0.0.1", 8003));

// implement callbacks

bool PeerConnected(Peer peer) {
  Console.WriteLine("Peer " + peer.ToString() + " connected!");
  return true;
}
bool PeerDisconnected(Peer peer) {
  Console.WriteLine("Peer " + peer.ToString() + " disconnected!");
  return true;
}
bool AsyncMessageReceived(Peer peer, byte[] data) {
  Console.WriteLine(peer.ToString() + " says: " + Encoding.UTF8.GetString(data));
  return true;
}
byte[] SyncMessageReceived(Peer peer, byte[] data) {
  Console.WriteLine(peer.ToString() + " says: " + Encoding.UTF8.GetString(data));
  Console.Write("Type your response: ");
  return Encoding.UTF8.GetBytes(Console.ReadLine());
}

// send messages

byte[] data = Encoding.UTF8.GetBytes("Hello from Watson Mesh!");
if (!mesh.SendAsync("127.0.0.1", 8001, data)) { // handle errors }
if (!mesh.Broadcast(data)) { // handle errors }
byte[] response;
if (!mesh.SendSync("127.0.0.1", 8001, 5000, data, out response)) { // handle errors }
```

## Running under Mono

The preferred framework for WatsonMesh for multiplatform environments is .NET Core.  However, the project works well in Mono environments with hte .NET Frameworks to the extent that we have tested it. It is recommended that when running under Mono, you execute the containing EXE using --server and after using the Mono Ahead-of-Time Compiler (AOT).  Note that TLS 1.2 is hard-coded, which may need to be downgraded to TLS in Mono environments.

NOTE: Windows accepts '0.0.0.0' as an IP address representing any interface.  On Mac and Linux you must be specified ('127.0.0.1' is also acceptable, but '0.0.0.0' is NOT).
```
mono --aot=nrgctx-trampolines=8096,nimt-trampolines=8096,ntrampolines=4048 --server myapp.exe
mono --server myapp.exe
```
 
## Version History

Release content from previous versions will be shown here.

v1.1.x

- Added support for sending streams in async messages to support larger messages
- Sync messages (expecting a response) still use byte arrays, as these are usually smaller, interactive messages
- Default constructor for Peer
- Bugfixes and minor refactor

v1.0.x

- Retarget to support .NET Core 2.0 and .NET Framework 4.6.1
- WarningMessage function, which can be useful for sending warning messages to the consuming application.  Useful for debugging issues in particular with synchronous messaging.
- Sync message API (awaits and returns response within specified timeout)
- Initial release
