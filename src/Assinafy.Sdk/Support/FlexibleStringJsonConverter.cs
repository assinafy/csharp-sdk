using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Assinafy.Sdk.Support;

internal sealed class FlexibleStringJsonConverter : JsonConverter<string>
{
    public override string? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => ReadNumber(ref reader),
            JsonTokenType.True => bool.TrueString,
            JsonTokenType.False => bool.FalseString,
            JsonTokenType.Null => null,
            _ => throw new JsonException($"Cannot convert {reader.TokenType} to string."),
        };
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }

    // Preserve the exact numeric token text instead of round-tripping through Int64/decimal,
    // which could reformat (trailing zeros, exponent form) or throw on very large values.
    private static string ReadNumber(ref Utf8JsonReader reader)
    {
        var bytes = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;
        return Encoding.UTF8.GetString(bytes);
    }
}
