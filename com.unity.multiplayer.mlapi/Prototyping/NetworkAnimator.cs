using System.Linq;
using System.Collections.Generic;
using MLAPI.Messaging;
using MLAPI.Serialization;
using UnityEngine;

namespace MLAPI.Prototyping
{
    /// <summary>
    /// A prototype component for syncing animations
    /// </summary>
    [AddComponentMenu("MLAPI/NetworkAnimator")]
    public class NetworkAnimator : NetworkBehaviour
    {
        
        private class AnimatorSnapshot : INetworkSerializable
        {
            public Dictionary<int, bool> BoolParameters;
            public Dictionary<int, float> FloatParameters;
            public Dictionary<int, int> IntParameters;
            public HashSet<int> TriggerParameters;
            public LayerState[] LayerStates;

            public AnimatorSnapshot(Dictionary<int, bool> boolParameters, Dictionary<int, float> floatParameters, Dictionary<int, int> intParameters, HashSet<int> triggerParameters, LayerState[] layerStates)
            {
                BoolParameters = boolParameters;
                FloatParameters = floatParameters;
                IntParameters = intParameters;
                TriggerParameters = triggerParameters;
                LayerStates = layerStates;
            }

            public AnimatorSnapshot()
            {
                BoolParameters = new Dictionary<int, bool>(0);
                FloatParameters = new Dictionary<int, float>(0);
                IntParameters = new Dictionary<int, int>(0);
                TriggerParameters = new HashSet<int>();
                LayerStates = new LayerState[0];
            }

            public bool SetInt(int key, int value)
            {
                if (IntParameters.TryGetValue(key, out var existingValue) && existingValue == value)
                {
                    return false;
                }

                IntParameters[key] = value;
                return true;
            }

            public bool SetBool(int key, bool value)
            {
                if (BoolParameters.TryGetValue(key, out var existingValue) && existingValue == value)
                {
                    return false;
                }

                BoolParameters[key] = value;
                return true;
            }

            public bool SetFloat(int key, float value)
            {
                if (FloatParameters.TryGetValue(key, out var existingValue) &&
                    Mathf.Abs(existingValue - value) < Mathf.Epsilon)
                {
                    return false;
                }

                FloatParameters[key] = value;
                return true;
            }

            public bool SetTrigger(int key)
            {
                return TriggerParameters.Add(key);
            }

            public void NetworkSerialize(NetworkSerializer serializer)
            {
                SerializeIntParameters(serializer);
                SerializeFloatParameters(serializer);
                SerializeBoolParameters(serializer);
                SerializeTriggerParameters(serializer);
                SerializeAnimatorLayerStates(serializer);
            }

            private void SerializeAnimatorLayerStates(NetworkSerializer serializer)
            {
                int layerCount = serializer.IsReading ? 0 : LayerStates.Length;
                serializer.Serialize(ref layerCount);

                if (serializer.IsReading && LayerStates.Length != layerCount)
                {
                    LayerStates = new LayerState[layerCount];
                }

                for (int paramIndex = 0; paramIndex < layerCount; paramIndex++)
                {
                    var stateHash = serializer.IsReading ? 0 : LayerStates[paramIndex].StateHash;
                    serializer.Serialize(ref stateHash);

                    var layerWeight = serializer.IsReading ? 0 : LayerStates[paramIndex].LayerWeight;
                    serializer.Serialize(ref layerWeight);

                    var normalizedStateTime = serializer.IsReading ? 0 : LayerStates[paramIndex].NormalizedStateTime;
                    serializer.Serialize(ref normalizedStateTime);

                    if (serializer.IsReading)
                    {
                        LayerStates[paramIndex] = new LayerState()
                        {
                            LayerWeight = layerWeight,
                            StateHash = stateHash,
                            NormalizedStateTime = normalizedStateTime
                        };
                    }
                }
            }

            private void SerializeTriggerParameters(NetworkSerializer serializer)
            {
                int paramCount = serializer.IsReading ? 0 : TriggerParameters.Count;
                serializer.Serialize(ref paramCount);

                var paramArray = serializer.IsReading ? new int[paramCount] : TriggerParameters.ToArray();
                for (int i = 0; i < paramCount; i++)
                {
                    var paramId = serializer.IsReading ? 0 : paramArray[i];
                    serializer.Serialize(ref paramId);

                    if (serializer.IsReading)
                    {
                        paramArray[i] = paramId;
                    }
                }

                if (serializer.IsReading)
                {
                    TriggerParameters = new HashSet<int>(paramArray);
                }
            }

