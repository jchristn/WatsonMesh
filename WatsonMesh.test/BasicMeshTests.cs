using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace WatsonMesh.test
{
    [TestFixture]
    public class BasicMeshTests
    {
        private const int MeshPort = 37000;
        private MeshNode _testNode;
        private ManualResetEvent _connectEvent;
        private ManualResetEvent _disConnectEvent;

        [OneTimeSetUp] 
        public void CreateTestEnvironment()
        {
            _connectEvent = new ManualResetEvent(false);
            _disConnectEvent = new ManualResetEvent(false);

            _testNode = new MeshNode("127.0.0.1", MeshPort);
            _testNode.Add(new MeshPeer("127.0.0.1:" + (MeshPort + 1)));
            _testNode.MessageReceived += IntMeshMessagerecieved;
            _testNode.PeerConnected += IntMeshPeerConnected;
            _testNode.PeerDisconnected += IntMeshPeerDisconnected;
            _testNode.Start();
        }

        private void IntMeshPeerDisconnected(object sender, ServerConnectionEventArgs e)
        {
            _disConnectEvent?.Set();
        }

        private void IntMeshPeerConnected(object sender, ServerConnectionEventArgs e)
        {
            _connectEvent?.Set();
        }

        private void IntMeshMessagerecieved(object sender, MessageReceivedEventArgs e)
        {
            // just echo back, what we sent
            _testNode.SendAsync(e.SourceIpPort, e.Data);
        }

        [OneTimeTearDown]
        public void BreakDownTestEnvironment()
        {
            _disConnectEvent.Dispose();
            _connectEvent.Dispose();
        }

        [Test, Sequential]
        public void SendOneMessage()
        {
            _connectEvent.Reset();
            _disConnectEvent.Reset();

            var otherNode = new MeshNode("127.0.0.1", MeshPort + 1);
            otherNode.Add(new MeshPeer("127.0.0.1:" + MeshPort));
            otherNode.Start();

            _connectEvent.WaitOne(2000);

            var dataToSend = Encoding.UTF8.GetBytes("Hello World!");
            var result = string.Empty;

            var waitForMe = new ManualResetEvent(false);

            otherNode.MessageReceived += (sender, args) => {
                result = Encoding.UTF8.GetString(args.Data);
                waitForMe.Set();
            };

            otherNode.SendAsync("127.0.0.1:" + MeshPort, dataToSend);

            waitForMe.WaitOne(5000);

            Assert.That(result, Is.EqualTo("Hello World!"), "What we received should be what we sent");
        }


        [Test, Sequential]
        public void TestSending100Messages()
        {
            _connectEvent.Reset();
            _disConnectEvent.Reset();

            var otherNode = new MeshNode("127.0.0.1", MeshPort+1);
            otherNode.Add(new MeshPeer("127.0.0.1:" + MeshPort));
            otherNode.Start();

            _connectEvent.WaitOne(2000);

            var dataToSend = Encoding.UTF8.GetBytes("Hello World!");
            var result = new List<string>();

            var waitForMe = new ManualResetEvent(false);

            otherNode.MessageReceived += (sender,args) => {
                var tmp = Encoding.UTF8.GetString(args.Data);
                lock (result)
                    result.Add(tmp);
                
                if (result.Count == 100)
                    waitForMe.Set();
            };

            for (int i = 0; i < 100; i++)
            {
                otherNode.SendAsync("127.0.0.1:" + MeshPort, dataToSend);
            }

            waitForMe.WaitOne(5000);

            Assert.That(result.Count, Is.EqualTo(100), "We have sent 100 messages, this is what we expect o receive");
            for(int i = 0; i < 100; i++)
                Assert.That(result[i], Is.EqualTo("Hello World!"), "What we received should be what we sent");
        }


    }
}
