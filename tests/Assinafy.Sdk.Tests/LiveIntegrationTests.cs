using System.Text;
using Assinafy.Sdk;
using Assinafy.Sdk.Models;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests;

/// <summary>
/// End-to-end tests that exercise the real Assinafy API. They are inert unless the
/// <c>ASSINAFY_API_KEY</c> and <c>ASSINAFY_ACCOUNT_ID</c> environment variables are set
/// (optionally <c>ASSINAFY_BASE_URL</c>, defaulting to the sandbox), so the normal unit-test
/// run and CI are unaffected. Run against the sandbox with, e.g.:
/// <code>
/// ASSINAFY_API_KEY=... ASSINAFY_ACCOUNT_ID=... ASSINAFY_BASE_URL=https://sandbox.assinafy.com.br/v1 \
///   dotnet test --filter FullyQualifiedName~LiveIntegrationTests
/// </code>
/// </summary>
public sealed class LiveIntegrationTests
{
    private static AssinafyClient? TryCreateClient()
    {
        var apiKey = Environment.GetEnvironmentVariable("ASSINAFY_API_KEY");
        var accountId = Environment.GetEnvironmentVariable("ASSINAFY_ACCOUNT_ID");
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(accountId))
            return null;

        var options = new AssinafyClientOptions { ApiKey = apiKey, AccountId = accountId };
        var baseUrl = Environment.GetEnvironmentVariable("ASSINAFY_BASE_URL");
        if (!string.IsNullOrWhiteSpace(baseUrl))
            options.BaseUrl = baseUrl;

        return new AssinafyClient(options);
    }

    [Fact]
    public async Task ReadEndpoints_AuthenticateAndReturnData()
    {
        var client = TryCreateClient();
        if (client is null) return; // not configured — skipped

        using (client)
        {
            (await client.Documents.ListStatusesAsync()).Should().NotBeEmpty();
            (await client.Fields.ListTypesAsync()).Should().NotBeEmpty();
            (await client.Webhooks.ListEventTypesAsync()).Should().NotBeEmpty();

            var documents = await client.Documents.ListAsync(
                new Dictionary<string, string?> { ["per-page"] = "1" });
            documents.Should().NotBeNull();

            await client.Tags.ListAsync();
            await client.Fields.ListAsync();
        }
    }

    [Fact]
    public async Task UploadWaitGetDelete_RoundTrips()
    {
        var client = TryCreateClient();
        if (client is null) return; // not configured — skipped

        using (client)
        {
            using var pdf = new MemoryStream(BuildMinimalPdf());
            var uploaded = await client.Documents.UploadAsync(pdf, "sdk-live-audit.pdf");
            uploaded.Id.Should().NotBeNullOrEmpty();

            try
            {
                var ready = await client.Documents.WaitUntilReadyAsync(uploaded.Id);
                ready.Id.Should().Be(uploaded.Id);

                var fetched = await client.Documents.GetAsync(uploaded.Id);
                fetched.Id.Should().Be(uploaded.Id);

                (await client.Documents.ActivitiesAsync(uploaded.Id)).Should().NotBeEmpty();
            }
            finally
            {
                await client.Documents.DeleteAsync(uploaded.Id);
            }
        }
    }

    /// <summary>Builds a tiny but valid single-page PDF the API will accept.</summary>
    private static byte[] BuildMinimalPdf()
    {
        var stream = "BT /F1 24 Tf 72 700 Td (Assinafy SDK live test) Tj ET";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {stream.Length} >>\nstream\n{stream}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        };

        using var ms = new MemoryStream();
        void Write(string text) => ms.Write(Encoding.ASCII.GetBytes(text));

        Write("%PDF-1.4\n");
        var offsets = new long[objects.Length];
        for (var i = 0; i < objects.Length; i++)
        {
            offsets[i] = ms.Length;
            Write($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = ms.Length;
        Write($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
            Write($"{offset:D10} 00000 n \n");
        Write($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");

        return ms.ToArray();
    }
}
