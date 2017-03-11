﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.TestPlatform.AcceptanceTests
{
    using System.Threading;

    using global::TestPlatform.TestUtilities;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ExecutionTests : AcceptanceTestBase
    {
        [CustomDataTestMethod]
        [NET46TargetFramework]
        [NETCORETargetFramework]
        public void RunMultipleTestAssemblies(string runnerFramework, string targetFramework, string targetRuntime)
        {
            AcceptanceTestBase.SetTestEnvironment(this.testEnvironment, runnerFramework, targetFramework, targetRuntime);

            var assemblyPaths =
                this.BuildMultipleAssemblyPath("SimpleTestProject.dll", "SimpleTestProject2.dll").Trim('\"');
            this.InvokeVsTestForExecution(assemblyPaths, this.GetTestAdapterPath(), string.Empty, this.FrameworkArgValue);
            this.ValidateSummaryStatus(2, 2, 2);
        }

        // Randomly failing with error "The active test run was aborted. Reason: Destination array was not long enough.
        // Check destIndex and length, and the array's lower bounds. Test Run Failed."
        // Issue: https://github.com/Microsoft/vstest/issues/292
        [Ignore]
        [CustomDataTestMethod]
        [NET46TargetFramework]
        [NETCORETargetFramework]
        public void RunMultipleTestAssembliesInParallel(string runnerFramework, string targetFramework, string targetRuntime)
        {
            AcceptanceTestBase.SetTestEnvironment(this.testEnvironment, runnerFramework, targetFramework, targetRuntime);
            var assemblyPaths =
                this.BuildMultipleAssemblyPath("SimpleTestProject.dll", "SimpleTestProject2.dll").Trim('\"');
            var arguments = PrepareArguments(assemblyPaths, this.GetTestAdapterPath(), string.Empty, this.FrameworkArgValue);
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
        [NET46TargetFramework]
        [NETCORETargetFramework]
        public void WorkingDirectoryIsSourceDirectory(string runnerFramework, string targetFramework, string targetRuntime)
        {
            AcceptanceTestBase.SetTestEnvironment(this.testEnvironment, runnerFramework, targetFramework, targetRuntime);

            var assemblyPaths =
                this.BuildMultipleAssemblyPath("SimpleTestProject3.dll").Trim('\"');
            var arguments = PrepareArguments(assemblyPaths, this.GetTestAdapterPath(), string.Empty, this.FrameworkArgValue);
            arguments = string.Concat(arguments, " /tests:WorkingDirectoryTest");
            this.InvokeVsTest(arguments);
            this.ValidateSummaryStatus(1, 0, 0);
        }

        [CustomDataTestMethod]
        [NET46TargetFramework]
        [NETCORETargetFramework]
        public void ShouldGetErrorMessageOnTesthostExitWithError(string runnerFramework, string targetFramework, string targetRuntime)
        {
            AcceptanceTestBase.SetTestEnvironment(this.testEnvironment, runnerFramework, targetFramework, targetRuntime);

            var assemblyPaths =
                this.BuildMultipleAssemblyPath("SimpleTestProject3.dll").Trim('\"');
            var arguments = PrepareArguments(assemblyPaths, this.GetTestAdapterPath(), string.Empty, this.FrameworkArgValue);
            arguments = string.Concat(arguments, " /tests:ExitWithStdErrorMessageTest");
            this.InvokeVsTest(arguments);
            this.StdErrorContains("The active test run was aborted.");
        }
    }
}