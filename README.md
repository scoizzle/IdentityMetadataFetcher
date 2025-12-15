# SAML Metadata Fetcher

[![Build Status](https://github.com/scoizzle/IdentityMetadataFetcher/actions/workflows/build.yml/badge.svg)](https://github.com/scoizzle/IdentityMetadataFetcher/actions/workflows/build.yml)

A production-ready .NET class library for fetching and parsing SAML and WS-Federation (WSFED) metadata from multiple identity provider endpoints. Includes an IIS module for automatic metadata polling and caching.

## 🚀 Getting Started

The fastest way to try it is the console utility:

```powershell
# Build (Windows)
msbuild /t:restore
msbuild /t:build /p:GenerateFullPaths=true /consoleloggerparameters:NoSummary

# Run: fetch and summarize metadata
IdentityMetadataFetcher.Console.exe https://login.microsoftonline.com/common/federationmetadata/2007-06/federationmetadata.xml

# Print raw XML too
IdentityMetadataFetcher.Console.exe https://login.microsoftonline.com/common/federationmetadata/2007-06/federationmetadata.xml --raw
```

See full details in the Console Utility section below.

## Features

- **📦 Dual Components**: Core library for direct usage + IIS HTTP module for ASP.NET applications
- **🔄 Multiple Metadata Types**: Support for both WSFED and SAML metadata formats
- **🚀 Sync & Async APIs**: Choose between blocking and async/await patterns for optimal performance
- **⚙️ Highly Configurable**: Control timeouts, retries, SSL validation, and error handling
- **🛡️ Production-Ready**: Comprehensive error handling, thread-safe design, and full unit test coverage
- **🔌 Zero Dependencies**: Uses only .NET Framework built-in assemblies
- **♻️ IIS Auto-Polling**: Automatic background metadata refresh with configurable intervals
- **💾 In-Memory Caching**: Fast access to cached metadata in ASP.NET applications
- **🔒 Auto-Apply to IdentityModel**: Optional runtime updates to System.IdentityModel configuration

## Requirements

- .NET Framework 4.6.2, 4.7, or 4.8
- Microsoft.IdentityModel.Protocols.WsFederation 8.1.2+ (NuGet package)
- Microsoft.IdentityModel.Tokens.Saml 8.1.2+ (NuGet package)
- System.IdentityModel.Services (built-in to .NET Framework - IIS module only)

> **⚠️ Windows Only**: This library targets .NET Framework and requires a Windows environment to build and run.
>
> **📦 Microsoft.IdentityModel Migration**: The library has been fully migrated to Microsoft.IdentityModel packages. Metadata is now returned as `WsFederationConfiguration` instead of the legacy `EntityDescriptor`. See [MIGRATION_COMPLETE.md](MIGRATION_COMPLETE.md) for full details.

---

## 📚 Table of Contents

- [Library Usage](#-library-usage)
  - [Installation](#installation)
  - [Basic Examples](#basic-examples)
  - [Configuration Options](#configuration-options)
- [IIS Module](#-iis-module)
  - [Installation & Setup](#iis-module-installation)
  - [Configuration](#iis-module-configuration)
  - [Auto-Apply to IdentityModel](#auto-apply-to-identitymodel)
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
    Name = "Azure AD"
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
        Name = "SAML Identity Provider"
    },
    new IssuerEndpoint
    {
        Id = "wsfed-provider",
        Endpoint = "https://issuer2.example.com/metadata",
        Name = "WS-Fed Identity Provider"
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
    // Access WsFederationConfiguration
    var config = result.Metadata;
    
    Console.WriteLine($"Issuer: {config.Issuer}");
    Console.WriteLine($"Token Endpoint: {config.TokenEndpoint}");
    
    // Access signing keys
    foreach (var key in config.SigningKeys)
    {
        if (key is Microsoft.IdentityModel.Tokens.X509SecurityKey x509Key)
        {
            Console.WriteLine($"Certificate: {x509Key.Certificate.Subject}");
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
                        autoApplyIdentityModel="false"
                        pollingIntervalMinutes="60" 
                        httpTimeoutSeconds="30"
                        validateServerCertificate="true"
                        maxRetries="1">
    <issuers>
      <!-- Azure AD -->
      <add id="azure-ad" 
           endpoint="https://login.microsoftonline.com/common/federationmetadata/2007-06/federationmetadata.xml" 
           name="Azure Active Directory" />
      
      <!-- Auth0 with custom timeout -->
      <add id="auth0" 
           endpoint="https://example.auth0.com/samlp/metadata" 
           name="Auth0" 
           timeoutSeconds="45" />
      
      <!-- Okta -->
      <add id="okta" 
           endpoint="https://dev-12345.okta.com/app/123/sso/saml/metadata" 
           name="Okta" />
    </issuers>
  </samlMetadataPolling>
</configuration>
```

#### Configuration Reference

**Root Element: `<samlMetadataPolling>`**

| Attribute | Type | Default | Required | Description |
|-----------|------|---------|----------|-------------|
| `enabled` | bool | true | No | Enable/disable the polling service |
| `autoApplyIdentityModel` | bool | false | No | Automatically update System.IdentityModel with fetched metadata |
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
| `timeoutSeconds` | int | No | Override default timeout for this endpoint (5-300) |

---

## 🔒 Auto-Apply to IdentityModel

The IIS module can optionally apply fetched metadata directly to `System.IdentityModel` configuration at runtime. This feature automatically updates your application's identity configuration with the latest certificates and endpoints from your identity providers.

### Enabling Auto-Apply

Set `autoApplyIdentityModel="true"` in your configuration:

```xml
<samlMetadataPolling enabled="true"
                     autoApplyIdentityModel="true"
                     pollingIntervalMinutes="60"
                     httpTimeoutSeconds="30"
                     authFailureRecoveryIntervalMinutes="5">
  <issuers>
    <add id="azure-ad" 
         endpoint="https://login.microsoftonline.com/common/federationmetadata/2007-06/federationmetadata.xml" 
         name="Azure Active Directory" 
/>
  </issuers>
</samlMetadataPolling>
```

### What Gets Updated

When `autoApplyIdentityModel` is enabled, the module automatically:

1. **Updates Signing Certificates**: Extracts X.509 certificates from metadata and applies them to the IdentityModel configuration
2. **Updates Issuer Information**: Configures valid issuers based on EntityID from metadata
3. **Updates Endpoints**: Applies SSO and other service endpoints from the metadata
4. **Maintains Security**: Only applies valid, properly signed metadata

### Authentication Failure Recovery

When `autoApplyIdentityModel` is enabled, the module also provides **automatic recovery from certificate rotation failures**:

#### How It Works

1. **Detects Certificate Trust Failures**: The module intercepts `System.IdentityModel` authentication failures and analyzes whether they're caused by untrusted issuer certificates
2. **Identifies the Issuer**: Extracts the issuer identifier from the exception to determine which identity provider's metadata needs refreshing
3. **Checks Polling Threshold**: Verifies that sufficient time has elapsed since the last forced poll for that issuer (configurable via `authFailureRecoveryIntervalMinutes`)
4. **Refreshes Metadata**: Immediately polls the issuer's metadata endpoint to check for certificate rotation
5. **Applies New Configuration**: If new certificates are found, applies them to `System.IdentityModel` configuration
6. **Allows Retry**: Subsequent authentication requests will use the updated certificates

#### Configuration

```xml
<samlMetadataPolling enabled="true"
                     autoApplyIdentityModel="true"
                     pollingIntervalMinutes="60"
                     httpTimeoutSeconds="30"
                     authFailureRecoveryIntervalMinutes="5">
  <!-- authFailureRecoveryIntervalMinutes: minimum time (1-60 minutes) between 
       forced metadata refreshes triggered by authentication failures.
       Default: 5 minutes -->
  <issuers>
    <add id="azure-ad" 
         endpoint="https://login.microsoftonline.com/common/federationmetadata/2007-06/federationmetadata.xml" 
         name="Azure Active Directory" 
/>
  </issuers>
</samlMetadataPolling>
```

#### Important Notes

- **Current Request Fails**: The request that triggered the recovery will still fail with an authentication error
- **Subsequent Requests Succeed**: After metadata is refreshed, subsequent authentication requests will succeed with the new certificates
- **Rate Limiting**: The `authFailureRecoveryIntervalMinutes` setting prevents excessive polling during rapid authentication failures
- **Asynchronous Recovery**: Recovery happens in the background and doesn't block the failing request
- **Synchronous Recovery**: Blocks the request thread while attempting recovery, then redirects on success
- **Diagnostic Logging**: All recovery attempts are logged to `System.Diagnostics.Trace` for monitoring

#### Example Scenario

1. Identity provider (e.g., Azure AD) rotates their signing certificates
2. User attempts to authenticate before scheduled polling occurs
3. Authentication fails with certificate trust error (e.g., ID4037)
4. Module detects the failure, identifies Azure AD as the issuer
5. Module immediately fetches fresh metadata from Azure AD
6. New certificates are applied to IdentityModel configuration
7. User's next authentication attempt succeeds with new certificates

### Monitoring Recovery

Subscribe to trace events to monitor recovery operations:

```csharp
// In your application startup or Global.asax
System.Diagnostics.Trace.Listeners.Add(
    new System.Diagnostics.TextWriterTraceListener("app_trace.log"));

// Recovery events will be logged:
// - "Authentication error detected"
// - "Detected certificate trust failure"
// - "Attempting metadata refresh for issuer"
// - "Successfully recovered from authentication failure"
```

### Security Considerations

> **⚠️ Important**: The `autoApplyIdentityModel` feature is **disabled by default** for security reasons.

Before enabling this feature in production:

- **Verify Metadata Sources**: Ensure all configured endpoints are from trusted identity providers
- **Use HTTPS**: Only fetch metadata from HTTPS endpoints
- **Certificate Validation**: Keep `validateServerCertificate="true"` (the default) to prevent man-in-the-middle attacks
- **Monitor Changes**: Subscribe to the `MetadataUpdated` event to log configuration changes
- **Test Thoroughly**: Test in a staging environment first to ensure proper behavior
- **Rate Limiting**: Configure `authFailureRecoveryIntervalMinutes` appropriately to prevent DoS from excessive polling

### Example: Production Configuration with Auto-Apply

```xml
<samlMetadataPolling enabled="true"
                     autoApplyIdentityModel="true"
                     pollingIntervalMinutes="120"
                     httpTimeoutSeconds="30"
                     validateServerCertificate="true"
                     maxRetries="2"
                     authFailureRecoveryIntervalMinutes="5">
  <issuers>
    <!-- Only trusted, HTTPS endpoints -->
    <add id="azure-ad" 
         endpoint="https://login.microsoftonline.com/your-tenant-id/federationmetadata/2007-06/federationmetadata.xml" 
         name="Azure Active Directory" 
/>
  </issuers>
</samlMetadataPolling>
```

### Monitoring Auto-Apply Operations

Use event handlers to monitor when IdentityModel configuration is updated:

```csharp
var service = MetadataPollingHttpModule.PollingService;

service.MetadataUpdated += (sender, e) =>
{
    if (e.AutoApplied)
    {
        logger.LogInformation($"IdentityModel configuration updated for {e.IssuerName} at {e.UpdatedAt:O}");
        
        // Optionally trigger cache invalidation or other actions
        InvalidateAuthenticationCache();
    }
};
```

### Disabling Auto-Apply (Default)

If you prefer manual control over IdentityModel configuration, keep the default setting or explicitly disable:

```xml
<samlMetadataPolling enabled="true"
                     autoApplyIdentityModel="false"
                     pollingIntervalMinutes="60">
  <!-- Metadata will be fetched and cached but NOT applied to IdentityModel -->
  <!-- Authentication failure recovery will NOT be active -->
  <issuers>
    <add id="azure-ad" 
         endpoint="https://login.microsoftonline.com/common/federationmetadata/2007-06/federationmetadata.xml" 
         name="Azure Active Directory" 
/>
  </issuers>
</samlMetadataPolling>
```

With auto-apply disabled, you can still:
- Access cached metadata via `MetadataPollingHttpModule.MetadataCache`
- Manually apply configuration changes when needed
- Implement custom validation logic before applying updates

---

## 🖥️ Console Utility

A Windows-only console tool is included to fetch metadata from a URL and display a friendly summary.

- Project: `src/IdentityMetadataFetcher.Console`
- Target: `.NET Framework 4.8`
- Usage: `IdentityMetadataFetcher.Console <metadata-url> [--raw]`

### Example

```powershell
# Build (Windows)
msbuild /t:restore
msbuild /t:build /p:GenerateFullPaths=true /consoleloggerparameters:NoSummary

# Run: Azure AD federation metadata
IdentityMetadataFetcher.Console.exe https://login.microsoftonline.com/common/federationmetadata/2007-06/federationmetadata.xml

# Include raw XML output
IdentityMetadataFetcher.Console.exe https://login.microsoftonline.com/common/federationmetadata/2007-06/federationmetadata.xml --raw
```

### Output

- Entity ID
- Roles discovered (STS / IDP)
- Endpoints (Passive Requestor / Single Sign-On)
- Signing Keys (key info types)

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

#### IIS Module Classes

- **`MetadataPollingHttpModule`** - HTTP module for ASP.NET
- **`MetadataCache`** - Thread-safe metadata cache
- **`MetadataPollingService`** - Background polling service

### Security Considerations

1. **SSL/TLS Certificate Validation**: By default, the library validates server certificates. Only disable validation (`ValidateServerCertificate = false`) in development/test environments.

2. **Metadata Sources**: Only fetch metadata from trusted issuer endpoints.

3. **Auto-Apply Feature**: The `autoApplyIdentityModel` setting is disabled by default. Only enable it for trusted metadata sources over HTTPS.

4. **Error Information**: Exception details may contain sensitive information about endpoints. Handle exceptions carefully in production environments.

5. **Timeout Configuration**: Set appropriate timeouts to prevent resource exhaustion from slow or unresponsive endpoints.

### Performance Tips

1. **Use Async Methods**: For better scalability when fetching from multiple endpoints
2. **Batch Operations**: Fetch from multiple endpoints in a single operation rather than individual calls
3. **IIS Module**: Use the IIS module for ASP.NET applications to avoid repeated fetching
4. **Appropriate Timeouts**: Set realistic timeout values based on network conditions

### Troubleshooting

| Problem | Solution |
|---------|----------|
| **HttpRequestException** | Verify endpoint URL, check network/firewall, validate SSL certificate, increase timeout |
| **MetadataFetchException** | Ensure endpoint returns valid metadata |
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
