// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.CommunicationUtilities
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.Interfaces;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;

    /// <summary>
    /// Facilitates communication using sockets
    /// </summary>    
    public class SocketCommunicationManager2 : ICommunicationManager
    {
        /// <summary>
        /// TCP Listener to host TCP channel and listen
        /// </summary>
        private TcpListener tcpListener;

        /// <summary>
        /// TCP Client that can connect to a TCP listener
        /// </summary>
        private TcpClient tcpClient;

        /// <summary>
        /// Serializer for the data objects
        /// </summary>
        private IDataSerializer dataSerializer;

        /// <summary>
        /// Event used to maintain client connection state
        /// </summary>
        private ManualResetEvent clientConnectedEvent = new ManualResetEvent(false);


        /// <summary>
        /// Event used to maintain client connection state
        /// </summary>
        private ManualResetEvent clientConnectionAcceptedEvent = new ManualResetEvent(false);

        /// <summary>
        /// Sync object for sending messages 
        /// SendMessage over socket channel is NOT thread-safe
        /// </summary>
        private object sendSyncObject = new object();

        /// <summary>
        /// Stream to use read timeout
        /// </summary>
        private NetworkStream stream;

        private Socket socket;

        /// <summary>
        /// The server stream read timeout constant (in microseconds).
        /// </summary>
        private const int StreamReadTimeout = 1000 * 1000;

        /// <summary>
        /// Initializes a new instance of the <see cref="SocketCommunicationManager"/> class.
        /// </summary>
        public SocketCommunicationManager2()
        {
        }

        #region ServerMethods

        /// <summary>
        /// Host TCP Socket Server and start listening
        /// </summary>
        /// <returns></returns>
        public int HostServer()
        {
            var endpoint = new IPEndPoint(IPAddress.Loopback, 0);
            this.tcpListener = new TcpListener(endpoint);

            this.tcpListener.Start();

            var portNumber = ((IPEndPoint)this.tcpListener.LocalEndpoint).Port;
            EqtTrace.Info("Listening on port : {0}", portNumber);

            return portNumber;
        }

        /// <summary>
        /// Accepts client async
        /// </summary>
        public async Task AcceptClientAsync()
        {
            if (this.tcpListener != null)
            {
                this.clientConnectedEvent.Reset();

                var client = await this.tcpListener.AcceptTcpClientAsync();
                this.socket = client.Client;
                this.stream = client.GetStream();
                this.dataSerializer = new StreamingDataSerializer(this.stream);

                this.clientConnectedEvent.Set();

                EqtTrace.Info("Accepted Client request and set the flag");
            }
        }

        /// <summary>
        /// Waits for Client Connection
        /// </summary>
        /// <param name="clientConnectionTimeout">Time to Wait for the connection</param>
        /// <returns>True if Client is connected, false otherwise</returns>
        public bool WaitForClientConnection(int clientConnectionTimeout)
        {
            return this.clientConnectedEvent.WaitOne(clientConnectionTimeout);
        }

        /// <summary>
        /// Stop Listener
        /// </summary>
        public void StopServer()
        {
            this.tcpListener?.Stop();
            this.tcpListener = null;

            // TODO dispose data serializer
        }

        #endregion

        #region ClientMethods

        /// <summary>
        /// Connects to server async
        /// </summary>
        public async Task SetupClientAsync(int portNumber)
        {
            this.clientConnectionAcceptedEvent.Reset();
            EqtTrace.Info("Trying to connect to server on port : {0}", portNumber);
            this.tcpClient = new TcpClient();
            this.socket = this.tcpClient.Client;
            await this.tcpClient.ConnectAsync(IPAddress.Loopback, portNumber);
            this.stream = this.tcpClient.GetStream();
            this.dataSerializer = new StreamingDataSerializer(this.stream);
            this.clientConnectionAcceptedEvent.Set();
            EqtTrace.Info("Connected to the server successfully ");
        }

        /// <summary>
        /// Waits for server to be connected
        /// Whoever creating the client and trying to connect to a server 
        /// should use this method to wait for connection to be established with server
        /// </summary>
        /// <param name="connectionTimeout">Time to wait for the connection</param>
        /// <returns>True, if Server got a connection from client</returns>
        public bool WaitForServerConnection(int connectionTimeout)
        {
            return this.clientConnectionAcceptedEvent.WaitOne(connectionTimeout);
        }

        /// <summary>
        /// Stop Listener
        /// </summary>
        public void StopClient()
        {
            this.tcpClient?.Dispose();
            this.tcpClient = null;

            // TODO dispose data serializer
        }

        #endregion

        /// <summary>
        /// Writes message to the binary writer.
        /// </summary>
        /// <param name="messageType">Type of Message to be sent, for instance TestSessionStart</param>
        public void SendMessage(string messageType)
        {
            this.dataSerializer.SerializeMessage(messageType);
        }

        /// <summary>
        /// Reads message from the binary reader
        /// </summary>
        /// <returns>Returns message read from the binary reader</returns>
        public Message ReceiveMessage()
        {
            var token = new CancellationTokenSource();
            this.TryReceiveRawMessage(token.Token);
            return this.dataSerializer.DeserializeMessage(string.Empty);
        }

        /// <summary>
        ///  Writes message to the binary writer with payload
        /// </summary>
        /// <param name="messageType">Type of Message to be sent, for instance TestSessionStart</param>
        /// <param name="payload">payload to be sent</param>
        public void SendMessage(string messageType, object payload)
        {
            this.dataSerializer.SerializePayload(messageType, payload);
        }

        /// <summary>
        /// Reads message from the binary reader
        /// </summary>
        /// <returns> Raw message string </returns>
        public string ReceiveRawMessage()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Send serialized raw message
        /// </summary>
        /// <param name="rawMessage">serialized message</param>
        public void SendRawMessage(string rawMessage)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Deserializes the Message into actual TestPlatform objects
        /// </summary>
        /// <typeparam name="T"> The type of object to deserialize to. </typeparam>
        /// <param name="message"> Message object </param>
        /// <returns> TestPlatform object </returns>
        public T DeserializePayload<T>(Message message)
        {
            return this.dataSerializer.DeserializePayload<T>(message);
        }

        /// <summary>
        /// Reads message from the binary reader using read timeout
        /// </summary>
        /// <param name="cancellationToken">
        /// The cancellation Token.
        /// </param>
        /// <returns>
        /// Returns message read from the binary reader
        /// </returns>
        public async Task<Message> ReceiveMessageAsync(CancellationToken cancellationToken)
        {
            return await Task.Run(() => new Message());
            //throw new NotImplementedException();
            //var task = await Task.Run(() => this.dataSerializer.DeserializeMessage(string.Empty));
            //return task;
        }

        /// <summary>
        /// Reads message from the binary reader using read timeout 
        /// </summary>
        /// <param name="cancellationToken">
        /// The cancellation Token.
        /// </param>
        /// <returns>
        /// Raw message string 
        /// </returns>
        public async Task<string> ReceiveRawMessageAsync(CancellationToken cancellationToken)
        {
            return await Task.Run(() => string.Empty);
            //var str = await Task.Run(() => this.TryReceiveRawMessage(cancellationToken));
            //return str;
        }

        private void TryReceiveRawMessage(CancellationToken cancellationToken)
        {
            bool success = false;

            // Set read timeout to avoid blocking receive raw message
            while (!cancellationToken.IsCancellationRequested && !success)
            {
                try
                {
                    if (this.socket.Poll(StreamReadTimeout, SelectMode.SelectRead))
                    {
                        success = true;
                    }
                }
                catch (IOException ioException)
                {
                    var socketException = ioException.InnerException as SocketException;
                    if (socketException != null
                        && socketException.SocketErrorCode == SocketError.TimedOut)
                    {
                        EqtTrace.Info(
                            "SocketCommunicationManager ReceiveMessage: failed to receive message because read timeout {0}",
                            ioException);
                    }
                    else
                    {
                        EqtTrace.Error(
                            "SocketCommunicationManager ReceiveMessage: failed to receive message {0}",
                            ioException);
                        break;
                    }
                }
                catch (Exception exception)
                {
                    EqtTrace.Error(
                        "SocketCommunicationManager ReceiveMessage: failed to receive message {0}",
                        exception);
                    break;
                }
            }
        }
    }
}
