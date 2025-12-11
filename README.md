# SAML Metadata Fetcher

A .NET class library for fetching and parsing metadata for WS-Federation (WSFED) and SAML issuers from multiple endpoints. Built with support for .NET Framework 4.5 and higher.

## Features

- **Multiple Metadata Types**: Supports both WSFED and SAML metadata formats
- **Multiple Endpoint Support**: Fetch metadata from multiple issuing authorities in a single operation
- **Synchronous & Asynchronous APIs**: Choose between blocking and async/await patterns
- **System.IdentityModel.Metadata Integration**: Leverages the robust `MetadataSerializer` class from the .NET Framework
- **Configurable Options**: Control timeouts, retries, SSL validation, caching preferences, and error handling behavior
- **Error Handling**: Comprehensive exception handling with detailed error information
- **Resilient**: Optional retry logic and continue-on-error configuration for batch operations

## Requirements

- .NET Framework 4.5, 4.6, 4.7, or 4.8
- System.IdentityModel (built-in to .NET Framework)
- System.IdentityModel.Metadata (built-in to .NET Framework)
- System.IdentityModel.Services (built-in to .NET Framework)

> **⚠️ Windows Only**: This library targets .NET Framework and uses Windows-specific assemblies (System.IdentityModel). It requires a Windows environment to build and run. Use Windows, Visual Studio, or GitHub Actions with `windows-latest` runners.

## Installation

### Using the Library

1. Build the `IdentityMetadataFetcher.csproj` project
2. Reference the resulting DLL in your application
3. Add a reference to `System.IdentityModel.Metadata` in your project

### From Source

Clone or extract the source code and build using MSBuild, Visual Studio 2015+, or dotnet CLI:

```bash
# Using dotnet CLI (requires Windows)
dotnet build IdentityMetadataFetcher.sln

# Or using MSBuild
msbuild IdentityMetadataFetcher.sln
```

## Quick Start

### Basic Usage - Single Endpoint

```csharp
using IdentityMetadataFetcher.Models;
using IdentityMetadataFetcher.Services;

// Create a fetcher instance
var fetcher = new MetadataFetcher();

// Define an issuer endpoint
var endpoint = new IssuerEndpoint
{
    Id = "azure-ad",
    Endpoint = "https://login.microsoftonline.com/common/federationmetadata/2007-06/federationmetadata.xml",
    Name = "Azure AD",
    MetadataType = MetadataType.WSFED
};

// Fetch metadata
var result = fetcher.FetchMetadata(endpoint);

if (result.IsSuccess)
{
    // Use the metadata
    var metadata = result.Metadata;
    Console.WriteLine($"Successfully fetched metadata from {result.Endpoint.Name}");
}
else
{
    Console.WriteLine($"Error: {result.ErrorMessage}");
}
```

### Asynchronous Usage

```csharp
// Async fetch from single endpoint
var result = await fetcher.FetchMetadataAsync(endpoint);
```

### Multiple Endpoints

```csharp
var endpoints = new[]
{
    new IssuerEndpoint
    {
        Id = "issuer1",
        Endpoint = "https://issuer1.example.com/metadata",
        Name = "Issuer 1",
        MetadataType = MetadataType.SAML
    },
    new IssuerEndpoint
    {
        Id = "issuer2",
        Endpoint = "https://issuer2.example.com/metadata",
        Name = "Issuer 2",
        MetadataType = MetadataType.WSFED
    }
};

// Fetch from all endpoints
var results = fetcher.FetchMetadataFromMultipleEndpoints(endpoints);

foreach (var result in results)
{
    if (result.IsSuccess)
    {
        Console.WriteLine($"✓ {result.Endpoint.Name}");
    }
    else
    {
        Console.WriteLine($"✗ {result.Endpoint.Name}: {result.ErrorMessage}");
    }
}
```

### Async Multiple Endpoints

```csharp
// Fetch from all endpoints asynchronously
var results = await fetcher.FetchMetadataFromMultipleEndpointsAsync(endpoints);
```

## Configuration

Control the behavior of metadata fetching using `MetadataFetchOptions`:

```csharp
var options = new MetadataFetchOptions
{
    DefaultTimeoutMs = 30000,              // 30 second timeout
    ContinueOnError = true,                // Keep fetching even if one endpoint fails
    ValidateServerCertificate = true,      // Validate SSL/TLS certificates
    MaxRetries = 2,                        // Retry failed requests up to 2 times
    CacheMetadata = false,                 // Don't cache results
    CacheDurationMinutes = 60              // Cache duration (if enabled)
};

var fetcher = new MetadataFetcher(options);
```

## API Reference

### MetadataFetcher Class

#### Constructor

```csharp
// Default constructor with standard options
public MetadataFetcher()

// Constructor with custom options
public MetadataFetcher(MetadataFetchOptions options)
```

#### Methods

```csharp
// Synchronous methods
MetadataFetchResult FetchMetadata(IssuerEndpoint endpoint)
IEnumerable<MetadataFetchResult> FetchMetadataFromMultipleEndpoints(IEnumerable<IssuerEndpoint> endpoints)

// Asynchronous methods
Task<MetadataFetchResult> FetchMetadataAsync(IssuerEndpoint endpoint)
Task<IEnumerable<MetadataFetchResult>> FetchMetadataFromMultipleEndpointsAsync(IEnumerable<IssuerEndpoint> endpoints)
```

### IssuerEndpoint Class

Properties:

- `string Id` - Unique identifier for the endpoint
- `string Endpoint` - The URL endpoint to fetch metadata from
- `string Name` - Human-readable name for the issuer
- `MetadataType MetadataType` - Type of metadata (WSFED or SAML)
- `int? Timeout` - Optional timeout in milliseconds (overrides default)

### MetadataFetchResult Class

