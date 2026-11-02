# SDK .NET da Assinafy

*Português · [Read in English](README.en.md)*

Cliente .NET tipado para a [API Assinafy](https://api.assinafy.com.br/v1/docs): documentos,
contratos, templates, signatários, assignments, assinatura, tags, campos, webhooks, contas,
usuários, MFA e OAuth 2.1. Compatível com `net8.0`, `net9.0` e `net10.0`, sem dependências NuGet.
Para aplicações novas, use .NET 10 LTS. A referência completa de métodos, payloads e erros está em
[docs/API.md](docs/API.md#sdk-method-index).

## Instalação

```bash
dotnet add package Assinafy.Sdk --version 2.7.1
```

## Cliente e credenciais

```csharp
using Assinafy.Sdk;
using Assinafy.Sdk.Models;

using var client = new AssinafyClient(new AssinafyClientOptions
{
    ApiKey = Environment.GetEnvironmentVariable("ASSINAFY_API_KEY"),
    AccountId = Environment.GetEnvironmentVariable("ASSINAFY_ACCOUNT_ID"),
    BaseUrl = "https://sandbox.assinafy.com.br/v1", // omita em produção
});
```

Use uma chave de API somente no servidor e somente para sua própria workspace. Para uma workspace
conectada por OAuth, informe `Token` em vez de `ApiKey`; os dois são mutuamente exclusivos. Nunca
inclua uma chave de API em aplicativo móvel, navegador, repositório ou log. `BaseUrl` aceita apenas
HTTPS e o caminho `/v1`. Reutilize um `AssinafyClient`: ele é thread-safe e mantém o pool HTTP.
Resultados com credenciais ou links bearer ocultam esses valores em `ToString()`.

Se fornecer um `HttpClient`, construa o handler com `AssinafyClient.CreatePrimaryHandler()` para
desativar cookies e redirecionamentos, mantenha `BaseAddress` igual a `BaseUrl` e seja o dono do
tempo de vida do transporte. Credenciais são anexadas por requisição, nunca aos headers padrão.

Respostas REST usam `{ status, message, data }`; o SDK devolve `data` tipado. OAuth usa JSON plano.
Listas paginadas retornam `PaginatedResult<T>`. Todo método aceita `CancellationToken`. O SDK não
repete chamadas; somente `Documents.WaitUntilReadyAsync` trata `404`, `429` e `5xx` como transitórios.

## Fluxo completo de assinatura

1. Autentique a workspace com chave de API ou token OAuth.
2. Envie o PDF e aguarde `metadata_ready`.
3. Crie signatários e estime custo/créditos.
4. Crie o assignment; essa chamada envia os convites.
5. O signatário confirma dados e termos, conclui a verificação e assina.
6. Se houver revisão biométrica, decida-a; a certificação aguarda todas as aprovações.
7. Receba `document_ready` ou consulte até `certificated`, então baixe o artefato.

```csharp
await using var pdf = File.OpenRead("contrato.pdf");
var documento = await client.Documents.UploadAsync(pdf, "contrato.pdf");
documento = await client.Documents.WaitUntilReadyAsync(documento.Id);

var signatario = await client.Signers.CreateAsync(new CreateSignerRequest
{
    FullName = "João da Silva",
    Email = "joao@example.com",
});

var pedido = new CreateAssignmentRequest
{
    Method = AssignmentMethods.Virtual,
    Message = "Revise e assine.",
    Signers =
    [
        new SignerRef
        {
            Id = signatario.Id,
            VerificationMethod = SignerChannels.Email,
            NotificationMethods = [SignerChannels.Email],
        },
    ],
};

var custo = await client.Assignments.EstimateCostAsync(documento.Id, pedido);
if (!custo.HasSufficientResources)
    throw new InvalidOperationException(custo.BlockingReason);

var assignment = await client.Assignments.CreateAsync(documento.Id, pedido);
// Guarde assignment.SigningUrls ou deixe a Assinafy entregar os convites.
```

`AssignmentMethods.Virtual` cobre assinatura remota; `Collect` exige `Entries` posicionando campos.
`UploadAndRequestSignaturesAsync` oferece o mesmo fluxo como conveniência, mas não é transacional.
Em falha parcial, os recursos já criados permanecem disponíveis para inspeção ou limpeza.

## Verificação, notificação e assinatura

Cada signatário usa exatamente um canal de notificação (`Email` ou `Whatsapp`). Email e WhatsApp
validam um OTP. `DigitalCertificate` e `IdDocumentFaceMatchBasic` usam um desses canais apenas para
notificar; sempre chame `Assignments.EstimateCostAsync`, pois plano e créditos podem bloquear o pedido.

| `VerificationMethod` | Fluxo |
|---|---|
| `SignerChannels.Email` | OTP enviado por email |
| `SignerChannels.Whatsapp` | OTP por WhatsApp; exige plano habilitado |
| `SignerChannels.IdDocumentFaceMatchBasic` | CPF, documento, face, prova de vida e possível revisão |
| `SignerChannels.DigitalCertificate` | Certificado ICP-Brasil A1/A3 via Web PKI; assignment `virtual` |

Rotas do signatário recebem somente `signerAccessCode`; a credencial da workspace nunca é enviada.
Carregue a sessão com `Signing.GetAsync`, confirme dados/termos com `Signers.ConfirmDataAsync`, valide
OTP com `Signers.VerifyAsync` e assine com `Signing.SignAsync` (campos em `collect`, lista vazia em
`virtual`). `Signing.DeclineAsync` registra a recusa. Código ausente ou vazio falha antes da rede.

Na biometria, obtenha consentimento explícito antes de
`Signing.StartIdentityVerificationAsync`. Abra a URL completa, inclusive o fragmento, sem log ou
armazenamento no navegador. `InReview` permite assinar, mas mantém o documento em
`pending_identity_review`; leia a evidência somente durante a revisão e decida com
`Assignments.DecideIdentityReviewAsync`. Rejeitar cancela o documento.

No certificado, confirme CPF/CNPJ e termos, chame `Signing.StartCertificateAsync`, assine o token
no navegador/dispositivo com Web PKI e envie-o a `Signing.CompleteCertificateAsync`. Essas duas
rotas existem somente em produção. O artefato `pades` existe quando a assinatura o gerou; `bundle`
é ZIP. Email, WhatsApp, biometria e certificado A1/A3 são suportados em produção.

## OAuth 2.1

Use OAuth para conectar workspaces de outras pessoas. Gere `state` e um par PKCE S256 novo com
`OAuthResource.CreateState()` e `CreatePkcePair()`, monte a URL com `BuildAuthorizationUrl` e abra-a
como navegação completa. No retorno, valide `state` e `iss`, trate `error`, consuma o código uma vez
com `OAuth.ExchangeCodeAsync` e confira os escopos concedidos. Aplicativos públicos/móveis omitem
`ClientSecret`; confidenciais o guardam somente no servidor.

Solicite `offline_access` para receber refresh token. Cada `RefreshTokenAsync` devolve um novo token
e aposenta o anterior: serialize renovações por conexão e persista o resultado atomicamente antes
de continuar. Nunca repita token/revogação automaticamente. Crie um novo `AssinafyClient` com o
novo access token. Ao desconectar, chame `OAuth.RevokeAsync`. Um `OAuthException` com
`Error == "insufficient_scope"` informa o escopo em `Scope`; reconecte pedindo essa permissão.

## Webhooks

Crie endpoints com `Webhooks.CreateEndpointAsync`; ative `SigningEnabled` e armazene o `Secret`
retornado. OAuth não pode reler o segredo, mas `RotateEndpointSecretAsync` o substitui imediatamente.
Verifique cada entrega sobre o corpo bruto antes de desserializar:

```csharp
using Assinafy.Sdk.Webhooks;

var segredo = Environment.GetEnvironmentVariable("ASSINAFY_WEBHOOK_SECRET")!;
var webhookId = "example-webhook-id";
var webhookTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
var webhookSignature = "v1,ZXhhbXBsZQ==";
var corpoBruto = "{}";
var valido = WebhookSignature.Verify(
    segredo,
    webhookId,
    webhookTimestamp,
    webhookSignature,
    corpoBruto);
```

O corpo é `WebhookEvent`, não o envelope REST. Deduplique pelo `webhook-id`, responda `2xx` rápido e
processe de forma idempotente. Há até seis tentativas, aproximadamente dez minutos entre elas; dez
falhas consecutivas pausam o endpoint. Objetos excluídos mantêm o estado da exclusão e relações
vazias; sujeitos excluídos trazem apenas `type`. Veja o
[catálogo de eventos](docs/API.md#webhook-payloads).

## Outros recursos e erros

`Templates`, `Tags`, `Fields`, `Contracts`, `Accounts`, `Users`, `Authentication` e `Signatures`
seguem o mesmo padrão tipado. Contratos usam `updated_at` para concorrência otimista e não devem ser
reenviados automaticamente. A matriz de 118 operações, payloads completos, OAuth, webhooks e as
rotas adicionais de templates/certificado estão em [docs/API.md](docs/API.md).

Todas as falhas do SDK derivam de `AssinafyException`: `ValidationException`, `ApiException`,
`OAuthException`, `NetworkException` e `SerializationException`. `ApiException` preserva detalhes e
limites (`RetryAfter`, `RateLimitLimit`, `RateLimitRemaining`, `RateLimitReset`). Argumentos,
cancelamento e descarte mantêm as exceções padrão do .NET.

## Desenvolvimento

```bash
dotnet build
dotnet test
dotnet pack src/Assinafy.Sdk/Assinafy.Sdk.csproj -c Release -o ./artifacts
```

Testes de integração usam somente o sandbox e se ignoram sem configuração. Consulte
[README.en.md](README.en.md) para a referência por recurso, [docs/API.md](docs/API.md) para contratos
HTTP completos, [CHANGELOG.md](CHANGELOG.md) para versões e [SECURITY.md](SECURITY.md) para segurança.

Licença MIT.
