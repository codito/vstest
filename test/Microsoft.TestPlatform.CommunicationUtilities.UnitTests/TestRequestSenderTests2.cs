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

        public TestRequestSenderTests2()
        {
            this.mockChannel = new Mock<ICommunicationChannel>();
            this.mockServer = new Mock<ICommunicationServer>();
            this.mockDataSerializer = new Mock<IDataSerializer>();
            this.testRequestSender = new TestableTestRequestSender(this.mockServer.Object, this.mockDataSerializer.Object);

            this.connectedEventArgs = new ConnectedEventArgs(this.mockChannel.Object);
            this.mockDiscoveryEventsHandler = new Mock<ITestDiscoveryEventsHandler>();
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
            this.SetupDeserializeMessage(MessageType.TestCasesFound, new TestCase[2]);
            this.SetupFakeCommunicationChannel();

            this.testRequestSender.DiscoverTests(new DiscoveryCriteria(), this.mockDiscoveryEventsHandler.Object);

            this.RaiseMessageReceivedEvent();
            this.mockDiscoveryEventsHandler.Verify(eh => eh.HandleDiscoveredTests(It.Is<IEnumerable<TestCase>>(t => t.Count() == 2)));

            ////var sources = new List<string> { "Hello", "World" };
            ////string settingsXml = "SettingsXml";
            ////var mockHandler = new Mock<ITestDiscoveryEventsHandler>();
            ////var discoveryCriteria = new DiscoveryCriteria(sources, 100, settingsXml);

            ////var testCases = new List<TestCase> { new TestCase("x.y.z", new Uri("x://y"), "x.dll") };
            ////var rawMessage = "OnDiscoveredTests";
            ////var message = new Message { MessageType = MessageType.TestCasesFound, Payload = null };

            ////this.SetupReceiveRawMessageAsyncAndDeserializeMessageAndInitialize(rawMessage, message);
            ////this.mockDataSerializer.Setup(ds => ds.DeserializePayload<IEnumerable<TestCase>>(message)).Returns(testCases);

            ////var completePayload = new DiscoveryCompletePayload
            ////{
            ////    IsAborted = false,
            ////    LastDiscoveredTests = null,
            ////    TotalTests = 1
            ////};
            ////var completeMessage = new Message { MessageType = MessageType.DiscoveryComplete, Payload = null };
            ////mockHandler.Setup(mh => mh.HandleDiscoveredTests(testCases)).Callback(
            ////    () =>
            ////    {
            ////        this.mockDataSerializer.Setup(ds => ds.DeserializeMessage(It.IsAny<string>())).Returns(completeMessage);
            ////        this.mockDataSerializer.Setup(ds => ds.DeserializePayload<DiscoveryCompletePayload>(completeMessage)).Returns(completePayload);
            ////    });

            ////this.testRequestSender.DiscoverTests(discoveryCriteria, mockHandler.Object);

            ////this.mockServer.Verify(mc => mc.SendMessage(MessageType.StartDiscovery, discoveryCriteria), Times.Once);
            ////this.mockDataSerializer.Verify(ds => ds.DeserializeMessage(rawMessage), Times.Exactly(2));
            ////mockHandler.Verify(mh => mh.HandleDiscoveredTests(testCases), Times.Once);
            ////mockHandler.Verify(mh => mh.HandleRawMessage(rawMessage), Times.Exactly(2));
        }

        ////[TestMethod]
        ////public void DiscoverTestsShouldCallHandleLogMessageOnTestMessage()
        ////{
        ////    var sources = new List<string>() { "Hello", "World" };
        ////    string settingsXml = "SettingsXml";
        ////    var mockHandler = new Mock<ITestDiscoveryEventsHandler>();
        ////    var discoveryCriteria = new DiscoveryCriteria(sources, 100, settingsXml);

        ////    var rawMessage = "TestMessage";
        ////    var messagePayload = new TestMessagePayload() { MessageLevel = TestMessageLevel.Error, Message = rawMessage };
        ////    var message = new Message() { MessageType = MessageType.TestMessage, Payload = null };

        ////    this.SetupReceiveRawMessageAsyncAndDeserializeMessageAndInitialize(rawMessage, message);
        ////    this.mockDataSerializer.Setup(ds => ds.DeserializePayload<TestMessagePayload>(message)).Returns(messagePayload);

        ////    var completePayload = new DiscoveryCompletePayload()
        ////    {
        ////        IsAborted = false,
        ////        LastDiscoveredTests = null,
        ////        TotalTests = 1
        ////    };
        ////    var completeMessage = new Message() { MessageType = MessageType.DiscoveryComplete, Payload = null };
        ////    mockHandler.Setup(mh => mh.HandleLogMessage(TestMessageLevel.Error, rawMessage)).Callback(
        ////        () =>
        ////        {
        ////            this.mockDataSerializer.Setup(ds => ds.DeserializeMessage(It.IsAny<string>())).Returns(completeMessage);
        ////            this.mockDataSerializer.Setup(ds => ds.DeserializePayload<DiscoveryCompletePayload>(completeMessage)).Returns(completePayload);
        ////        });

        ////    this.testRequestSender.DiscoverTests(discoveryCriteria, mockHandler.Object);

        ////    this.mockServer.Verify(mc => mc.SendMessage(MessageType.StartDiscovery, discoveryCriteria), Times.Once);
        ////    this.mockDataSerializer.Verify(ds => ds.DeserializeMessage(rawMessage), Times.Exactly(2));
        ////    mockHandler.Verify(mh => mh.HandleLogMessage(TestMessageLevel.Error, rawMessage), Times.Once);
        ////    mockHandler.Verify(mh => mh.HandleRawMessage(rawMessage), Times.Exactly(2));
        ////}

        ////[TestMethod]
        ////public void DiscoverTestsShouldCallHandleDiscoveryCompleteOnDiscoveryCompletion()
        ////{
        ////    var sources = new List<string>() { "Hello", "World" };
        ////    string settingsXml = "SettingsXml";
        ////    var mockHandler = new Mock<ITestDiscoveryEventsHandler>();
        ////    var discoveryCriteria = new DiscoveryCriteria(sources, 100, settingsXml);

        ////    var rawMessage = "RunComplete";
        ////    var completePayload = new DiscoveryCompletePayload()
        ////    {
        ////        IsAborted = false,
        ////        LastDiscoveredTests = null,
        ////        TotalTests = 1
        ////    };
        ////    var message = new Message() { MessageType = MessageType.DiscoveryComplete, Payload = null };

        ////    this.SetupReceiveRawMessageAsyncAndDeserializeMessageAndInitialize(rawMessage, message);
        ////    this.mockDataSerializer.Setup(ds => ds.DeserializePayload<DiscoveryCompletePayload>(message)).Returns(completePayload);

        ////    this.testRequestSender.DiscoverTests(discoveryCriteria, mockHandler.Object);

        ////    this.mockServer.Verify(mc => mc.SendMessage(MessageType.StartDiscovery, discoveryCriteria), Times.Once);
        ////    this.mockDataSerializer.Verify(ds => ds.DeserializeMessage(rawMessage), Times.Once);
        ////    mockHandler.Verify(mh => mh.HandleDiscoveryComplete(1, null, false), Times.Once);
        ////    mockHandler.Verify(mh => mh.HandleRawMessage(rawMessage), Times.Once);
        ////}

        ////[TestMethod]
        ////public void DiscoverTestsShouldHandleExceptionOnSendMessage()
        ////{
        ////    var sources = new List<string>() { "Hello", "World" };
        ////    string settingsXml = "SettingsXml";
        ////    var mockHandler = new Mock<ITestDiscoveryEventsHandler>();
        ////    var discoveryCriteria = new DiscoveryCriteria(sources, 100, settingsXml);
        ////    var exception = new Exception();
        ////    this.mockServer.Setup(cm => cm.SendMessage(MessageType.StartDiscovery, discoveryCriteria))
        ////        .Throws(exception);

        ////    this.testRequestSender.DiscoverTests(discoveryCriteria, mockHandler.Object);

        ////    mockHandler.Verify(mh => mh.HandleDiscoveryComplete(-1, null, true), Times.Once);
        ////    mockHandler.Verify(mh => mh.HandleLogMessage(TestMessageLevel.Error, It.IsAny<string>()), Times.Once);
        ////    mockHandler.Verify(mh => mh.HandleRawMessage(It.IsAny<string>()), Times.Exactly(2));
        ////}

        ////[TestMethod]
        ////public void DiscoverTestsShouldHandleDiscoveryCompleteOnCommunicationFailure()
        ////{
        ////    this.DiscoverTestsErrorScenarioTestTemplates(CommunicationUtilitiesResources.UnableToCommunicateToTestHost, (s) => { });
        ////}

        ////[TestMethod]
        ////public void DiscoverTestsShouldHandleDiscoveryCompleteOnProcessExit()
        ////{
        ////    this.DiscoverTestsErrorScenarioTestTemplates("Error Message", (s) => this.testRequestSender.OnClientProcessExit(s));
        ////}

        ////public void DiscoverTestsErrorScenarioTestTemplates(string errorMessage, Action<string> exitCallback)
        ////{
        ////    var sources = new List<string>() { "Hello", "World" };
        ////    string settingsXml = "SettingsXml";
        ////    var mockHandler = new Mock<ITestDiscoveryEventsHandler>();
        ////    var discoveryCriteria = new DiscoveryCriteria(sources, 100, settingsXml);
        ////    this.mockServer.Setup(cm => cm.ReceiveRawMessageAsync(It.IsAny<CancellationToken>())).
        ////        Returns(Task.FromResult((string)null)).Callback(() => exitCallback(errorMessage));

        ////    this.testRequestSender.InitializeCommunication();
        ////    this.testRequestSender.DiscoverTests(discoveryCriteria, mockHandler.Object);

        ////    mockHandler.Verify(mh => mh.HandleDiscoveryComplete(-1, null, true), Times.Once);
        ////    mockHandler.Verify(mh => mh.HandleLogMessage(TestMessageLevel.Error, string.Format(CommunicationUtilitiesResources.AbortedTestDiscovery, errorMessage)), Times.Once);
        ////    mockHandler.Verify(mh => mh.HandleRawMessage(It.IsAny<string>()), Times.Exactly(2));
        ////}
        #endregion

        [TestMethod]
        public void InitializeExecutionShouldSendCommunicationMessageWithCorrectParameters()
        {
            this.SetupFakeCommunicationChannel();

            this.testRequestSender.InitializeExecution(this.pathToAdditionalExtensions, true);

            this.mockDataSerializer.Verify(d => d.SerializePayload(MessageType.ExecutionInitialize, this.pathToAdditionalExtensions), Times.Once);
            this.mockChannel.Verify(mc => mc.Send(It.IsAny<string>()), Times.Once);
        }

        ////[TestMethod]
        ////public void StartTestRunWithSourcesShouldCallHandleTestRunStatsChange()
        ////{
        ////    var mockHandler = new Mock<ITestRunEventsHandler>();
        ////    var runCriteria = new TestRunCriteriaWithSources(null, null, null);

        ////    var testRunChangedArgs = new TestRunChangedEventArgs(null, null, null);
        ////    var rawMessage = "OnTestRunStatsChange";
        ////    var message = new Message() { MessageType = MessageType.TestRunStatsChange, Payload = null };

        ////    this.SetupReceiveRawMessageAsyncAndDeserializeMessageAndInitialize(rawMessage, message);
        ////    this.mockDataSerializer.Setup(ds => ds.DeserializePayload<TestRunChangedEventArgs>(message)).Returns(testRunChangedArgs);

        ////    var completePayload = new TestRunCompletePayload()
        ////    {
        ////        ExecutorUris = null,
        ////        LastRunTests = null,
        ////        RunAttachments = null,
        ////        TestRunCompleteArgs = null
        ////    };
        ////    var completeMessage = new Message() { MessageType = MessageType.ExecutionComplete, Payload = null };
        ////    mockHandler.Setup(mh => mh.HandleTestRunStatsChange(testRunChangedArgs)).Callback(
        ////        () =>
        ////        {
        ////            this.mockDataSerializer.Setup(ds => ds.DeserializeMessage(It.IsAny<string>())).Returns(completeMessage);
        ////            this.mockDataSerializer.Setup(ds => ds.DeserializePayload<TestRunCompletePayload>(completeMessage)).Returns(completePayload);
        ////        });

        ////    var waitHandle = new AutoResetEvent(false);
        ////    mockHandler.Setup(mh => mh.HandleTestRunComplete(
        ////        It.IsAny<TestRunCompleteEventArgs>(),
        ////        It.IsAny<TestRunChangedEventArgs>(),
        ////        It.IsAny<ICollection<AttachmentSet>>(),
        ////        It.IsAny<ICollection<string>>())).Callback(() => waitHandle.Set());

        ////    this.testRequestSender.StartTestRun(runCriteria, mockHandler.Object);

        ////    waitHandle.WaitOne();

        ////    this.mockServer.Verify(mc => mc.SendMessage(MessageType.StartTestExecutionWithSources, runCriteria), Times.Once);

        ////    // One for run stats and another for runcomplete
        ////    this.mockDataSerializer.Verify(ds => ds.DeserializeMessage(rawMessage), Times.Exactly(2));
        ////    mockHandler.Verify(mh => mh.HandleTestRunStatsChange(testRunChangedArgs), Times.Once);
        ////    mockHandler.Verify(mh => mh.HandleRawMessage(rawMessage), Times.Exactly(2));
        ////}

        ////[TestMethod]
        ////public void StartTestRunWithTestsShouldCallHandleTestRunStatsChange()
        ////{
        ////    var mockHandler = new Mock<ITestRunEventsHandler>();
        ////    var runCriteria = new TestRunCriteriaWithTests(null, null, null);

        ////    var testRunChangedArgs = new TestRunChangedEventArgs(null, null, null);
        ////    var rawMessage = "OnTestRunStatsChange";
        ////    var message = new Message() { MessageType = MessageType.TestRunStatsChange, Payload = null };

        ////    this.SetupReceiveRawMessageAsyncAndDeserializeMessageAndInitialize(rawMessage, message);
        ////    this.mockDataSerializer.Setup(ds => ds.DeserializePayload<TestRunChangedEventArgs>(message)).Returns(testRunChangedArgs);

        ////    var completePayload = new TestRunCompletePayload()
        ////    {
        ////        ExecutorUris = null,
        ////        LastRunTests = null,
        ////        RunAttachments = null,
        ////        TestRunCompleteArgs = null
        ////    };
        ////    var completeMessage = new Message() { MessageType = MessageType.ExecutionComplete, Payload = null };
        ////    var waitHandle = new AutoResetEvent(false);
        ////    mockHandler.Setup(mh => mh.HandleTestRunStatsChange(testRunChangedArgs)).Callback(
        ////        () =>
        ////        {
        ////            this.mockDataSerializer.Setup(ds => ds.DeserializeMessage(It.IsAny<string>())).Returns(completeMessage);
        ////            this.mockDataSerializer.Setup(ds => ds.DeserializePayload<TestRunCompletePayload>(completeMessage)).Returns(completePayload);
        ////            waitHandle.Set();
        ////        });

        ////    this.testRequestSender.StartTestRun(runCriteria, mockHandler.Object);

        ////    waitHandle.WaitOne();
        ////    this.mockServer.Verify(mc => mc.SendMessage(MessageType.StartTestExecutionWithTests, runCriteria), Times.Once);
        ////    mockHandler.Verify(mh => mh.HandleTestRunStatsChange(testRunChangedArgs), Times.Once);
        ////    mockHandler.Verify(mh => mh.HandleRawMessage(rawMessage), Times.AtLeastOnce);
        ////}

        ////[TestMethod]
        ////public void StartTestRunShouldCallHandleLogMessageOnTestMessage()
        ////{
        ////    var mockHandler = new Mock<ITestRunEventsHandler>();
        ////    var runCriteria = new TestRunCriteriaWithSources(null, null, null);

        ////    var rawMessage = "OnLogMessage";
        ////    var message = new Message() { MessageType = MessageType.TestMessage, Payload = null };
        ////    var payload = new TestMessagePayload() { MessageLevel = TestMessageLevel.Error, Message = rawMessage };

        ////    this.SetupReceiveRawMessageAsyncAndDeserializeMessageAndInitialize(rawMessage, message);
        ////    this.mockDataSerializer.Setup(ds => ds.DeserializePayload<TestMessagePayload>(message)).Returns(payload);

        ////    var completePayload = new TestRunCompletePayload()
        ////    {
        ////        ExecutorUris = null, LastRunTests = null, RunAttachments = null, TestRunCompleteArgs = null
        ////    };
        ////    var completeMessage = new Message() { MessageType = MessageType.ExecutionComplete, Payload = null };
        ////    var waitHandle = new AutoResetEvent(false);
        ////    mockHandler.Setup(mh => mh.HandleLogMessage(TestMessageLevel.Error, rawMessage)).Callback(
        ////        () =>
        ////        {
        ////            this.mockDataSerializer.Setup(ds => ds.DeserializeMessage(It.IsAny<string>())).Returns(completeMessage);
        ////            this.mockDataSerializer.Setup(ds => ds.DeserializePayload<TestRunCompletePayload>(completeMessage)).Returns(completePayload);
        ////            waitHandle.Set();
        ////        });

        ////    this.testRequestSender.StartTestRun(runCriteria, mockHandler.Object);
        ////    waitHandle.WaitOne();

        ////    this.mockServer.Verify(mc => mc.SendMessage(MessageType.StartTestExecutionWithSources, runCriteria), Times.Once);
        ////    this.mockDataSerializer.Verify(ds => ds.DeserializeMessage(It.IsAny<string>()), Times.Exactly(2));
        ////    mockHandler.Verify(mh => mh.HandleLogMessage(payload.MessageLevel, payload.Message), Times.Once);
        ////    mockHandler.Verify(mh => mh.HandleRawMessage(rawMessage), Times.AtLeastOnce);
        ////}

        ////[TestMethod]
        ////public void StartTestRunShouldCallLaunchProcessWithDebuggerAndWaitForCallback()
        ////{
        ////    var mockHandler = new Mock<ITestRunEventsHandler>();
        ////    var runCriteria = new TestRunCriteriaWithSources(null, null, null);

        ////    var rawMessage = "LaunchProcessWithDebugger";
        ////    var message = new Message() { MessageType = MessageType.LaunchAdapterProcessWithDebuggerAttached, Payload = null };
        ////    var payload = new TestProcessStartInfo();

        ////    this.SetupReceiveRawMessageAsyncAndDeserializeMessageAndInitialize(rawMessage, message);
        ////    this.mockDataSerializer.Setup(ds => ds.DeserializePayload<TestProcessStartInfo>(message)).Returns(payload);

        ////    var completePayload = new TestRunCompletePayload()
        ////    {
        ////        ExecutorUris = null,
        ////        LastRunTests = null,
        ////        RunAttachments = null,
        ////        TestRunCompleteArgs = null
        ////    };
        ////    var completeMessage = new Message() { MessageType = MessageType.ExecutionComplete, Payload = null };
        ////    mockHandler.Setup(mh => mh.LaunchProcessWithDebuggerAttached(payload)).Callback(
        ////        () =>
        ////        {
        ////            this.mockDataSerializer.Setup(ds => ds.DeserializeMessage(It.IsAny<string>())).Returns(completeMessage);
        ////            this.mockDataSerializer.Setup(ds => ds.DeserializePayload<TestRunCompletePayload>(completeMessage)).Returns(completePayload);
        ////        });

        ////    var waitHandle = new AutoResetEvent(false);
        ////    mockHandler.Setup(mh => mh.HandleTestRunComplete(
        ////        It.IsAny<TestRunCompleteEventArgs>(),
        ////        It.IsAny<TestRunChangedEventArgs>(),
        ////        It.IsAny<ICollection<AttachmentSet>>(),
        ////        It.IsAny<ICollection<string>>())).Callback(() => waitHandle.Set());

        ////    this.testRequestSender.StartTestRun(runCriteria, mockHandler.Object);

        ////    waitHandle.WaitOne();

        ////    this.mockServer.Verify(mc => mc.SendMessage(MessageType.StartTestExecutionWithSources, runCriteria), Times.Once);

        ////    this.mockDataSerializer.Verify(ds => ds.DeserializeMessage(It.IsAny<string>()), Times.Exactly(2));
        ////    mockHandler.Verify(mh => mh.LaunchProcessWithDebuggerAttached(payload), Times.Once);
        ////    mockHandler.Verify(mh => mh.HandleRawMessage(rawMessage), Times.Exactly(2));
        ////    this.mockServer.Verify(mc => mc.SendMessage(MessageType.LaunchAdapterProcessWithDebuggerAttachedCallback, It.IsAny<object>()), Times.Once);
        ////}

        ////[TestMethod]
        ////public void StartTestRunShouldCallHandleTestRunCompleteOnRunCompletion()
        ////{
        ////    var mockHandler = new Mock<ITestRunEventsHandler>();
        ////    var runCriteria = new TestRunCriteriaWithTests(null, null, null);

        ////    var rawMessage = "ExecComplete";
        ////    var message = new Message() { MessageType = MessageType.ExecutionComplete, Payload = null };
        ////    var payload = new TestRunCompletePayload() { ExecutorUris = null, LastRunTests = null, RunAttachments = null, TestRunCompleteArgs = null };

        ////    this.SetupReceiveRawMessageAsyncAndDeserializeMessageAndInitialize(rawMessage, message);
        ////    this.mockDataSerializer.Setup(ds => ds.DeserializePayload<TestRunCompletePayload>(message)).Returns(payload);

        ////    var waitHandle = new AutoResetEvent(false);
        ////    mockHandler.Setup(mh => mh.HandleTestRunComplete(
        ////        It.IsAny<TestRunCompleteEventArgs>(),
        ////        It.IsAny<TestRunChangedEventArgs>(),
        ////        It.IsAny<ICollection<AttachmentSet>>(),
        ////        It.IsAny<ICollection<string>>())).Callback(() => waitHandle.Set());

        ////    this.testRequestSender.StartTestRun(runCriteria, mockHandler.Object);

        ////    waitHandle.WaitOne();

        ////    this.mockServer.Verify(mc => mc.SendMessage(MessageType.StartTestExecutionWithTests, runCriteria), Times.Once);
        ////    this.mockDataSerializer.Verify(ds => ds.DeserializeMessage(rawMessage), Times.Once);
        ////    mockHandler.Verify(mh => mh.HandleTestRunComplete(null, null, null, null), Times.Once);
        ////    mockHandler.Verify(mh => mh.HandleRawMessage(rawMessage), Times.Once);
        ////}

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

        private class TestableTestRequestSender : TestRequestSender2
        {
            public TestableTestRequestSender(ICommunicationServer server, IDataSerializer serializer)
                : base(server, serializer)
            {
            }
        }
    }
}