Properties:

- `IssuerEndpoint Endpoint` - The endpoint that was queried
- `bool IsSuccess` - Whether the fetch was successful
- `MetadataBase Metadata` - The parsed metadata (if successful)
- `string RawMetadata` - The raw XML metadata (if successful)
- `Exception Exception` - The exception that occurred (if failed)
- `string ErrorMessage` - Description of the error (if failed)
- `DateTime FetchedAt` - When the metadata was fetched (UTC)

### MetadataType Enum

```csharp
public enum MetadataType
{
    WSFED,  // WS-Federation metadata
    SAML    // SAML metadata
}
```

### MetadataFetchException

Exception thrown when metadata operations fail:

```csharp
public class MetadataFetchException : Exception
{
    public string Endpoint { get; set; }      // The endpoint that failed
    public int? HttpStatusCode { get; set; }  // HTTP status code if applicable
}
```

## Examples

### Example 1: Handling Certificate Validation Issues

For development or testing with self-signed certificates:

```csharp
var options = new MetadataFetchOptions
{
    ValidateServerCertificate = false  // ⚠️ WARNING: Only for development!
};

var fetcher = new MetadataFetcher(options);
```

### Example 2: Resilient Fetching with Retries

```csharp
var options = new MetadataFetchOptions
{
    MaxRetries = 3,           // Retry up to 3 times
    DefaultTimeoutMs = 15000, // 15 second timeout
    ContinueOnError = true    // Don't stop on first failure
};

var fetcher = new MetadataFetcher(options);
var results = fetcher.FetchMetadataFromMultipleEndpoints(endpoints);
```

### Example 3: Per-Endpoint Timeout Override

```csharp
var endpoint = new IssuerEndpoint
{
    Id = "slow-issuer",
    Endpoint = "https://slow-issuer.example.com/metadata",
    Name = "Slow Issuer",
    MetadataType = MetadataType.SAML,
    Timeout = 60000  // 60 second timeout just for this endpoint
};

var result = fetcher.FetchMetadata(endpoint);
```

### Example 4: Processing Metadata

```csharp
var result = await fetcher.FetchMetadataAsync(endpoint);

if (result.IsSuccess)
{
    // Cast to appropriate type based on metadata type
    if (result.Metadata is EntityDescriptor entityDescriptor)
    {
        var entityId = entityDescriptor.EntityId;
        
        // Access SAML/WSFED-specific information
        foreach (var role in entityDescriptor.RoleDescriptors)
        {
            // Process role descriptor
        }
    }
}
```

## Testing

The solution includes a test project (`IdentityMetadataFetcher.Tests`) with unit tests using NUnit.

### Run Tests

```bash
# Using NUnit console runner
nunit3-console IdentityMetadataFetcher.Tests.dll

# Or using Visual Studio Test Explorer
# Build the solution and run tests from Test > Run All Tests
```

### Test Coverage

- Constructor validation
- Null/empty parameter handling
- Single endpoint fetching (sync and async)
- Multiple endpoint fetching (sync and async)
- Error handling and resilience
- Configuration options
- Model serialization

## Architecture

```
IdentityMetadataFetcher/
├── Models/
│   ├── IssuerEndpoint.cs          # Endpoint configuration
│   ├── MetadataFetchResult.cs     # Result container
│   └── MetadataFetchOptions.cs    # Fetch options
├── Services/
│   ├── IMetadataFetcher.cs        # Service interface
│   └── MetadataFetcher.cs         # Service implementation
└── Exceptions/
    └── MetadataFetchException.cs  # Custom exceptions
```

## Security Considerations

1. **SSL/TLS Certificate Validation**: By default, the library validates server certificates. Only disable validation (`ValidateServerCertificate = false`) in development/test environments.

2. **Metadata Sources**: Only fetch metadata from trusted issuer endpoints.

3. **Error Information**: Exception details may contain sensitive information about endpoints. Handle exceptions carefully in production environments.

4. **Timeout Configuration**: Set appropriate timeouts to prevent resource exhaustion from slow or unresponsive endpoints.

## Performance Tips

1. **Async Operations**: Use async methods for better scalability when fetching from multiple endpoints
2. **Batching**: Fetch from multiple endpoints in a single operation rather than individual calls
3. **Caching**: Consider implementing metadata caching at the application level for frequently accessed data
4. **Timeout Tuning**: Adjust timeout values based on your network conditions and endpoint responsiveness

## Troubleshooting

### Common Issues

**HttpRequestException when fetching metadata**
- Verify the endpoint URL is correct and accessible
- Check network connectivity and firewall rules
- Validate SSL/TLS certificate if using HTTPS
- Increase timeout if endpoints are slow

**MetadataFetchException: Failed to parse metadata**
- Ensure the endpoint returns valid WSFED or SAML metadata
- Verify the MetadataType is set correctly
- Check that metadata is well-formed XML

**TimeoutException**
- Increase DefaultTimeoutMs in MetadataFetchOptions
- Check endpoint responsiveness
- Verify network latency

## Dependencies

The library uses only the .NET Framework Class Library:
- System (core types)
- System.IdentityModel (WIF - Windows Identity Foundation)
- System.IdentityModel.Metadata (MetadataSerializer)
- System.IdentityModel.Services (Federation services)
- System.Net.Http (HTTP client)
- System.Xml (XML parsing)

No third-party NuGet packages are required for the main library.

## License

This library is provided as-is for use in your applications.

## Support

For issues, questions, or suggestions, please refer to the source code documentation and unit tests for usage examples.

## Changelog

### Version 1.0.0
- Initial release
- Support for WSFED and SAML metadata fetching
- Multiple endpoint support
- Synchronous and asynchronous APIs
- Comprehensive configuration options
- Full unit test coverage
