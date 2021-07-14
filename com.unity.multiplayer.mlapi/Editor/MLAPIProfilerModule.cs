using System;
using System.Collections.Generic;
using MLAPI.Profiling;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

namespace MLAPI
{
    [InitializeOnLoad]
    internal static class MLAPIProfilerModule
    {
#if UNITY_2020_2_OR_NEWER && ENABLE_PROFILER
        private const string k_RpcModuleName = "MLAPI RPCs";
        private const string k_OperationModuleName = "MLAPI Operations";
        private const string k_MessageModuleName = "MLAPI Messages";

#pragma warning disable IDE1006 // disable naming rule violation check
        /// <summary>
        /// This needs to be in synced with the internal dynamic module structure to provide our own counters
        /// </summary>
        [Serializable]
        private class MLAPIProfilerCounter
        {
            // Note: These fields are named this way for internal serialization
            public string m_Name;
            public string m_Category;
        }

        /// <summary>
        /// This needs to be in synced with the internal dynamic module structure to provide our own counters
        /// </summary>
        [Serializable]
        private class MLAPIProfilerModuleData
        {
            // Note: These fields are named this way for internal serialization
            public List<MLAPIProfilerCounter> m_ChartCounters = new List<MLAPIProfilerCounter>();
            public List<MLAPIProfilerCounter> m_DetailCounters = new List<MLAPIProfilerCounter>();
            public string m_Name;
        }

        /// <summary>
        /// This needs to be in synced with the internal dynamic module structure to provide our own counters
        /// </summary>
        [Serializable]
        private class MLAPIModules
        {
            // Note: These fields are named this way for internal serialization
            public List<MLAPIProfilerModuleData> m_Modules;
        }
#pragma warning restore IDE1006 // restore naming rule violation check

        private static List<MLAPIProfilerCounter> CreateRPCCounters() => new List<MLAPIProfilerCounter>()
        {
            new MLAPIProfilerCounter { m_Name = ProfilerConstants.MessagesSent, m_Category = ProfilerCategory.Network.Name },
            new MLAPIProfilerCounter { m_Name = ProfilerConstants.MessagesReceived, m_Category = ProfilerCategory.Network.Name },
            new MLAPIProfilerCounter { m_Name = ProfilerConstants.MessageBatchesSent, m_Category = ProfilerCategory.Network.Name },
            new MLAPIProfilerCounter { m_Name = ProfilerConstants.MessageBatchesReceived, m_Category = ProfilerCategory.Network.Name },
            new MLAPIProfilerCounter { m_Name = ProfilerConstants.MessageQueueProcessed, m_Category = ProfilerCategory.Network.Name },
            new MLAPIProfilerCounter { m_Name = ProfilerConstants.MessageInQueueSize, m_Category = ProfilerCategory.Network.Name },
            new MLAPIProfilerCounter { m_Name = ProfilerConstants.MessageOutQueueSize, m_Category = ProfilerCategory.Network.Name },
        };

        private static List<MLAPIProfilerCounter> CreateOperationsCounters() => new List<MLAPIProfilerCounter>()
        {
            new MLAPIProfilerCounter { m_Name = ProfilerConstants.Connections, m_Category = ProfilerCategory.Network.Name },
            new MLAPIProfilerCounter { m_Name = ProfilerConstants.ReceiveTickRate, m_Category = ProfilerCategory.Network.Name },
        };

        private static List<MLAPIProfilerCounter> CreateMessagesCounters() => new List<MLAPIProfilerCounter>()
        {
            new MLAPIProfilerCounter { m_Name = ProfilerConstants.NamedMessageReceived, m_Category = ProfilerCategory.Network.Name },
            new MLAPIProfilerCounter { m_Name = ProfilerConstants.UnnamedMessageReceived, m_Category = ProfilerCategory.Network.Name },
            new MLAPIProfilerCounter { m_Name = ProfilerConstants.NamedMessageSent, m_Category = ProfilerCategory.Network.Name },
            new MLAPIProfilerCounter { m_Name = ProfilerConstants.UnnamedMessageSent, m_Category = ProfilerCategory.Network.Name },
            new MLAPIProfilerCounter { m_Name = ProfilerConstants.ByteSent, m_Category = ProfilerCategory.Network.Name },
            new MLAPIProfilerCounter { m_Name = ProfilerConstants.ByteReceived, m_Category = ProfilerCategory.Network.Name },
            new MLAPIProfilerCounter { m_Name = ProfilerConstants.NetworkVarUpdates, m_Category = ProfilerCategory.Network.Name },
            new MLAPIProfilerCounter { m_Name = ProfilerConstants.NetworkVarDeltas, m_Category = ProfilerCategory.Network.Name },
        };

        private delegate List<MLAPIProfilerCounter> CounterListFactoryDelegate();

        private static bool CreateMLAPIDynamicModule(ref MLAPIModules mlapiModules, string moduleName, CounterListFactoryDelegate counterListFactoryDelegate)
        {
            var module = mlapiModules.m_Modules.Find(x => x.m_Name == moduleName);
            if (module == null)
            {
                var newModule = new MLAPIProfilerModuleData
                {
                    m_Name = moduleName,
                    m_ChartCounters = counterListFactoryDelegate(),
                    m_DetailCounters = counterListFactoryDelegate(),
                };
                mlapiModules.m_Modules.Add(newModule);
                return true;
            }

            return false;
        }
#endif

        static MLAPIProfilerModule()
        {
#if UNITY_2020_2_OR_NEWER && ENABLE_PROFILER
            var dynamicModulesJson = EditorPrefs.GetString("ProfilerWindow.DynamicModules");
            var dynamicModules = JsonUtility.FromJson<MLAPIModules>(dynamicModulesJson);

            if (dynamicModules != null)
            {
                bool wasCreated = CreateMLAPIDynamicModule(ref dynamicModules, k_RpcModuleName, CreateRPCCounters);
                wasCreated |= CreateMLAPIDynamicModule(ref dynamicModules, k_OperationModuleName, CreateOperationsCounters);
                wasCreated |= CreateMLAPIDynamicModule(ref dynamicModules, k_MessageModuleName, CreateMessagesCounters);

                if (wasCreated)
                {
                    EditorPrefs.SetString("ProfilerWindow.DynamicModules", JsonUtility.ToJson(dynamicModules));
                }
            }
#endif
        }
    }
}
