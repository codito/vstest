// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace TestPlatform.CrossPlatEngine.UnitTests
{
    using System.Collections.Generic;

    using Microsoft.VisualStudio.TestPlatform.CrossPlatEngine;
    using Microsoft.VisualStudio.TestPlatform.CrossPlatEngine.Client;
    using Microsoft.VisualStudio.TestPlatform.CrossPlatEngine.Client.Parallel;
    using Microsoft.VisualStudio.TestPlatform.CrossPlatEngine.Hosting;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Engine;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Host;

    using Moq;

    [TestClass]
    public class TestEngineTests
    {
        private readonly ITestEngine testEngine;

        private Mock<ITestRuntimeProvider> mockTestHostManager;

        public TestEngineTests()
        {
            this.testEngine = new TestEngine();
            this.mockTestHostManager = new Mock<ITestRuntimeProvider>();
            
            // Default setting for host manager
            this.mockTestHostManager.Setup(p => p.Shared).Returns(true);
        }

        [TestMethod]
        public void GetDiscoveryManagerShouldReturnANonNullInstance()
        {
            var discoveryCriteria = new DiscoveryCriteria(new List<string> { "1.dll" }, 100, null);
            Assert.IsNotNull(this.testEngine.GetDiscoveryManager(this.mockTestHostManager.Object, discoveryCriteria));
        }

        [TestMethod]
        public void GetDiscoveryManagerShouldReturnsNewInstanceOfProxyDiscoveryManagerIfTestHostIsShared()
        {
            var discoveryCriteria = new DiscoveryCriteria(new List<string> { "1.dll" }, 100, null);
            var discoveryManager = this.testEngine.GetDiscoveryManager(this.mockTestHostManager.Object, discoveryCriteria);

            Assert.AreNotSame(discoveryManager, this.testEngine.GetDiscoveryManager(this.mockTestHostManager.Object, discoveryCriteria));
            Assert.IsInstanceOfType(this.testEngine.GetDiscoveryManager(this.mockTestHostManager.Object, discoveryCriteria), typeof(ProxyDiscoveryManager));
        }

        [TestMethod]
        public void GetDiscoveryManagerShouldReturnsParallelDiscoveryManagerIfTestHostIsNotShared()
        {
            var discoveryCriteria = new DiscoveryCriteria(new List<string> { "1.dll" }, 100, null);
            this.mockTestHostManager.Setup(p => p.Shared).Returns(false);
            
            Assert.IsNotNull(this.testEngine.GetDiscoveryManager(this.mockTestHostManager.Object, discoveryCriteria));
            Assert.IsInstanceOfType(this.testEngine.GetDiscoveryManager(this.mockTestHostManager.Object, discoveryCriteria), typeof(ParallelProxyDiscoveryManager));
        }

        [TestMethod]
        public void GetExecutionManagerShouldReturnANonNullInstance()
        {
            var testRunCriteria = new TestRunCriteria(new List<string> { "1.dll" }, 100);

            Assert.IsNotNull(this.testEngine.GetExecutionManager(this.mockTestHostManager.Object, testRunCriteria));
        }

        [TestMethod]
        public void GetExecutionManagerShouldReturnNewInstance()
        {
            var testRunCriteria = new TestRunCriteria(new List<string> { "1.dll" }, 100);
            var executionManager = this.testEngine.GetExecutionManager(this.mockTestHostManager.Object, testRunCriteria);

            Assert.AreNotSame(executionManager, this.testEngine.GetExecutionManager(this.mockTestHostManager.Object, testRunCriteria));
        }

        [TestMethod]
        public void GetExecutionManagerShouldReturnDefaultExecutionManagerIfParallelDisabled()
        {
            string settingXml = @"<RunSettings><RunConfiguration></RunConfiguration ></RunSettings>";
            var testRunCriteria = new TestRunCriteria(new List<string> { "1.dll" }, 100, false, settingXml);

            Assert.IsNotNull(this.testEngine.GetExecutionManager(this.mockTestHostManager.Object, testRunCriteria));
            Assert.IsInstanceOfType(this.testEngine.GetExecutionManager(this.mockTestHostManager.Object, testRunCriteria), typeof(ProxyExecutionManager));
        }

        [TestMethod]
        public void GetExecutionManagerWithSingleSourceShouldReturnDefaultExecutionManagerEvenIfParallelEnabled()
        {
            string settingXml = @"<RunSettings><RunConfiguration><MaxCpuCount>2</MaxCpuCount></RunConfiguration ></RunSettings>";
            var testRunCriteria = new TestRunCriteria(new List<string> { "1.dll" }, 100, false, settingXml);

            Assert.IsNotNull(this.testEngine.GetExecutionManager(this.mockTestHostManager.Object, testRunCriteria));
            Assert.IsInstanceOfType(this.testEngine.GetExecutionManager(this.mockTestHostManager.Object, testRunCriteria), typeof(ProxyExecutionManager));
        }

        [TestMethod]
        public void GetExecutionManagerShouldReturnParallelExecutionManagerIfParallelEnabled()
        {
            string settingXml = @"<RunSettings><RunConfiguration><MaxCpuCount>2</MaxCpuCount></RunConfiguration></RunSettings>";
            var testRunCriteria = new TestRunCriteria(new List<string> { "1.dll", "2.dll" }, 100, false, settingXml);

            Assert.IsNotNull(this.testEngine.GetExecutionManager(this.mockTestHostManager.Object, testRunCriteria));
            Assert.IsInstanceOfType(this.testEngine.GetExecutionManager(this.mockTestHostManager.Object, testRunCriteria), typeof(ParallelProxyExecutionManager));
        }

        [TestMethod]
        public void GetExecutionManagerShouldReturnParallelExecutionManagerIfHostIsNotShared()
        {
            this.mockTestHostManager.Setup(p => p.Shared).Returns(false);
            var testRunCriteria = new TestRunCriteria(new List<string> { "1.dll", "2.dll" }, 100, false, null);

            Assert.IsNotNull(this.testEngine.GetExecutionManager(this.mockTestHostManager.Object, testRunCriteria));
            Assert.IsInstanceOfType(this.testEngine.GetExecutionManager(this.mockTestHostManager.Object, testRunCriteria), typeof(ParallelProxyExecutionManager));
        }

        [TestMethod]
        public void GetExcecutionManagerShouldReturnExectuionManagerWithDataCollectionIfDataCollectionIsEnabled()
        {
            var settingXml = @"<RunSettings><DataCollectionRunSettings><DataCollectors><DataCollector friendlyName=""Code Coverage"" uri=""datacollector://Microsoft/CodeCoverage/2.0"" assemblyQualifiedName=""Microsoft.VisualStudio.Coverage.DynamicCoverageDataCollector, Microsoft.VisualStudio.TraceCollector, Version=11.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a""></DataCollector></DataCollectors></DataCollectionRunSettings></RunSettings>";
            var testRunCriteria = new TestRunCriteria(new List<string> { "1.dll" }, 100, false, settingXml);
            var result = this.testEngine.GetExecutionManager(this.mockTestHostManager.Object, testRunCriteria);

            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(ProxyExecutionManagerWithDataCollection));
        }

        [TestMethod]
        public void GetExtensionManagerShouldReturnANonNullInstance()
        {
            Assert.IsNotNull(this.testEngine.GetExtensionManager());
        }

        [TestMethod]
        public void GetDefaultTestHostManagerReturnsANonNullInstance()
        {
            var rc = new RunConfiguration() { TargetFrameworkVersion = Framework.DefaultFramework, TargetPlatform = Architecture.X86 };
            Assert.IsNotNull(this.testEngine.GetDefaultTestHostManager(rc));
        }

        [TestMethod]
        public void GetDefaultTestHostManagerReturnsANewInstanceEverytime()
        {
            var rc = new RunConfiguration() { TargetFrameworkVersion = Framework.DefaultFramework, TargetPlatform = Architecture.X86 };
            var instance1 = this.testEngine.GetDefaultTestHostManager(rc);
            var instance2 = this.testEngine.GetDefaultTestHostManager(rc);

            Assert.AreNotEqual(instance1, instance2);
        }

        [TestMethod]
        public void GetDefaultTestHostManagerReturnsDotnetCoreHostManagerIfFrameworkIsNetCore()
        {
            var rc = new RunConfiguration() { TargetFrameworkVersion = Framework.FromString(".NETCoreApp,Version=v1.0"), TargetPlatform = Architecture.X64 };
            var testHostManager = this.testEngine.GetDefaultTestHostManager(rc);

            Assert.AreEqual(typeof(DotnetTestHostManager), testHostManager.GetType());
        }

        [TestMethod]
        public void GetDefaultTestHostManagerReturnsASharedManagerIfDisableAppDomainIsFalse()
        {
            var rc = new RunConfiguration() { TargetFrameworkVersion = Framework.FromString(".NETFramework,Version=v4.6"), TargetPlatform = Architecture.X86 };

            var testHostManager = this.testEngine.GetDefaultTestHostManager(rc);
            Assert.IsNotNull(testHostManager);

            Assert.IsTrue(testHostManager.Shared, "Default TestHostManager must be shared if DisableAppDomain is false");
        }

        [TestMethod]
        public void GetDefaultTestHostManagerReturnsANonSharedManagerIfDisableAppDomainIsFalse()
        {
            var rc = new RunConfiguration() { TargetFrameworkVersion = Framework.FromString(".NETFramework,Version=v4.6"), TargetPlatform = Architecture.X86, DisableAppDomain = true };

            var testHostManager = this.testEngine.GetDefaultTestHostManager(rc);
            Assert.IsNotNull(testHostManager);

            Assert.IsFalse(testHostManager.Shared, "Default TestHostManager must NOT be shared if DisableAppDomain is true");
        }
    }
}
