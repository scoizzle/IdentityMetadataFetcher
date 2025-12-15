# Identity Metadata Fetcher - Quick Reference Guide

## Installation & Setup

```csharp
using IdentityMetadataFetcher.Models;
using IdentityMetadataFetcher.Services;
using IdentityMetadataFetcher.Exceptions;

// Create fetcher with default options
var fetcher = new MetadataFetcher();

// Or with custom options
var options = new MetadataFetchOptions { DefaultTimeoutMs = 15000 };
var fetcher = new MetadataFetcher(options);
```

## Quick Examples

### Single Endpoint (Sync)
```csharp
var endpoint = new IssuerEndpoint
{
    Id = "issuer1",
    Endpoint = "https://issuer.example.com/metadata",
    Name = "My Issuer",

};

var result = fetcher.FetchMetadata(endpoint);
if (result.IsSuccess)
{
    var metadata = result.Metadata;  // Microsoft.IdentityModel.Protocols.WsFederation.WsFederationConfiguration
    // Process metadata...
}
```

### Single Endpoint (Async)
```csharp
var result = await fetcher.FetchMetadataAsync(endpoint);
```

### OIDC Endpoint
```csharp
var oidcEndpoint = new IssuerEndpoint
{
    Id = "google-oidc",
    Endpoint = "https://accounts.google.com/.well-known/openid-configuration",
    Name = "Google OIDC"
};

var result = await fetcher.FetchMetadataAsync(oidcEndpoint);
if (result.IsSuccess && result.Metadata is OpenIdConnectMetadataDocument oidcDoc)
{
    var config = oidcDoc.Configuration;
    Console.WriteLine($"Issuer: {config.Issuer}");
    Console.WriteLine($"Token Endpoint: {config.TokenEndpoint}");
}
```

### Multiple Endpoints (Sync)
```csharp
var endpoints = new[]
{
    new IssuerEndpoint { Id = "ep1", Endpoint = "...", Name = "EP1",},
    new IssuerEndpoint { Id = "ep2", Endpoint = "...", Name = "EP2",}
};

var results = fetcher.FetchMetadataFromMultipleEndpoints(endpoints);
foreach (var result in results)
{
    if (result.IsSuccess)
    {
        // Use result.Metadata
    }
    else
    {
        Console.WriteLine($"Error: {result.ErrorMessage}");
    }
}
```

### Multiple Endpoints (Async)
```csharp
var results = await fetcher.FetchMetadataFromMultipleEndpointsAsync(endpoints);
```

## Configuration Options

| Option | Type | Default | Purpose |
|--------|------|---------|---------|
| `DefaultTimeoutMs` | int | 30000 | HTTP request timeout in milliseconds |
| `ContinueOnError` | bool | true | Continue fetching other endpoints if one fails |
| `ValidateServerCertificate` | bool | true | Validate SSL/TLS certificates |
| `MaxRetries` | int | 0 | Number of retry attempts on failure |
| `CacheMetadata` | bool | false | Enable metadata caching (future) |
| `CacheDurationMinutes` | int | 60 | Cache TTL in minutes (future) |

## Common Patterns

### Development with Self-Signed Certificates
```csharp
var options = new MetadataFetchOptions { ValidateServerCertificate = false };
var fetcher = new MetadataFetcher(options);
```

### Resilient Fetching
```csharp
var options = new MetadataFetchOptions
{
    MaxRetries = 3,
    ContinueOnError = true,
    DefaultTimeoutMs = 20000
};
var fetcher = new MetadataFetcher(options);
```

### Per-Endpoint Timeout Override
```csharp
var endpoint = new IssuerEndpoint
{
    Endpoint = "https://slow-issuer.example.com/metadata",

    Timeout = 60000  // 60 seconds for this endpoint only
};
```

### Processing Results
```csharp
var results = fetcher.FetchMetadataFromMultipleEndpoints(endpoints);

var successful = results.Where(r => r.IsSuccess).ToList();
var failed = results.Where(r => !r.IsSuccess).ToList();

Console.WriteLine($"Success: {successful.Count}, Failed: {failed.Count}");

foreach (var result in successful)
{
    var entityDescriptor = result.Metadata as System.IdentityModel.Metadata.EntityDescriptor;
    var entityId = entityDescriptor?.EntityId;
}
```

## Error Handling

### Check Success Flag
```csharp
var result = fetcher.FetchMetadata(endpoint);
if (!result.IsSuccess)
{
    Console.WriteLine($"Error: {result.ErrorMessage}");
    if (result.Exception != null)
    {
        Console.WriteLine($"Details: {result.Exception}");
    }
}
```

