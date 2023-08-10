using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Runtime.InteropServices;
using Venomcc.Utility.Networking;

namespace Venomcc.Networking
{
    public enum SendMessageType
    {
        Normal = 0,
        Broadcast = 1,
    }

    public enum ConsoleMessageType
    {
        None = 0,
        NEWLINE = 1,
        NEWSECTION = 2,
    }

    public struct connSockInfo
    {
        public Dictionary<IPEndPoint, (Socket connSock, Queue<string> dataQueue)> connections = new Dictionary<IPEndPoint, (Socket connSock, Queue<string> dataQueue)>();

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
            ["SC"]  = "<~SC~>",
            ["EC"]  = "<~EC~>",
        };

        public enum MessageHeaderTagTypes
        {
            SM, EM, SCD, CCD, SC, EC
        };

        static public Queue<(ConsoleMessageType type, string data)> nextInData = new Queue<(ConsoleMessageType type, string data)>();
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

        public void OutputConnectionsDataQueue(IPEndPoint endPoint)
        {
            Queue<string> dataQueue = netUserInfo.connections[endPoint].dataQueue;
            while (dataQueue.Count > 0)
            {
                string currentData = dataQueue.Dequeue(); 
                enqueueInData(ConsoleMessageType.NEWLINE, currentData);
            }
        }

        public void OutputConnectionsDataQueue(int amount)
        {
            if (amount > netUserInfo.connections.Count()) amount = netUserInfo.connections.Count();

            KeyValuePair<IPEndPoint, (Socket connSock, Queue<string> dataQueue)> dataKVP;
            for (int idx = 0; idx < amount; idx++)
            {
                dataKVP = netUserInfo.connections.ToList()[idx];
                if (dataKVP.Value.dataQueue.Count() > 0)
                {
                    for (int dataIdx = 0; dataIdx < dataKVP.Value.dataQueue.Count(); dataIdx++)
                    {
                        enqueueInData(ConsoleMessageType.NEWLINE, dataKVP.Value.dataQueue.Dequeue().Trim());
                    }
                }
            }
        }

        public static void enqueueInData(ConsoleMessageType typeData, string inData)
        {
            nextInData.Enqueue((typeData, inData));
        }

        public static void enqueueOutData(IPEndPoint endPoint, ArraySegment<byte> outData)
        {
            nextOutData.Enqueue((endPoint, outData));
        }

        public void GetConnections()
        {
            lock(_lock)
            {
                uint idx = 1;
                foreach (KeyValuePair<IPEndPoint, (Socket, Queue<string>)> entry in netUserInfo.connections.ToList())
                {
                    enqueueInData(ConsoleMessageType.NEWSECTION,
                                  "[/] Client[" + idx + "]: " + entry.Key + " . . .");
                }
            }
        }

        public int GetConnectionsCount()
        {
            return netUserInfo.connections.Count();
        }

        public static void ConsumeNextInData()
        {
            if (nextInData.Count() > 0)
            {
                (ConsoleMessageType type, string data) incoming = nextInData.Dequeue();

                switch (incoming.type)
                {
                    case ConsoleMessageType.NEWLINE:
                        incoming.data = incoming.data + "\n";
                        break;
                    case ConsoleMessageType.NEWSECTION:
                       incoming.data = "\n" + incoming.data + "\n";
                       break;
                }

                Console.Write(incoming.data);
            }
        }

        public void ConsumeNextOutData()
        {
            if (netUserInfo.connSock != null && netUserInfo.connSockEndPoint != null)
            {
                (IPEndPoint endPoint, ArraySegment<byte> data) outgoing = nextOutData.Dequeue();
                netUserInfo.connSock.SendToAsync(outgoing.data, outgoing.endPoint);
            }
        }

        private async Task Send(SendMessageType typeData, dynamic outData, [Optional] IPEndPoint endPoint)
        {
            await Task.Run(() =>
            {
                if (netUserInfo.connections.Count() > 0 && netUserInfo.connSock != null)
                {
                    List<byte[]> dataChunks = NetworkUtilities.generateDataChunksWithHeaderTags(outData, 1024);

                    switch (typeData)
                    {
                        case SendMessageType.Normal:
                            if (endPoint != null)
                            {
                                lock (_lock)
                                {
                                    foreach (byte[] chunk in dataChunks)
                                    {
                                        enqueueOutData(endPoint, chunk);
                                        ConsumeNextOutData();
                                    }
                                }
                            }
                            break;
                        case SendMessageType.Broadcast:
                            lock (_lock)
                            {
                                foreach (KeyValuePair<IPEndPoint, (Socket, Queue<string>)> entry in netUserInfo.connections.ToList())
                                {
                                    foreach (byte[] chunk in dataChunks)
                                    {
                                        enqueueOutData(entry.Key, chunk);
                                        ConsumeNextOutData();
                                    }
                                }
                            }
                            break;
                    }
                }
            });
        }

