using MLAPI.Messaging;
using MLAPI.Profiling;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MLAPI.EditorTests.Profiling
{
    public class InternalMessageHandlerProfilingDecoratorTests
    {
        private InternalMessageHandlerProfilingDecorator m_Decorator;

        [SetUp]
        public void Setup()
        {
            m_Decorator = new InternalMessageHandlerProfilingDecorator(new DummyMessageHandler());
        }

        [Test]
        public void HandleConnectionRequestCallsUnderlyingHandler()
        {
            m_Decorator.HandleConnectionRequest(0, null);

            LogAssert.Expect(LogType.Log, nameof(m_Decorator.HandleConnectionRequest));
        }

        [Test]
        public void HandleConnectionApprovedCallsUnderlyingHandler()
        {
            m_Decorator.HandleConnectionApproved(0, null, 0.0f);

            LogAssert.Expect(LogType.Log, nameof(m_Decorator.HandleConnectionApproved));
        }

        [Test]
        public void HandleAddObjectCallsUnderlyingHandler()
        {
            m_Decorator.HandleAddObject(0, null);

            LogAssert.Expect(LogType.Log, nameof(m_Decorator.HandleAddObject));
        }

        [Test]
        public void HandleDestroyObjectCallsUnderlyingHandler()
        {
            m_Decorator.HandleDestroyObject(0, null);

            LogAssert.Expect(LogType.Log, nameof(m_Decorator.HandleDestroyObject));
        }

        [Test]
        public void HandleSceneEventCallsUnderlyingHandler()
        {
            m_Decorator.HandleSceneEvent(0, null);

            LogAssert.Expect(LogType.Log, nameof(m_Decorator.HandleSceneEvent));
        }

        [Test]
        public void HandleChangeOwnerCallsUnderlyingHandler()
        {
            m_Decorator.HandleChangeOwner(0, null);

            LogAssert.Expect(LogType.Log, nameof(m_Decorator.HandleChangeOwner));
        }

        [Test]
        public void HandleAddObjectsCallsUnderlyingHandler()
        {
            m_Decorator.HandleAddObjects(0, null);

            LogAssert.Expect(LogType.Log, nameof(m_Decorator.HandleAddObjects));
        }

        [Test]
        public void HandleDestroyObjectsCallsUnderlyingHandler()
        {
            m_Decorator.HandleDestroyObjects(0, null);

            LogAssert.Expect(LogType.Log, nameof(m_Decorator.HandleDestroyObjects));
        }

        [Test]
        public void HandleTimeSyncCallsUnderlyingHandler()
        {
            m_Decorator.HandleTimeSync(0, null, 0.0f);

            LogAssert.Expect(LogType.Log, nameof(m_Decorator.HandleTimeSync));
        }

        [Test]
        public void HandleNetworkVariableDeltaCallsUnderlyingHandler()
        {
            m_Decorator.HandleNetworkVariableDelta(0, null, null, default);

            LogAssert.Expect(LogType.Log, nameof(m_Decorator.HandleNetworkVariableDelta));
        }

        [Test]
        public void HandleUnnamedMessageCallsUnderlyingHandler()
        {
            m_Decorator.HandleUnnamedMessage(0, null);

            LogAssert.Expect(LogType.Log, nameof(m_Decorator.HandleUnnamedMessage));
        }

        [Test]
        public void HandleNamedMessageCallsUnderlyingHandler()
        {
            m_Decorator.HandleNamedMessage(0, null);

            LogAssert.Expect(LogType.Log, nameof(m_Decorator.HandleNamedMessage));
        }

        [Test]
        public void HandleNetworkLogCallsUnderlyingHandler()
        {
            m_Decorator.HandleNetworkLog(0, null);

            LogAssert.Expect(LogType.Log, nameof(m_Decorator.HandleNetworkLog));
        }

        [Test]
        public void RpcReceiveQueueItemCallsUnderlyingHandler()
        {
            m_Decorator.RpcReceiveQueueItem(0, null, 0.0f, RpcQueueContainer.QueueItemType.None);

            LogAssert.Expect(LogType.Log, nameof(m_Decorator.RpcReceiveQueueItem));
        }

        [Test]
        public void HandleAllClientsSwitchSceneCompleted()
        {
            m_Decorator.HandleAllClientsSwitchSceneCompleted(0, null);

            LogAssert.Expect(LogType.Log, nameof(m_Decorator.HandleAllClientsSwitchSceneCompleted));
        }
    }
}
