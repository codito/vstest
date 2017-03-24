// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.Interfaces
{
    using System.IO;

    /// <summary>
    /// Provides properties for the connected communication channel.
    /// </summary>
    public class ConnectedEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectedEventArgs"/> class.
        /// </summary>
        /// <param name="stream">Underlying stream for the communication channel.</param>
        public ConnectedEventArgs(Stream stream)
        {
            this.ChannelStream = stream;
        }

        /// <summary>
        /// Gets the channel stream.
        /// </summary>
        public Stream ChannelStream { get; private set; }
    }
}