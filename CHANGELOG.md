# Changelog

## 1.0.0

- Aligned the SDK surface with the documented Assinafy API at `https://api.assinafy.com.br/v1/docs`.
- Added documented resources for authentication, fields, public documents, signer-facing signing flows, and signature images.
- Fixed request URI handling so the `/v1` base path is preserved for all relative SDK requests.
- Removed undocumented workspace CRUD and webhook HMAC verifier helpers.
- Removed Docker-only repository scaffolding; tests run with the .NET SDK via `dotnet test Assinafy.Sdk.sln`.
- Updated models for documented document, signer, assignment, template, field, webhook, authentication, and public document response shapes.
- Added focused tests for the documented SDK resources and HTTP request serialization.
