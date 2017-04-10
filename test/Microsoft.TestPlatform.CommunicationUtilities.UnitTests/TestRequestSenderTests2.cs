// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.TestPlatform.CommunicationUtilities.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
    using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.Interfaces;
    using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using CommunicationUtilitiesResources = Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.Resources.Resources;

    [TestClass]
    public class TestRequestSenderTests2
    {
        private readonly ITestRequestSender testRequestSender;
        private readonly Mock<ICommunicationServer> mockServer;
        private readonly Mock<IDataSerializer> mockDataSerializer;
        private readonly Mock<ICommunicationChannel> mockChannel;

        private readonly ConnectedEventArgs connectedEventArgs;
        private readonly List<string> pathToAdditionalExtensions = new List<string> { "Hello", "World" };
        private readonly Mock<ITestDiscoveryEventsHandler> mockDiscoveryEventsHandler;
        private readonly Mock<ITestRunEventsHandler> mockExecutionEventsHandler;
        private readonly TestRunCriteriaWithSources testRunCriteriaWithSources;

        public TestRequestSenderTests2()
        {
            this.mockChannel = new Mock<ICommunicationChannel>();
            this.mockServer = new Mock<ICommunicationServer>();
            this.mockDataSerializer = new Mock<IDataSerializer>();
            this.testRequestSender = new TestableTestRequestSender(this.mockServer.Object, this.mockDataSerializer.Object);

            this.connectedEventArgs = new ConnectedEventArgs(this.mockChannel.Object);
            this.mockDiscoveryEventsHandler = new Mock<ITestDiscoveryEventsHandler>();
            this.mockExecutionEventsHandler = new Mock<ITestRunEventsHandler>();
            this.testRunCriteriaWithSources = new TestRunCriteriaWithSources(new Dictionary<string, IEnumerable<string>>(), "runsettings", null);
        }

        [TestMethod]
        public void InitializeCommunicationShouldHostServerAndAcceptClient()
        {
            var port = this.SetupFakeCommunicationChannel();

            this.mockServer.Verify(mc => mc.Start(), Times.Once);
            Assert.AreEqual(port.ToString(), "123", "Correct port must be returned.");
        }

        [TestMethod]
        public void WaitForRequestHandlerConnectionShouldWaitForClientToConnect()
        {
            this.SetupFakeCommunicationChannel();

            var connected = this.testRequestSender.WaitForRequestHandlerConnection(1);

            Assert.IsTrue(connected);
        }

        [TestMethod]
        public void CloseShouldCallStopServerOnCommunicationManager()
        {
            this.testRequestSender.Close();

            this.mockServer.Verify(mc => mc.Stop(), Times.Once);
        }

        [TestMethod]
        public void DisposeShouldCallStopServerOnCommunicationManager()
        {
            this.testRequestSender.Dispose();

            this.mockServer.Verify(mc => mc.Stop(), Times.Once);
        }

        #region Discovery Protocol Tests
        [TestMethod]
        public void InitializeDiscoveryShouldSendCommunicationMessageWithCorrectParameters()
        {
            this.SetupFakeCommunicationChannel();

            this.testRequestSender.InitializeDiscovery(this.pathToAdditionalExtensions, false);

            this.mockDataSerializer.Verify(d => d.SerializePayload(MessageType.DiscoveryInitialize, this.pathToAdditionalExtensions), Times.Once);
            this.mockChannel.Verify(mc => mc.Send(It.IsAny<string>()), Times.Once);
        }

        [TestMethod]
        public void DiscoverTestsShouldSendStartDiscoveryMessageOnChannel()
        {
            this.SetupFakeCommunicationChannel();

            this.testRequestSender.DiscoverTests(new DiscoveryCriteria(), this.mockDiscoveryEventsHandler.Object);

            this.mockDataSerializer.Verify(
                s => s.SerializePayload(MessageType.StartDiscovery, It.IsAny<DiscoveryCriteria>()),
                Times.Once);
            this.mockChannel.Verify(mc => mc.Send(It.IsAny<string>()), Times.Once);
        }

        [TestMethod]
        public void DiscoverTestsShouldCallHandleRawMessageOnMessageReceive()
        {
            this.SetupDeserializeMessage(MessageType.TestMessage, new TestMessagePayload());
            this.SetupFakeCommunicationChannel();

            this.testRequestSender.DiscoverTests(new DiscoveryCriteria(), this.mockDiscoveryEventsHandler.Object);

            this.RaiseMessageReceivedEvent();
            this.mockDiscoveryEventsHandler.Verify(eh => eh.HandleRawMessage("DummyData"));
        }

        [TestMethod]
        public void DiscoverTestsShouldCallHandleDiscoveredTestsOnTestCaseEvent()
        {
            this.SetupDeserializeMessage<IEnumerable<TestCase>>(MessageType.TestCasesFound, new TestCase[2]);
            this.SetupFakeCommunicationChannel();

            this.testRequestSender.DiscoverTests(new DiscoveryCriteria(), this.mockDiscoveryEventsHandler.Object);

            this.RaiseMessageReceivedEvent();
            this.mockDiscoveryEventsHandler.Verify(eh => eh.HandleDiscoveredTests(It.Is<IEnumerable<TestCase>>(t => t.Count() == 2)));
        }

        [TestMethod]
        public void DiscoverTestsShouldCallHandleDiscoveryCompleteOnDiscoveryCompletion()
        {
            var completePayload = new DiscoveryCompletePayload { TotalTests = 10, IsAborted = true };
            this.SetupDeserializeMessage<DiscoveryCompletePayload>(MessageType.DiscoveryComplete, completePayload);
            this.SetupFakeCommunicationChannel();

            this.testRequestSender.DiscoverTests(new DiscoveryCriteria(), this.mockDiscoveryEventsHandler.Object);

            this.RaiseMessageReceivedEvent();
            this.mockDiscoveryEventsHandler.Verify(eh => eh.HandleDiscoveryComplete(10, null, true));
        }

        [TestMethod]
        public void DiscoverTestShouldCallHandleLogMessageOnTestMessage()
        {
            var message = new TestMessagePayload { MessageLevel = TestMessageLevel.Error, Message = "Message1" };
            this.SetupDeserializeMessage<TestMessagePayload>(MessageType.TestMessage, message);
            this.SetupFakeCommunicationChannel();

            this.testRequestSender.DiscoverTests(new DiscoveryCriteria(), this.mockDiscoveryEventsHandler.Object);

            this.RaiseMessageReceivedEvent();
            this.mockDiscoveryEventsHandler.Verify(eh => eh.HandleLogMessage(TestMessageLevel.Error, "Message1"));
        }

        [TestMethod]
        public void DiscoverTestShouldAbortDiscoveryIfExceptionThrownOnMessageReceived()
        {
            this.mockDataSerializer.Setup(ds => ds.DeserializeMessage(It.IsAny<string>())).Callback(() => throw new Exception("Dummy Message"));
            this.SetupFakeCommunicationChannel();

            this.testRequestSender.DiscoverTests(new DiscoveryCriteria(), this.mockDiscoveryEventsHandler.Object);

            this.RaiseMessageReceivedEvent();
            this.mockDiscoveryEventsHandler.Verify(eh => eh.HandleLogMessage(TestMessageLevel.Error, It.Is<string>(s => s.Contains("Dummy Message"))));
        }

        [TestMethod]
        public void DiscoverTestShouldNotifyRawMessageIfExceptionThrownOnMessageReceived()
        {
            this.mockDataSerializer.Setup(ds => ds.DeserializeMessage(It.IsAny<string>())).Callback(() => throw new Exception("Dummy Message"));
            this.SetupFakeCommunicationChannel();

            this.testRequestSender.DiscoverTests(new DiscoveryCriteria(), this.mockDiscoveryEventsHandler.Object);

            this.RaiseMessageReceivedEvent();
            this.mockDataSerializer.Verify(ds => ds.SerializePayload(MessageType.TestMessage, It.IsAny<TestMessagePayload>()), Times.Once);
            this.mockDiscoveryEventsHandler.Verify(eh => eh.HandleRawMessage(It.IsAny<string>()));
        }

        [TestMethod]
        public void DiscoverTestShouldNotifyHandleDiscoveryCompleteIfExceptionThrownOnMessageReceived()
        {
            this.mockDataSerializer.Setup(ds => ds.DeserializeMessage(It.IsAny<string>())).Callback(() => throw new Exception("Dummy Message"));
            this.SetupFakeCommunicationChannel();

            this.testRequestSender.DiscoverTests(new DiscoveryCriteria(), this.mockDiscoveryEventsHandler.Object);

            this.RaiseMessageReceivedEvent();
            this.mockDiscoveryEventsHandler.Verify(eh => eh.HandleDiscoveryComplete(-1, null, true));
        }

        #endregion

        #region Execution Protocol Tests

        [TestMethod]
        public void InitializeExecutionShouldSendCommunicationMessageWithCorrectParameters()
        {
            this.SetupFakeCommunicationChannel();

            this.testRequestSender.InitializeExecution(this.pathToAdditionalExtensions, true);

            this.mockDataSerializer.Verify(d => d.SerializePayload(MessageType.ExecutionInitialize, this.pathToAdditionalExtensions), Times.Once);
            this.mockChannel.Verify(mc => mc.Send(It.IsAny<string>()), Times.Once);
        }

        [TestMethod]
        public void StartTestRunShouldSendStartTestExecutionWithSourcesOnChannel()
        {
            this.SetupFakeCommunicationChannel();

            this.testRequestSender.StartTestRun(this.testRunCriteriaWithSources, this.mockExecutionEventsHandler.Object);

            this.mockDataSerializer.Verify(d => d.SerializePayload(MessageType.StartTestExecutionWithSources, this.testRunCriteriaWithSources), Times.Once);
            this.mockChannel.Verify(mc => mc.Send(It.IsAny<string>()), Times.Once);
        }

        [TestMethod]
        public void StartTestRunWithTestsShouldSendStartTestExecutionWithTestsOnChannel()
        {
            var runCriteria = new TestRunCriteriaWithTests(new TestCase[2], "runsettings", null);
            this.SetupFakeCommunicationChannel();

            this.testRequestSender.StartTestRun(runCriteria, this.mockExecutionEventsHandler.Object);

            this.mockDataSerializer.Verify(d => d.SerializePayload(MessageType.StartTestExecutionWithTests, runCriteria), Times.Once);
            this.mockChannel.Verify(mc => mc.Send(It.IsAny<string>()), Times.Once);
        }

        [TestMethod]
        public void StartTestRunShouldNotifyRawMessageOnMessageReceived()
        {
            this.SetupFakeCommunicationChannel();

            this.testRequestSender.StartTestRun(this.testRunCriteriaWithSources, this.mockExecutionEventsHandler.Object);

            this.mockDataSerializer.Verify(d => d.SerializePayload(MessageType.StartTestExecutionWithSources, this.testRunCriteriaWithSources), Times.Once);
            this.mockChannel.Verify(mc => mc.Send(It.IsAny<string>()), Times.Once);
        }

        [TestMethod]
        public void StartTestRunShouldNotifyTestRunStatsOnMessageReceived()
        {
            var testRunChangedArgs = new TestRunChangedEventArgs(
                null,
                new Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult[2],
                new TestCase[2]);
            this.SetupDeserializeMessage(MessageType.TestRunStatsChange, testRunChangedArgs);
            this.SetupFakeCommunicationChannel();

            this.testRequestSender.StartTestRun(this.testRunCriteriaWithSources, this.mockExecutionEventsHandler.Object);

            this.RaiseMessageReceivedEvent();
            this.mockExecutionEventsHandler.Verify(eh => eh.HandleTestRunStatsChange(testRunChangedArgs), Times.Once);
        }

        [TestMethod]
        public void StartTestRunShouldNotifyExecutionCompleteOnMessageReceived()
        {
            var testRunCompletePayload = new TestRunCompletePayload
            {
                TestRunCompleteArgs = new TestRunCompleteEventArgs(null, false, false, null, null, TimeSpan.MaxValue),
                LastRunTests = new TestRunChangedEventArgs(null, null, null),
                RunAttachments = new List<AttachmentSet>()
            };
            this.SetupDeserializeMessage(MessageType.ExecutionComplete, testRunCompletePayload);
            this.SetupFakeCommunicationChannel();

            this.testRequestSender.StartTestRun(this.testRunCriteriaWithSources, this.mockExecutionEventsHandler.Object);

            this.RaiseMessageReceivedEvent();
            this.mockExecutionEventsHandler.Verify(
                eh => eh.HandleTestRunComplete(
                    testRunCompletePayload.TestRunCompleteArgs,
                    testRunCompletePayload.LastRunTests,
                    testRunCompletePayload.RunAttachments,
                    It.IsAny<ICollection<string>>()),
                Times.Once);
        }

        [TestMethod]
        public void StartTestRunShouldNotifyTestMessageOnMessageReceived()
        {
            var testMessagePayload = new TestMessagePayload { MessageLevel = TestMessageLevel.Error, Message = "Dummy" };
            this.SetupDeserializeMessage(MessageType.TestMessage, testMessagePayload);
            this.SetupFakeCommunicationChannel();

            this.testRequestSender.StartTestRun(this.testRunCriteriaWithSources, this.mockExecutionEventsHandler.Object);

            this.RaiseMessageReceivedEvent();
            this.mockExecutionEventsHandler.Verify(eh => eh.HandleLogMessage(TestMessageLevel.Error, "Dummy"), Times.Once);
        }

        [TestMethod]
        public void StartTestRunShouldNotifyLaunchWithDebuggerOnMessageReceived()
        {
            var launchMessagePayload = new TestProcessStartInfo();
            this.SetupDeserializeMessage(MessageType.LaunchAdapterProcessWithDebuggerAttached, launchMessagePayload);
            this.SetupFakeCommunicationChannel();

            this.testRequestSender.StartTestRun(this.testRunCriteriaWithSources, this.mockExecutionEventsHandler.Object);

            this.RaiseMessageReceivedEvent();
            this.mockExecutionEventsHandler.Verify(eh => eh.LaunchProcessWithDebuggerAttached(launchMessagePayload), Times.Once);
        }

        [TestMethod]
        public void StartTestRunShouldSendLaunchDebuggerAttachedCallbackOnMessageReceived()
        {
            var launchMessagePayload = new TestProcessStartInfo();
            this.SetupDeserializeMessage(MessageType.LaunchAdapterProcessWithDebuggerAttached, launchMessagePayload);
            this.SetupFakeCommunicationChannel();

            this.testRequestSender.StartTestRun(this.testRunCriteriaWithSources, this.mockExecutionEventsHandler.Object);

            this.RaiseMessageReceivedEvent();
            this.mockDataSerializer.Verify(ds => ds.SerializePayload(MessageType.LaunchAdapterProcessWithDebuggerAttachedCallback, It.IsAny<int>()), Times.Once);
            this.mockChannel.Verify(c => c.Send(It.IsAny<string>()), Times.AtLeastOnce);
        }

        [TestMethod]
        public void StartTestRunShouldNotifyErrorLogMessageOnMessageReceivedIfExceptionIsThrown()
        {
            this.SetupExceptionOnMessageReceived();
            this.SetupFakeCommunicationChannel();

            this.testRequestSender.StartTestRun(this.testRunCriteriaWithSources, this.mockExecutionEventsHandler.Object);

            this.RaiseMessageReceivedEvent();
            this.mockExecutionEventsHandler.Verify(eh => eh.HandleLogMessage(TestMessageLevel.Error, It.Is<string>(s => s.Contains("Dummy Message"))), Times.Once);
            this.mockExecutionEventsHandler.Verify(eh => eh.HandleRawMessage("SerializedMessage"), Times.Once);
        }

        [TestMethod]
        public void StartTestRunShouldNotifyExecutionCompleteOnMessageReceivedIfExceptionIsThrown()
        {
            this.SetupExceptionOnMessageReceived();
            this.SetupFakeCommunicationChannel();

            this.testRequestSender.StartTestRun(this.testRunCriteriaWithSources, this.mockExecutionEventsHandler.Object);

            this.RaiseMessageReceivedEvent();
            this.mockExecutionEventsHandler.Verify(eh => eh.HandleTestRunComplete(It.Is<TestRunCompleteEventArgs>(t => t.IsAborted), null, null, null), Times.Once);
            this.mockExecutionEventsHandler.Verify(eh => eh.HandleRawMessage("SerializedAbortedPayload"), Times.Once);
        }

        [TestMethod]
        public void SendTestRunCancelShouldSendCancelTestRunMessage()
        {
            this.testRequestSender.SendTestRunCancel();

            this.mockDataSerializer.Verify(ds => ds.SerializeMessage(MessageType.CancelTestRun), Times.Once);
            this.mockChannel.Verify(c => c.Send(It.IsAny<string>()), Times.Once);
        }

        [TestMethod]
        public void SendTestRunAbortShouldSendAbortTestRunMessage()
        {
            this.testRequestSender.SendTestRunAbort();

            this.mockDataSerializer.Verify(ds => ds.SerializeMessage(MessageType.AbortTestRun), Times.Once);
            this.mockChannel.Verify(c => c.Send(It.IsAny<string>()), Times.Once);
        }

        [TestMethod]
        public void EndSessionShouldSendSessionEndMessage()
        {
            this.testRequestSender.EndSession();

            this.mockDataSerializer.Verify(ds => ds.SerializeMessage(MessageType.SessionEnd), Times.Once);
            this.mockChannel.Verify(c => c.Send(It.IsAny<string>()), Times.Once);
        }

        ////[TestMethod]
        ////public void StartTestRunShouldCallHandleTestRunCompleteAndHandleLogMessageOnConnectionBreak()
        ////{
        ////    this.StartTestRunErrorTestsTemplate(CommunicationUtilitiesResources.UnableToCommunicateToTestHost, (s) => { });
        ////}

        ////[TestMethod]
        ////public void StartTestRunShouldCallHandleTestRunCompleteAndHandleLogMessageOnProcessExit()
        ////{
        ////    this.StartTestRunErrorTestsTemplate("Error Message", (s) => this.testRequestSender.OnClientProcessExit(s));
        ////}

        ////[TestMethod]
        ////public void EndSessionShouldSendCorrectEventMessage()
        ////{
        ////    this.testRequestSender.EndSession();

        ////    this.mockServer.Verify(mc => mc.SendMessage(MessageType.SessionEnd), Times.Once);
        ////}

        ////[TestMethod]
        ////public void CancelTestRunSessionShouldSendCorrectEventMessage()
        ////{
        ////    this.testRequestSender.SendTestRunCancel();

        ////    this.mockServer.Verify(mc => mc.SendMessage(MessageType.CancelTestRun), Times.Once);
        ////}

        ////private void SetupReceiveRawMessageAsyncAndDeserializeMessageAndInitialize(string rawMessage, Message message)
        ////{
        ////    this.testRequestSender.InitializeCommunication();
        ////    this.mockServer.Setup(mc => mc.ReceiveRawMessageAsync(It.IsAny<CancellationToken>())).Returns(Task.FromResult(rawMessage));
        ////    this.mockDataSerializer.Setup(ds => ds.DeserializeMessage(rawMessage)).Returns(message);
        ////}

        ////private void StartTestRunErrorTestsTemplate(string errorMessage, Action<string> onClientProcessExitCallback)
        ////{
        ////    var mockHandler = new Mock<ITestRunEventsHandler>();
        ////    var runCriteria = new TestRunCriteriaWithSources(null, null, null);
        ////    this.mockServer.Setup(mc => mc.ReceiveRawMessageAsync(It.IsAny<CancellationToken>()))
        ////        .Callback(() => onClientProcessExitCallback(errorMessage)).Returns(Task.FromResult((string)null));
        ////    string testCompleteRawMessage =
        ////        "{\"MessageType\":\"TestExecution.Completed\",\"Payload\":{\"TestRunCompleteArgs\":{\"TestRunStatistics\":null,\"IsCanceled\":false,\"IsAborted\":true,\"Error\":{\"ClassName\":\"System.IO.IOException\",\"Message\":\"Unable to read data from the transport connection: An existing connection was forcibly closed by the remote host.\",\"Data\":null,\"InnerException\":null},\"AttachmentSets\":null,\"ElapsedTimeInRunningTests\":\"00:00:00\"},\"LastRunTests\":null,\"RunAttachments\":null,\"ExecutorUris\":null}}";
        ////    this.mockDataSerializer.Setup(
        ////            md => md.SerializePayload(MessageType.ExecutionComplete, It.IsAny<TestRunCompletePayload>()))
        ////        .Returns(testCompleteRawMessage);
        ////    var waitHandle = new AutoResetEvent(false);
        ////    mockHandler.Setup(mh => mh.HandleTestRunComplete(It.IsAny<TestRunCompleteEventArgs>(), null, null, null)).Callback(() => waitHandle.Set());

        ////    this.testRequestSender.InitializeCommunication();
        ////    this.testRequestSender.StartTestRun(runCriteria, mockHandler.Object);
        ////    waitHandle.WaitOne();
        ////    this.testRequestSender.EndSession();

        ////    mockHandler.Verify(mh => mh.HandleLogMessage(TestMessageLevel.Error, string.Format(CommunicationUtilitiesResources.AbortedTestRun, errorMessage)), Times.Once);
        ////    mockHandler.Verify(mh => mh.HandleTestRunComplete(It.IsAny<TestRunCompleteEventArgs>(), null, null, null), Times.Once);
        ////    mockHandler.Verify(mh => mh.HandleRawMessage(testCompleteRawMessage), Times.Once);
        ////    this.mockServer.Verify(mc => mc.SendMessage(MessageType.SessionEnd), Times.Never);
        ////}

        #endregion

        private string SetupFakeCommunicationChannel(string connectionArgs = "123")
        {
            // Setup mock connected event and initialize communication channel
            this.mockServer.Setup(mc => mc.Start())
                .Returns(connectionArgs)
                .Callback(() => this.mockServer.Raise(s => s.ClientConnected += null, this.mockServer.Object, this.connectedEventArgs));

            return this.testRequestSender.InitializeCommunication().ToString();
        }

        private void RaiseMessageReceivedEvent()
        {
            this.mockChannel.Raise(
                c => c.MessageReceived += null,
                this.mockChannel.Object,
                new MessageReceivedEventArgs { Data = "DummyData" });
        }

        private void SetupDeserializeMessage<TPayload>(string messageType, TPayload payload)
        {
            this.mockDataSerializer.Setup(ds => ds.DeserializeMessage(It.IsAny<string>()))
                .Returns(new Message { MessageType = messageType });
            this.mockDataSerializer.Setup(ds => ds.DeserializePayload<TPayload>(It.IsAny<Message>()))
                .Returns(payload);
        }

        private void SetupExceptionOnMessageReceived()
        {
            // Serialize the exception message
            this.mockDataSerializer
                .Setup(ds => ds.SerializePayload(MessageType.TestMessage, It.Is<TestMessagePayload>(p => p.Message.Contains("Dummy Message"))))
                .Returns("SerializedMessage");

            // Serialize the execution complete
            this.mockDataSerializer
                .Setup(ds => ds.SerializePayload(MessageType.ExecutionComplete, It.Is<TestRunCompletePayload>(p => p.TestRunCompleteArgs.IsAborted)))
                .Returns("SerializedAbortedPayload");

            this.mockDataSerializer.Setup(ds => ds.DeserializeMessage(It.IsAny<string>()))
                .Callback(() => throw new Exception("Dummy Message"));
        }

        private class TestableTestRequestSender : TestRequestSender2
        {
            public TestableTestRequestSender(ICommunicationServer server, IDataSerializer serializer)
                : base(server, serializer)
            {
            }
        }
    }
}
