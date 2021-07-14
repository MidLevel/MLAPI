namespace MLAPI.Messaging
{
    public interface IHasUpdateStage
    {
        public NetworkUpdateStage UpdateStage
        {
            get;
            set;
        }
    }

    public struct ServerRpcSendParams: IHasUpdateStage
    {
        private NetworkUpdateStage m_UpdateStage;

        public NetworkUpdateStage UpdateStage
        {
            get => m_UpdateStage;
            set => m_UpdateStage = value;
        }
    }

    public struct ServerRpcReceiveParams: IHasUpdateStage
    {
        private NetworkUpdateStage m_UpdateStage;

        public NetworkUpdateStage UpdateStage
        {
            get => m_UpdateStage;
            set => m_UpdateStage = value;
        }
        public ulong SenderClientId;
    }

    public struct ServerRpcParams
    {
        public ServerRpcSendParams Send;
        public ServerRpcReceiveParams Receive;
    }

    public struct ClientRpcSendParams: IHasUpdateStage
    {
        private NetworkUpdateStage m_UpdateStage;

        public NetworkUpdateStage UpdateStage
        {
            get => m_UpdateStage;
            set => m_UpdateStage = value;
        }
        public ulong[] TargetClientIds;
    }

    public struct ClientRpcReceiveParams: IHasUpdateStage
    {
        private NetworkUpdateStage m_UpdateStage;

        public NetworkUpdateStage UpdateStage
        {
            get => m_UpdateStage;
            set => m_UpdateStage = value;
        }
    }

    public struct ClientRpcParams
    {
        public ClientRpcSendParams Send;
        public ClientRpcReceiveParams Receive;
    }

#pragma warning disable IDE1006 // disable naming rule violation check
    // RuntimeAccessModifiersILPP will make this `public`
    internal struct __RpcParams
#pragma warning restore IDE1006 // restore naming rule violation check
    {
        public ServerRpcParams Server;
        public ClientRpcParams Client;
    }
}
