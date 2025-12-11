# SAML Metadata Fetcher - Design Document

## Overview

The SAML Metadata Fetcher is a .NET Framework 4.5+ class library designed to facilitate the fetching and parsing of WS-Federation (WSFED) and SAML metadata from multiple issuing authorities. It provides both synchronous and asynchronous APIs with comprehensive error handling and configurable behavior.

## Design Principles

1. **Simplicity**: Easy to use with sensible defaults
2. **Flexibility**: Highly configurable for various scenarios
3. **Robustness**: Comprehensive error handling and resilience options
4. **Standards Compliance**: Leverages built-in System.IdentityModel.Metadata APIs
5. **Async Support**: Full async/await support alongside synchronous APIs

## Architecture

### Component Diagram

```
┌─────────────────────────────────────────────────────┐
│         IdentityMetadataFetcher Library                 │
├─────────────────────────────────────────────────────┤
│                                                       │
│  ┌──────────────────────────────────────────────┐   │
│  │  IMetadataFetcher (Interface)                │   │
│  │  ├─ FetchMetadata()                          │   │
│  │  ├─ FetchMetadataAsync()                     │   │
│  │  ├─ FetchMetadataFromMultipleEndpoints()     │   │
│  │  └─ FetchMetadataFromMultipleEndpointsAsync()   │   │
│  └──────────────────────────────────────────────┘   │
│              △                                        │
│              │                                        │
│  ┌──────────────────────────────────────────────┐   │
│  │  MetadataFetcher (Implementation)            │   │
│  │  ├─ FetchMetadataInternal()                  │   │
│  │  ├─ FetchMetadataInternalAsync()             │   │
│  │  ├─ DownloadMetadataXml()                    │   │
│  │  ├─ DownloadMetadataXmlAsync()               │   │
│  │  └─ ParseMetadata()                          │   │
│  └──────────────────────────────────────────────┘   │
│                                                       │
│  ┌──────────────────────────────────────────────┐   │
│  │  Models                                      │   │
│  │  ├─ IssuerEndpoint                           │   │
│  │  ├─ MetadataFetchResult                      │   │
│  │  ├─ MetadataFetchOptions                     │   │
│  │  └─ MetadataType (Enum)                      │   │
│  └──────────────────────────────────────────────┘   │
│                                                       │
│  ┌──────────────────────────────────────────────┐   │
│  │  Exceptions                                  │   │
│  │  └─ MetadataFetchException                   │   │
│  └──────────────────────────────────────────────┘   │
│                                                       │
└─────────────────────────────────────────────────────┘
         │
         │ Uses
         ▼
┌─────────────────────────────────────────────────────┐
│    System.IdentityModel                             │
│    ├─ System.IdentityModel.Metadata                 │
│    │  └─ MetadataSerializer                         │
│    ├─ System.Net.Http                               │
│    │  └─ HttpClient                                 │
│    └─ System.Xml                                    │
│       └─ XmlReader                                  │
└─────────────────────────────────────────────────────┘
```

## Class Descriptions

### IMetadataFetcher Interface

The primary service interface defining all metadata fetching operations.

**Responsibilities:**
- Define contract for metadata fetching operations
- Support both sync and async patterns
- Handle single and batch operations

**Methods:**
- `FetchMetadata(IssuerEndpoint)` - Synchronous single endpoint fetch
- `FetchMetadataAsync(IssuerEndpoint)` - Asynchronous single endpoint fetch
- `FetchMetadataFromMultipleEndpoints(IEnumerable<IssuerEndpoint>)` - Synchronous batch fetch
- `FetchMetadataFromMultipleEndpointsAsync(IEnumerable<IssuerEndpoint>)` - Asynchronous batch fetch

### MetadataFetcher Class

The concrete implementation of IMetadataFetcher.

**Responsibilities:**
- Download metadata XML from endpoints via HTTP
- Parse XML using MetadataSerializer
- Handle errors and retries
- Manage configuration options

**Key Methods:**
- `FetchMetadataInternal(IssuerEndpoint)` - Core sync fetch logic
- `FetchMetadataInternalAsync(IssuerEndpoint)` - Core async fetch logic
- `DownloadMetadataXml(string, int)` - HTTP download with retries
- `DownloadMetadataXmlAsync(string, int)` - Async HTTP download
- `ParseMetadata(string, MetadataType)` - XML parsing using MetadataSerializer

**Internal Flow:**

```
FetchMetadata(endpoint)
    ↓
FetchMetadataInternal(endpoint)
    ↓
DownloadMetadataXml(url, timeout)
    ├─ Create HttpClient with timeout
    ├─ Execute GET request (with retries)
    └─ Return raw XML
    ↓
ParseMetadata(xml, type)
    ├─ Create XmlReader from XML string
    ├─ Create MetadataSerializer
    ├─ Call ReadMetadata()
    └─ Return MetadataBase
    ↓
Return MetadataFetchResult
```

