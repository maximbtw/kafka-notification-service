using Confluent.Kafka;

namespace Utils.Kafka;

public class KafkaProtobufSerializer<T> : ISerializer<T> where T : class
{
    /// <inheritdoc/>
    public byte[] Serialize(T data, SerializationContext context)
    {
        using var ms = new MemoryStream();
        ProtoBuf.Serializer.Serialize(ms, data);
        return ms.ToArray();
    }
}