using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GetSomeInput;
using WatsonMesh;

namespace TestNetCore
{
    class Program
    {
        static string _Ip;
        static int _Port = 0;
        static string _IpPort;
        static Guid _Guid = default(Guid);
        static MeshSettings _Settings; 
        static MeshNode _Mesh;
        static Dictionary<Guid, string> _Peers = new Dictionary<Guid, string>();

        static bool _RunForever = true;

        static void Main(string[] args)
        {
            if (args != null && args.Length > 0)
            { 
                ParseArguments(args, out _Ip, out _Port, out _Guid, out _Peers);
                _IpPort = _Ip + ":" + _Port;

                Console.WriteLine("");
                Console.WriteLine("Using arguments:");
                Console.WriteLine("  IP      : " + _Ip);
                Console.WriteLine("  Port    : " + _Port);
                Console.WriteLine("  IP:port : " + _IpPort);
                Console.WriteLine("  GUID    : " + _Guid.ToString());
                Console.WriteLine("  Peers   : " + _Peers.Count);
                Console.WriteLine("");
            }
            else
            {
                _IpPort = Inputty.GetString("Local IP:port :", "127.0.0.1:8000", false);
                ParseIpPort(_IpPort, out _Ip, out _Port);
            }

            _Settings = new MeshSettings(); 
            _Settings.AcceptInvalidCertificates = true;
            _Settings.AutomaticReconnect = true; 
            _Settings.MutuallyAuthenticate = false;
            _Settings.PresharedKey = null; 
            _Settings.StreamBufferSize = 65536;
            _Settings.ReconnectIntervalMs = 1000;
            _Settings.Guid = _Guid;

            _Mesh = new MeshNode(_Settings, _Ip, _Port);
            _Mesh.PeerConnected += PeerConnected;
            _Mesh.PeerDisconnected += PeerDisconnected;
            _Mesh.MessageReceived += MessageReceived;
            _Mesh.SyncMessageReceived = SyncMessageReceived;
            _Mesh.Logger = Logger;

            _Mesh.Start();

            if (_Peers != null && _Peers.Count > 0)
            {
                foreach (KeyValuePair<Guid, string> peer in _Peers)
                {
                    _Mesh.Add(new MeshPeer(peer.Key, peer.Value));
                }
            }

            while (_RunForever)
            {
                string userInput = Inputty.GetString("WatsonMesh [? for help] >", null, false);

                List<MeshPeer> peers;

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
                        peers = _Mesh.GetPeers().ToList();
                        Console.WriteLine("");
                        if (peers != null && peers.Count > 0)
                        {
                            Console.WriteLine("Configured peers: " + peers.Count);
                            foreach (MeshPeer curr in peers) Console.WriteLine("  " + curr.ToString());
                        }
                        else
                        {
                            Console.WriteLine("None");
                        }
                        Console.WriteLine("");
                        break;

                    case "failed":
                        peers = _Mesh.GetDisconnectedPeers();
                        Console.WriteLine("");
                        if (peers != null && peers.Count > 0)
                        {
                            Console.WriteLine("Failed peers: " + peers.Count);
                            foreach (MeshPeer currPeer in peers) Console.WriteLine("  " + currPeer.ToString());
                        }
                        else
                        {
                            Console.WriteLine("None");
                        }
                        Console.WriteLine("");
                        break;

                    case "send":
                        Send();
                        break;

                    case "send md":
                        SendMetadata();
                        break;

                    case "sendsync":
                        SendSync();
                        break;

                    case "sendsync md":
                        SendSyncMetadata();
                        break;

                    case "bcast":
                        Broadcast();
                        break;

                    case "add":
                        _Mesh.Add(
                            new MeshPeer(
                                Inputty.GetGuid  ("GUID   :", default(Guid)),
                                Inputty.GetString("IP:port:", "127.0.0.1:8000", false)));
                        break;

                    case "del":
                        _Mesh.Remove(Inputty.GetGuid("GUID:", default(Guid)));
                        break;

                    case "health":
                        Console.WriteLine("Healthy: " + _Mesh.IsHealthy);
                        break;

                    case "nodehealth":
                        Console.WriteLine(_Mesh.IsPeerConnected(Inputty.GetGuid("GUID:", default(Guid))));
                        break;
                }
            }
        }

