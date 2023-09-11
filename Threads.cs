using System;

namespace Venomcc.Threads
{
    public struct threadInfo
    {
        public ManualResetEvent ManualResetEvent = new ManualResetEvent(true);
        public int ID;
        public Thread? Thread = null;

        public threadInfo(int ID, Thread Thread)
        {
            this.ID = ID;
            this.Thread = Thread;
        }

        public void Start()
        {
            if (Thread != null)
            {
                Thread.Start();
            }
        }

        public void Pause() => ManualResetEvent.Reset();
        public void Resume() => ManualResetEvent.Set();


        public void Abort()
        {
            if (Thread != null)
            {
                //Thread.Abort();
                Thread.Join();
            }
        }
    }

    public struct taskInfo
    {
        public int ID;
        public Task? Task = null;
        public Action? Method = null;

        public taskInfo(int ID, Action? Method)
        {
            if (Method != null)
            {
                this.ID = ID;
                Task = new Task(Method);
                this.Method = Method;
            }
        }

        public async Task Start()
        {
            Task? localTaskCopy = Task;
            await Task.Run(() =>
            {
                if (localTaskCopy != null)
                {
                    localTaskCopy.Start();
                }
            });
        }
    }

    class Threading
    {
        private object? _lock = null;
        public object GetLock()
        {
            if (_lock == null)
            {
                _lock = new object();
            }
            return _lock;
        }

        static int threadID = 0;
        static Dictionary<int, (ThreadStart threadStart, threadInfo threadInfo)> AllThreads = new Dictionary<int, (ThreadStart, threadInfo)>();
        static Dictionary<int, taskInfo> AllTasks = new Dictionary<int, taskInfo>();

        public threadInfo CreateThread(ThreadStart child)
        {
            threadID++;
            threadInfo newThreadInfo = new threadInfo(threadID, new Thread(child));
            AllThreads.Add(threadID, (child, newThreadInfo));
            return newThreadInfo;
        }

        public async Task<int> CreateTask(Action child)
        {
            Func<int> value = () =>
            {
                threadID++;
                taskInfo newThreadInfo = new taskInfo(threadID, child);
                if (newThreadInfo.Task != null)
                {
                    AllTasks.Add(threadID, newThreadInfo);
                    return newThreadInfo.ID;
                }
                threadID--;
                return -1;
            };

            return await Task.Run(value);
        }

        public threadInfo GetThread(int ID)
        {
            return AllThreads[ID].threadInfo;
        }

        public taskInfo GetTask(int ID)
        {
            return AllTasks[ID];
        }

        public void StartThread(int ID)
        {
            if (_lock != null)
            {
                lock (_lock)
                {
                    AllThreads[ID].threadInfo.Start();
                }
            }
        }

        public async Task StartTask(int ID)
        {
            await Task.Run(() =>
            {
                AllTasks[ID].Method?.Invoke();
            });
        }

        public async Task StartThreads()
        {
            await Task.Run(() =>
            {
                if (_lock != null)
                {
                    lock (_lock)
                    {
                        foreach (KeyValuePair<int, (ThreadStart threadStart, threadInfo threadInfo)> threadKVP in AllThreads)
                        {
                            threadKVP.Value.threadInfo.Start();
                        }
                    }
                }
            });
        }

        public async Task StartTasks()
        {
            await Task.Run(() =>
            {
                taskInfo currentTaskInfo;
                Task backgroundTask;
                foreach (KeyValuePair<int, taskInfo> taskKVP in AllTasks)
                {
                    currentTaskInfo = taskKVP.Value;
                    backgroundTask = Task.CompletedTask;

                    try
                    {
                        backgroundTask.Wait();
                    }
                    catch (AggregateException aggregateException)
                    {
                        aggregateException.Handle(exceptionHandled => true);
                    }

                    backgroundTask = Task.Run(() =>
                    {
                        currentTaskInfo.Method?.Invoke();
                    });
                }
            });
        }

        public void JoinThread(int ID)
        {
            //if (AllThreads[ID].Thread != null) AllThreads[ID].Thread.Join();
        }

        public void AbortThread(int ID)
        {
            AllThreads[ID].threadInfo.Abort();
        }

        public static void LockThread()
        {

        }

        public static void UnlockThread()
        {

        }
    }
}
