// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.CommunicationUtilities
{
    using System;
    using System.IO;

    using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.Interfaces;
    using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.Serialization;

    using Newtonsoft.Json;

    public class StreamingDataSerializer : IDataSerializer
    {
        private readonly JsonWriter jsonWriter;
        private readonly JsonReader jsonReader;
        private readonly JsonSerializer serializer;

        public StreamingDataSerializer(Stream stream)
        {
            this.jsonWriter = new JsonTextWriter(new StreamWriter(stream));
            this.jsonReader = new JsonTextReader(new StreamReader(stream));
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
            return this.serializer.Deserialize<Message>(this.jsonReader);
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
            this.serializer.Serialize(this.jsonWriter, new Message2 { MessageType = messageType, Payload = null });
            return string.Empty;
        }

        /// <inheritdoc/>
        public string SerializePayload(string messageType, object payload)
        {
            this.serializer.Serialize(this.jsonWriter, new Message2 { MessageType = messageType, Payload = payload });
            return string.Empty;
        }
    }
}