        static void Menu()
        {
            Console.WriteLine("");
            Console.WriteLine("Available commands:");
            Console.WriteLine("  ?             help, this menu");
            Console.WriteLine("  cls           clear the screen");
            Console.WriteLine("  q             quit the application");
            Console.WriteLine("  list          list all peers");
            Console.WriteLine("  failed        list failed peers");
            Console.WriteLine("  add           add a peer");
            Console.WriteLine("  del           delete a peer");
            Console.WriteLine("  send          send a message to a peer asynchronously");
            Console.WriteLine("  send md       send a message to a peer with sample metadata");
            Console.WriteLine("  sendsync      send a message to a peer and await a response");
            Console.WriteLine("  sendsync md   send a message to a peer with sample metadata and await a response");
            Console.WriteLine("  bcast         send a message to all peers");
            Console.WriteLine("  health        display if the mesh is healthy");
            Console.WriteLine("  nodehealth    display if a connection to a peer is healthy");
            Console.WriteLine("");
        }
        
        static void ParseArguments(string[] args, out string ip, out int port, out Guid guid, out Dictionary<Guid, string> peers)
        {
            ip = null;
            port = -1;
            guid = default(Guid);
            peers = new Dictionary<Guid, string>();

            if (args != null && args.Length > 0)
            { 
                foreach (string curr in args)
                { 
                    if (curr.StartsWith("-ipport="))
                    {
                        string val = curr.Replace("-ipport=", "");
                        if (!String.IsNullOrEmpty(val))
                        {
                            if (val.Contains(":"))
                            {
                                ParseIpPort(val, out ip, out port);
                            }
                        }
                    }
                    else if (curr.StartsWith("-guid="))
                    {
                        string val = curr.Replace("-guid=", "");
                        if (!String.IsNullOrEmpty(val))
                        {
                            guid = Guid.Parse(val);
                        }
                    }
                    else if (curr.StartsWith("-peer="))
                    {
                        string val = curr.Replace("-peer=", "");
                        if (!String.IsNullOrEmpty(val))
                        {
                            string[] parts = val.Split('/');
                            if (parts == null) throw new ArgumentException("The supplied peer must be of the form guid/ipport");
                            if (parts.Length != 2) throw new ArgumentException("The supplied peer must be of the form guid/ipport");
                            peers.Add(Guid.Parse(parts[0]), parts[1]);
                        }
                    }
                }
            }
        }

        static void Send()
        {
            string userInput = Inputty.GetString("Data:", "some data", false);
            if (_Mesh.Send(Inputty.GetGuid("GUID:", default(Guid)), userInput).Result)
            {
                Console.WriteLine("Success"); 
            }
            else
            {
                Console.WriteLine("Failed");
            }
        }

        static void SendMetadata()
        {
            Dictionary<string, object> md = new Dictionary<string, object>();
            md.Add("Key1", "Val1");
            md.Add("Key2", "Val2");

            string userInput = Inputty.GetString("Data:", "some data", false);
            if (_Mesh.Send(Inputty.GetGuid("GUID:", default(Guid)), userInput, md).Result)
            {
                Console.WriteLine("Success");
            }
            else
            {
                Console.WriteLine("Failed");
            }
        }

