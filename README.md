# SAML Metadata Fetcher

[![Build Status](https://github.com/scoizzle/IdentityMetadataFetcher/actions/workflows/build.yml/badge.svg)](https://github.com/scoizzle/IdentityMetadataFetcher/actions/workflows/build.yml)

A production-ready .NET class library for fetching and parsing SAML and WS-Federation (WSFED) metadata from multiple identity provider endpoints. Includes an IIS module for automatic metadata polling and caching.

## Features

- **📦 Dual Components**: Core library for direct usage + IIS HTTP module for ASP.NET applications
- **🔄 Multiple Metadata Types**: Support for both WSFED and SAML metadata formats
- **🚀 Sync & Async APIs**: Choose between blocking and async/await patterns for optimal performance
- **⚙️ Highly Configurable**: Control timeouts, retries, SSL validation, and error handling
- **🛡️ Production-Ready**: Comprehensive error handling, thread-safe design, and full unit test coverage
- **🔌 Zero Dependencies**: Uses only .NET Framework built-in assemblies
- **♻️ IIS Auto-Polling**: Automatic background metadata refresh with configurable intervals
- **💾 In-Memory Caching**: Fast access to cached metadata in ASP.NET applications

## Requirements

- .NET Framework 4.5, 4.6, 4.7, or 4.8
- System.IdentityModel (built-in to .NET Framework)
- System.IdentityModel.Metadata (built-in to .NET Framework)
- System.IdentityModel.Services (built-in to .NET Framework)

> **⚠️ Windows Only**: This library targets .NET Framework and uses Windows-specific assemblies (System.IdentityModel). It requires a Windows environment to build and run.

---

## 📚 Table of Contents

- [Library Usage](#-library-usage)
  - [Installation](#installation)
  - [Basic Examples](#basic-examples)
  - [Configuration Options](#configuration-options)
- [IIS Module](#-iis-module)
  - [Installation & Setup](#iis-module-installation)
  - [Configuration](#iis-module-configuration)
  - [Usage in Code](#usage-in-code)
- [Building from Source](#-building-from-source)
  - [Prerequisites](#prerequisites)
  - [Build Instructions](#build-instructions)
  - [Running Tests](#running-tests)
- [Additional Resources](#additional-resources)

---

## 📖 Library Usage

### Installation

#### Option 1: Build from Source

1. Clone the repository
2. Build the `IdentityMetadataFetcher.csproj` project
3. Reference the resulting DLL in your application
4. Add a reference to `System.IdentityModel.Metadata` in your project

```bash
# Using dotnet CLI (requires Windows)
dotnet build src/IdentityMetadataFetcher/IdentityMetadataFetcher.csproj

# Or using MSBuild
msbuild src/IdentityMetadataFetcher/IdentityMetadataFetcher.csproj
```

#### Option 2: Reference the DLL

Add a reference to `IdentityMetadataFetcher.dll` in your project and include the required using statements:

```csharp
using IdentityMetadataFetcher.Models;
using IdentityMetadataFetcher.Services;
```

### Basic Examples

#### Synchronous - Single Endpoint

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

// Fetch metadata synchronously
var result = fetcher.FetchMetadata(endpoint);

if (result.IsSuccess)
{
    // Use the metadata
    var metadata = result.Metadata;
    Console.WriteLine($"✓ Successfully fetched metadata from {result.Endpoint.Name}");
    Console.WriteLine($"  Fetched at: {result.FetchedAt:O}");
}
else
{
    Console.WriteLine($"✗ Error: {result.ErrorMessage}");
}
```

#### Asynchronous - Single Endpoint

```csharp
// Async fetch from single endpoint
var result = await fetcher.FetchMetadataAsync(endpoint);

if (result.IsSuccess)
{
    Console.WriteLine($"✓ Metadata retrieved successfully");
}
```

#### Synchronous - Multiple Endpoints

```csharp
var endpoints = new[]
{
    new IssuerEndpoint
    {
        Id = "saml-provider",
        Endpoint = "https://issuer1.example.com/metadata",
        Name = "SAML Identity Provider",
        MetadataType = MetadataType.SAML
    },
    new IssuerEndpoint
    {
        Id = "wsfed-provider",
        Endpoint = "https://issuer2.example.com/metadata",
        Name = "WS-Fed Identity Provider",
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

#### Asynchronous - Multiple Endpoints

```csharp
// Fetch from all endpoints asynchronously for better performance
var results = await fetcher.FetchMetadataFromMultipleEndpointsAsync(endpoints);

// Process results
var successCount = results.Count(r => r.IsSuccess);
var failureCount = results.Count(r => !r.IsSuccess);
Console.WriteLine($"Completed: {successCount} succeeded, {failureCount} failed");
```

#### Processing Retrieved Metadata

```csharp
var result = await fetcher.FetchMetadataAsync(endpoint);

if (result.IsSuccess)
{
    // Cast to appropriate type based on metadata type
    if (result.Metadata is EntityDescriptor entityDescriptor)
    {
        var entityId = entityDescriptor.EntityId?.Id;
        Console.WriteLine($"Entity ID: {entityId}");
        
        // Access SAML/WSFED-specific information
        foreach (var role in entityDescriptor.RoleDescriptors)
        {
            Console.WriteLine($"Role: {role.GetType().Name}");
            // Process role descriptor (IDPSSODescriptor, SPSSODescriptor, etc.)
        }
    }
    
    // Access raw XML metadata if needed
    var rawXml = result.RawMetadata;
}
```

### Configuration Options

Control the behavior of metadata fetching using `MetadataFetchOptions`:

```csharp
var options = new MetadataFetchOptions
{
    DefaultTimeoutMs = 30000,              // 30 second timeout (default)
    ContinueOnError = true,                // Keep fetching even if one endpoint fails
    ValidateServerCertificate = true,      // Validate SSL/TLS certificates (recommended)
    MaxRetries = 2,                        // Retry failed requests up to 2 times
    CacheMetadata = false,                 // Disable caching (reserved for future use)
    CacheDurationMinutes = 60              // Cache duration if enabled (future)
};

var fetcher = new MetadataFetcher(options);
```

#### Configuration Reference

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `DefaultTimeoutMs` | int | 30000 | HTTP request timeout in milliseconds |
| `ContinueOnError` | bool | true | Continue fetching remaining endpoints if one fails |
| `ValidateServerCertificate` | bool | true | Validate SSL/TLS certificates (disable only for dev/test) |
| `MaxRetries` | int | 0 | Number of retry attempts on failure (0-5) |
| `CacheMetadata` | bool | false | Reserved for future caching implementation |
| `CacheDurationMinutes` | int | 60 | Cache TTL in minutes (reserved for future) |

#### Advanced Configuration Examples

**Development with Self-Signed Certificates:**

```csharp
var options = new MetadataFetchOptions 
{ 
    ValidateServerCertificate = false  // ⚠️ WARNING: Only for development!
};
var fetcher = new MetadataFetcher(options);
```

**Resilient Fetching with Retries:**

```csharp
var options = new MetadataFetchOptions
{
    MaxRetries = 3,           // Retry up to 3 times
    DefaultTimeoutMs = 15000, // 15 second timeout
    ContinueOnError = true    // Don't stop on first failure
};
var fetcher = new MetadataFetcher(options);
```

**Per-Endpoint Timeout Override:**

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

---

## 🔌 IIS Module

The `IdentityMetadataFetcher.Iis` module is an ASP.NET HTTP Module that automatically polls SAML/WSFED metadata endpoints and maintains an in-memory cache. This enables ASP.NET applications to use up-to-date metadata for identity validation without manual intervention.

### IIS Module Installation

#### 1. Deploy Assemblies

Copy both DLLs to your ASP.NET application's `bin` directory:
- `IdentityMetadataFetcher.dll` (core library)
- `IdentityMetadataFetcher.Iis.dll` (IIS module)

#### 2. Register Module in Web.config

Add the module registration to your `Web.config` in the `<system.webServer>` section:

```xml
<configuration>
  <system.webServer>
    <modules>
      <add name="SamlMetadataPollingModule" 
           type="IdentityMetadataFetcher.Iis.Modules.MetadataPollingHttpModule, IdentityMetadataFetcher.Iis" />
    </modules>
  </system.webServer>
</configuration>
```

### IIS Module Configuration

Add configuration sections to define metadata endpoints and polling behavior:

```xml
<configuration>
  <configSections>
    <section name="samlMetadataPolling" 
             type="IdentityMetadataFetcher.Iis.Configuration.MetadataPollingConfigurationSection, IdentityMetadataFetcher.Iis" />
  </configSections>
  
  <samlMetadataPolling enabled="true" 
                        pollingIntervalMinutes="60" 
                        httpTimeoutSeconds="30"
                        validateServerCertificate="true"
                        maxRetries="1">
    <issuers>
      <!-- Azure AD -->
      <add id="azure-ad" 
           endpoint="https://login.microsoftonline.com/common/federationmetadata/2007-06/federationmetadata.xml" 
           name="Azure Active Directory" 
           metadataType="WSFED" />
      
      <!-- Auth0 with custom timeout -->
      <add id="auth0" 
           endpoint="https://example.auth0.com/samlp/metadata" 
           name="Auth0" 
           metadataType="SAML" 
           timeoutSeconds="45" />
      
      <!-- Okta -->
      <add id="okta" 
           endpoint="https://dev-12345.okta.com/app/123/sso/saml/metadata" 
           name="Okta" 
           metadataType="SAML" />
    </issuers>
  </samlMetadataPolling>
</configuration>
```

#### Configuration Reference

**Root Element: `<samlMetadataPolling>`**

| Attribute | Type | Default | Required | Description |
|-----------|------|---------|----------|-------------|
| `enabled` | bool | true | No | Enable/disable the polling service |
| `pollingIntervalMinutes` | int | 60 | No | How often to poll (1-10080 minutes) |
| `httpTimeoutSeconds` | int | 30 | No | HTTP request timeout (5-300 seconds) |
| `validateServerCertificate` | bool | true | No | Validate SSL/TLS certificates |
| `maxRetries` | int | 1 | No | Retry failed requests (0-5) |

**Child Element: `<issuers>` Collection**

Each `<add>` element defines an issuer endpoint:

| Attribute | Type | Required | Description |
|-----------|------|----------|-------------|
| `id` | string | Yes | Unique identifier for the issuer |
| `endpoint` | string | Yes | Full URL to the metadata endpoint |
| `name` | string | Yes | Human-readable issuer name |
| `metadataType` | enum | Yes | Either "WSFED" or "SAML" |
| `timeoutSeconds` | int | No | Override default timeout for this endpoint (5-300) |

#### Complete Web.config Example

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <configSections>
    <section name="samlMetadataPolling" 
             type="IdentityMetadataFetcher.Iis.Configuration.MetadataPollingConfigurationSection, IdentityMetadataFetcher.Iis" />
  </configSections>

  <!-- SAML Metadata Polling Configuration -->
  <samlMetadataPolling enabled="true" 
                        pollingIntervalMinutes="60" 
                        httpTimeoutSeconds="30"
                        validateServerCertificate="true"
                        maxRetries="1">
    <issuers>
      <!-- Azure AD -->
      <add id="azure-ad" 
           endpoint="https://login.microsoftonline.com/common/federationmetadata/2007-06/federationmetadata.xml" 
           name="Azure Active Directory" 
           metadataType="WSFED" />
      
      <!-- Auth0 with custom timeout -->
      <add id="auth0" 
           endpoint="https://example.auth0.com/samlp/metadata" 
           name="Auth0" 
           metadataType="SAML" 
           timeoutSeconds="45" />
      
      <!-- Okta -->
      <add id="okta" 
           endpoint="https://dev-12345.okta.com/app/123/sso/saml/metadata" 
           name="Okta" 
           metadataType="SAML" />
    </issuers>
  </samlMetadataPolling>

  <system.webServer>
    <modules>
      <add name="SamlMetadataPollingModule" 
           type="IdentityMetadataFetcher.Iis.Modules.MetadataPollingHttpModule, IdentityMetadataFetcher.Iis" />
    </modules>
  </system.webServer>

  <startup>
    <supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.5" />
  </startup>
</configuration>
```

### Usage in Code

Once the IIS module is initialized, access cached metadata from anywhere in your ASP.NET application:

#### Accessing Cached Metadata

```csharp
using IdentityMetadataFetcher.Iis.Modules;
using System.IdentityModel.Metadata;

// Get the cache instance
var cache = MetadataPollingHttpModule.MetadataCache;

// Retrieve metadata for an issuer by ID
var metadata = cache.GetMetadata("azure-ad");

if (metadata is EntityDescriptor entity)
{
    var entityId = entity.EntityId?.Id;
    Console.WriteLine($"Entity ID: {entityId}");
    // Use the metadata for authentication/authorization
}

// Check if metadata is available
if (cache.HasMetadata("auth0"))
{
    var auth0Metadata = cache.GetMetadata("auth0");
    // Process Auth0 metadata...
}

// Get cache entry with timestamp
var entry = cache.GetCacheEntry("okta");
if (entry != null)
{
    Console.WriteLine($"Metadata cached at: {entry.CachedAt:O}");
    var age = DateTime.UtcNow - entry.CachedAt;
    Console.WriteLine($"Metadata age: {age.TotalMinutes:F2} minutes");
}
```

#### Manually Triggering Polling

```csharp
using IdentityMetadataFetcher.Iis.Modules;

var pollingService = MetadataPollingHttpModule.PollingService;

if (pollingService != null)
{
    // Manually trigger polling (useful for manual refresh)
    await pollingService.PollNowAsync();
    Console.WriteLine("Manual polling triggered");
}
```

#### Subscribing to Events

The polling service raises events for monitoring and diagnostics:

```csharp
var service = MetadataPollingHttpModule.PollingService;

// Fired when polling starts
service.PollingStarted += (sender, e) =>
{
    System.Diagnostics.Trace.TraceInformation($"Polling started at {e.StartTime:O}");
};

// Fired when polling completes
service.PollingCompleted += (sender, e) =>
{
    System.Diagnostics.Trace.TraceInformation(
        $"Polling completed: {e.SuccessCount} success, {e.FailureCount} failures, " +
        $"Duration: {e.Duration?.TotalSeconds:F2}s");
};

// Fired when an individual endpoint fails
service.PollingError += (sender, e) =>
{
    System.Diagnostics.Trace.TraceWarning(
        $"Error polling {e.IssuerName}: {e.ErrorMessage}");
    
    // Log to your monitoring system
    logger.LogWarning($"Metadata polling failed: {e.IssuerName}", e.Exception);
};

// Fired when metadata is successfully updated
service.MetadataUpdated += (sender, e) =>
{
    System.Diagnostics.Trace.TraceInformation(
        $"Metadata updated: {e.IssuerName} at {e.UpdatedAt:O}");
    
    // Trigger cache invalidation or other actions
    logger.LogInformation($"Metadata refreshed: {e.IssuerName}");
};
```

#### IIS Module Features

- ✅ **Automatic Polling**: Starts when application initializes, performs immediate initial fetch, then polls at configured intervals
- ✅ **Thread-Safe Caching**: All caching operations are thread-safe for concurrent requests
- ✅ **Non-Blocking**: Uses async/await for non-blocking background operations
- ✅ **Resilient**: Continues polling other endpoints if one fails
- ✅ **Event-Driven**: Subscribe to events for monitoring and diagnostics
- ✅ **Configuration Validation**: Validates configuration on startup with clear error messages

---

## 🔨 Building from Source

### Prerequisites

Before building the project, ensure you have the following installed:

- **Windows OS** (required for .NET Framework)
- **.NET Framework 4.5 or higher** - [Download](https://dotnet.microsoft.com/download/dotnet-framework)
- **Visual Studio 2015+** (recommended) or **MSBuild 14.0+**
- **NUnit 3.x** (for running tests, optional)

### Build Instructions

#### Option 1: Visual Studio (Easiest)

1. Open the solution file:
   ```powershell
   start IdentityMetadataFetcher.sln
   ```

2. In Visual Studio:
   - Select **Build > Build Solution** (or press `Ctrl+Shift+B`)
   - Output appears in `bin/Debug` or `bin/Release`

#### Option 2: MSBuild Command Line

```bash
# Navigate to repository root
cd /path/to/IdentityMetadataFetcher

# Restore NuGet packages (if needed)
nuget restore IdentityMetadataFetcher.sln

# Debug build
msbuild IdentityMetadataFetcher.sln /p:Configuration=Debug

# Release build
msbuild IdentityMetadataFetcher.sln /p:Configuration=Release

# Clean build
msbuild IdentityMetadataFetcher.sln /t:Clean /p:Configuration=Debug
msbuild IdentityMetadataFetcher.sln /p:Configuration=Debug
```

#### Option 3: dotnet CLI

```bash
# Build entire solution
dotnet build IdentityMetadataFetcher.sln

# Build specific project
dotnet build src/IdentityMetadataFetcher/IdentityMetadataFetcher.csproj

# Build for Release
dotnet build IdentityMetadataFetcher.sln --configuration Release
```

### Running Tests

#### In Visual Studio

1. Open **Test Explorer**: `Test > Windows > Test Explorer`
2. Tests appear in the list
3. Click **Run All** or select individual tests
4. View results in the Test Explorer window

#### Command Line with NUnit

```bash
# Install NUnit console runner (first time only)
nuget install NUnit.ConsoleRunner -Version 3.16.3 -OutputDirectory packages

# Run all tests
./packages/NUnit.ConsoleRunner.3.16.3/tools/nunit3-console.exe \
  tests/IdentityMetadataFetcher.Tests/bin/Debug/IdentityMetadataFetcher.Tests.dll

# Run tests with XML output
./packages/NUnit.ConsoleRunner.3.16.3/tools/nunit3-console.exe \
  tests/IdentityMetadataFetcher.Tests/bin/Debug/IdentityMetadataFetcher.Tests.dll \
  --result=TestResults.xml
```

#### Using dotnet test

```bash
# Run all tests in solution
dotnet test IdentityMetadataFetcher.sln

# Run with detailed output
dotnet test IdentityMetadataFetcher.sln --verbosity detailed

# Run specific test project
dotnet test tests/IdentityMetadataFetcher.Tests/IdentityMetadataFetcher.Tests.csproj
```

### Build Output

After building, find the compiled assemblies:

```
src/IdentityMetadataFetcher/bin/Debug/IdentityMetadataFetcher.dll
src/IdentityMetadataFetcher/bin/Release/IdentityMetadataFetcher.dll
src/IdentityMetadataFetcher.Iis/bin/Debug/IdentityMetadataFetcher.Iis.dll
src/IdentityMetadataFetcher.Iis/bin/Release/IdentityMetadataFetcher.Iis.dll
```

### Verification

Verify the build was successful:

```bash
# Check if DLLs were created
ls src/IdentityMetadataFetcher/bin/Debug/IdentityMetadataFetcher.dll
ls src/IdentityMetadataFetcher.Iis/bin/Debug/IdentityMetadataFetcher.Iis.dll

# Run quick test
dotnet test tests/IdentityMetadataFetcher.Tests/IdentityMetadataFetcher.Tests.csproj
```

---

## Additional Resources

### Documentation

- **[IIS_MODULE_USAGE.md](IIS_MODULE_USAGE.md)** - Detailed IIS module documentation
- **[BUILD.md](BUILD.md)** - Advanced build instructions and CI/CD examples
- **[QUICKREF.md](QUICKREF.md)** - Quick reference guide with code snippets
- **[DESIGN.md](DESIGN.md)** - Architecture and design documentation
- **[CHANGELOG.md](CHANGELOG.md)** - Version history and changes

### API Reference

#### Core Classes

- **`MetadataFetcher`** - Main service for fetching metadata
- **`IMetadataFetcher`** - Service interface for dependency injection
- **`IssuerEndpoint`** - Endpoint configuration model
- **`MetadataFetchResult`** - Result container with success/failure information
- **`MetadataFetchOptions`** - Configuration options for fetcher behavior
- **`MetadataType`** - Enum: `WSFED` or `SAML`

#### IIS Module Classes

- **`MetadataPollingHttpModule`** - HTTP module for ASP.NET
- **`MetadataCache`** - Thread-safe metadata cache
- **`MetadataPollingService`** - Background polling service

### Security Considerations

1. **SSL/TLS Certificate Validation**: By default, the library validates server certificates. Only disable validation (`ValidateServerCertificate = false`) in development/test environments.

2. **Metadata Sources**: Only fetch metadata from trusted issuer endpoints.

3. **Error Information**: Exception details may contain sensitive information about endpoints. Handle exceptions carefully in production environments.

4. **Timeout Configuration**: Set appropriate timeouts to prevent resource exhaustion from slow or unresponsive endpoints.

### Performance Tips

1. **Use Async Methods**: For better scalability when fetching from multiple endpoints
2. **Batch Operations**: Fetch from multiple endpoints in a single operation rather than individual calls
3. **IIS Module**: Use the IIS module for ASP.NET applications to avoid repeated fetching
4. **Appropriate Timeouts**: Set realistic timeout values based on network conditions

### Troubleshooting

| Problem | Solution |
|---------|----------|
| **HttpRequestException** | Verify endpoint URL, check network/firewall, validate SSL certificate, increase timeout |
| **MetadataFetchException** | Ensure endpoint returns valid metadata, verify `MetadataType` is correct |
| **TimeoutException** | Increase `DefaultTimeoutMs` or per-endpoint `Timeout`, check endpoint responsiveness |
| **Build Errors** | Ensure .NET Framework 4.5+ is installed, restore NuGet packages, clean and rebuild |
| **IIS Module Not Loading** | Verify DLLs are in bin directory, check Web.config registration, review IIS logs |

### License

This library is provided as-is for use in your applications.

### Support

For issues, questions, or contributions, please refer to:
- Source code documentation and inline comments
- Unit tests for usage examples
- Additional documentation files in the repository

---

**Version**: 1.0.0  
**Target Framework**: .NET Framework 4.5+  
**Status**: Production Ready ✅