### IssuerEndpoint Class

Model representing a single metadata endpoint.

**Properties:**
- `Id` (string) - Unique identifier
- `Endpoint` (string) - URL to metadata document
- `Name` (string) - Human-readable name
- `MetadataType` (enum) - WSFED or SAML
- `Timeout` (int?) - Optional endpoint-specific timeout

**Usage:**
Encapsulates endpoint configuration, allowing per-endpoint customization while supporting global defaults.

### MetadataFetchResult Class

Model containing the result of a fetch operation.

**Properties:**
- `Endpoint` (IssuerEndpoint) - The queried endpoint
- `IsSuccess` (bool) - Operation success/failure
- `Metadata` (MetadataBase) - Parsed metadata (if successful)
- `RawMetadata` (string) - Raw XML (if successful)
- `Exception` (Exception) - Exception (if failed)
- `ErrorMessage` (string) - Error description (if failed)
- `FetchedAt` (DateTime) - Fetch timestamp (UTC)

**Design Notes:**
- Provides unified response model for all operations
- Includes both parsed and raw metadata for flexibility
- Timestamp helps with cache invalidation
- Contains full exception information for debugging

### MetadataFetchOptions Class

Configuration object for controlling fetch behavior.

**Properties:**
- `DefaultTimeoutMs` (int) - HTTP request timeout
- `ContinueOnError` (bool) - Continue fetching if one fails
- `ValidateServerCertificate` (bool) - SSL/TLS validation
- `MaxRetries` (int) - Retry attempt count
- `CacheMetadata` (bool) - Enable caching (reserved for future)
- `CacheDurationMinutes` (int) - Cache TTL (reserved for future)

**Default Values:**
- DefaultTimeoutMs: 30000 (30 seconds)
- ContinueOnError: true
- ValidateServerCertificate: true
- MaxRetries: 0 (no retries)
- CacheMetadata: false
- CacheDurationMinutes: 60

### MetadataFetchException Class

Custom exception for metadata-related errors.

**Properties:**
- `Endpoint` (string) - The endpoint that failed
- `HttpStatusCode` (int?) - HTTP status code (if applicable)

**Design Notes:**
- Derives from System.Exception
- Includes context about which endpoint failed
- Optional HTTP status code for HTTP-specific errors
- Supports nested inner exceptions for full error chain

## Data Flow Diagrams

### Single Endpoint Synchronous Fetch

```
┌─────────┐
│ Caller  │
└────┬────┘
     │
     │ fetcher.FetchMetadata(endpoint)
     ▼
┌─────────────────────────────────────────────────┐
│ MetadataFetcher.FetchMetadata()                 │
│ ├─ Validate inputs                              │
│ └─ Create MetadataFetchResult                   │
└────┬────────────────────────────────────────────┘
     │
     │ try
     ▼
┌─────────────────────────────────────────────────┐
│ FetchMetadataInternal()                         │
│ ├─ Get timeout (endpoint or default)            │
│ └─ Call DownloadMetadataXml()                   │
└────┬────────────────────────────────────────────┘
     │
     ▼
┌─────────────────────────────────────────────────┐
│ DownloadMetadataXml()                           │
│ ├─ Create HttpClient                            │
│ ├─ Set timeout                                  │
│ ├─ Loop MaxRetries + 1 times:                   │
│ │  ├─ GET request                               │
│ │  ├─ Check status code                         │
│ │  └─ Return content or retry                   │
│ └─ Throw MetadataFetchException on failure      │
└────┬────────────────────────────────────────────┘
     │
     │ XML received
     ▼
┌─────────────────────────────────────────────────┐
│ ParseMetadata()                                 │
│ ├─ Create XmlReader                             │
│ ├─ Create MetadataSerializer                    │
│ ├─ Call ReadMetadata()                          │
│ └─ Return MetadataBase                          │
└────┬────────────────────────────────────────────┘
     │
     │ Success
     ▼
┌─────────────────────────────────────────────────┐
│ FetchMetadata() continued                       │
│ ├─ Set IsSuccess = true                         │
│ ├─ Set Metadata                                 │
│ └─ Return result                                │
└────┬────────────────────────────────────────────┘
     │
     │ or catch Exception
     ▼
┌─────────────────────────────────────────────────┐
│ FetchMetadata() error handling                  │
│ ├─ Set IsSuccess = false                        │
│ ├─ Set Exception                                │
│ ├─ Set ErrorMessage                             │
│ └─ Return result                                │
└────┬────────────────────────────────────────────┘
     │
     ▼
┌─────────┐
│ Caller  │
│ Result  │
└─────────┘
```

### Multiple Endpoints Batch Fetch

