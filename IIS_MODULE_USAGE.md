# IIS Module - Configuration and Usage Guide

## Overview

The `IdentityMetadataFetcher.Iis` module is an ASP.NET HTTP Module that automatically polls SAML/WSFED metadata endpoints and maintains an in-memory cache of the current metadata. This enables ASP.NET applications to use up-to-date metadata for identity validation without manual intervention.

## Installation

### 1. Add Assembly to Bin Directory

Deploy `IdentityMetadataFetcher.Iis.dll` to your application's `bin` directory along with the core `IdentityMetadataFetcher.dll`.

### 2. Register Module in Web.config

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

### 3. Configure Metadata Polling

Add a configuration section in your `Web.config`:

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
      <add id="azure-ad" 
           endpoint="https://login.microsoftonline.com/common/federationmetadata/2007-06/federationmetadata.xml" 
           name="Azure Active Directory" />
      
      <add id="auth0" 
           endpoint="https://example.auth0.com/samlp/metadata" 
           name="Auth0" 

           timeoutSeconds="45" />
    </issuers>
  </samlMetadataPolling>
  
  <!-- Rest of configuration... -->
</configuration>
```

## Configuration Reference

### Root Element: `<samlMetadataPolling>`

| Attribute | Type | Default | Required | Description |
|-----------|------|---------|----------|-------------|
| `enabled` | bool | true | No | Enable/disable the polling service |
| `pollingIntervalMinutes` | int | 60 | No | How often to poll (1-10080 minutes) |
| `httpTimeoutSeconds` | int | 30 | No | HTTP request timeout (5-300 seconds) |
| `validateServerCertificate` | bool | true | No | Validate SSL/TLS certificates |
| `maxRetries` | int | 1 | No | Retry failed requests (0-5) |

### Child Element: `<issuers>` Collection

Contains one or more `<add>` elements defining issuer endpoints.

#### `<add>` Element

| Attribute | Type | Required | Description |
|-----------|------|----------|-------------|
| `id` | string | Yes | Unique identifier for the issuer (must be unique) |
| `endpoint` | string | Yes | Full URL to the metadata endpoint |
| `name` | string | Yes | Human-readable issuer name |
| `timeoutSeconds` | int | No | Override default timeout for this endpoint (5-300) |

## Complete Example Web.config

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

## Usage in Code

### Accessing Cached Metadata

Once the module is initialized, you can access cached metadata from anywhere in your application:

```csharp
using IdentityMetadataFetcher.Iis.Modules;
using System.IdentityModel.Metadata;

// Get the cache instance
var cache = MetadataPollingHttpModule.MetadataCache;

// Retrieve metadata for an issuer
var config = cache.GetMetadata("azure-ad");

if (config != null)
{
    var issuer = config.Issuer;
    var tokenEndpoint = config.TokenEndpoint;
    // Use the metadata...
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

### Accessing Polling Service

You can also interact with the polling service directly:

```csharp
using IdentityMetadataFetcher.Iis.Modules;

var pollingService = MetadataPollingHttpModule.PollingService;

if (pollingService != null)
{
    // Manually trigger polling (useful for manual refresh)
    await pollingService.PollNowAsync();
    
    // Subscribe to polling events
    pollingService.MetadataUpdated += (sender, e) =>
    {
        Console.WriteLine($"Updated: {e.IssuerName} at {e.UpdatedAt:O}");
    };
    
    pollingService.PollingError += (sender, e) =>
    {
        Console.WriteLine($"Error polling {e.IssuerName}: {e.ErrorMessage}");
    };
}
```

### Subscribing to Events

The polling service raises several events:

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
};