### Catch Exceptions
```csharp
try
{
    var endpoint = new IssuerEndpoint { Endpoint = null,};
    fetcher.FetchMetadata(endpoint);  // Throws ArgumentException
}
catch (ArgumentException ex)
{
    Console.WriteLine($"Invalid argument: {ex.Message}");
}
catch (MetadataFetchException ex)
{
    Console.WriteLine($"Fetch failed at {ex.Endpoint}: {ex.Message}");
    if (ex.HttpStatusCode.HasValue)
    {
        Console.WriteLine($"HTTP Status: {ex.HttpStatusCode}");
    }
}
```

## Metadata Types



## Result Object Properties

```csharp
var result = fetcher.FetchMetadata(endpoint);

// General
result.IsSuccess              // bool - Was fetch successful?
result.FetchedAt              // DateTime - When was it fetched? (UTC)
result.Endpoint               // IssuerEndpoint - Which endpoint?

// On Success
result.Metadata               // MetadataDocument - Parsed metadata (WsFederationMetadataDocument or OpenIdConnectMetadataDocument)
result.RawMetadata            // string - Original XML or JSON

// On Failure
result.ErrorMessage           // string - Human-readable error
result.Exception              // Exception - Full exception details
```

## Common Metadata Properties

Once you have `WsFederationConfiguration`, access its properties:

```csharp
if (result.IsSuccess)
{
    var config = result.Metadata;
    Console.WriteLine($"Issuer: {config.Issuer}");
    Console.WriteLine($"Token Endpoint: {config.TokenEndpoint}");
    Console.WriteLine($"Signing Keys: {config.SigningKeys.Count}");
{
    var entityId = entity.EntityId?.Id;
    
    // Access role descriptors
    foreach (var role in entity.RoleDescriptors)
    {
        var roleName = role.GetType().Name;
        // Process role (IDPSSODescriptor, SPSSODescriptor, etc.)
    }
}
```

## Thread Safety

✓ **Thread-Safe**: MetadataFetcher is stateless and reentrant. Safe to use in:
- ASP.NET applications
- Concurrent console applications
- Windows Services
- Task Parallel Library (TPL) scenarios

```csharp
var fetcher = new MetadataFetcher();  // Single instance can handle concurrent requests

Task.WhenAll(
    fetcher.FetchMetadataAsync(endpoint1),
    fetcher.FetchMetadataAsync(endpoint2),
    fetcher.FetchMetadataAsync(endpoint3)
);
```

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Timeout errors | Increase `DefaultTimeoutMs` or set per-endpoint `Timeout` |
| SSL certificate errors | Set `ValidateServerCertificate = false` for dev/test only |
| Network errors | Set `MaxRetries > 0` for automatic retry |
| "Invalid metadata XML" | Verify endpoint returns valid XML |
| ArgumentException thrown | Check for null endpoints and valid endpoint URLs |

## Performance Tips

1. **Use Async**: For better scalability in web applications
   ```csharp
   await fetcher.FetchMetadataFromMultipleEndpointsAsync(endpoints)
   ```

2. **Batch Requests**: Fetch multiple endpoints together
   ```csharp
   // Good: Single call with multiple endpoints
   var results = fetcher.FetchMetadataFromMultipleEndpoints(endpoints);
   
   // Avoid: Multiple separate calls
   foreach (var endpoint in endpoints)
       var result = fetcher.FetchMetadata(endpoint);
   ```

3. **Appropriate Timeouts**: Don't use excessively long timeouts
   ```csharp
   DefaultTimeoutMs = 30000  // 30 seconds is usually enough
   ```

4. **Reuse Instance**: Create once, use multiple times
   ```csharp
   // Good: Single instance
   private static readonly MetadataFetcher _fetcher = new MetadataFetcher();
   
   // Avoid: Creating new instances repeatedly
   var fetcher = new MetadataFetcher();  // Create only once
   ```

## API Reference Quick Links

- **IMetadataFetcher** - Service interface
- **MetadataFetcher** - Service implementation  
- **IssuerEndpoint** - Endpoint model
- **MetadataFetchResult** - Result model
- **MetadataFetchOptions** - Configuration model
- **MetadataFetchException** - Custom exception

## Dependencies

Built-in Framework dependencies only:
- `System.IdentityModel`
- `System.IdentityModel.Metadata`
- `System.IdentityModel.Services`
- `System.Net.Http`
- `System.Xml`

No NuGet packages required for the library.

## Version Info

- **Version**: 1.0.0
- **Target Framework**: .NET Framework 4.5+
- **License**: [Your License]
- **Status**: Production Ready

---

**For full documentation, see README.md and DESIGN.md**
