using System.Text.Json;
using System.Text.Json.Serialization;
using Assinafy.Sdk.Models;

namespace Assinafy.Sdk.Support;

internal sealed class ContractRecordJsonConverter : JsonConverter<ContractRecord>
{
    public override ContractRecord? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("resource", out var resource))
            throw new JsonException("Contract response is missing its resource discriminator.");

        return resource.ValueKind == JsonValueKind.String ? resource.GetString() switch
        {
            "document_contract" => root.Deserialize<DocumentContract>(options),
            "document_contract_amendment" => root.Deserialize<DocumentContractAmendment>(options),
            _ => throw new JsonException("Unknown contract resource discriminator."),
        } : throw new JsonException("Invalid contract resource discriminator.");
    }

    public override void Write(Utf8JsonWriter writer, ContractRecord value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
}
