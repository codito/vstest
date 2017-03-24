// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.TestPlatform.CommunicationUtilities.PlatformTests
{
    using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class SocketServerTests
    {
        [TestMethod]
        public void SocketServerStartShouldHostServer()
        {
            var socketServer = new SocketServer();

            socketServer.Start();
        }

        // SocketServerStartShouldReturnHostServerConnectionParameters
        // SocketServerStartShouldRaiseClientConnectedEventOnClientConnection
        // SocketServerStartShouldRaiseClientDisconnectedEventOnClientDisconnection
        // SocketServerStartShouldNotAcceptMoreThanOneClients
        // SocketServerStopShouldStopListening
        // SocketServerStopShouldRaiseClientDisconnectedEventOnClientDisconnection
        // SocketServerStopShouldCloseChannel
    }
}
