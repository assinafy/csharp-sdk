using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

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

    private static string ReadNumber(ref Utf8JsonReader reader)
    {
        if (reader.TryGetInt64(out var integer))
            return integer.ToString(CultureInfo.InvariantCulture);

        if (reader.TryGetDecimal(out var number))
            return number.ToString(CultureInfo.InvariantCulture);

        throw new JsonException("Cannot convert JSON number to string.");
    }
}
