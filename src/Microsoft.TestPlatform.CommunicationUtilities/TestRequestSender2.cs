// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.CommunicationUtilities
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.Interfaces;
    using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Resources;

    using CommonResources = Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.Resources.Resources;

    /// <summary>
    /// Test request sender implementation.
    /// </summary>
    public class TestRequestSender2 : ITestRequestSender
    {
        private ICommunicationServer communicationServer;

        private IDataSerializer dataSerializer;

        private ICommunicationChannel channel;

        private ManualResetEvent connected;

        private EventHandler<MessageReceivedEventArgs> onMessageReceived;

        private Action<DisconnectedEventArgs> onDisconnected;

        // Set to 1 if Discovery/Execution is complete, i.e. complete handlers have been invoked
        private int operationCompleted;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestRequestSender2"/> class.
        /// </summary>
        public TestRequestSender2()
            : this(new SocketServer(), JsonDataSerializer.Instance)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TestRequestSender2"/> class.
        /// </summary>
        /// <param name="server">Communication server implementation.</param>
        /// <param name="serializer">Serializer implementation.</param>
        protected TestRequestSender2(ICommunicationServer server, IDataSerializer serializer)
        {
            this.communicationServer = server;
            this.dataSerializer = serializer;
            this.connected = new ManualResetEvent(false);
            this.operationCompleted = 0;
        }

        /// <inheritdoc />
        public int InitializeCommunication()
        {
            this.communicationServer.ClientConnected += (sender, args) =>
                {
                    this.channel = args.Channel;
                    this.connected.Set();
                };
            this.communicationServer.ClientDisconnected += (sender, args) =>
                {
                    // If there's an disconnected event handler, call it
                    if (this.onDisconnected != null)
                    {
                        this.onDisconnected(args);
                    }
                };

            // Server start returns the listener port
            return int.Parse(this.communicationServer.Start());
        }

        /// <inheritdoc />
        public bool WaitForRequestHandlerConnection(int connectionTimeout)
        {
            return this.connected.WaitOne(connectionTimeout);
        }

        #region Discovery Protocol

        /// <inheritdoc />
        public void InitializeDiscovery(IEnumerable<string> pathToAdditionalExtensions, bool loadOnlyWellKnownExtensions)
        {
            var message = this.dataSerializer.SerializePayload(
                MessageType.DiscoveryInitialize,
                pathToAdditionalExtensions);
            this.channel.Send(message);
        }

        /// <inheritdoc />
        public void DiscoverTests(DiscoveryCriteria discoveryCriteria, ITestDiscoveryEventsHandler discoveryEventsHandler)
        {
            this.onDisconnected = (disconnectedEventArgs) =>
                {
                    this.OnDiscoveryAbort(discoveryEventsHandler, disconnectedEventArgs.Error);
                };
            this.onMessageReceived = (sender, args) =>
                {
                    try
                    {
                        var rawMessage = args.Data;

                        // Currently each of the operations are not separate tasks since they should not each take much time. This is just a notification.
                        if (EqtTrace.IsInfoEnabled)
                        {
                            EqtTrace.Info("TestRequestSender: Received message: {0}", rawMessage);
                        }

                        // Send raw message first to unblock handlers waiting to send message to IDEs
                        discoveryEventsHandler.HandleRawMessage(rawMessage);

                        var data = this.dataSerializer.DeserializeMessage(rawMessage);
                        switch (data.MessageType)
                        {
                            case MessageType.TestCasesFound:
                                var testCases = this.dataSerializer.DeserializePayload<IEnumerable<TestCase>>(data);
                                discoveryEventsHandler.HandleDiscoveredTests(testCases);
                                break;
                            case MessageType.DiscoveryComplete:
                                var discoveryCompletePayload =
                                    this.dataSerializer.DeserializePayload<DiscoveryCompletePayload>(data);
                                discoveryEventsHandler.HandleDiscoveryComplete(
                                    discoveryCompletePayload.TotalTests,
                                    discoveryCompletePayload.LastDiscoveredTests,
                                    discoveryCompletePayload.IsAborted);

                                this.SetOperationComplete();
                                break;
                            case MessageType.TestMessage:
                                var testMessagePayload = this.dataSerializer.DeserializePayload<TestMessagePayload>(
                                    data);
                                discoveryEventsHandler.HandleLogMessage(
                                    testMessagePayload.MessageLevel,
                                    testMessagePayload.Message);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        this.OnDiscoveryAbort(discoveryEventsHandler, ex);
                    }
                };

            this.channel.MessageReceived += this.onMessageReceived;
            var message = this.dataSerializer.SerializePayload(
                MessageType.StartDiscovery,
                discoveryCriteria);
            this.channel.Send(message);
        }

        #endregion

        #region Execution Protocol

        /// <inheritdoc />
        public void InitializeExecution(IEnumerable<string> pathToAdditionalExtensions, bool loadOnlyWellKnownExtensions)
        {
            var message = this.dataSerializer.SerializePayload(
                MessageType.ExecutionInitialize,
                pathToAdditionalExtensions);
            this.channel.Send(message);
        }

        /// <inheritdoc />
        public void StartTestRun(TestRunCriteriaWithSources runCriteria, ITestRunEventsHandler eventHandler)
        {
            this.onDisconnected = (disconnectedEventArgs) =>
                {
                    this.OnTestRunAbort(eventHandler, disconnectedEventArgs.Error);
                };
            this.onMessageReceived = (sender, args) => this.OnExecutionMessageReceived(sender, args, eventHandler);
            this.channel.MessageReceived += this.onMessageReceived;

            var message = this.dataSerializer.SerializePayload(
                MessageType.StartTestExecutionWithSources,
                runCriteria);
            this.channel.Send(message);
        }

        /// <inheritdoc />
        public void StartTestRun(TestRunCriteriaWithTests runCriteria, ITestRunEventsHandler eventHandler)
        {
            this.onDisconnected = (disconnectedEventArgs) =>
                {
                    this.OnTestRunAbort(eventHandler, disconnectedEventArgs.Error);
                };
            this.onMessageReceived = (sender, args) => this.OnExecutionMessageReceived(sender, args, eventHandler);
            this.channel.MessageReceived += this.onMessageReceived;

            var message = this.dataSerializer.SerializePayload(
                MessageType.StartTestExecutionWithTests,
                runCriteria);
            this.channel.Send(message);
        }

        /// <inheritdoc />
        public void SendTestRunCancel()
        {
            this.channel.Send(this.dataSerializer.SerializeMessage(MessageType.CancelTestRun));
        }

        /// <inheritdoc />
        public void SendTestRunAbort()
        {
            this.channel.Send(this.dataSerializer.SerializeMessage(MessageType.AbortTestRun));
        }

        #endregion

        /// <inheritdoc />
        public void EndSession()
        {
            if (!this.IsOperationComplete())
            {
                this.channel.Send(this.dataSerializer.SerializeMessage(MessageType.SessionEnd));
            }
        }

        /// <inheritdoc />
        public void OnClientProcessExit(string stdError)
        {
            // This method is called on test host exit. If test host has any errors, stdError is not
            // empty. In case of test host crash, there is a race condition between
            // ClientDisconnected and process exit. We're sending a message to event handlers if
            // stdErr is not empty. Ideally, "whether a message is an error" should be provided to
            // this API.
        }

        /// <inheritdoc />
        public void Close()
        {
            this.Dispose();
            EqtTrace.Info("Closing the connection");
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (this.channel != null)
            {
                this.channel.MessageReceived -= this.onMessageReceived;
            }

            this.communicationServer.Stop();
        }

        private void OnExecutionMessageReceived(object sender, MessageReceivedEventArgs messageReceived, ITestRunEventsHandler testRunEventsHandler)
        {
            try
            {
                var rawMessage = messageReceived.Data;

                // Send raw message first to unblock handlers waiting to send message to IDEs
                testRunEventsHandler.HandleRawMessage(rawMessage);

                var message = this.dataSerializer.DeserializeMessage(rawMessage);
                switch (message.MessageType)
                {
                    case MessageType.TestRunStatsChange:
                        var testRunChangedArgs = this.dataSerializer.DeserializePayload<TestRunChangedEventArgs>(message);
                        testRunEventsHandler.HandleTestRunStatsChange(testRunChangedArgs);
                        break;
                    case MessageType.ExecutionComplete:
                        var testRunCompletePayload = this.dataSerializer.DeserializePayload<TestRunCompletePayload>(message);

                        testRunEventsHandler.HandleTestRunComplete(
                            testRunCompletePayload.TestRunCompleteArgs,
                            testRunCompletePayload.LastRunTests,
                            testRunCompletePayload.RunAttachments,
                            testRunCompletePayload.ExecutorUris);

                        this.SetOperationComplete();
                        break;
                    case MessageType.TestMessage:
                        var testMessagePayload = this.dataSerializer.DeserializePayload<TestMessagePayload>(message);
                        testRunEventsHandler.HandleLogMessage(testMessagePayload.MessageLevel, testMessagePayload.Message);
                        break;
                    case MessageType.LaunchAdapterProcessWithDebuggerAttached:
                        var testProcessStartInfo = this.dataSerializer.DeserializePayload<TestProcessStartInfo>(message);
                        int processId = testRunEventsHandler.LaunchProcessWithDebuggerAttached(testProcessStartInfo);

                        var data =
                            this.dataSerializer.SerializePayload(
                                MessageType.LaunchAdapterProcessWithDebuggerAttachedCallback,
                                processId);

                        this.channel.Send(data);
                        break;
                }
            }
            catch (Exception exception)
            {
                this.OnTestRunAbort(testRunEventsHandler, exception);
            }
        }

        private void OnTestRunAbort(ITestRunEventsHandler testRunEventsHandler, Exception exception)
        {
            if (this.IsOperationComplete())
            {
                return;
            }

            this.SetOperationComplete();
            EqtTrace.Error("Server: TestExecution: Aborting test run because {0}", exception);

            var reason = string.Format(CommonResources.AbortedTestRun, exception?.Message);

            // log console message to vstest console
            testRunEventsHandler.HandleLogMessage(TestMessageLevel.Error, reason);

            // log console message to vstest console wrapper
            var testMessagePayload = new TestMessagePayload { MessageLevel = TestMessageLevel.Error, Message = reason };
            var rawMessage = this.dataSerializer.SerializePayload(MessageType.TestMessage, testMessagePayload);
            testRunEventsHandler.HandleRawMessage(rawMessage);

            // notify test run abort to vstest console wrapper.
            var completeArgs = new TestRunCompleteEventArgs(null, false, true, exception, null, TimeSpan.Zero);
            var payload = new TestRunCompletePayload { TestRunCompleteArgs = completeArgs };
            rawMessage = this.dataSerializer.SerializePayload(MessageType.ExecutionComplete, payload);
            testRunEventsHandler.HandleRawMessage(rawMessage);

            // notify of a test run complete and bail out.
            testRunEventsHandler.HandleTestRunComplete(completeArgs, null, null, null);

            this.CleanupCommunicationIfProcessExit();
        }

        private void OnDiscoveryAbort(ITestDiscoveryEventsHandler eventHandler, Exception exception)
        {
            if (this.IsOperationComplete())
            {
                return;
            }

            this.SetOperationComplete();
            EqtTrace.Error("Server: TestExecution: Aborting test discovery because {0}", exception);

            var reason = string.Format(CommonResources.AbortedTestDiscovery, exception?.Message);

            // Log to vstest console
            eventHandler.HandleLogMessage(TestMessageLevel.Error, reason);

            // Log to vs ide test output
            var testMessagePayload = new TestMessagePayload { MessageLevel = TestMessageLevel.Error, Message = reason };
            var rawMessage = this.dataSerializer.SerializePayload(MessageType.TestMessage, testMessagePayload);
            eventHandler.HandleRawMessage(rawMessage);

            // Notify discovery abort to IDE test output
            var payload = new DiscoveryCompletePayload()
            {
                IsAborted = true,
                LastDiscoveredTests = null,
                TotalTests = -1
            };
            rawMessage = this.dataSerializer.SerializePayload(MessageType.DiscoveryComplete, payload);
            eventHandler.HandleRawMessage(rawMessage);

            // Complete discovery
            eventHandler.HandleDiscoveryComplete(-1, null, true);

            this.CleanupCommunicationIfProcessExit();
        }

        private bool IsOperationComplete()
        {
            return this.operationCompleted == 1;
        }

        private void SetOperationComplete()
        {
            // Complete the currently ongoing operation (Discovery/Execution)
            Interlocked.CompareExchange(ref this.operationCompleted, 1, 0);
        }

        private void CleanupCommunicationIfProcessExit()
        {
        }
    }
}
