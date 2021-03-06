using System;
using System.Collections.Generic;
using System.Threading;
using static com.fpnn.rtm.RTMClient;

#if UNITY_2017_1_OR_NEWER
using UnityEngine;
#endif

namespace com.fpnn.rtm
{
    public static class RTMControlCenter
    {
        private static object interLocker = new object();
        //private static volatile bool networkReachable = true;
        private static volatile NetworkType networkType = NetworkType.NetworkType_Uninited;
        private static Dictionary<UInt64, RTMClient> rtmClients = new Dictionary<ulong, RTMClient>();
        private static Dictionary<RTMClient, Int64> reloginClients = new Dictionary<RTMClient, Int64>();

        private static Dictionary<string, Dictionary<TCPClient, long>> fileClients = new Dictionary<string, Dictionary<TCPClient, long>>();

        private static volatile bool routineInited;
        private static volatile bool routineRunning;
        private static Thread routineThread;

        static RTMControlCenter()
        {
            routineInited = false;
        }

        //===========================[ Session Functions ]=========================//
        internal static void RegisterSession(UInt64 connectionId, RTMClient client)
        {
            CheckRoutineInit();

            lock (interLocker)
            {
                rtmClients.Add(connectionId, client);
            }
        }

        internal static void UnregisterSession(UInt64 connectionId)
        {
            lock (interLocker)
            {
                rtmClients.Remove(connectionId);
            }
        }

        internal static void CloseSession(UInt64 connectionId)
        {
            RTMClient client = null;
            lock (interLocker)
            {
                rtmClients.TryGetValue(connectionId, out client);
            }

            if (client != null)
                client.Close();
        }

        internal static ClientStatus GetClientStatus(UInt64 connectionId)
        { 
            RTMClient client = null;
            lock (interLocker)
            {
                rtmClients.TryGetValue(connectionId, out client);
            }

            if (client != null)
                return client.Status;
            else
                return ClientStatus.Closed;
        }

        internal static RTMClient GetClient(UInt64 connectionId)
        { 
            RTMClient client = null;
            lock (interLocker)
            {
                rtmClients.TryGetValue(connectionId, out client);
            }
            return client;
        }

        //===========================[ Relogin Functions ]=========================//
        internal static void DelayRelogin(RTMClient client, long triggeredMs)
        {
            CheckRoutineInit();

            lock (interLocker)
            {
                try
                {
                    reloginClients.Add(client, triggeredMs);
                }
                catch (ArgumentException)
                {
                     //-- Do nothing.
                }
            }
        }

        private static void ReloginCheck()
        {
            //if (!networkReachable)
            //return;
            if (networkType != NetworkType.NetworkType_4G && networkType != NetworkType.NetworkType_Wifi)
                return;

            HashSet<RTMClient> clients = new HashSet<RTMClient>();
            long now = ClientEngine.GetCurrentMilliseconds();

            lock (interLocker)
            {
                foreach (KeyValuePair<RTMClient, Int64> kvp in reloginClients)
                {
                    if (kvp.Value <= now)
                        clients.Add(kvp.Key);
                }

                foreach (RTMClient client in clients)
                    reloginClients.Remove(client);
            }

            foreach (RTMClient client in clients)
            {
                ClientEngine.RunTask(() => {
                    client.StartRelogin();
                });
            }
        }

        //internal static void NetworkReachableChanged(bool reachable)
        //{
        //    if (reachable != networkReachable)
        //    {
        //        networkReachable = reachable;
        //        long now = ClientEngine.GetCurrentMilliseconds();
        //        if (reachable)
        //        {
        //            Dictionary<RTMClient, Int64> clients = new Dictionary<RTMClient, Int64>();
        //            lock (interLocker)
        //            {
        //                foreach (KeyValuePair<RTMClient, Int64> kvp in reloginClients)
        //                    clients.Add(kvp.Key, now);

        //                reloginClients = clients;
        //            }
        //        }
        //        else
        //        {
        //            lock (interLocker)
        //            {
        //                foreach (KeyValuePair<UInt64, RTMClient> kvp in rtmClients)
        //                {
        //                    kvp.Value.Close();
        //                    reloginClients.Add(kvp.Value, now);
        //                }
        //            }
        //        }
        //    }
        //}

