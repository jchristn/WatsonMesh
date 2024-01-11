![alt tag](https://github.com/jchristn/watsonmesh/blob/master/assets/watson.ico)

# Watson Mesh Networking

[![NuGet Version](https://img.shields.io/nuget/v/WatsonMesh.svg?style=flat)](https://www.nuget.org/packages/WatsonMesh/) [![NuGet](https://img.shields.io/nuget/dt/WatsonMesh.svg)](https://www.nuget.org/packages/WatsonMesh) 

A simple C# mesh networking library using TCP (with or without SSL) with integrated framing for reliable transmission and receipt of data amongst multiple nodes.  Tested on Windows and Ubuntu.

## New in v5.0.x

- Update to latest WatsonTcp
- Migrated from string IP:port methods to GUID
- Migrated to async methods
- Rename enums, add JSON converter details
- Consistent logging headers
- Usings inside of namespace
- Async callback for SyncRequest
- Cancellation token and ```ConfigureAwait```

## What is Watson Mesh?

Watson Mesh is a simple library for mesh networking (all nodes have connections to one another).  Instantiate the ```WatsonMesh``` class after defining the mesh network settings in ```MeshSettings```.  Then, add ```MeshPeer``` objects representing the other nodes in the network.  Each ```WatsonMesh``` node runs a local server and one client for each defined ```MeshPeer```.  By default, a node will attempt to reconnect to each of the configured peers should the connection become severed.  

The network ```IsHealthy``` from a node's perspective if it has established outbound connections to each of its defined peers.  Thus, the health of the network is determined by each node from its own viewpoint.  The state of inbound connections from other nodes is not considered, but rather its ability to reach out to its peers.
  
Under the hood, ```WatsonMesh``` relies on ```WatsonTcp``` (see https://github.com/dotnet/WatsonTcp) for framing and reliable delivery.

## Test App

Refer to the ```Test``` project for a full working example.  Multiple instances can be started with each server listening on a different port.  Then add each node as peers on each of the nodes you start.

## Local vs External Connections

**IMPORTANT**

The IP address you specify determines whether or not WatsonMesh will allow external connections.   

* If you specify ```127.0.0.1``` as the IP address, WatsonMesh will only be able to accept connections from within the local host.  
* To accept connections from other machines:
  * Use a specific interface IP address (i.e. ```ipconfig``` from Command Prompt or ```ifconfig``` from Linux shell)
  * Make sure you create a permit rule on your firewall to allow inbound connections on that port
* If you use a port number under 1024, admin privileges will be required

**REMINDER** If you use ```127.0.0.1``` as your IP address, WatsonMesh will only be allowed to receive connections from the local machine.

## Known Limitations

WatsonMesh will not work in environments where NAT (Network Address Translation) exists in between members of the mesh network.

## Example

The following example shows a simple example using byte arrays and without SSL.  Make sure you start instances for mesh nodes running on ports 8000, 8001, and 8002.  You can use multiple instances of the ```Test``` project to see a more complete example. 

```csharp
using WatsonMesh; 

// initialize
MeshNode mesh = new MeshNode(new MeshSettings(), "127.0.0.1", 8000);

// define callbacks and start
mesh.PeerConnected += PeerConnected;
mesh.PeerDisconnected += PeerDisconnected;
mesh.MessageReceived += MessageReceived; 
mesh.SyncMessageReceived = SyncMessageReceived;
mesh.Start();

// add peers 
mesh.Add(new Peer([guid], "127.0.0.1:8001"));
mesh.Add(new Peer([guid], "127.0.0.1:8002")); 

// implement events

static void PeerConnected(object sender, ServerConnectionEventArgs args) 
{
    Console.WriteLine("Peer " + args.PeerNode.ToString() + " connected");
}

static void PeerDisconnected(object sender, ServerConnectionEventArgs args) 
{
    Console.WriteLine("Peer " + args.PeerNode.ToString() + " disconnected");
}

static void MessageReceived(object sender, MeshMessageReceivedEventArgs args) 
{
	Console.WriteLine("Message from " + args.SourceIpPort + ": " + Encoding.UTF8.GetBytes(args.Data));
}

static SyncResponse SyncMessageReceived(MeshMessageReceivedEventArgs args) 
{
	Console.WriteLine("Message from " + args.SourceIpPort + ": " + Encoding.UTF8.GetBytes(args.Data));
	return new SyncResponse(SyncResponseStatus.Success, "Hello back at you!");
}

// send messages 
if (!await mesh.Send([guid], "Hello, world!")) { // handle errors }
if (!await mesh.Broadcast("Hello, world!")) { // handle errors }
SyncResponse resp = await mesh.SendAndWait([guid], 5000, "Hello, world!");
```

## Version History

Refer to CHANGELOG.md for a list of changes by version.
