﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.TestPlatform.AcceptanceTests
{
    using System;
    using System.IO;
    using System.Threading;

    using global::TestPlatform.TestUtilities;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ExecutionTests : AcceptanceTestBase
    {
        [CustomDataTestMethod]
        [NETFullTargetFramework(inIsolation: true, inProcess: true)]
        [NETCORETargetFramework]
        public void RunMultipleTestAssemblies(RunnerInfo runnerInfo)
        {
            AcceptanceTestBase.SetTestEnvironment(this.testEnvironment, runnerInfo);

            var assemblyPaths =
                this.BuildMultipleAssemblyPath("SimpleTestProject.dll", "SimpleTestProject2.dll").Trim('\"');
            this.InvokeVsTestForExecution(assemblyPaths, this.GetTestAdapterPath(), string.Empty, this.FrameworkArgValue);
            this.ValidateSummaryStatus(2, 2, 2);
        }

        [CustomDataTestMethod]
        [NETFullTargetFramework(inIsolation: true, inProcess: true)]
        [NETCORETargetFramework]
        public void RunMultipleTestAssembliesWithoutTestAdapterPath(RunnerInfo runnerInfo)
        {
            AcceptanceTestBase.SetTestEnvironment(this.testEnvironment, runnerInfo);

            var assemblyPaths = this.BuildMultipleAssemblyPath("SimpleTestProject.dll").Trim('\"');
            var xunitAssemblyPath = this.testEnvironment.TargetFramework.Equals("net451")?
                testEnvironment.GetTestAsset("XUTestProject.dll", "net46") :
                testEnvironment.GetTestAsset("XUTestProject.dll");

            assemblyPaths = string.Concat(assemblyPaths, "\" \"", xunitAssemblyPath);
            this.InvokeVsTestForExecution(assemblyPaths, string.Empty, string.Empty, this.FrameworkArgValue);
            this.ValidateSummaryStatus(2, 2, 1);
        }

        [CustomDataTestMethod]
        [NETFullTargetFramework]
        [NETCORETargetFramework]
        public void RunMultipleTestAssembliesInParallel(RunnerInfo runnerInfo)
        {
            AcceptanceTestBase.SetTestEnvironment(this.testEnvironment, runnerInfo);

            var assemblyPaths =
                this.BuildMultipleAssemblyPath("SimpleTestProject.dll", "SimpleTestProject2.dll").Trim('\"');
            var arguments = PrepareArguments(assemblyPaths, this.GetTestAdapterPath(), string.Empty, this.FrameworkArgValue, runnerInfo.InIsolationValue);
            arguments = string.Concat(arguments, " /Parallel");
            arguments = string.Concat(arguments, " /Platform:x86");
            string testhostProcessName = string.Empty;
            int expectedNumOfProcessCreated = 0;
            if (this.IsDesktopTargetFramework())
            {
                testhostProcessName = "testhost.x86";
                expectedNumOfProcessCreated = 2;
            }
            else
            {
                testhostProcessName = "dotnet";
                if (this.IsDesktopRunner())
                {
                    expectedNumOfProcessCreated = 2;
                }
                else
                {
                    // Include launcher dotnet.exe
                    expectedNumOfProcessCreated = 3;
                }
            }

            var cts = new CancellationTokenSource();
            var numOfProcessCreatedTask = NumberOfProcessLaunchedUtility.NumberOfProcessCreated(
                cts,
                testhostProcessName);

            this.InvokeVsTest(arguments);
            cts.Cancel();

            Assert.AreEqual(
                expectedNumOfProcessCreated,
                numOfProcessCreatedTask.Result,
                $"Number of {testhostProcessName} process created, expected: {expectedNumOfProcessCreated} actual: {numOfProcessCreatedTask.Result}");
            this.ValidateSummaryStatus(2, 2, 2);
        }

        [CustomDataTestMethod]
        [NETFullTargetFramework(inIsolation: true, inProcess: true)]
        [NETCORETargetFramework]
        public void TestSessionTimeOutTests(RunnerInfo runnerInfo)
        {
            AcceptanceTestBase.SetTestEnvironment(this.testEnvironment, runnerInfo);

            var assemblyPaths =
                this.BuildMultipleAssemblyPath("SimpleTestProject3.dll").Trim('\"');
            var arguments = PrepareArguments(assemblyPaths, this.GetTestAdapterPath(), string.Empty, this.FrameworkArgValue, runnerInfo.InIsolationValue);
            arguments = string.Concat(arguments, " /TestCaseFilter:TestSessionTimeoutTest");

            // set TestSessionTimeOut = 7 sec
            arguments = string.Concat(arguments, " -- RunConfiguration.TestSessionTimeout=7000");
            this.InvokeVsTest(arguments);

            this.StdErrorContains("Test Run Aborted.");
            this.StdErrorContains("Aborting test run: test run timeout of 7000 milliseconds exceeded.");
            this.StdOutputDoesNotContains("Total tests: 6");
        }

        [CustomDataTestMethod]
        [NETCORETargetFramework]
        public void TestPlatformShouldBeCompatibleWithOldTestHost(RunnerInfo runnerInfo)
        {
            AcceptanceTestBase.SetTestEnvironment(this.testEnvironment, runnerInfo);

            var assemblyPaths = this.BuildMultipleAssemblyPath("SampleProjectWithOldTestHost.dll").Trim('\"');
            var arguments = PrepareArguments(assemblyPaths, this.GetTestAdapterPath(), string.Empty, this.FrameworkArgValue, runnerInfo.InIsolationValue);

            this.InvokeVsTest(arguments);

            this.ValidateSummaryStatus(1, 0, 0);
        }

        [CustomDataTestMethod]
        [NETFullTargetFramework(inIsolation: true, inProcess: true)]
        [NETCORETargetFramework]
        public void WorkingDirectoryIsSourceDirectory(RunnerInfo runnerInfo)
        {
            AcceptanceTestBase.SetTestEnvironment(this.testEnvironment, runnerInfo);

            var assemblyPaths =
                this.BuildMultipleAssemblyPath("SimpleTestProject3.dll").Trim('\"');
            var arguments = PrepareArguments(assemblyPaths, this.GetTestAdapterPath(), string.Empty, this.FrameworkArgValue, runnerInfo.InIsolationValue);
            arguments = string.Concat(arguments, " /tests:WorkingDirectoryTest");
            this.InvokeVsTest(arguments);
            this.ValidateSummaryStatus(1, 0, 0);
        }

        [CustomDataTestMethod]
        [NETFullTargetFramework]
        [NETCORETargetFramework]
        public void StackOverflowExceptionShouldBeLoggedToConsoleAndDiagLogFile(RunnerInfo runnerInfo)
        {
            AcceptanceTestBase.SetTestEnvironment(this.testEnvironment, runnerInfo);

            if (this.testEnvironment.BuildConfiguration.Equals("release", StringComparison.OrdinalIgnoreCase)
                && runnerInfo.TargetFramework.StartsWith("netcoreapp"))
            {
                Assert.Inconclusive("On StackOverflowException testhost not exited in release configuration.");
                return;
            }

            var diagLogFilePath = Path.Combine(Path.GetTempPath(), $"std_error_log_{Guid.NewGuid()}.txt");
            File.Delete(diagLogFilePath);

            var assemblyPaths =
                this.BuildMultipleAssemblyPath("SimpleTestProject3.dll").Trim('\"');
            var arguments = PrepareArguments(assemblyPaths, this.GetTestAdapterPath(), string.Empty, this.FrameworkArgValue, runnerInfo.InIsolationValue);
            arguments = string.Concat(arguments, " /testcasefilter:ExitWithStackoverFlow");
            arguments = string.Concat(arguments, $" /diag:{diagLogFilePath}");

            this.InvokeVsTest(arguments);
            var errorMessage = string.Empty;

            if (runnerInfo.TargetFramework.StartsWith("netcoreapp2."))
            {
                errorMessage = "Process is terminating due to StackOverflowException.";
            }
            else
            {
                errorMessage = "Process is terminated due to StackOverflowException.";
            }

            FileAssert.Contains(diagLogFilePath, errorMessage);
            this.StdErrorContains(errorMessage);
            File.Delete(diagLogFilePath);
        }

        [CustomDataTestMethod]
        [NETFullTargetFramework]
        [NETCORETargetFramework]
        public void UnhandleExceptionExceptionShouldBeLoggedToDiagLogFile(RunnerInfo runnerInfo)
        {
            AcceptanceTestBase.SetTestEnvironment(this.testEnvironment, runnerInfo);

            var diagLogFilePath = Path.Combine(Path.GetTempPath(), $"std_error_log_{Guid.NewGuid()}.txt");
            File.Delete(diagLogFilePath);

            var assemblyPaths =
                this.BuildMultipleAssemblyPath("SimpleTestProject3.dll").Trim('\"');
            var arguments = PrepareArguments(assemblyPaths, this.GetTestAdapterPath(), string.Empty, this.FrameworkArgValue, runnerInfo.InIsolationValue);
            arguments = string.Concat(arguments, " /testcasefilter:ExitwithUnhandleException");
            arguments = string.Concat(arguments, $" /diag:{diagLogFilePath}");

            this.InvokeVsTest(arguments);
            var errorFirstLine = "Test host standard error line: Unhandled Exception: System.InvalidOperationException: Operation is not valid due to the current state of the object.";
            FileAssert.Contains(diagLogFilePath, errorFirstLine);
            File.Delete(diagLogFilePath);
        }
    }
}