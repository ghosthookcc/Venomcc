using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Runtime.InteropServices;
using Venomcc.Utility.Networking;
using Venomcc.UI;
using System.Data;

namespace Venomcc.Networking
{
    public enum SendMessageType
    {
        Normal = 0,
        Broadcast = 1,
    }

    public struct connSockInfo
    {
        public readonly object _connectionsLock = new object();
        public Dictionary<string, Socket> connections = new Dictionary<string, Socket>();

        public Socket? connSock = null;
        public readonly ushort port;

        public IPAddress? connSockIPAddress = null;
        public IPEndPoint? connSockEndPoint = null;
        public SocketAddress? connSockAddress;

        public ushort chunkSize = 1024;

        public string IpAsString()
        {
            if (connSockIPAddress != null) return connSockIPAddress.ToString();
            else return "NOT AN IP";
        }

        public connSockInfo(ushort port)
        {
            if (connSock == null)
            {
                connSock = new Socket(AddressFamily.InterNetwork,
                                      SocketType.Stream,
                                      ProtocolType.Tcp);
                this.port = port;
            }
        }

        public void SetEndPoint()
        {
            if (connSockIPAddress != null)
            {
                connSockEndPoint = new IPEndPoint(connSockIPAddress, this.port);
                connSockAddress = connSockEndPoint.Serialize();
                connSockEndPoint = (IPEndPoint)connSockEndPoint.Create(connSockAddress);
            }
        }
    }

    public abstract class NetUser
    {
        static public Dictionary<string, string> MessageHeaderTags = new Dictionary<string, string>()
        {
            // <SM> = starting point of a message (STARTMESSAGE)
            // <EM> = ending point of a message (ENDMESSAGE)
            // <SCD> = signifies the start of continues data as it could not be contained in one message (START CONTINUES DATA)
            // <CCD> = signifies the continuation of a continues data stream (CONTINUATION CONTINUES DATA)
            // <SC>  = signifies the start of a command to be executed (STARTCOMMAND)
            // <EC>  = signifies the end of a command to be executed (ENDCOMMAND)
            ["SM"] = "<~SM~>",
            ["EM"] = "<~EM~>",
            //["SCD"] = "<~SCD~>",
            ["CCD"] = "<~CCD~>",
            ["SC"] = "<~SC~>",
            ["EC"] = "<~EC~>",
        };

        public enum MessageHeaderTagTypes
        {
            SM, EM, SCD, CCD, SC, EC
        };

        static public Queue<(IPEndPoint endPoint, ArraySegment<byte> data)> nextOutData = new Queue<(IPEndPoint endPoint, ArraySegment<byte> data)>();

        readonly protected object _lock;

        protected connSockInfo netUserInfo;
        public NetUser(object _lock, ushort port)
        {
            netUserInfo = new connSockInfo(port);
            if (netUserInfo.connSock != null) netUserInfo.connSock.ReceiveBufferSize = netUserInfo.chunkSize;
            this._lock = _lock;
        }

        public IPEndPoint? GetEndPoint()
        {
            if (netUserInfo.connSockEndPoint != null)
            {
                return netUserInfo.connSockEndPoint;
            }
            return null;
        }

        public void GetConnections()
        {
            uint idx = 1;
            lock (netUserInfo._connectionsLock)
            {
                foreach (KeyValuePair<string, Socket> entry in netUserInfo.connections)
                {
                    ConsoleUI.enqueueConsoleData(ConsoleMessageType.NEWSECTION,
                                     "[/] Client[" + idx + "]: " + entry.Key + " . . .\n");
                    idx++;
                }
            }
        }

        public int GetConnectionsCount()
        {
            return netUserInfo.connections.Count();
        }

        public void ConsumeNextOutgoingData()
        {
            if (netUserInfo.connSock != null && netUserInfo.connSockEndPoint != null)
            {
                lock (netUserInfo._connectionsLock)
                {
                    (string? endPoint, ArraySegment<byte>? data) outgoing = ConsoleUI.ConsumeNextOutgoingData();
                    if (outgoing.endPoint != null && outgoing.data != null)
                    {
                        netUserInfo.connSock.SendTo(outgoing.data.Value, IPEndPoint.Parse(outgoing.endPoint));
                    }
                }
            }
        }

