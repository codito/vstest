// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.CommunicationUtilities
{
    using System.IO;
    using System.Text;

    using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.Interfaces;
    using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.Serialization;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    public class StreamingDataSerializer : IDataSerializer
    {
        private readonly Stream stream;
        private readonly JsonSerializer serializer;

        public StreamingDataSerializer(Stream stream)
        {
            this.stream = stream;
            this.serializer =
                JsonSerializer.Create(
                    new JsonSerializerSettings
                        {
                            ContractResolver = new TestPlatformContractResolver(),
                            DateFormatHandling = DateFormatHandling.IsoDateFormat,
                            DateParseHandling = DateParseHandling.DateTimeOffset,
                            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                            TypeNameHandling = TypeNameHandling.None
                        });
#if DEBUG

            // MemoryTraceWriter can help diagnose serialization issues. Enable it for
            // debug builds only.
            this.serializer.TraceWriter = new MemoryTraceWriter();
#endif
        }

        /// <inheritdoc/>
        public Message DeserializeMessage(string rawMessage)
        {
            using (var binaryReader = new BinaryReader(this.stream, Encoding.UTF8, true))
            {
                using (var reader = new JsonTextReader(new StringReader(binaryReader.ReadString())))
                {
                    reader.CloseInput = false;
                    return this.serializer.Deserialize<Message>(reader);
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
                data = message.Payload.ToObject<T>(this.serializer);
            }

            return data;
        }

        /// <inheritdoc/>
        public string SerializeMessage(string messageType)
        {
            using (var stringWriter = new StringWriter())
            {
                using (var writer = new JsonTextWriter(stringWriter))
                {
                    this.serializer.Serialize(writer, new Message2 { MessageType = messageType, Payload = null });

                    using (var binaryWriter = new BinaryWriter(this.stream, Encoding.UTF8, true))
                    {
                        binaryWriter.Write(stringWriter.ToString());
                    }
                }
            }

            return string.Empty;
        }

        /// <inheritdoc/>
        public string SerializePayload(string messageType, object payload)
        {
            using (var stringWriter = new StringWriter())
            {
                using (var writer = new JsonTextWriter(stringWriter))
                {
                    this.serializer.Serialize(writer, new Message2 { MessageType = messageType, Payload = payload });

                    using (var binaryWriter = new BinaryWriter(this.stream, Encoding.UTF8, true))
                    {
                        binaryWriter.Write(stringWriter.ToString());
                    }
                }
            }

            return string.Empty;
        }
    }
}