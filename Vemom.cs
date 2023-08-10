using System;
using Venomcc.Networking;
using Venomcc.Threads;

namespace Venomcc
{
    class Venom
    {
        static async Task Main(string[] args)
        {
            Threading threading = new Threading();
            object _lock = threading.GetLock();

            Server? server = new Server(_lock, 1313);
            Client? client = new Client(_lock, "192.168.1.109", 1313);

            if (server != null)
            {
                await server.Connect();
              
                int AcceptThreadID = threading.CreateTask(server.AcceptIncomingConnections).Result;
                int ReceiveThreadID = threading.CreateTask(server.ReceiveData).Result;

                if (client != null)
                {
                    await client.Connect();
                }

                int i = 0;
                while (true)
                {
                    await threading.StartTasks();
                    NetUser.ConsumeNextInData();
                    if (client != null && client.GetConnectionsCount() > 0 && i < 4)
                    {
                        string data = "";
                        if ( i == 0 )
                        {
                            data = "Z" + ("HELLO").PadLeft(4000, 'X') + "W";
                        }
                        else if ( i == 1 )
                        {
                            data = "W" + ("OLLEH").PadLeft(2041, 'X') + "Z";
                        }
                        else if ( i == 2 )
                        {
                            data = "C" + ("ASDAGHJ").PadLeft(6323, 'X') + "V";
                        }
                        else
                        {
                            data = "WWW" + data.PadLeft(2000, 'X') + "WWW";
                        }

                        await client.SendTo(SendMessageType.Broadcast, data);
                        i++;
                    }
                }
            }
        }
    }
}