        private async Task Send(SendMessageType typeData, dynamic outData, string? endPoint = null)
        {
            await Task.Run(() =>
            {
                lock (netUserInfo._connectionsLock)
                {
                    if (netUserInfo.connections.Count() > 0 && netUserInfo.connSock != null)
                    {
                        List<byte[]> dataChunks = NetworkUtilities.generateDataChunksWithHeaderTags(outData, 1024);

                        switch (typeData)
                        {
                            case SendMessageType.Normal:
                                if (endPoint != null)
                                {
                                    foreach (byte[] chunk in dataChunks)
                                    {
                                        ConsoleUI.enqueueOutData(ConsoleMessageType.None, chunk, endPoint.ToString());
                                    }
                                }
                                break;
                            case SendMessageType.Broadcast:
                                foreach (KeyValuePair<string, Socket> entry in netUserInfo.connections)
                                {
                                    foreach (byte[] chunk in dataChunks)
                                    {
                                        ConsoleUI.enqueueOutData(ConsoleMessageType.None, chunk, entry.Key.ToString());
                                    }

                                }
                                break;
                        }
                    }
                }
            });
        }

        public async Task SendTo(SendMessageType typeData, string outData, [Optional] string endPoint)
        {
            string formattedOutData = MessageHeaderTags["SM"] + outData + MessageHeaderTags["EM"];

            await Send(typeData, formattedOutData, endPoint);
        }

        public async Task SendTo(SendMessageType typeData, ArraySegment<byte> outData, [Optional] string endPoint)
        {
            byte[]? formattedOutData = null;
            if (outData.Array != null)
            {
                formattedOutData = new byte[MessageHeaderTags["SM"].Length +
                                            outData.Array.Length +
                                            MessageHeaderTags["EM"].Length];

                Buffer.BlockCopy(Encoding.UTF8.GetBytes(MessageHeaderTags["SM"]), 0, formattedOutData, 0, MessageHeaderTags["SM"].Length);
                Buffer.BlockCopy(outData.Array, 0, formattedOutData, MessageHeaderTags["SM"].Length, outData.Array.Length);
                Buffer.BlockCopy(Encoding.UTF8.GetBytes(MessageHeaderTags["EM"]), 0, formattedOutData, MessageHeaderTags["SM"].Length + outData.Array.Length, MessageHeaderTags["EM"].Length);
            }

            ArraySegment<byte>? formattedOutDataSegment = null;
            if (formattedOutData != null)
            {
                formattedOutDataSegment = new ArraySegment<byte>(formattedOutData);
            }

            if (formattedOutDataSegment != null)
            {
                await Send(typeData, formattedOutDataSegment, endPoint);
            }
        }

        public abstract void ReceiveData();
        public abstract Task Connect();
        public abstract Task Disconnect();
    }

    public class Server : NetUser
    {
        public Server(object _lock, string ip, ushort port) : base(_lock, port)
        {
            IPAddress.TryParse(ip, out netUserInfo.connSockIPAddress);
            netUserInfo.SetEndPoint();
        }

        public Server(object _lock, ushort port) : base(_lock, port)
        {
            netUserInfo.connSockIPAddress = IPAddress.Any;
            netUserInfo.SetEndPoint();
        }

        public void AcceptIncomingConnections()
        {
            while (true)
            {
                Socket newConnection;
                IPEndPoint? newConnectionEndPoint;
                if (netUserInfo.connSock != null)
                {
                    newConnection = netUserInfo.connSock.Accept();
                    newConnectionEndPoint = newConnection.RemoteEndPoint as IPEndPoint;

                    if (newConnectionEndPoint != null)
                    {
                        lock (netUserInfo._connectionsLock)
                        {
                            netUserInfo.connections.Add(newConnectionEndPoint.ToString(), newConnection);
                            ConsoleUI.enqueueConsoleData(ConsoleMessageType.NEWSECTION,
                                                 "[+] Received new connection from: " + newConnection.RemoteEndPoint + " . . .");
                            GetConnections();
                        }
                    }
                }
            }
        }

