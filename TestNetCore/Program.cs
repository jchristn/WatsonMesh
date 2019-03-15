using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Watson;

namespace TestNetCore
{
    class Program
    {
        static string _Ip;
        static int _Port;
        static MeshSettings _Settings;
        static Peer _Self;
        static WatsonMesh _Mesh;

        static bool _RunForever = true;

        static void Main(string[] args)
        {
            _Ip = Common.InputString("Listener IP:", "127.0.0.1", false);
            _Port = Common.InputInteger("Listener Port:", 8000, true, false);

            _Settings = new MeshSettings();
            _Self = new Peer(_Ip, _Port, false);

            _Mesh = new WatsonMesh(_Settings, _Self);
            _Mesh.PeerConnected = PeerConnected;
            _Mesh.PeerDisconnected = PeerDisconnected;
            _Mesh.AsyncMessageReceived = AsyncMessageReceived;
            _Mesh.SyncMessageReceived = SyncMessageReceived;

            _Mesh.StartServer();

            while (_RunForever)
            {
                string userInput = Common.InputString("WatsonMesh [? for help] >", null, false);

                List<Peer> peers;

                switch (userInput)
                {
                    case "?":
                        Menu();
                        break;
                    case "q":
                    case "quit":
                        _RunForever = false;
                        break;
                    case "c":
                    case "cls":
                        Console.Clear();
                        break;
                    case "list":
                        peers = _Mesh.GetPeers();
                        if (peers != null && peers.Count > 0)
                        {
                            Console.WriteLine("Configured peers: " + peers.Count);
                            foreach (Peer curr in peers) Console.WriteLine("  " + curr.ToString());
                        }
                        else
                        {
                            Console.WriteLine("None");
                        }
                        break;
                    case "failed":
                        peers = _Mesh.GetDisconnectedPeers();
                        if (peers != null && peers.Count > 0)
                        {
                            Console.WriteLine("Failed peers: " + peers.Count);
                            foreach (Peer currPeer in peers) Console.WriteLine("  " + currPeer.ToString());
                        }
                        else
                        {
                            Console.WriteLine("None");
                        }
                        break;
                    case "sendasync":
                        if (_Mesh.SendAsync(
                            Common.InputString("Peer IP", "127.0.0.1", false),
                            Common.InputInteger("Peer port:", 8000, true, false),
                            Encoding.UTF8.GetBytes(Common.InputString("Data:", "some data", false))))
                        {
                            Console.WriteLine("Success");
                        }
                        else
                        {
                            Console.WriteLine("Failed");
                        }
                        break;
                    case "sendsync":
                        byte[] responseData = null;
                        if (_Mesh.SendSync(
                            Common.InputString("Peer IP", "127.0.0.1", false),
                            Common.InputInteger("Peer port:", 8000, true, false),
                            Common.InputInteger("Timeout ms:", 5000, true, false),
                            Encoding.UTF8.GetBytes(Common.InputString("Data:", "some data", false)),
                            out responseData))
                        {
                            Console.WriteLine("Success");
                            if (responseData != null && responseData.Length > 0)
                            {
                                Console.WriteLine("Response: " + Encoding.UTF8.GetString(responseData));
                            }
                        }
                        else
                        {
                            Console.WriteLine("Failed");
                        }
                        break;
                    case "synccount":
                        Console.WriteLine("Pending sync requests  : " + _Mesh.SyncRequests.Count);
                        Console.WriteLine("Pending sync responses : " + _Mesh.SyncResponses.Count);
                        break;
                    case "bcast":
                        if (_Mesh.Broadcast(
                            Encoding.UTF8.GetBytes(Common.InputString("Data:", "some data", false))))
                        {
                            Console.WriteLine("Success");
                        }
                        else
                        {
                            Console.WriteLine("Failed");
                        }
                        break;
                    case "add":
                        _Mesh.Add(
                            new Peer(
                                Common.InputString("Peer IP:", "127.0.0.1", false),
                                Common.InputInteger("Peer port:", 8000, true, false),
                                false));
                        break;
                    case "del":
                        _Mesh.Remove(
                            new Peer(
                                Common.InputString("Peer IP:", "127.0.0.1", false),
                                Common.InputInteger("Peer port:", 8000, true, false),
                                false));
                        break;
                    case "health":
                        Console.WriteLine("Healthy: " + _Mesh.IsHealthy());
                        break;
                    case "nodehealth":
                        Console.WriteLine(
                            _Mesh.IsHealthy(
                                Common.InputString("Peer IP:", "127.0.0.1", false),
                                Common.InputInteger("Peer port:", 8000, true, false)));
                        break;
                }
            }
        }

        static void Menu()
        {
            Console.WriteLine("Available commands:");
            Console.WriteLine("  ?           help, this menu");
            Console.WriteLine("  cls         clear the screen");
            Console.WriteLine("  q           quit the application");
            Console.WriteLine("  list        list all peers");
            Console.WriteLine("  failed      list failed peers");
            Console.WriteLine("  add         add a peer");
            Console.WriteLine("  del         delete a peer");
            Console.WriteLine("  sendasync   send a message to a peer asynchronously");
            Console.WriteLine("  sendsync    send a message to a peer and await a response");
            Console.WriteLine("  synccount   number of sync requests and responses pending");
            Console.WriteLine("  bcast       send a message to all peers");
            Console.WriteLine("  health      display if the mesh is healthy");
            Console.WriteLine("  nodehealth  display if a connection to a peer is healthy");
        }

        static bool PeerConnected(Peer peer)
        {
            Console.WriteLine("Peer connected: " + peer.ToString());
            return true;
        }

        static bool PeerDisconnected(Peer peer)
        {
            Console.WriteLine("Peer disconnected: " + peer.ToString());
            return true;
        }

        static bool AsyncMessageReceived(Peer peer, byte[] data)
        {
            Console.WriteLine(peer.ToString() + ": " + Encoding.UTF8.GetString(data));
            return true;
        }

        static byte[] SyncMessageReceived(Peer peer, byte[] data)
        {
            Console.WriteLine(peer.ToString() + ": " + Encoding.UTF8.GetString(data));
            string resp = Common.InputString("Response:", "This is a response", false);
            return Encoding.UTF8.GetBytes(resp);
        }
    }
}