        static void SendSync()
        {
            string userInput = Inputty.GetString("Data       :", "some data", false);
            SyncResponse resp = _Mesh.SendAndWait(
                Inputty.GetGuid                 ("GUID       :", default(Guid)),
                Inputty.GetInteger              ("Timeout ms :", 15000, true, false),
                userInput).Result;

            if (resp != null)
            {
                Console.WriteLine("Status: " + resp.Status.ToString());
                if (resp.Data != null)
                {
                    Console.WriteLine("Response: " + Encoding.UTF8.GetString(resp.Data));
                }
                else
                {
                    Console.WriteLine("(null)");
                }
            }
            else
            {
                Console.WriteLine("Failed");
            }
        }

        static void SendSyncMetadata()
        {
            Dictionary<string, object> md = new Dictionary<string, object>();
            md.Add("Key1", "Val1");
            md.Add("Key2", "Val2");

            string userInput = Inputty.GetString("Data       :", "some data", false);
            SyncResponse resp = _Mesh.SendAndWait(
                Inputty.GetGuid                 ("GUID       :", default(Guid)),
                Inputty.GetInteger              ("Timeout ms :", 15000, true, false),
                userInput, md).Result;

            if (resp != null)
            {
                Console.WriteLine("Status: " + resp.Status.ToString());
                if (resp.Data != null)
                {
                    Console.WriteLine("Response: " + Encoding.UTF8.GetString(resp.Data));
                }
                else
                {
                    Console.WriteLine("(null)");
                }
            }
            else
            {
                Console.WriteLine("Failed");
            }
        }

        static void Broadcast()
        {
            string userInput = Inputty.GetString("Data:", "some data", false);
            if (_Mesh.Broadcast(userInput).Result)
            {
                Console.WriteLine("Success");
            }
            else
            {
                Console.WriteLine("Failed");
            } 
        }

        static void PeerConnected(object sender, ServerConnectionEventArgs args)
        {
            Console.WriteLine("Peer " + args.PeerNode.Guid + " " + args.PeerNode.IpPort + " connected"); 
        }
         
        static void PeerDisconnected(object sender, ServerConnectionEventArgs args) 
        {
            Console.WriteLine("Peer " + args.PeerNode.Guid + " " + args.PeerNode.IpPort + " disconnected"); 
        }
         
        static void MessageReceived(object sender, MeshMessageReceivedEventArgs args)
        {
            string msg = "";
            if (args.IsBroadcast) msg = "[bcast] ";

            Console.WriteLine(msg + args.SourceGuid + " " + args.SourceIpPort + ": " + Encoding.UTF8.GetString(args.Data));

            if (args.Metadata != null && args.Metadata.Count > 0)
            {
                Console.WriteLine("Metadata:");
                foreach (KeyValuePair<string, object> curr in args.Metadata)
                {
                    Console.WriteLine("  " + curr.Key.ToString() + ": " + curr.Value.ToString());
                }
            }
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        static async Task<SyncResponse> SyncMessageReceived(MeshMessageReceivedEventArgs args)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            Console.WriteLine("[sync] " + args.SourceGuid + " " + args.SourceIpPort + ": " + Encoding.UTF8.GetString(args.Data));
            if (args.Metadata != null && args.Metadata.Count > 0)
            {
                Console.WriteLine("Metadata:");
                foreach (KeyValuePair<string, object> curr in args.Metadata)
                {
                    Console.WriteLine("  " + curr.Key.ToString() + ": " + curr.Value.ToString());
                }
            }

            Console.WriteLine("Sending synchronous response...");
            string resp = "Thank you for your synchronous inquiry!";
            byte[] respData = Encoding.UTF8.GetBytes(resp);
            return new SyncResponse(SyncResponseStatusEnum.Success, respData);
        } 

        static void ParseIpPort(string ipPort, out string ip, out int port)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));

            ip = null;
            port = -1;

            int colonIndex = ipPort.LastIndexOf(':');
            if (colonIndex != -1)
            {
                ip = ipPort.Substring(0, colonIndex);
                port = Convert.ToInt32(ipPort.Substring(colonIndex + 1));
            }
        }

        static void Logger(string msg)
        {
            Console.WriteLine(msg);
        }
    }
}
