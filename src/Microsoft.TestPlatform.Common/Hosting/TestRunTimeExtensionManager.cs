// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.Common.Hosting
{
    using Microsoft.VisualStudio.TestPlatform.Common.ExtensionFramework;
    using Microsoft.VisualStudio.TestPlatform.Common.ExtensionFramework.Utilities;
    using Microsoft.VisualStudio.TestPlatform.Common.Interfaces;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Host;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
    using System.Collections.Generic;

    /// <summary>
    /// Manages loading and provides access to testhost extensions implementing the
    /// ITestRuntimeProvider interface.
    /// </summary>
    internal class TestRuntimeExtensionManager : TestExtensionManager<ITestRuntimeProvider, ITestRuntimeCapabilities>
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="unfilteredTestExtensions">
        /// The unfiltered Test Extensions.
        /// </param>
        /// <param name="testExtensions">
        /// The test Extensions.
        /// </param>
        /// <param name="logger">
        /// The logger.
        /// </param>
        /// <remarks>
        /// The constructor is not public because the factory method should be used to get instances of this class.
        /// </remarks>
        protected TestRuntimeExtensionManager(
            IEnumerable<LazyExtension<ITestRuntimeProvider, Dictionary<string, object>>> unfilteredTestExtensions,
            IEnumerable<LazyExtension<ITestRuntimeProvider, ITestRuntimeCapabilities>> testExtensions,
            IMessageLogger logger)
            : base(unfilteredTestExtensions, testExtensions, logger)
        {
        }

        /// <summary>
        /// Gets an instance of the TestLoggerExtensionManager.
        /// </summary>
        /// <param name="messageLogger">
        /// The message Logger.
        /// </param>
        /// <returns>
        /// The TestLoggerExtensionManager.
        /// </returns>
        public static TestRuntimeExtensionManager Create(IMessageLogger messageLogger)
        {
            IEnumerable<LazyExtension<ITestRuntimeProvider, ITestRuntimeCapabilities>> filteredTestExtensions;
            IEnumerable<LazyExtension<ITestRuntimeProvider, Dictionary<string, object>>> unfilteredTestExtensions;

            TestPluginManager.Instance.GetTestExtensions<ITestRuntimeProvider, ITestRuntimeCapabilities, TestRuntimeMetadata>(
                out unfilteredTestExtensions,
                out filteredTestExtensions);

            return new TestRuntimeExtensionManager(unfilteredTestExtensions, filteredTestExtensions, messageLogger);
        }
    }

}
