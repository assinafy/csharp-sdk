using System.Text.Json;
using System.Text.Json.Serialization;

namespace Assinafy.Sdk.Support;

internal sealed class UnixSecondsJsonConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var seconds))
            return seconds;
        if (reader.TokenType == JsonTokenType.String && reader.TryGetDateTimeOffset(out var timestamp))
            return timestamp.ToUnixTimeSeconds();

        throw new JsonException("Expected an ISO-8601 timestamp or integer Unix seconds.");
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value);
}