        internal static void NetworkChanged(NetworkType type)
        {
            if (networkType == NetworkType.NetworkType_Uninited)
                networkType = type;
            if (type == NetworkType.NetworkType_Unknown)
                type = NetworkType.NetworkType_Unreachable;
            if (networkType == type)
                return;
            long now = ClientEngine.GetCurrentMilliseconds();
            if (networkType == NetworkType.NetworkType_Unreachable && (type == NetworkType.NetworkType_4G || type == NetworkType.NetworkType_Wifi))
            {//之前没有网络，现在有网络
                Dictionary<RTMClient, Int64> clients = new Dictionary<RTMClient, Int64>();
                lock (interLocker)
                {
                    foreach (KeyValuePair<RTMClient, Int64> kvp in reloginClients)
                        clients.Add(kvp.Key, now);

                    reloginClients = clients;
                }
            }
            else
            {
                lock (interLocker)
                {
                    foreach (KeyValuePair<UInt64, RTMClient> kvp in rtmClients)
                    {
                        kvp.Value.Close();
                        reloginClients.Add(kvp.Value, now);
                    }
                }
            }
            networkType = type;
        }

        //===========================[ File Gate Client Functions ]=========================//
        internal static void ActiveFileGateClient(string endpoint, TCPClient client)
        {
            lock (interLocker)
            {
                if (fileClients.TryGetValue(endpoint, out Dictionary<TCPClient, long> clients))
                {
                    if (clients.ContainsKey(client))
                        clients[client] = ClientEngine.GetCurrentSeconds();
                    else
                        clients.Add(client, ClientEngine.GetCurrentSeconds());
                }
                else
                {
                    clients = new Dictionary<TCPClient, long>
                    {
                        { client, ClientEngine.GetCurrentSeconds() }
                    };
                    fileClients.Add(endpoint, clients);
                }
            }
        }

        internal static TCPClient FecthFileGateClient(string endpoint)
        {
            lock (interLocker)
            {
                if (fileClients.TryGetValue(endpoint, out Dictionary<TCPClient, long> clients))
                {
                    foreach (KeyValuePair<TCPClient, long> kvp in clients)
                        return kvp.Key;
                }
            }

            return null;
        }

        private static void CheckFileGateClients()
        {
            HashSet<string> emptyEndpoints = new HashSet<string>();

            lock (interLocker)
            {
                long threshold = ClientEngine.GetCurrentSeconds() - RTMConfig.fileGateClientHoldingSeconds;

                foreach (KeyValuePair<string, Dictionary<TCPClient, long>> kvp in fileClients)
                {
                    HashSet<TCPClient> unactivedClients = new HashSet<TCPClient>();

                    foreach (KeyValuePair<TCPClient, long> subKvp in kvp.Value)
                    {
                        if (subKvp.Value <= threshold)
                            unactivedClients.Add(subKvp.Key);
                    }

                    foreach (TCPClient client in unactivedClients)
                    {
                        kvp.Value.Remove(client);
                    }

                    if (kvp.Value.Count == 0)
                        emptyEndpoints.Add(kvp.Key);
                }

                foreach (string endpoint in emptyEndpoints)
                    fileClients.Remove(endpoint);
            }
        }

        //===========================[ Init & Routine Functions ]=========================//
        public static void Init()
        {
            Init(null);
        }

        public static void Init(RTMConfig config)
        {
#if UNITY_2017_1_OR_NEWER
            StatusMonitor.Instance.Init();
#endif

            if (config == null)
                return;

            RTMConfig.Config(config);
        }

        private static void CheckRoutineInit()
        {
            if (routineInited)
                return;

            lock (interLocker)
            {
                if (routineInited)
                    return;

                routineRunning = true;

                routineThread = new Thread(RoutineFunc)
                {
                    Name = "RTM.ControlCenter.RoutineThread",
#if UNITY_2017_1_OR_NEWER
#else
                    IsBackground = true
#endif
                };
                routineThread.Start();


                routineInited = true;
            }
        }

        private static void RoutineFunc()
        {
            while (routineRunning)
            {
                Thread.Sleep(1000);

                HashSet<RTMClient> clients = new HashSet<RTMClient>();

                lock (interLocker)
                {
                    foreach (KeyValuePair<UInt64, RTMClient> kvp in rtmClients)
                        clients.Add(kvp.Value);
                }

                foreach (RTMClient client in clients)
                    if (client.ConnectionIsAlive() == false)
                        client.Close(false, true);

                CheckFileGateClients();
                ReloginCheck();
            }
        }

        public static void Close()
        {
            lock (interLocker)
            {
                if (!routineInited)
                    return;

                if (!routineRunning)
                    return;

                routineRunning = false;
            }

#if UNITY_2017_1_OR_NEWER
            routineThread.Join();
#endif
            HashSet<RTMClient> clients = new HashSet<RTMClient>();

            lock (interLocker)
            {
                foreach (KeyValuePair<UInt64, RTMClient> kvp in rtmClients)
                    clients.Add(kvp.Value);
            }

            foreach (RTMClient client in clients)
                    client.Close(true, true);

        }
    }
}
