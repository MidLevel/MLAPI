using System.Collections.Generic;

namespace MLAPI.Profiling
{
    public static class ProfilerStatManager
    {
        public static List<ProfilerStat> AllStats = new List<ProfilerStat>();

        public static readonly ProfilerIncStat Connections = new ProfilerIncStat("Connections");
        public static readonly ProfilerStat BytesRcvd = new ProfilerStat("Bytes Rcvd");
        public static readonly ProfilerStat BytesSent = new ProfilerStat("Bytes Sent");
        public static readonly ProfilerStat RcvTickRate = new ProfilerStat("Rcv Tick Rate");
        public static readonly ProfilerStat NetworkVarsRcvd = new ProfilerStat("Network Vars Rcvd");
        public static readonly ProfilerStat NamedMessage = new ProfilerStat("Named Message");
        public static readonly ProfilerStat UnnamedMessage = new ProfilerStat("UnNamed Message");

        public static readonly ProfilerStat MessagesRcvd = new ProfilerStat("Messages Rcvd");
        public static readonly ProfilerStat MessagesSent = new ProfilerStat("Messages Sent");
        public static readonly ProfilerStat MessageBatchesRcvd = new ProfilerStat("Message Batches Rcvd");
        public static readonly ProfilerStat MessageBatchesSent = new ProfilerStat("Message Batches Sent");
        public static readonly ProfilerStat MessagesQueueProc = new ProfilerStat("Messages-Processed");
        public static readonly ProfilerStat MessageInQueueSize = new ProfilerStat("InQFrameSize");
        public static readonly ProfilerStat MessageOutQueueSize = new ProfilerStat("OutQFrameSize");
        public static readonly ProfilerIncStat NetTranforms = new ProfilerIncStat("NetTransforms");


        public static void Add(ProfilerStat s)
        {
            AllStats.Add(s);
        }
    }
}
