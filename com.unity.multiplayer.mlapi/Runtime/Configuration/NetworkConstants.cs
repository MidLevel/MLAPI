namespace MLAPI.Configuration
{
    /// <summary>
    /// A static class containing MLAPI constants
    /// </summary>
    internal static class NetworkConstants
    {
        internal const string PROTOCOL_VERSION = "13.0.0";

        internal const byte CONNECTION_REQUEST = 3;
        internal const byte CONNECTION_APPROVED = 4;
        internal const byte ADD_OBJECT = 5;
        internal const byte DESTROY_OBJECT = 6;
        internal const byte CHANGE_OWNER = 9;
        internal const byte ADD_OBJECTS = 10;
        internal const byte TIME_SYNC = 11;
        internal const byte NETWORK_VARIABLE_DELTA = 12;
        internal const byte ALL_CLIENTS_LOADED_SCENE = 14;
        internal const byte PARENT_SYNC = 16;
        internal const byte UNNAMED_MESSAGE = 20;
        internal const byte DESTROY_OBJECTS = 21;
        internal const byte NAMED_MESSAGE = 22;
        internal const byte SERVER_LOG = 23;
        internal const byte SNAPSHOT_DATA = 25;
        internal const byte SNAPSHOT_ACK = 26;
        internal const byte SERVER_RPC = 30;
        internal const byte CLIENT_RPC = 31;
        internal const byte SCENE_EVENT = 33;
        internal const byte CLIENT_SWITCH_SCENE_COMPLETED = 35;
        internal const byte INVALID = 36;

        internal static readonly string[] MESSAGE_NAMES =
        {
            "", // 0
            "",
            "",
            "CONNECTION_REQUEST",
            "CONNECTION_APPROVED",
            "ADD_OBJECT",
            "DESTROY_OBJECT",
            "",
            "",
            "CHANGE_OWNER",
            "ADD_OBJECTS",
            "TIME_SYNC",
            "NETWORK_VARIABLE_DELTA",
            "",
            "ALL_CLIENTS_SWITCH_SCENE_COMPLETED",
            "",
            "PARENT_SYNC", // 16
            "",
            "",
            "",
            "UNNAMED_MESSAGE",
            "DESTROY_OBJECTS",
            "NAMED_MESSAGE",
            "SERVER_LOG",
            "",
            "SNAPSHOT_DATA",
            "SNAPSHOT_ACK",
            "",
            "",
            "",
            "SERVER_RPC",
            "CLIENT_RPC",
            "SCENE_EVENT",              // New Scene Event command
            "INVALID" // 36
        };
    }
}
