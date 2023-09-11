using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Venomcc.UI
{
    public enum State
    {
        Idle = 0,
        Reading = 1,
        Writing = 2,
        ReadingAndWriting = 3,
    }

    public enum ConsoleMessageType
    {
        None = 0,
        PRENEWLINE = 1,
        DOUBLEPRENEWLINE = 2,
        NEWLINE = 3,
        NEWSECTION = 4,
        INVALID = 5,
    }

    public enum ConsoleDataType
    {
        Console = 0,
        Outgoing = 1,
    }

    public class ConsoleUI
    {
        public static State InputStreamState = State.Idle;
        public static State ConsoleDataQueueState = State.Idle;

        public static ConcurrentQueue<(ConsoleMessageType type, string data)> allInputDataToConsole = new ConcurrentQueue<(ConsoleMessageType type, string data)>();

        public static Dictionary<string, ConcurrentQueue<(ConsoleMessageType type, string data)>> allConsoleData = new Dictionary<string, ConcurrentQueue<(ConsoleMessageType type, string data)>>();
        protected static Stack<string> allConsoleDataHistory = new Stack<string>();

        public static Dictionary<string, ConcurrentQueue<(ConsoleMessageType type, byte[] data)>> allOutgoingData = new Dictionary<string, ConcurrentQueue<(ConsoleMessageType type, byte[] data)>>();
        protected static Stack<string> allOutgoingDataHistory = new Stack<string>();

        static public void ConsumeNextInputData()
        {
            if (allInputDataToConsole.Count() > 0)
            {
                (ConsoleMessageType type, string data) current;
                bool didDequeue = allInputDataToConsole.TryDequeue(out current);

                if (didDequeue)
                {
                    switch (current.type)
                    {
                        case ConsoleMessageType.PRENEWLINE:
                            current.data = "\n" + current.data;
                            break;
                        case ConsoleMessageType.DOUBLEPRENEWLINE:
                            current.data = "\n\n" + current.data;
                            break;
                        case ConsoleMessageType.NEWLINE:
                            current.data = current.data + "\n";
                            break;
                        case ConsoleMessageType.NEWSECTION:
                            current.data = "\n" + current.data + "\n";
                            break;
                    }

                    Console.Write(current.data);
                }
            }
        }

        static public void ConsumeNextConsoleData()
        {
            if (allConsoleData["127.0.0.1"].Count() > 0)
            {
                (ConsoleMessageType type, string data) current;
                bool didDequeue = allConsoleData["127.0.0.1"].TryDequeue(out current);

                if (didDequeue)
                {
                    switch (current.type)
                    {
                        case ConsoleMessageType.PRENEWLINE:
                            current.data = "\n" + current.data;
                            break;
                        case ConsoleMessageType.DOUBLEPRENEWLINE:
                            current.data = "\n\n" + current.data;
                            break;
                        case ConsoleMessageType.NEWLINE:
                            current.data = current.data + "\n";
                            break;
                        case ConsoleMessageType.NEWSECTION:
                            current.data = "\n" + current.data + "\n";
                            break;
                    }

                    allConsoleDataHistory.Push(current.data);
                    Console.Write(current.data);
                }
            }
        }

        static public (string? endPoint, ArraySegment<byte>? data) ConsumeNextOutgoingData()
        {
            (string? endPoint, ArraySegment<byte>? data) next = (null, null);
            if (allOutgoingData.Count() > 0)
            {
                KeyValuePair<string, ConcurrentQueue<(ConsoleMessageType type, byte[] data)>> nextKVP = allOutgoingData.ToList()[0];
                if (nextKVP.Value.Count() > 0)
                {
                    next.endPoint = nextKVP.Key;
                    (ConsoleMessageType, byte[] data) bytesRetrieved;
                    bool didDequeue = nextKVP.Value.TryDequeue(out bytesRetrieved);
                    if (didDequeue)
                    {
                        allOutgoingDataHistory.Push(Encoding.UTF8.GetString(bytesRetrieved.data));
                        next.data = new ArraySegment<byte>(bytesRetrieved.data);
                    }
                }
            }
            return next;
        }

        public static void enqueueInputData(ConsoleMessageType typeData, string inputData)
        {
            allInputDataToConsole.Enqueue((typeData, inputData));
        }

        public static void enqueueConsoleData(ConsoleMessageType typeData, string consoleData, string endPoint = "127.0.0.1")
        {
            if (!allConsoleData.ContainsKey("127.0.0.1")) allConsoleData["127.0.0.1"] = new ConcurrentQueue<(ConsoleMessageType, string)>();
            allConsoleData["127.0.0.1"].Enqueue((typeData, consoleData));
        }
        public static void enqueueOutData(ConsoleMessageType typeData, byte[] outData, string endPoint = "255.255.255.255")
        {
            if (!allOutgoingData.ContainsKey(endPoint)) allOutgoingData[endPoint] = new ConcurrentQueue<(ConsoleMessageType type, byte[] data)>();
            allOutgoingData[endPoint].Enqueue((typeData, outData));
        }
        public static (ConsoleMessageType, string) TryOutputDataFromQueue(Queue<(ConsoleMessageType, string)> dataQueue, (ConsoleMessageType, string) data)
        {
            (ConsoleMessageType, string) foundData = (ConsoleMessageType.None, "");
            if (dataQueue.Contains(data))
            {
                foundData = new Queue<(ConsoleMessageType, string)>(dataQueue.Where(_data => _data == data)).Dequeue();
                dataQueue = new Queue<(ConsoleMessageType, string)>(dataQueue.Where(_data => _data != data));
            }
            return foundData;
        }

        public static void OutputDataDict()
        {

        }

        public static void OutputConsoleDict()
        {
            foreach (KeyValuePair<string, ConcurrentQueue<(ConsoleMessageType, string)>> KVP in allConsoleData)
            {
                while (KVP.Value.Count() > 0)
                {
                    ConsumeNextConsoleData();
                }
            }
        }

        public static void HandleInputStream()
        {
            InputStreamState = State.Writing;

            (ConsoleMessageType typeData, string data) cmd = (ConsoleMessageType.None, "#~>\t");
            enqueueInputData(cmd.typeData, cmd.data);
            ConsumeNextInputData();

            string? data = Console.ReadLine();
            if (data != null && data.Length > 0) enqueueInputData(ConsoleMessageType.NEWSECTION, data);
            enqueueInputData(ConsoleMessageType.PRENEWLINE, "");

            while (allInputDataToConsole.Count() > 0)
            {
                ConsumeNextInputData();
            }

            InputStreamState = State.Idle;
        }

        public static void HandleConsoleStream()
        {
            ConsoleDataQueueState = State.Writing;
            OutputConsoleDict();
            ConsoleDataQueueState = State.Idle;
        }

        public static void HandleICStream()
        {
            while (true)
            {
                HandleInputStream();
                HandleConsoleStream();
            }
        }

        public ConsoleUI()
        {

        }
    }
}