        public async Task SendTo(SendMessageType typeData, string outData, [Optional] IPEndPoint endPoint)
        {
            string formattedOutData = MessageHeaderTags["SM"] + outData + MessageHeaderTags["EM"];

            await Send(typeData, formattedOutData, endPoint);
        }

        public async Task SendTo(SendMessageType typeData, ArraySegment<byte> outData, [Optional] IPEndPoint endPoint)
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
                Socket newConnection;
                IPEndPoint? newConnectionEndPoint;
                if (netUserInfo.connSock != null)
                {
                    newConnection = netUserInfo.connSock.Accept();
                    newConnectionEndPoint = newConnection.RemoteEndPoint as IPEndPoint;

                    if (newConnectionEndPoint != null)
                    {
                        lock (_lock)
                        {
                            netUserInfo.connections.Add(newConnectionEndPoint, (newConnection, new Queue<string>()));
                            enqueueInData(ConsoleMessageType.NEWSECTION,
                                          "[+] Received new connection from: " + newConnection.RemoteEndPoint + " . . .");
                            GetConnections();
                        }
                    }
                }
        }

        public override void ReceiveData()
        {
                if (netUserInfo.connSock != null)
                {
                    ArraySegment<byte> incomingData = new ArraySegment<byte>(new byte[netUserInfo.chunkSize]);
                    int receivedAmount = 0;

                    IPEndPoint? currentConnectionSelected = null;
                    List<string> savedConnectionData = new List<string>();
                    string finalData = "";

                    string dataAsString;
                    List<string> parsedMessageData;

                    byte[] resizedData;
                    lock (_lock)
                    {
                        foreach (KeyValuePair<IPEndPoint, (Socket connSock, Queue<string> dataQueue)> connectionKVP in netUserInfo.connections)
                        {
                            while (connectionKVP.Value.connSock.Available > 0)
                            {
                                currentConnectionSelected = connectionKVP.Key;

                                receivedAmount = connectionKVP.Value.connSock.Receive(incomingData);
                                resizedData = incomingData.Take(receivedAmount).ToArray();
                                dataAsString = Encoding.UTF8.GetString(resizedData);
                                savedConnectionData.Add(dataAsString);
                                if (incomingData.Array != null) Array.Clear(incomingData.Array, 0, incomingData.Array.Length);
                            }
                            if (currentConnectionSelected != null && savedConnectionData.Count() > 0)
                            {
                                enqueueInData(ConsoleMessageType.NEWSECTION, "");
                                for (int dataIdx = 0; dataIdx < savedConnectionData.Count(); dataIdx++)
                                {
                                    finalData += savedConnectionData[dataIdx];
                                }
                                parsedMessageData = NetworkUtilities.parseMessagesIgnoringHeaderTags(finalData);

                                for (int idx = 0; idx < parsedMessageData.Count(); idx++)
                                {
                                    connectionKVP.Value.dataQueue.Enqueue(parsedMessageData[idx]);
                                }

                                OutputConnectionsDataQueue(currentConnectionSelected);

                                finalData = "";
                                savedConnectionData.Clear();
                            }
                        }
                    }
                }
        }

        public async override Task Connect()
        {
            await Task.Run(() =>
            {
                Bind();
                Listen();
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
                    enqueueInData(ConsoleMessageType.NEWSECTION,
                                  "[!] Socket powering down. . .");
                }
            });
        }

        protected void Bind()
        {
            if (netUserInfo.connSock != null && netUserInfo.connSockEndPoint != null)
            {
                netUserInfo.connSock.Bind(netUserInfo.connSockEndPoint);
                enqueueInData(ConsoleMessageType.NEWLINE, 
                              "[+] Bound to " + netUserInfo.connSockEndPoint.ToString() + " . . .");
            }
        }

        protected void Listen()
        {
            if (netUserInfo.connSock != null)
            {
                netUserInfo.connSock.Listen(10);
                enqueueInData(ConsoleMessageType.NEWLINE,
                              "[+] Listening for incoming connection(s) . . .");
            }
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
                    netUserInfo.connections.Add(netUserInfo.connSockEndPoint, (netUserInfo.connSock, new Queue<string>()));
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
                    enqueueInData(ConsoleMessageType.NEWSECTION,
                                  "[!] Socket powering down . . .");
                }
            });
        }
    }
}
