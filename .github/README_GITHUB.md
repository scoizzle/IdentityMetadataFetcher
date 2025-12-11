# IdentityMetadataFetcher

A .NET library for fetching and parsing SAML and WS-Federation identity metadata from multiple endpoints.

## Features

- Supports SAML 2.0 and WS-Federation metadata
- Multi-endpoint batch operations
- Async/await and synchronous APIs
- Configurable timeouts, retries, and SSL validation
- IIS module for automatic metadata polling and caching

## Requirements

- .NET Framework 4.5, 4.6, 4.7, or 4.8
- Windows environment (uses System.IdentityModel)

## Quick Start

```csharp
using IdentityMetadataFetcher.Models;
using IdentityMetadataFetcher.Services;

var fetcher = new MetadataFetcher();
var endpoint = new IssuerEndpoint
{
    Id = "my-idp",
    Endpoint = "https://idp.example.com/metadata",
    MetadataType = MetadataType.Saml
};

var result = await fetcher.FetchMetadataFromEndpointAsync(endpoint);
if (result.IsSuccess)
{
    Console.WriteLine($"Metadata retrieved for {result.Endpoint.Name}");
}
```

## Documentation

See [README.md](README.md) for full documentation.

## License

See LICENSE file for details.
