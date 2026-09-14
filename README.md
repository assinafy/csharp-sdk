# SDK .NET da Assinafy

*Português · [Read in English](README.en.md)*

Cliente .NET tipado para a API de assinatura eletrônica da
[Assinafy](https://api.assinafy.com.br/v1/docs) — plataforma brasileira de assinatura de documentos.
Cobre toda a superfície HTTP documentada — documentos, templates, signatários, assignments, o fluxo
de assinatura do signatário, imagens de assinatura, tags, campos, webhooks, contas e usuários — como
recursos fortemente tipados em um único `AssinafyClient`, com uma hierarquia de exceções, tratamento
de envelope e paginação já resolvidos.

Compatível com `net8.0`, `net9.0` e `net10.0`.

> **Referência completa em inglês.** Este documento cobre instalação, autenticação e os fluxos
> principais. O manual de referência por recurso está em **[README.en.md](README.en.md)**.

## Instalação

```bash
dotnet add package Assinafy.Sdk --version 2.0.0
```

Aplicações precisam de um runtime compatível com `net8.0`, `net9.0` ou `net10.0`. Quem contribui
precisa dos SDKs .NET 8.0.424, 9.0.317 e 10.0.400; o [`global.json`](global.json) seleciona o .NET 10
para os comandos do repositório.

## Credenciais e ambientes

A Assinafy aceita qualquer uma das duas credenciais:

| Credencial | Enviada como | Obtida de | Usar para |
|---|---|---|---|
| Chave de API | Header `X-Api-Key` | `Authentication.CreateApiKeyAsync` (ou o app web) | Integrações servidor-a-servidor |
| Token de acesso | `Authorization: Bearer …` | `Authentication.LoginAsync` / `SocialLoginAsync` | Agir como um usuário logado |

As duas são mutuamente exclusivas — informar ambas lança `ValidationException`. Crie um usuário
**separado** para a integração por chave de API, para que ele receba apenas o acesso necessário, e
nunca comite a chave.

Desenvolva contra o sandbox e troque uma única opção para ir a produção:

| Ambiente | URL base da API | App web |
|---|---|---|
| Sandbox | `https://sandbox.assinafy.com.br/v1` | `https://app-sandbox.assinafy.com.br` |
| Produção (padrão) | `https://api.assinafy.com.br/v1` | `https://app.assinafy.com.br` |

A maioria dos endpoints tem escopo de uma **conta** (workspace). Defina `AccountId` uma vez no
cliente e todo método com escopo de conta o utiliza; cada um desses métodos também aceita um
`accountId` opcional para sobrescrever. `Accounts.ListAsync()` descobre os IDs disponíveis para a
credencial atual.

## Criando um cliente

```csharp
using Assinafy.Sdk;
using Assinafy.Sdk.Models;

using var client = new AssinafyClient(new AssinafyClientOptions
{
    ApiKey = Environment.GetEnvironmentVariable("ASSINAFY_API_KEY"),
    AccountId = Environment.GetEnvironmentVariable("ASSINAFY_ACCOUNT_ID"),
    BaseUrl = "https://sandbox.assinafy.com.br/v1",   // omita para produção
    Timeout = TimeSpan.FromSeconds(30),               // padrão
});
```

Existem dois atalhos para os casos comuns:

```csharp
using var fromArgs = AssinafyClient.Create(apiKey, accountId);

using var fromSettings = AssinafyClient.FromConfig(new Dictionary<string, string?>
{
    ["api_key"] = configuration["Assinafy:ApiKey"],
    ["account_id"] = configuration["Assinafy:AccountId"],
});
```

`FromConfig` aceita chaves em snake_case ou camelCase (`api_key`/`apiKey`, `account_id`/`accountId`,
`token`/`access_token`/`accessToken`, `base_url`/`baseUrl`).

**Tempo de vida.** `AssinafyClient` é thread-safe e mantém um `HttpClient` com pool. Crie um por
aplicação e reutilize — criar um por requisição esgota os sockets. Faça `Dispose` apenas quando ele
for dono do próprio transporte: os construtores acima são, a sobrecarga com `HttpClient` não, deixando
o tempo de vida daquele cliente com você.

**Endurecimento do transporte.** `BaseUrl` precisa ser uma URL HTTPS absoluta cujo caminho seja
exatamente `/v1`; informação de usuário, segmentos extras, query string e fragmento são rejeitados. O
transporte próprio do SDK desativa redirecionamentos automáticos, porque o .NET repassa headers
customizados como `X-Api-Key` para o destino do redirecionamento. Se você fornecer seu próprio
`HttpClient`, o `BaseAddress` dele precisa bater com `BaseUrl` e **você** precisa desativar os
redirecionamentos no handler primário:

```csharp
using var handler = new SocketsHttpHandler { AllowAutoRedirect = false };
using var http = new HttpClient(handler)
{
    BaseAddress = new Uri("https://sandbox.assinafy.com.br/v1/"),
};
using var client = new AssinafyClient(
    new AssinafyClientOptions
    {
        ApiKey = apiKey,
        AccountId = accountId,
        BaseUrl = "https://sandbox.assinafy.com.br/v1",
    },
    http);
```

`AssinafyClient.CreatePrimaryHandler()` devolve exatamente o handler que o SDK usa no próprio
transporte — redirecionamentos desativados, tempo de vida de conexão de cinco minutos — então
`new HttpClient(AssinafyClient.CreatePrimaryHandler())` é a versão de uma linha do trecho acima.

As credenciais são anexadas por requisição, de modo que os headers padrão de um `HttpClient`
fornecido nunca são alterados e a instância continua segura para compartilhar. O `Timeout` dele fica
intocado — defina você mesmo.

## Métodos de verificação do signatário

Definidos por signatário ao criar o assignment. O método de verificação e o de notificação são
**acoplados**: envie um, os dois ou nenhum — o lado que faltar é inferido. Sem nenhum dos dois, ambos
assumem `Email`.

| Método | Como funciona | Custo por signatário |
| --- | --- | --- |
| `Email` *(padrão)* | Código de uso único (OTP) por e-mail, exigido antes de assinar | Gratuito |
| `Whatsapp` | Código de uso único (OTP) por WhatsApp | Verificação gratuita; notificação 0,45 crédito, só em planos pagos |
| `DigitalCertificate` | O signatário assina com o **próprio certificado ICP-Brasil (A1/A3)**, pela extensão de navegador Web PKI, gerando uma assinatura **PAdES qualificada** | 2 créditos |

Combinações permitidas: `Email` → notifica por `Email`; `Whatsapp` → notifica por `Whatsapp`;
`DigitalCertificate` → notifica por `Email` **ou** `Whatsapp`. Apenas um método de notificação por
signatário.

## Assinatura por certificado digital ICP-Brasil

Exige o recurso **Certificado Digital** na conta (planos Standard e Pro), CPF ou CNPJ em
`GovernmentId` do signatário, e exatamente **um signatário por certificado naquele passo**. Um CPF
exige o certificado daquela pessoa (e-CPF, ou e-CNPJ que a nomeie como representante legal); um CNPJ
exige um e-CNPJ da empresa.

Quando o método de verificação do assignment é `DigitalCertificate`, assinar é um handshake de dois
passos com a extensão Web PKI, e não o envio de campos:

```csharp
var operacao = await client.Signing.StartCertificateAsync(signerAccessCode);
var tokenAssinado = await AssinarComWebPkiAsync(operacao.Token);   // sua ponte com a Web PKI
var resultado = await client.Signing.CompleteCertificateAsync(signerAccessCode, tokenAssinado);

Console.WriteLine(resultado.SignerName);   // lido do certificado
```

> Ambas as rotas são extensões implantadas **somente em produção**: o sandbox não as expõe e elas não
> constam do documento OpenAPI publicado. Exigem um assignment de certificado real em produção e um
> token Web PKI assinado pelo navegador.

Concluído o fluxo, baixar o artefato `pades` devolve a assinatura PAdES qualificada.

## Trilha de atividades e artefatos

As atividades de um documento devolvem todos os eventos registrados, cada um com um snapshot do
`payload` do evento e a `origin` da requisição (`ip`, `user-agent`).

Artefatos disponíveis para download:

| Artefato | Conteúdo |
| --- | --- |
| `original` | O PDF enviado, como recebido |
| `certificated` | O documento assinado, com a certificação da plataforma |
| `certificate-page` | Apenas a página de certificação |
| `pades` | Assinaturas ICP-Brasil dos signatários + caixa de certificação — só existe em documentos que tiveram signatários por certificado digital |
| `bundle` | Zip com `original`, `certificated` e `certificate-page`, mais o `pades` quando houver |

A verificação pública confere um documento assinado pelo hash da assinatura, sem autenticação.

## Documentação

- **[README.en.md](README.en.md)** — referência completa por recurso, em inglês
- [docs/API.md](docs/API.md) — contrato por endpoint
- [Documentação da API](https://api.assinafy.com.br/v1/docs)

## Licença

Distribuído sob a licença [MIT](LICENSE).
