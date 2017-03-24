// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.CommunicationUtilities
{
    using System;

    using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.Interfaces;

    /// <summary>
    /// Communication server implementation over sockets.
    /// </summary>
    public class SocketServer : ICommunicationServer
    {
        /// <inheritdoc />
        public event EventHandler<ConnectedEventArgs> ClientConnected;

        /// <inheritdoc />
        public event EventHandler<DisconnectedEventArgs> ClientDisconnected;

        /// <inheritdoc />
        public string Start()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void Stop()
        {
            throw new NotImplementedException();
        }
    }
}