        public override void ReceiveData()
        {
            while (true)
            {
                if (netUserInfo.connSock != null)
                {
                    ArraySegment<byte> incomingData = new ArraySegment<byte>(new byte[netUserInfo.chunkSize]);
                    int receivedAmount = 0;

                    string? currentConnectionSelected = null;
                    List<string> savedConnectionData = new List<string>();
                    string finalData = "";

                    string dataAsString;

                    byte[] resizedData;
                    lock (netUserInfo._connectionsLock)
                    {
                        foreach (KeyValuePair<string, Socket> connectionKVP in netUserInfo.connections)
                        {
                            while (connectionKVP.Value.Available > 0)
                            {
                                currentConnectionSelected = connectionKVP.Key;

                                receivedAmount = connectionKVP.Value.Receive(incomingData);
                                resizedData = incomingData.Take(receivedAmount).ToArray();
                                dataAsString = Encoding.UTF8.GetString(resizedData);
                                savedConnectionData.Add(dataAsString);
                                if (incomingData.Array != null) Array.Clear(incomingData.Array, 0, incomingData.Array.Length);
                            }
                            if (currentConnectionSelected != null && savedConnectionData.Count() > 0)
                            {
                                for (int dataIdx = 0; dataIdx < savedConnectionData.Count(); dataIdx++)
                                {
                                    finalData += savedConnectionData[dataIdx];
                                }
                                finalData = NetworkUtilities.parseMessageIgnoringHeaderTags(finalData);
                                ConsoleUI.enqueueConsoleData(ConsoleMessageType.None, finalData);

                                finalData = "";
                                savedConnectionData.Clear();
                            }
                        }
                    }
                }
            }
        }

        public async override Task Connect()
        {
            await Bind();
            await Listen();
        }

        public async override Task Disconnect()
        {
            await Task.Run(() =>
            {
                if (netUserInfo.connSock != null)
                {
                    netUserInfo.connSock.Shutdown(SocketShutdown.Both);
                    netUserInfo.connSock.Close();
                    ConsoleUI.enqueueConsoleData(ConsoleMessageType.NEWSECTION,
                                     "[!] Socket powering down. . .");
                }
            });
        }

        protected async Task Bind()
        {
            await Task.Run(() =>
            {
                if (netUserInfo.connSock != null && netUserInfo.connSockEndPoint != null)
                {
                    netUserInfo.connSock.Bind(netUserInfo.connSockEndPoint);
                    ConsoleUI.enqueueConsoleData(ConsoleMessageType.NEWLINE,
                                     "[+] Bound to " + netUserInfo.connSockEndPoint.ToString() + " . . .");
                }
            });
        }

        protected async Task Listen()
        {
            await Task.Run(() =>
            {
                if (netUserInfo.connSock != null)
                {
                    netUserInfo.connSock.Listen(10);
                    ConsoleUI.enqueueConsoleData(ConsoleMessageType.NEWSECTION,
                                     "[+] Listening for incoming connection(s) . . .");
                }
            });
        }
    }

    public class Client : NetUser
    {
        public Client(object _lock, string ip, ushort port) : base(_lock, port)
        {
            IPAddress.TryParse(ip, out netUserInfo.connSockIPAddress);
            netUserInfo.SetEndPoint();
        }

        public override void ReceiveData()
        {

        }

        public async override Task Connect()
        {
            await Task.Run(() =>
            {
                if (netUserInfo.connSock != null && netUserInfo.connSockEndPoint != null)
                {
                    netUserInfo.connSock.ConnectAsync(netUserInfo.connSockEndPoint);
                    lock (netUserInfo._connectionsLock)
                    {
                        netUserInfo.connections.Add(netUserInfo.connSockEndPoint.ToString(), netUserInfo.connSock);
                    }
                }
            });
        }

        public async override Task Disconnect()
        {
            await Task.Run(() =>
            {
                if (netUserInfo.connSock != null)
                {
                    netUserInfo.connSock.Shutdown(SocketShutdown.Both);
                    netUserInfo.connSock.Close();
                    ConsoleUI.enqueueConsoleData(ConsoleMessageType.NEWSECTION,
                                     "[!] Socket powering down . . .");
                }
            });
        }
    }
}
