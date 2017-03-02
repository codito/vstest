// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.TestPlatform.PerformanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;

    using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
    using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;

    using TestResult = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    public class SerializationTests
    {
        [TestMethod]
        public void SerializationShouldBeFast()
        {
            var payload = CreateTestResults();
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 100; i++)
            {
                JsonDataSerializer.Instance.SerializePayload(MessageType.TestRunStatsChange, payload);
            }

            Assert.IsTrue(10000 > sw.ElapsedMilliseconds);
        }

        private static TestRunStatsPayload CreateTestResults()
        {
            var testresults = new List<TestResult>();
            var payload = new TestRunStatsPayload();

            for (int i = 0; i < 10000; i++)
            {
                testresults.Add(CreateTestResult(i.ToString()));
            }

            payload.TestRunChangedArgs =
                new TestRunChangedEventArgs(
                    new TestRunStatistics(new Dictionary<TestOutcome, long>()),
                    testresults,
                    Enumerable.Empty<TestCase>());
            return payload;
        }

        private static TestResult CreateTestResult(string uniqueId)
        {
            var testcase = new TestCase(
                "sampleTestClass.sampleTestCase" + uniqueId,
                new Uri("executor://sampleTestExecutor"),
                "sampleTest.dll");
            return new TestResult(testcase)
                       {
                           Outcome = TestOutcome.Passed,
                           ErrorMessage = "sampleError",
                           ErrorStackTrace = "sampleStackTrace",
                           DisplayName = "sampleTestResult",
                           ComputerName = "sampleComputerName",
                           Duration = TimeSpan.MaxValue,
                           StartTime = DateTimeOffset.MinValue,
                           EndTime = DateTimeOffset.MaxValue
                       };
        }
    }
}
