// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.CommunicationUtilities
{
    using System;
    using System.IO;
    using System.Text;

    using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.Interfaces;
    using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.Serialization;

    using Newtonsoft.Json;

    public class StreamingDataSerializer : IDataSerializer
    {
        private readonly Stream stream;
        private readonly JsonSerializer serializer;

        public StreamingDataSerializer(Stream stream)
        {
            this.stream = stream;
            this.serializer = JsonSerializer.Create(
                            new JsonSerializerSettings
                                {
                                    ContractResolver = new TestPlatformContractResolver(),
                                    DateFormatHandling = DateFormatHandling.IsoDateFormat,
                                    DateParseHandling = DateParseHandling.DateTimeOffset,
                                    DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                                    TypeNameHandling = TypeNameHandling.None
                                });
        }

        /// <inheritdoc/>
        public Message DeserializeMessage(string rawMessage)
        {
            using (var streamReader = new StreamReader(this.stream, System.Text.Encoding.UTF8, true))
            {
                using (var jsonReader = new JsonTextReader(streamReader))
                {
                    jsonReader.CloseInput = false;
                    return this.serializer.Deserialize<Message>(jsonReader);
                }
            }
        }

        /// <inheritdoc/>
        public T DeserializePayload<T>(Message message)
        {
            T data = default(T);

            if (MessageType.TestMessage.Equals(message.MessageType))
            {
                data = message.Payload.ToObject<T>();
            }
            else
            {
                data = message.Payload.ToObject<T>(serializer);
            }


            return data;
        }

        /// <inheritdoc/>
        public string SerializeMessage(string messageType)
        {
            using (var streamWriter = new StreamWriter(this.stream, System.Text.Encoding.UTF8, 1024, true))
            {
                using (var jsonWriter = new JsonTextWriter(streamWriter))
                {
                    jsonWriter.CloseOutput = false;
                    this.serializer.Serialize(jsonWriter, new Message2 { MessageType = messageType, Payload = null });
                }
            }
            return string.Empty;
        }

        /// <inheritdoc/>
        public string SerializePayload(string messageType, object payload)
        {
            using (var streamWriter = new StreamWriter(this.stream, System.Text.Encoding.UTF8, 1024, true))
            {
                using (var jsonWriter = new JsonTextWriter(streamWriter))
                {
                    jsonWriter.CloseOutput = false;
                    this.serializer.Serialize(jsonWriter, new Message2 { MessageType = messageType, Payload = payload });
                }
            }
            return string.Empty;
        }
    }
}
