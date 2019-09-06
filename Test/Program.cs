using System;
using System.Collections.Generic;
using System.IO;
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
            _Ip = InputString("Listener IP:", "127.0.0.1", false);
            _Port = InputInteger("Listener Port:", 8000, true, false);

            _Settings = new MeshSettings(); 
            _Settings.AcceptInvalidCertificates = true;
            _Settings.AutomaticReconnect = true; 
            _Settings.MutuallyAuthenticate = false;
            _Settings.PresharedKey = null; 
            _Settings.ReadStreamBufferSize = 65536;
            _Settings.ReconnectIntervalMs = 1000;
            // _Settings.Debug = true;

            _Self = new Peer(_Ip, _Port);

            _Mesh = new WatsonMesh(_Settings, _Self);
            _Mesh.PeerConnected = PeerConnected;
            _Mesh.PeerDisconnected = PeerDisconnected;
            _Mesh.MessageReceived = MessageReceived;
            _Mesh.SyncMessageReceived = SyncMessageReceived; 

            _Mesh.Start();

            while (_RunForever)
            {
                string userInput = InputString("WatsonMesh [? for help] >", null, false);

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

                    case "send":
                        Send().Wait();
                        break;

                    case "sendsync":
                        SendSync().Wait();
                        break;
                         
                    case "bcast":
                        Broadcast().Wait();
                        break;

                    case "add":
                        _Mesh.Add(
                            new Peer(
                                InputString("Peer IP:", "127.0.0.1", false),
                                InputInteger("Peer port:", 8000, true, false),
                                false));
                        break;

                    case "del":
                        _Mesh.Remove(
                            new Peer(
                                InputString("Peer IP:", "127.0.0.1", false),
                                InputInteger("Peer port:", 8000, true, false),
                                false));
                        break;

                    case "health":
                        Console.WriteLine("Healthy: " + _Mesh.IsHealthy());
                        break;

                    case "nodehealth":
                        Console.WriteLine(
                            _Mesh.IsHealthy(
                                InputString("Peer IP:", "127.0.0.1", false),
                                InputInteger("Peer port:", 8000, true, false)));
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
            Console.WriteLine("  send        send a message to a peer asynchronously");
            Console.WriteLine("  sendsync    send a message to a peer and await a response"); 
            Console.WriteLine("  bcast       send a message to all peers");
            Console.WriteLine("  health      display if the mesh is healthy");
            Console.WriteLine("  nodehealth  display if a connection to a peer is healthy");
        }
        
        static async Task Send()
        { 
            byte[] inputBytes = Encoding.UTF8.GetBytes(InputString("Data:", "some data", false));
            MemoryStream inputStream = new MemoryStream(inputBytes);
            inputStream.Seek(0, SeekOrigin.Begin);
             
            if (await _Mesh.Send(
                InputString("Peer IP", "127.0.0.1", false),
                InputInteger("Peer port:", 8000, true, false), 
                inputBytes.Length,
                inputStream))
            {
                Console.WriteLine("Success"); 
            }
            else
            {
                Console.WriteLine("Failed");
            }
        }

        static async Task SendSync()
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(InputString("Data:", "some data", false));
            MemoryStream inputStream = new MemoryStream(inputBytes);
            inputStream.Seek(0, SeekOrigin.Begin);
             
            SyncResponse resp = await _Mesh.SendSync(
                InputString("Peer IP", "127.0.0.1", false),
                InputInteger("Peer port:", 8000, true, false),
                InputInteger("Timeout ms:", 15000, true, false),
                inputBytes.Length,
                inputStream);

            if (resp != null)
            {
                Console.WriteLine("Status: " + resp.Status.ToString());
                if (resp.ContentLength > 0)
                {
                    if (resp.Data != null)
                    {
                        if (resp.Data.CanRead)
                        {
                            Console.WriteLine("Response: " + Encoding.UTF8.GetString(ReadStream(resp.ContentLength, resp.Data)));
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

        static async Task Broadcast()
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(InputString("Data:", "some data", false));
            MemoryStream inputStream = new MemoryStream(inputBytes);
            inputStream.Seek(0, SeekOrigin.Begin);

            if (await _Mesh.Broadcast(inputBytes.Length, inputStream))
            {
                Console.WriteLine("Success");
            }
            else
            {
                Console.WriteLine("Failed");
            } 
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        static async Task PeerConnected(Peer peer)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            Console.WriteLine("Peer " + peer.ToString() + " connected"); 
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        static async Task PeerDisconnected(Peer peer)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            Console.WriteLine("Peer " + peer.ToString() + " disconnected"); 
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        static async Task MessageReceived(Peer peer, long contentLength, Stream stream)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            Console.WriteLine(peer.ToString() + " " + contentLength + " bytes: " + Encoding.UTF8.GetString(ReadStream(contentLength, stream)));
        }
         
        static SyncResponse SyncMessageReceived(Peer peer, long contentLength, Stream stream)
        {
            Console.WriteLine("");
            Console.WriteLine("*** Synchronous Request ***");
            Console.WriteLine(peer.ToString() + " " + contentLength + " bytes: " + Encoding.UTF8.GetString(ReadStream(contentLength, stream))); 
            Console.WriteLine("");
            Console.WriteLine("Press ENTER and THEN type your response!"); 
            string resp = InputString("Response:", "This is a response", false);
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
    }
}
