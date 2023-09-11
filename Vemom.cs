using System;
using Venomcc.Networking;
using Venomcc.UI;
using Venomcc.Threads;

namespace Venomcc
{
    class Venom
    {
        static async Task Main(string[] args)
        {
            Threading threading = new Threading();
            object _lock = threading.GetLock();

            Server? server = new Server(_lock, 1314);

            if (server != null)
            {
                await server.Connect();

                ThreadStart acceptThreadStartInfo = new ThreadStart(server.AcceptIncomingConnections);
                threadInfo AcceptThreadInfo = threading.CreateThread(acceptThreadStartInfo);

                ThreadStart receiveThreadStartInfo = new ThreadStart(server.ReceiveData);
                threadInfo ReceiveThreadInfo = threading.CreateThread(receiveThreadStartInfo);

                await threading.StartThreads();

                int InputStreamThreadID = threading.CreateTask(ConsoleUI.HandleInputStream).Result;
                int ConsoleStreamThreadID = threading.CreateTask(ConsoleUI.HandleConsoleStream).Result;

                while (true)
                {
                    await threading.StartTask(InputStreamThreadID);
                    await threading.StartTask(ConsoleStreamThreadID);
                }
            }
        }
    }
}