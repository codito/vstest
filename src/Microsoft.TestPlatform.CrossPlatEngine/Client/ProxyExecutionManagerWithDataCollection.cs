// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.CrossPlatEngine.Client
{
    using System;
    using System.Collections.Generic;

    using Microsoft.VisualStudio.TestPlatform.CrossPlatEngine.DataCollection;
    using Microsoft.VisualStudio.TestPlatform.CrossPlatEngine.DataCollection.Interfaces;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Engine;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

    /// <summary>
    /// The proxy execution manager with data collection.
    /// </summary>
    internal class ProxyExecutionManagerWithDataCollection : ProxyExecutionManager
    {
        private IDictionary<string, string> dataCollectionEnvironmentVariables;
        private int dataCollectionPort;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProxyExecutionManagerWithDataCollection"/> class. 
        /// </summary>
        /// <param name="testHostManager">
        /// Test host manager for this operation.
        /// </param>
        /// <param name="proxyDataCollectionManager">
        /// The proxy Data Collection Manager.
        /// </param>
        public ProxyExecutionManagerWithDataCollection(ITestHostManager testHostManager, IProxyDataCollectionManager proxyDataCollectionManager) : base(testHostManager)
        {
            this.ProxyDataCollectionManager = proxyDataCollectionManager;
            this.DataCollectionRunEventsHandler = new DataCollectionRunEventsHandler();
        }

        /// <summary>
        /// Gets the data collection run events handler.
        /// </summary>
        internal DataCollectionRunEventsHandler DataCollectionRunEventsHandler
        {
            get; private set;
        }

        /// <summary>
        /// Gets the proxy data collection manager.
        /// </summary>
        internal IProxyDataCollectionManager ProxyDataCollectionManager
        {
            get; private set;
        }

        /// <summary>
        /// Ensure that the Execution component of engine is ready for execution usually by loading extensions.
        /// </summary>
        public override void Initialize()
        {
            try
            {
                this.ProxyDataCollectionManager.Initialize();
            }
            catch (Exception ex)
            {
                if (EqtTrace.IsErrorEnabled)
                {
                    EqtTrace.Error("ProxyExecutionManagerWithDataCollection: Error occured while communicating with DataCollection Process: {0}", ex.Message);
                }

                throw;
            }

            try
            {
                var dataCollectionParameters = this.ProxyDataCollectionManager.BeforeTestRunStart(
                                                   resetDataCollectors: true,
                                                   isRunStartingNow: true,
                                                   runEventsHandler: this.DataCollectionRunEventsHandler);

                if (dataCollectionParameters != null)
                {
                    this.dataCollectionEnvironmentVariables = dataCollectionParameters.EnvironmentVariables;
                    this.dataCollectionPort = dataCollectionParameters.DataCollectionEventsPort;
                }
            }
            catch (Exception ex)
            {
                try
                {
                    // On failure in calling BeforeTestRunStart, call AfterTestRunEnd to end DataCollectionProcess
                    this.ProxyDataCollectionManager.AfterTestRunEnd(isCanceled: true, runEventsHandler: this.DataCollectionRunEventsHandler);
                    throw;
                }
                finally
                {
                    // There is an issue with Data Collector, skipping data collection and continuing with test run.
                    if (EqtTrace.IsErrorEnabled)
                    {
                        EqtTrace.Error("ProxyExecutionManagerWithDataCollection: Error occured while communicating with DataCollection Process: {0}", ex.Message);
                    }
                }
            }

            base.Initialize();
        }

        /// <summary>
        /// Starts the test run
        /// </summary>
        /// <param name="testRunCriteria"> The settings/options for the test run. </param>
        /// <param name="eventHandler"> EventHandler for handling execution events from Engine. </param>
        /// <returns> The process id of the runner executing tests. </returns>
        public override int StartTestRun(TestRunCriteria testRunCriteria, ITestRunEventsHandler eventHandler)
        {
            var currentEventHandler = eventHandler;
            if (this.ProxyDataCollectionManager != null)
            {
                currentEventHandler = new DataCollectionTestRunEventsHandler(eventHandler, this.ProxyDataCollectionManager);
            }

            // Log all the exceptions that has occured while initializing DataCollectionClient
            if (this.DataCollectionRunEventsHandler?.ExceptionMessages?.Count > 0)
            {
                foreach (var message in this.DataCollectionRunEventsHandler.ExceptionMessages)
                {
                    currentEventHandler.HandleLogMessage(TestMessageLevel.Error, message);
                }
            }

            return base.StartTestRun(testRunCriteria, currentEventHandler);
        }

        /// <inheritdoc/>
        public override void Cancel()
        {
            try
            {
                this.ProxyDataCollectionManager.AfterTestRunEnd(isCanceled: true, runEventsHandler: this.DataCollectionRunEventsHandler);
            }
            catch (Exception ex)
            {
                if (EqtTrace.IsErrorEnabled)
                {
                    EqtTrace.Error("ProxyExecutionManagerWithDataCollection: Error occured while communicating with DataCollection Process: {0}", ex.Message);
                }

                throw;
            }
            finally
            {
                base.Cancel();
            }
        }

        /// <inheritdoc />
        protected override TestProcessStartInfo UpdateTestProcessStartInfo(TestProcessStartInfo testProcessStartInfo)
        {
            if (testProcessStartInfo.EnvironmentVariables == null)
            {
                testProcessStartInfo.EnvironmentVariables = this.dataCollectionEnvironmentVariables;
            }
            else
            {
                foreach (var kvp in this.dataCollectionEnvironmentVariables)
                {
                    testProcessStartInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
                }
            }

            testProcessStartInfo.Arguments += " --datacollectionport " + this.dataCollectionPort;

            return testProcessStartInfo;
        }
    }

    /// <summary>
    /// Handles Log events and stores them in list. Messages in the list will be emptied after test execution begins.
    /// </summary>
    internal class DataCollectionRunEventsHandler : ITestMessageEventHandler
    {
        /// <summary>
        /// The constructor.
        /// </summary>
        public DataCollectionRunEventsHandler()
        {
            this.ExceptionMessages = new List<string>();
        }

        /// <summary>
        /// Gets the exception messages.
        /// </summary>
        public List<string> ExceptionMessages { get; private set; }

        /// <summary>
        /// The handle log message.
        /// </summary>
        /// <param name="level">
        /// The level.
        /// </param>
        /// <param name="message">
        /// The message.
        /// </param>
        public void HandleLogMessage(TestMessageLevel level, string message)
        {
            this.ExceptionMessages.Add(message);
        }

        /// <summary>
        /// The handle raw message.
        /// </summary>
        /// <param name="rawMessage">
        /// The raw message.
        /// </param>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public void HandleRawMessage(string rawMessage)
        {
            throw new NotImplementedException();
        }
    }
}