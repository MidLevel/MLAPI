﻿using MLAPI.MonoBehaviours.Core;

namespace MLAPI.NetworkingManagerComponents.Core
{
    internal static partial class InternalMessageHandler
    {
        private static NetworkingManager netManager => NetworkingManager.singleton;
    }
}
