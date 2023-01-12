using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WatsonMesh;

namespace TestNetCore
{
    class Program
    {
        static string _Ip;
        static int _Port = 0;
        static string _IpPort;
        static List<string> _PeerIpPorts;
        static MeshSettings _Settings; 
        static MeshNode _Mesh;

        static bool _RunForever = true;

        static void Main(string[] args)
        {
            if (args != null && args.Length > 0)
            { 
                ParseArguments(args, out _Ip, out _Port, out _PeerIpPorts);
                _IpPort = _Ip + ":" + _Port;
            }
            else
            {
                _IpPort = InputString("Local IP:port:", "127.0.0.1:8000", false);
                ParseIpPort(_IpPort, out _Ip, out _Port);
            }

            _Settings = new MeshSettings(); 
            _Settings.AcceptInvalidCertificates = true;
            _Settings.AutomaticReconnect = true; 
            _Settings.MutuallyAuthenticate = false;
            _Settings.PresharedKey = null; 
            _Settings.StreamBufferSize = 65536;
            _Settings.ReconnectIntervalMs = 1000; 

            _Mesh = new MeshNode(_Ip, _Port);
            _Mesh.PeerConnected += PeerConnected;
            _Mesh.PeerDisconnected += PeerDisconnected;
            _Mesh.MessageReceived += MessageReceived;
            _Mesh.SyncMessageReceived = SyncMessageReceived;
            // _Mesh.Logger = Logger;

            _Mesh.Start();

            if (_PeerIpPorts != null && _PeerIpPorts.Count > 0)
            {
                Console.Write("Adding peers: ");
                foreach (string curr in _PeerIpPorts)
                {
                    Console.Write(curr + " ");
                    _Mesh.Add(new MeshPeer(curr, false));
                }
                Console.WriteLine("");
            }

            while (_RunForever)
            {
                string userInput = InputString("WatsonMesh [? for help] >", null, false);

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
                        peers = _Mesh.GetPeers();
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
                                InputString("IP:port:", "127.0.0.1:8000", false),
                                false));
                        break;

                    case "del":
                        _Mesh.Remove(
                            new MeshPeer(
                                InputString("IP:port:", "127.0.0.1:8000", false), 
                                false));
                        break;

                    case "health":
                        Console.WriteLine("Healthy: " + _Mesh.IsHealthy);
                        break;

                    case "nodehealth":
                        Console.WriteLine(
                            _Mesh.IsServerConnected(
                                InputString("IP:Port", "127.0.0.1:8000", false)));
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
        
        static void ParseArguments(string[] args, out string ip, out int port, out List<string> peerIpPorts)
        {
            ip = null;
            port = -1;
            peerIpPorts = new List<string>();

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

                    if (curr.StartsWith("-peers="))
                    {
                        string val = curr.Replace("-peers=", "");
                        string[] peers = val.Split(',');
                        foreach (string peer in peers) peerIpPorts.Add(peer);
                    }
                }
            }
        }

        static void Send()
        {
            string userInput = InputString("Data:", "some data", false);
            if (_Mesh.Send(
                InputString("IP:Port", "127.0.0.1:8000", false),
                userInput))
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

            string userInput = InputString("Data:", "some data", false);
            if (_Mesh.Send(
                InputString("IP:Port", "127.0.0.1:8000", false),
                md,
                userInput))
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
            string userInput = InputString("Data:", "some data", false);
            SyncResponse resp = _Mesh.SendAndWait(
                InputString("IP:Port", "127.0.0.1:8000", false),
                InputInteger("Timeout ms:", 15000, true, false),
                userInput);

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

            string userInput = InputString("Data:", "some data", false);
            SyncResponse resp = _Mesh.SendAndWait(
                InputString("IP:Port", "127.0.0.1:8000", false),
                InputInteger("Timeout ms:", 15000, true, false),
                md,
                userInput);

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
            string userInput = InputString("Data:", "some data", false);
            if (_Mesh.Broadcast(userInput))
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
            Console.WriteLine("Peer " + args.PeerNode.IpPort + " connected"); 
        }
         
        static void PeerDisconnected(object sender, ServerConnectionEventArgs args) 
        {
            Console.WriteLine("Peer " + args.PeerNode.IpPort + " disconnected"); 
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
         
        static SyncResponse SyncMessageReceived(MessageReceivedEventArgs args)
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
            return new SyncResponse(SyncResponseStatus.Success, respData.Length, ms);
        } 

        static bool InputBoolean(string question, bool yesDefault)
        {
            Console.Write(question);

            if (yesDefault) Console.Write(" [Y/n]? ");
            else Console.Write(" [y/N]? ");

            string userInput = Console.ReadLine();

            if (String.IsNullOrEmpty(userInput))
            {
                if (yesDefault) return true;
                return false;
            }

            userInput = userInput.ToLower();

            if (yesDefault)
            {
                if (
                    (String.Compare(userInput, "n") == 0)
                    || (String.Compare(userInput, "no") == 0)
                   )
                {
                    return false;
                }

                return true;
            }
            else
            {
                if (
                    (String.Compare(userInput, "y") == 0)
                    || (String.Compare(userInput, "yes") == 0)
                   )
                {
                    return true;
                }

                return false;
            }
        }

        static string InputString(string question, string defaultAnswer, bool allowNull)
        {
            while (true)
            {
                Console.Write(question);

                if (!String.IsNullOrEmpty(defaultAnswer))
                {
                    Console.Write(" [" + defaultAnswer + "]");
                }

                Console.Write(" ");

                string userInput = Console.ReadLine();

                if (String.IsNullOrEmpty(userInput))
                {
                    if (!String.IsNullOrEmpty(defaultAnswer)) return defaultAnswer;
                    if (allowNull) return null;
                    else continue;
                }

                return userInput;
            }
        }

        static int InputInteger(string question, int defaultAnswer, bool positiveOnly, bool allowZero)
        {
            while (true)
            {
                Console.Write(question);
                Console.Write(" [" + defaultAnswer + "] ");

                string userInput = Console.ReadLine();

                if (String.IsNullOrEmpty(userInput))
                {
                    return defaultAnswer;
                }

                int ret = 0;
                if (!Int32.TryParse(userInput, out ret))
                {
                    Console.WriteLine("Please enter a valid integer.");
                    continue;
                }

                if (ret == 0)
                {
                    if (allowZero)
                    {
                        return 0;
                    }
                }

                if (ret < 0)
                {
                    if (positiveOnly)
                    {
                        Console.WriteLine("Please enter a value greater than zero.");
                        continue;
                    }
                }

                return ret;
            }
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