            private void SerializeBoolParameters(NetworkSerializer serializer)
            {
                int paramCount = serializer.IsReading ? 0 : BoolParameters.Count;
                serializer.Serialize(ref paramCount);

                var paramArray = serializer.IsReading ? new KeyValuePair<int, bool>[paramCount] : BoolParameters.ToArray();
                for (int paramIndex = 0; paramIndex < paramCount; paramIndex++)
                {
                    var paramId = serializer.IsReading ? 0 : paramArray[paramIndex].Key;
                    serializer.Serialize(ref paramId);

                    var paramBool = serializer.IsReading ? false : paramArray[paramIndex].Value;
                    serializer.Serialize(ref paramBool);

                    if (serializer.IsReading)
                    {
                        paramArray[paramIndex] = new KeyValuePair<int, bool>(paramId, paramBool);
                    }
                }

                if (serializer.IsReading)
                {
                    BoolParameters = paramArray.ToDictionary(pair => pair.Key, pair => pair.Value);
                }
            }

            private void SerializeFloatParameters(NetworkSerializer serializer)
            {
                int paramCount = serializer.IsReading ? 0 : FloatParameters.Count;
                serializer.Serialize(ref paramCount);

                var paramArray = serializer.IsReading ? new KeyValuePair<int, float>[paramCount] : FloatParameters.ToArray();
                for (int paramIndex = 0; paramIndex < paramCount; paramIndex++)
                {
                    var paramId = serializer.IsReading ? 0 : paramArray[paramIndex].Key;
                    serializer.Serialize(ref paramId);

                    var paramFloat = serializer.IsReading ? 0 : paramArray[paramIndex].Value;
                    serializer.Serialize(ref paramFloat);

                    if (serializer.IsReading)
                    {
                        paramArray[paramIndex] = new KeyValuePair<int, float>(paramId, paramFloat);
                    }
                }

                if (serializer.IsReading)
                {
                    FloatParameters = paramArray.ToDictionary(pair => pair.Key, pair => pair.Value);
                }
            }

            private void SerializeIntParameters(NetworkSerializer serializer)
            {
                int paramCount = serializer.IsReading ? 0 : IntParameters.Count;
                serializer.Serialize(ref paramCount);

                var paramArray = serializer.IsReading ? new KeyValuePair<int, int>[paramCount] : IntParameters.ToArray();

                for (int paramIndex = 0; paramIndex < paramCount; paramIndex++)
                {
                    var paramId = serializer.IsReading ? 0 : paramArray[paramIndex].Key;
                    serializer.Serialize(ref paramId);

                    var paramInt = serializer.IsReading ? 0 : paramArray[paramIndex].Value;
                    serializer.Serialize(ref paramInt);

                    if (serializer.IsReading)
                    {
                        paramArray[paramIndex] = new KeyValuePair<int, int>(paramId, paramInt);
                    }
                }

                if (serializer.IsReading)
                {
                    IntParameters = paramArray.ToDictionary(pair => pair.Key, pair => pair.Value);
                }
            }
        }

        private struct LayerState
        {
            public int StateHash;
            public float NormalizedStateTime;
            public float LayerWeight;
        }

        /// <summary>
        /// Server authority only allows the server to update this animator
        /// Client authority only allows the client owner to update this animator
        /// </summary>
        public enum Authority
        {
            Server = 0,
            Owner
        }
        
        /// <summary>
        /// This constant is used to force the resync if the delta between current
        /// and last synced normalized state time goes above it
        /// </summary>
        private const float NormalizedTimeResyncThreshold = 0.15f;

        /// <summary>
        /// Specifies who can update this animator
        /// </summary>
        [Tooltip("Defines who can update this animator.")]
        public Authority AnimatorAuthority = Authority.Owner;

        [SerializeField]
        private float m_SendRate = 0.1f;
        private double m_NextSendTime = 0.0f;
        private bool m_ServerRequestsAnimationResync = false;
        [SerializeField]
        private Animator m_Animator;

        private AnimatorSnapshot m_AnimatorSnapshot;
        private List<(int, AnimatorControllerParameterType)> m_CachedAnimatorParameters;

        public bool IsAuthorityOverAnimator => (IsClient && AnimatorAuthority == Authority.Owner && IsOwner) || (IsServer && AnimatorAuthority == Authority.Server);