// Fired when metadata is successfully updated
service.MetadataUpdated += (sender, e) =>
{
    System.Diagnostics.Trace.TraceInformation(
        $"Metadata updated: {e.IssuerName} at {e.UpdatedAt:O}");
};
```

## Feature Details

### Automatic Polling

- The module starts automatically when the application initializes
- Performs an initial metadata fetch immediately
- Then polls at the configured interval
- Uses async/await for non-blocking operations

### Metadata Caching

- All successfully fetched metadata is cached in memory
- Cache is thread-safe
- Includes both parsed metadata (WsFederationConfiguration) and raw XML
- Cached with timestamp for age tracking

### Error Handling

- If polling an endpoint fails, the module continues polling other endpoints
- Failed endpoints don't prevent the module from functioning
- Errors are traced to System.Diagnostics.Trace

### Configuration Validation

- The module validates all configuration on startup
- Invalid configurations prevent the application from starting
- Configuration errors are clear and descriptive

### Thread Safety

- All caching operations are thread-safe
- Concurrent requests can safely access cached metadata
- Background polling doesn't interfere with cache reads

## Troubleshooting

### Configuration Not Found Error

**Problem**: "Configuration section 'samlMetadataPolling' not found"

**Solution**: Ensure you've added both the `<configSections>` declaration and the configuration element to your `Web.config`.

### Module Not Initializing

**Problem**: No trace messages, metadata not being cached

**Solution**: 
1. Verify module is registered in `<system.webServer><modules>`
2. Check that DLLs are in the bin directory
3. Enable trace output to debug
4. Check IIS logs for errors

### Certificate Validation Errors

**Problem**: "The remote certificate is invalid" errors

**Solution**:
- For development/testing: Set `validateServerCertificate="false"` (not recommended for production)
- For production: Ensure the server's SSL certificate is valid and installed in the certificate store

### Slow Polling or Timeouts

**Problem**: Polling taking too long or timing out

**Solution**:
1. Increase `httpTimeoutSeconds`
2. Per-endpoint: Add `timeoutSeconds` attribute
3. Check endpoint responsiveness directly
4. Consider increasing `pollingIntervalMinutes` if endpoints are slow

### Metadata Not Updating

**Problem**: Cache appears stale

**Solution**:
1. Verify endpoints are accessible via browser
2. Check System.Diagnostics trace output for errors
3. Manually trigger polling: `await service.PollNowAsync();`
4. Verify `enabled="true"` in configuration

## Best Practices

### 1. Polling Interval Selection

- **Development**: 5-15 minutes for testing
- **Testing**: 10-30 minutes for integration testing
- **Production**: 60+ minutes for stable environments
- **High-Risk**: 15-30 minutes if frequent updates are critical

```xml
<!-- Production: 4-hour interval -->
<samlMetadataPolling pollingIntervalMinutes="240" ... >
```

### 2. Timeout Configuration

- Default 30 seconds is usually fine
- Slow endpoints (> 10 sec): Increase to 45-60 seconds
- Very fast endpoints (< 5 sec): Can reduce to 15-20 seconds

```xml
<!-- Slow endpoint -->
<add id="slow-issuer" endpoint="..." timeoutSeconds="60" ... />
```

### 3. Error Handling

Always handle cases where metadata might not be cached yet:

```csharp
var metadata = cache.GetMetadata("my-issuer");
if (metadata == null)
{
    // Metadata not yet available
    // Either fallback to backup or redirect
    return false;
}
```

### 4. Development vs Production

**Development**:
```xml
<samlMetadataPolling enabled="true" 
                     pollingIntervalMinutes="5" 
                     validateServerCertificate="false"
                     httpTimeoutSeconds="30">
```

**Production**:
```xml
<samlMetadataPolling enabled="true" 
                     pollingIntervalMinutes="120" 
                     validateServerCertificate="true"
                     httpTimeoutSeconds="30"
                     maxRetries="2">
```

### 5. Monitoring

Subscribe to events for production monitoring:

```csharp
service.PollingError += (sender, e) =>
{
    // Log to your monitoring system
    logger.LogWarning($"Metadata polling failed: {e.IssuerName}", e.Exception);
};

service.MetadataUpdated += (sender, e) =>
{
    // Log successful updates
    logger.LogInformation($"Metadata refreshed: {e.IssuerName}");
};
```

## Performance Considerations

- **Memory**: Each metadata entry uses ~5-50 KB depending on size
- **CPU**: Polling is async and non-blocking
- **Network**: Single HTTP request per endpoint per polling interval
- **Scalability**: Suitable for high-traffic applications

## Integration with System.IdentityModel

The module works seamlessly with System.IdentityModel:

```csharp
// Configure your federation service with cached metadata
var metadata = cache.GetMetadata("azure-ad");

var config = new SecurityTokenHandlerConfiguration
{
    IssuerNameRegistry = new MetadataIssuerNameRegistry(metadata)
};
```

## Disabling the Module

To disable without removing from Web.config:

```xml
<samlMetadataPolling enabled="false" ... >
```

Or remove the module registration:

```xml
<!-- Comment out or remove -->
<!-- <add name="SamlMetadataPollingModule" ... /> -->
```

## Limitations and Notes

- Module runs in application's main thread pool
- Metadata is cached in-memory only (lost on app recycle)
- No persistence layer (intentional design)
- Per-application instance (each app pool has its own cache)

---

For more information, see:
- IIS_MODULE_IMPLEMENTATION.md - Architecture details
- Examples in the Samples directory