```
┌─────────┐
│ Caller  │
└────┬────┘
     │
     │ FetchMetadataFromMultipleEndpoints(endpoints)
     ▼
┌──────────────────────────────────────────┐
│ Validate null input                      │
│ Create results list                      │
└────┬─────────────────────────────────────┘
     │
     ▼
┌──────────────────────────────────────────┐
│ For each endpoint:                       │
│                                          │
│  ┌────────────────────────────────────┐  │
│  │ try                                │  │
│  │ ├─ FetchMetadata(endpoint)         │  │
│  │ └─ Add to results                  │  │
│  └────────────────────────────────────┘  │
│                                          │
│  catch Exception (if ContinueOnError)    │
│  ├─ Create failure result               │
│  └─ Add to results                      │
│                                          │
└────┬─────────────────────────────────────┘
     │
     ▼
┌──────────────────────────────────────────┐
│ Return IEnumerable<result>               │
└────┬─────────────────────────────────────┘
     │
     ▼
┌─────────┐
│ Caller  │
└─────────┘
```

## Error Handling Strategy

### Approach

The library uses a non-throwing approach for batch operations (returns failure results) while preserving exceptions for critical input validation errors.

### Error Scenarios

1. **Invalid Input** (throws)
   - Null endpoint parameter
   - Empty endpoint URL
   - Null endpoints collection

2. **Network Errors** (returns failure)
   - Connection timeout
   - DNS resolution failure
   - Connection refused

3. **HTTP Errors** (returns failure)
   - 4xx status codes
   - 5xx status codes

4. **Parsing Errors** (returns failure)
   - Invalid XML
   - Wrong metadata type
   - Missing required elements

### Exception Hierarchy

```
Exception (System)
└─ MetadataFetchException
   ├─ From parsing failures
   ├─ From network failures
   └─ From HTTP errors
```

### Retry Strategy

- Triggered by MaxRetries option
- Applies to network-level failures only
- Exponential backoff for async operations (100ms * retry count)
- Gives up and returns failure (not exception) in batch mode

## Thread Safety

The MetadataFetcher is **stateless** and therefore **thread-safe**:
- No static state
- No instance state mutation
- HttpClient is thread-safe
- Each operation creates new XmlReader and MetadataSerializer

**Implications:**
- Single instance can serve multiple concurrent requests
- Safe for use in ASP.NET and other concurrent scenarios

## Performance Considerations

1. **HTTP Client Reuse**: Each fetch operation creates a new HttpClient to avoid socket exhaustion in long-running scenarios. Future optimization could pool clients.

2. **Synchronous Blocking**: Sync methods use `.Result` which blocks the calling thread. Use async methods for better scalability.

3. **Batch Operations**: All endpoints are fetched serially in sync mode, in parallel in async mode.

4. **Memory**: Stores full XML in memory. Large metadata documents could consume significant memory.

5. **Network**: No DNS caching or connection pooling currently implemented.

## Security Considerations

1. **SSL/TLS**: Certificate validation can be disabled for development but should always be enabled in production

2. **Metadata Validation**: Library doesn't validate metadata content, only XML structure

3. **Error Information**: Exception details may leak endpoint information - handle appropriately

4. **Network Isolation**: Honors system proxy settings automatically via HttpClient

## Testing Strategy

### Unit Tests Covered

- **Input Validation**
  - Null parameters
  - Empty strings
  - Invalid URLs

- **Success Cases**
  - Valid metadata fetch (mocked)
  - Multiple endpoint fetching
  - Async operations

- **Failure Cases**
  - Network errors
  - Invalid endpoints
  - Parsing failures

- **Configuration**
  - Default options
  - Custom options
  - Per-endpoint overrides

- **Models**
  - Property get/set
  - Enum values
  - Timestamps

### Test Framework

- NUnit 3.x for test execution
- No mocking framework (tests use real HTTP calls to invalid endpoints)

## Future Enhancement Opportunities

1. **Metadata Caching**: Implement TTL-based caching using the reserved CacheMetadata options

2. **Client Pooling**: Implement HttpClientFactory pattern for better socket management

3. **Metrics/Telemetry**: Add performance counters and tracing

4. **Validation**: Add WIF metadata validation before returning

5. **LINQ Support**: Query metadata using LINQ-to-Metadata

6. **Parallel Batch Fetching**: Use Task.WhenAll for faster batch operations

7. **Event Notifications**: Raise events during fetch lifecycle

8. **Policy-Based Retry**: Implement Polly integration for advanced retry strategies

## Deployment Considerations

1. **Framework Requirements**: .NET Framework 4.5 or later

2. **Permissions**: Requires network access and DNS resolution

3. **Certificate Store**: Uses system certificate store for SSL validation

4. **NuGet**: Library itself has no external dependencies

5. **Assembly Binding**: No binding redirects needed for standard framework versions

## Backward Compatibility

- All public APIs are final in version 1.0
- No plans for breaking changes in minor versions
- Future major versions may introduce interface changes

---

**Document Version**: 1.0  
**Last Updated**: December 2025