        public override void OnNetworkSpawn()
        {
            var parameters = m_Animator.parameters;
            m_CachedAnimatorParameters = new List<(int, AnimatorControllerParameterType)>(parameters.Length);

            int intCount = 0;
            int floatCount = 0;
            int boolCount = 0;

            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];

                if (m_Animator.IsParameterControlledByCurve(parameter.nameHash))
                {
                    //we are ignoring parameters that are controlled by animation curves - syncing the layer states indirectly syncs the values that are driven by the animation curves
                    continue;
                }

                m_CachedAnimatorParameters.Add((parameter.nameHash, parameter.type));

                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Float:
                        ++floatCount;
                        break;
                    case AnimatorControllerParameterType.Int:
                        ++intCount;
                        break;
                    case AnimatorControllerParameterType.Bool:
                        ++boolCount;
                        break;
                }
            }

            var intParameters = new Dictionary<int, int>(intCount);
            var floatParameters = new Dictionary<int, float>(floatCount);
            var boolParameters = new Dictionary<int, bool>(boolCount);
            var triggerParameters = new HashSet<int>();
            var states = new LayerState[m_Animator.layerCount];

            m_AnimatorSnapshot = new AnimatorSnapshot(boolParameters, floatParameters, intParameters, triggerParameters, states);
            
            if (!IsAuthorityOverAnimator)
            {
                m_Animator.StopPlayback();
            }
        }

        private void OnEnable()
        {
            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback += ServerOnClientConnectedCallback;
            }
        }

        private void OnDisable()
        {
            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback -= ServerOnClientConnectedCallback;
            }
        }

        private void ServerOnClientConnectedCallback(ulong clientId)
        {
            if (IsAuthorityOverAnimator)
            {
                m_ServerRequestsAnimationResync = true;
            }

            var clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = NetworkManager.ConnectedClientsList
                        .Where(c => c.ClientId != NetworkManager.ServerClientId)
                        .Select(c => c.ClientId)
                        .ToArray()
                }
            };

            RequestResyncClientRpc(clientRpcParams);
        }


        [ClientRpc]
        private void RequestResyncClientRpc(ClientRpcParams clientRpcParams = default)
        {
            if (!IsAuthorityOverAnimator)
            {
                return;
            }
            
            m_ServerRequestsAnimationResync = true;
        }
        
        private void FixedUpdate()
        {
            if (!NetworkObject.IsSpawned)
            {
                return;
            }

            if (IsAuthorityOverAnimator)
            {
                bool shouldSendBasedOnTime = CheckSendRate();
                bool shouldSendBasedOnChanges = StoreState();
                if (m_ServerRequestsAnimationResync || shouldSendBasedOnTime || shouldSendBasedOnChanges)
                {
                    SendAllParamsAndState();
                    m_AnimatorSnapshot.TriggerParameters.Clear();
                    m_ServerRequestsAnimationResync = false;
                }
            }
        }

        private bool CheckSendRate()
        {
            var networkTime = NetworkManager.LocalTime.FixedTime;
            if (m_SendRate != 0 && m_NextSendTime < networkTime)
            {
                m_NextSendTime = networkTime + m_SendRate;
                return true;
            }

            return false;
        }

        private bool StoreState()
        {
            bool layerStateChanged = StoreLayerState();
            bool animatorParametersChanged = StoreParameters();

            return layerStateChanged || animatorParametersChanged;
        }

        private bool StoreLayerState()
        {
            bool changed = false;

            for (int i = 0; i < m_AnimatorSnapshot.LayerStates.Length; i++)
            {
                var animStateInfo = m_Animator.GetCurrentAnimatorStateInfo(i);

                var snapshotAnimStateInfo = m_AnimatorSnapshot.LayerStates[i];
                bool didStateChange = snapshotAnimStateInfo.StateHash != animStateInfo.fullPathHash;
                bool enoughDelta = !didStateChange &&
                                   Mathf.Abs(animStateInfo.normalizedTime - snapshotAnimStateInfo.NormalizedStateTime) >= NormalizedTimeResyncThreshold;

                float newLayerWeight = m_Animator.GetLayerWeight(i);
                bool layerWeightChanged = Mathf.Abs(snapshotAnimStateInfo.LayerWeight - newLayerWeight) > Mathf.Epsilon;

                if (didStateChange || enoughDelta || layerWeightChanged || m_ServerRequestsAnimationResync)
                {
                    m_AnimatorSnapshot.LayerStates[i] = new LayerState
                    {
                        StateHash = animStateInfo.fullPathHash,
                        NormalizedStateTime = animStateInfo.normalizedTime,
                        LayerWeight = newLayerWeight
                    };
                    changed = true;
                }
            }

            return changed;
        }

        private bool StoreParameters()
        {
            bool changed = false;
            foreach (var animParam in m_CachedAnimatorParameters)
            {
                var animParamHash = animParam.Item1;
                var animParamType = animParam.Item2;

                switch (animParamType)
                {
                    case AnimatorControllerParameterType.Float:
                        changed = changed || m_AnimatorSnapshot.SetFloat(animParamHash, m_Animator.GetFloat(animParamHash));
                        break;
                    case AnimatorControllerParameterType.Int:
                        changed = changed || m_AnimatorSnapshot.SetInt(animParamHash, m_Animator.GetInteger(animParamHash));
                        break;
                    case AnimatorControllerParameterType.Bool:
                        changed = changed || m_AnimatorSnapshot.SetBool(animParamHash, m_Animator.GetBool(animParamHash));
                        break;
                    case AnimatorControllerParameterType.Trigger:
                        if (m_Animator.GetBool(animParamHash))
                        {
                            changed = changed || m_AnimatorSnapshot.SetTrigger(animParamHash);
                        }
                        break;
                }
            }

            return changed;
        }

        private void SendAllParamsAndState()
        {
            if (IsServer)
            {
                var clientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = NetworkManager.ConnectedClientsList
                            .Where(c => c.ClientId != NetworkManager.ServerClientId)
                            .Select(c => c.ClientId)
                            .ToArray()
                    }
                };

                SendParamsAndLayerStatesClientRpc(m_AnimatorSnapshot, clientRpcParams);
            }
            else
            {
                SendParamsAndLayerStatesServerRpc(m_AnimatorSnapshot);
            }
        }

        [ServerRpc]
        private void SendParamsAndLayerStatesServerRpc(AnimatorSnapshot animSnapshot, ServerRpcParams serverRpcParams = default)
        {
            if (!IsAuthorityOverAnimator)
            {
                ApplyAnimatorSnapshot(animSnapshot);
            }
            
            var clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = NetworkManager.ConnectedClientsList
                        .Where(c => c.ClientId != serverRpcParams.Receive.SenderClientId)
                        .Select(c => c.ClientId)
                        .ToArray()
                }
            };

            SendParamsAndLayerStatesClientRpc(animSnapshot, clientRpcParams);
        }

        [ClientRpc]
        private void SendParamsAndLayerStatesClientRpc(AnimatorSnapshot animSnapshot, ClientRpcParams clientRpcParams = default)
        {
            if (!IsAuthorityOverAnimator)
            {
                ApplyAnimatorSnapshot(animSnapshot);
            }
        }

        private void ApplyAnimatorSnapshot(AnimatorSnapshot animatorSnapshot)
        {
            foreach (var intParameter in animatorSnapshot.IntParameters)
            {
                m_Animator.SetInteger(intParameter.Key, intParameter.Value);
            }

            foreach (var floatParameter in animatorSnapshot.FloatParameters)
            {
                m_Animator.SetFloat(floatParameter.Key, floatParameter.Value);
            }

            foreach (var boolParameter in animatorSnapshot.BoolParameters)
            {
                m_Animator.SetBool(boolParameter.Key, boolParameter.Value);
            }

            foreach (var triggerParameter in animatorSnapshot.TriggerParameters)
            {
                m_Animator.SetTrigger(triggerParameter);
            }

            for (var layerIndex = 0; layerIndex < animatorSnapshot.LayerStates.Length; layerIndex++)
            {
                var layerState = animatorSnapshot.LayerStates[layerIndex];

                m_Animator.SetLayerWeight(layerIndex, layerState.LayerWeight);

                var currentAnimatorState = m_Animator.GetCurrentAnimatorStateInfo(layerIndex);

                bool stateChanged = currentAnimatorState.fullPathHash != layerState.StateHash;
                bool forceAnimationCatchup = !stateChanged &&
                                             Mathf.Abs(layerState.NormalizedStateTime - currentAnimatorState.normalizedTime) >= NormalizedTimeResyncThreshold;

                if (stateChanged || forceAnimationCatchup)
                {
                    m_Animator.Play(layerState.StateHash, layerIndex, layerState.NormalizedStateTime);
                }
            }
        }
    }
}
