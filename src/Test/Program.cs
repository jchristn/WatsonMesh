using System;
using System.Collections.Generic;
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

        static bool _RunForever = true;

        static void Main(string[] args)
        {
            if (args != null && args.Length > 0)
            { 
                ParseArguments(args, out _Ip, out _Port, out _Guid);
                _IpPort = _Ip + ":" + _Port;
            }
            else
            {
                _IpPort = Inputty.GetString("Local IP:port:", "127.0.0.1:8000", false);
                ParseIpPort(_IpPort, out _Ip, out _Port);
            }

            _Settings = new MeshSettings(); 
            _Settings.AcceptInvalidCertificates = true;
            _Settings.AutomaticReconnect = true; 
            _Settings.MutuallyAuthenticate = false;
            _Settings.PresharedKey = null; 
            _Settings.StreamBufferSize = 65536;
            _Settings.ReconnectIntervalMs = 1000; 

            _Mesh = new MeshNode(new MeshSettings(), _Ip, _Port);
            _Mesh.PeerConnected += PeerConnected;
            _Mesh.PeerDisconnected += PeerDisconnected;
            _Mesh.MessageReceived += MessageReceived;
            _Mesh.SyncMessageReceived = SyncMessageReceived;
            _Mesh.Logger = Logger;

            _Mesh.Start();

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
                        if (peers != null && peers.Count > 0)
                        {
                            Console.WriteLine("Configured peers: " + peers.Count);
                            foreach (MeshPeer curr in peers) Console.WriteLine("  " + curr.ToString());
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
                            foreach (MeshPeer currPeer in peers) Console.WriteLine("  " + currPeer.ToString());
                        }
                        else
                        {
                            Console.WriteLine("None");
                        }
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
                        Console.WriteLine(Inputty.GetGuid("GUID:", default(Guid)));
                        break;
                }
            }
        }

        static void Menu()
        {
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
        }
        
        static void ParseArguments(string[] args, out string ip, out int port, out Guid guid)
        {
            ip = null;
            port = -1;
            guid = default(Guid);

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
                }
            }
        }

        static void Send()
        {
            string userInput = Inputty.GetString("Data:", "some data", false);
            if (_Mesh.Send(
                Inputty.GetGuid("GUID:", default(Guid)),
                userInput).Result)
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
            Dictionary<object, object> md = new Dictionary<object, object>();
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
            string userInput = Inputty.GetString("Data:", "some data", false);
            SyncResponse resp = _Mesh.SendAndWait(
                Inputty.GetGuid   ("GUID      :", default(Guid)),
                Inputty.GetInteger("Timeout ms:", 15000, true, false),
                userInput).Result;

            if (resp != null)
            {
                Console.WriteLine("Status: " + resp.Status.ToString());
                if (resp.ContentLength > 0)
                {
                    if (resp.DataStream != null)
                    {
                        if (resp.DataStream.CanRead)
                        {
                            Console.WriteLine("Response: " + Encoding.UTF8.GetString(ReadStream(resp.ContentLength, resp.DataStream)));
                        }
                        else
                        {
                            Console.WriteLine("Cannot read from response stream");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Response stream is null");
                    }
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
            Dictionary<object, object> md = new Dictionary<object, object>();
            md.Add("Key1", "Val1");
            md.Add("Key2", "Val2");

            string userInput = Inputty.GetString("Data:", "some data", false);
            SyncResponse resp = _Mesh.SendAndWait(
                Inputty.GetGuid   ("GUID      :", default(Guid)),
                Inputty.GetInteger("Timeout ms:", 15000, true, false),
                userInput, md).Result;

            if (resp != null)
            {
                Console.WriteLine("Status: " + resp.Status.ToString());
                if (resp.ContentLength > 0)
                {
                    if (resp.DataStream != null)
                    {
                        if (resp.DataStream.CanRead)
                        {
                            Console.WriteLine("Response: " + Encoding.UTF8.GetString(ReadStream(resp.ContentLength, resp.DataStream)));
                        }
                        else
                        {
                            Console.WriteLine("Cannot read from response stream");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Response stream is null");
                    }
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
         
        static void MessageReceived(object sender, MessageReceivedEventArgs args) 
        {
            if (args.IsBroadcast) Console.Write("[bcast] ");
            Console.WriteLine("[async] " + args.SourceIpPort + " " + args.ContentLength + " bytes: " + Encoding.UTF8.GetString(args.Data));
            if (args.Metadata != null && args.Metadata.Count > 0)
            {
                Console.WriteLine("Metadata:");
                foreach (KeyValuePair<object, object> curr in args.Metadata)
                {
                    Console.WriteLine("  " + curr.Key.ToString() + ": " + curr.Value.ToString());
                }
            }
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        static async Task<SyncResponse> SyncMessageReceived(MessageReceivedEventArgs args)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            Console.WriteLine("[sync] " + args.SourceIpPort + " " + args.ContentLength + " bytes: " + Encoding.UTF8.GetString(args.Data));
            if (args.Metadata != null && args.Metadata.Count > 0)
            {
                Console.WriteLine("Metadata:");
                foreach (KeyValuePair<object, object> curr in args.Metadata)
                {
                    Console.WriteLine("  " + curr.Key.ToString() + ": " + curr.Value.ToString());
                }
            }
            Console.WriteLine("");
            Console.WriteLine("Sending synchronous response...");
            string resp = "Thank you for your synchronous inquiry!";
            byte[] respData = Encoding.UTF8.GetBytes(resp);
            MemoryStream ms = new MemoryStream(respData);
            ms.Seek(0, SeekOrigin.Begin);
            return new SyncResponse(SyncResponseStatusEnum.Success, respData.Length, ms);
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

        static byte[] ReadStream(long contentLength, Stream stream)
        {
            if (contentLength < 1) throw new ArgumentException("Content length must be greater than zero.");
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new ArgumentException("Cannot read from supplied stream.");

            int bytesRead = 0;
            long bytesRemaining = contentLength;
            byte[] buffer = new byte[65536];
            byte[] ret = null;

            while (bytesRemaining > 0)
            {
                bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    if (bytesRead == buffer.Length)
                    {
                        ret = AppendBytes(ret, buffer);
                    }
                    else
                    {
                        byte[] temp = new byte[bytesRead];
                        Buffer.BlockCopy(buffer, 0, temp, 0, bytesRead);
                        ret = AppendBytes(ret, temp);
                    }
                    bytesRemaining -= bytesRead;
                }
            }

            return ret;
        }

        static byte[] ReadStream(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new ArgumentException("Cannot read from supplied stream.");

            int bytesRead = 0;
            byte[] buffer = new byte[65536];
            byte[] ret = null;

            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (bytesRead == buffer.Length)
                {
                    ret = AppendBytes(ret, buffer);
                }
                else
                {
                    byte[] temp = new byte[bytesRead];
                    Buffer.BlockCopy(buffer, 0, temp, 0, bytesRead);
                    ret = AppendBytes(ret, buffer);
                }
            }

            return ret;
        }

        static byte[] AppendBytes(byte[] head, byte[] tail)
        {
            byte[] ret;

            if (head == null || head.Length == 0)
            {
                if (tail == null || tail.Length == 0) return null;

                ret = new byte[tail.Length];
                Buffer.BlockCopy(tail, 0, ret, 0, tail.Length);
                return ret;
            }
            else
            {
                if (tail == null || tail.Length == 0) return head;

                ret = new byte[head.Length + tail.Length];
                Buffer.BlockCopy(head, 0, ret, 0, head.Length);
                Buffer.BlockCopy(tail, 0, ret, head.Length, tail.Length);
                return ret;
            }
        } 

        static void Logger(string msg)
        {
            Console.WriteLine(msg);
        }
    }
}
