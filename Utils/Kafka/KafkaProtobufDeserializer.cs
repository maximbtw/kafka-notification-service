using Confluent.Kafka;

namespace Utils.Kafka;

public class KafkaProtobufDeserializer<T> : IDeserializer<T> where T : class, new()
{
    /// <inheritdoc/>
    public T Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
    {
        if (isNull || data.IsEmpty)
        {
            return new T();
        }
        
        return ProtoBuf.Serializer.Deserialize<T>(data);
    }
}