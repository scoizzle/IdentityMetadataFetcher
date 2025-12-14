# IIS Module - Implementation Details

## Architecture Overview

The IIS module extends the core `IdentityMetadataFetcher` library with automatic polling and caching capabilities specific to ASP.NET applications.

### Component Architecture

```
┌─────────────────────────────────────────────────┐
│          ASP.NET Application                    │
│         (Web.config configured)                 │
└────────────────────┬────────────────────────────┘
                     │
                     │ (Initializes on startup)
                     ▼
┌─────────────────────────────────────────────────┐
│  MetadataPollingHttpModule (IHttpModule)        │
│  - Reads Web.config configuration               │
│  - Initializes services (once per app domain)   │
│  - Manages lifecycle                            │
└─────────────┬───────────────────────────────────┘
              │
              ├──────────────────────┬──────────────────────┐
              │                      │                      │
              ▼                      ▼                      ▼
    ┌─────────────────┐  ┌──────────────────┐  ┌────────────────────┐
    │ MetadataCache   │  │ PollingService   │  │ Configuration      │
    │ - In-memory     │  │ - Timer-based    │  │ - System.Config    │
    │ - Thread-safe   │  │ - Async polling  │  │ - Web.config read  │
    │ - Stores parsed │  │ - Event-driven   │  │ - Validation       │
    │   metadata      │  │ - Resilient      │  │                    │
    └─────────────────┘  └────────┬─────────┘  └────────────────────┘
                                  │
                                  │ (Uses core library)
                                  ▼
                    ┌─────────────────────────────┐
                    │   IdentityMetadataFetcher       │
                    │   (Core Library)            │
                    │                             │
                    │  - HTTP downloads           │
                    │  - WsFederationMetadataSerializer│
                    │  - Metadata parsing         │
                    └─────────────────────────────┘
```

## Module Lifecycle

### 1. Application Startup

```
Web Application Start
    ↓
IIS loads modules
    ↓
MetadataPollingHttpModule.Init() called
    ↓
Read Web.config section
    ↓
Validate configuration
    ↓
Create MetadataFetcher with options
    ↓
Create MetadataCache
    ↓
Create MetadataPollingService
    ↓
Subscribe to service events
    ↓
Call service.Start()
    │
    ├── Perform initial poll immediately
    │
    └── Schedule timer for periodic polling
    ↓
Module initialized and ready
```

### 2. During Application Lifetime

```
Timer fires (every polling interval)
    ↓
MetadataPollingService.PollMetadataAsync()
    ├── Set _isPolling flag to prevent concurrent polls
    ├── Raise PollingStarted event
    │
    └─► For each issuer endpoint:
        ├── Fetch metadata via MetadataFetcher
        ├── If successful:
        │   ├── Update cache
        │   ├── Raise MetadataUpdated event
        │   └── Update success counter
        └── If failed:
            ├── Raise PollingError event
            └── Update failure counter
    │
    ├── Raise PollingCompleted event with summary
    └── Clear _isPolling flag
```

### 3. Application Shutdown

```
Application shutting down
    ↓
IHttpModule.Dispose() called
    ↓
Stop polling timer
    ↓
Dispose polling service
    ↓
Clear cache (GC handles)
    ↓
Module cleaned up
```

## Configuration System

### System.Configuration Integration

The module uses standard `System.Configuration` for configuration:

```csharp
// ConfigurationSection provides declarative approach
public class MetadataPollingConfigurationSection : ConfigurationSection
{
    [ConfigurationProperty("enabled", DefaultValue = true)]
    public bool Enabled { get; set; }
    
    [ConfigurationProperty("pollingIntervalMinutes", DefaultValue = 60)]
    [IntegerValidator(MinValue = 1, MaxValue = 10080)]
    public int PollingIntervalMinutes { get; set; }
    
    [ConfigurationProperty("issuers", IsDefaultCollection = false)]
    public IssuerElementCollection Issuers { get; set; }
}
```

### Configuration Element Hierarchy

```
<samlMetadataPolling>              ← MetadataPollingConfigurationSection
  <issuers>                         ← IssuerElementCollection
    <add id="..." endpoint="..." /> ← IssuerElement (multiple)
    <add id="..." endpoint="..." />
  </issuers>
</samlMetadataPolling>
```

### Loading Configuration

```csharp
// Read from Web.config
var config = ConfigurationManager.GetSection("samlMetadataPolling") 
    as MetadataPollingConfigurationSection;

// Validate all properties
if (config.PollingIntervalMinutes < 1)
    throw new ConfigurationErrorsException(...);

// Convert to domain objects
var endpoints = config.Issuers.ToIssuerEndpoints();
```

## Metadata Caching Strategy

### Cache Structure

```
MetadataCache
├── Dictionary<string, MetadataCacheEntry>
│   ├── "issuer-id-1" → MetadataCacheEntry
│   │   ├── IssuerId: string
│   │   ├── Metadata: WsFederationConfiguration (from Microsoft.IdentityModel.Protocols.WsFederation)
│   │   ├── RawXml: string
│   │   └── CachedAt: DateTime
│   │
│   └── "issuer-id-2" → MetadataCacheEntry
│       ├── ...
│       └── CachedAt: DateTime
│
└── Lock object for thread safety
```

### Thread Safety

```csharp
public class MetadataCache
{
    private readonly object _lockObject = new object();
    private readonly Dictionary<string, MetadataCacheEntry> _cache;
    
    public void AddOrUpdateMetadata(...)
    {
        lock (_lockObject)  // Prevents concurrent modifications
        {
            _cache[issuerId] = entry;
        }
    }
    
    public WsFederationConfiguration GetMetadata(...)
    {
        lock (_lockObject)  // Safe concurrent reads
        {
            // Read operations
        }
    }
}
```

### Cache Operations

**Write Operations** (During polling):
```
1. Fetch metadata from endpoint
2. Lock cache
3. Update or add entry
4. Unlock cache
5. Raise event
```

**Read Operations** (From application code):
```
1. Lock cache
2. Retrieve entry
3. Unlock cache
4. Return to caller
```

## Polling Service Implementation

### Timer-Based Polling

```csharp
public void Start()
{
    // Immediate first poll
    PollMetadataAsync();
    
    // Schedule periodic polling
    _pollingTimer = new Timer(
        callback: state => PollMetadataAsync(),
        state: null,
        dueTime: TimeSpan.FromMilliseconds(_pollingIntervalMs),
        period: TimeSpan.FromMilliseconds(_pollingIntervalMs)
    );
}
```

### Polling Flow

```csharp
private async Task PollMetadataAsync()
{
    if (_isPolling) return;  // Prevent concurrent polls
    
    try
    {
        _isPolling = true;
        
        OnPollingStarted(...);
        
        // Async fetch from all endpoints
        var results = await _metadataFetcher
            .FetchMetadataFromMultipleEndpointsAsync(_endpoints);
        
        // Process results
        foreach (var result in results)
        {
            if (result.IsSuccess)
            {
                _metadataCache.AddOrUpdateMetadata(...);
                OnMetadataUpdated(...);
            }
            else
            {
                OnPollingError(...);
            }
        }
        
        OnPollingCompleted(...);
    }
    finally
    {
        _isPolling = false;
    }
}
```

### Resilience Features

1. **Concurrent Poll Prevention**
   ```csharp
   if (_isPolling) return;  // Skip if already polling
   ```

2. **Error Isolation**
   ```csharp
   // One endpoint failure doesn't affect others
   foreach (var endpoint in endpoints)
   {
       // Errors caught per endpoint
   }
   ```

3. **Async/Await**
   ```csharp
   // Non-blocking polling
   var results = await fetcher.FetchMetadataFromMultipleEndpointsAsync(...);
   ```

## Event System

### Polling Events

```csharp
// Fired at polling start
public event EventHandler<PollingEventArgs> PollingStarted;

// Fired at polling completion
public event EventHandler<PollingEventArgs> PollingCompleted;

// Fired for individual endpoint errors
public event EventHandler<PollingErrorEventArgs> PollingError;

// Fired when metadata successfully updated
public event EventHandler<MetadataUpdatedEventArgs> MetadataUpdated;
```

### Event Handlers Registration

```csharp
service.PollingStarted += (sender, e) => 
{
    // Handle start
};

service.PollingCompleted += (sender, e) =>
{
    // Handle completion with summary
    Console.WriteLine($"Success: {e.SuccessCount}, Failures: {e.FailureCount}");
};

service.PollingError += (sender, e) =>
{
    // Handle individual error
    logger.LogError(e.ErrorMessage);
};

service.MetadataUpdated += (sender, e) =>
{
    // Handle successful metadata update
    logger.LogInfo($"Updated: {e.IssuerName}");
};
```

## Integration with Core Library

### Using MetadataFetcher

The module leverages the core library:

```csharp
// Create fetcher with configured options
var options = new MetadataFetchOptions
{
    DefaultTimeoutMs = config.HttpTimeoutSeconds * 1000,
    ValidateServerCertificate = config.ValidateServerCertificate,
    MaxRetries = config.MaxRetries,
    ContinueOnError = true
};

var fetcher = new MetadataFetcher(options);

// Fetch metadata
var results = await fetcher
    .FetchMetadataFromMultipleEndpointsAsync(endpoints);
```

### Using MetadataSerializer

The core library internally uses Microsoft.IdentityModel.Protocols.WsFederation:

```csharp
// In MetadataFetcher.ParseMetadata()
using (var reader = XmlReader.Create(new StringReader(metadataXml)))
{
    var serializer = new MetadataSerializer();
    var metadata = serializer.ReadMetadata(reader);
    return metadata;  // MetadataBase object
}
```

## Static Singleton Pattern

The module uses static members for application-wide access:

```csharp
public class MetadataPollingHttpModule : IHttpModule
{
    // Singleton instances (per app domain)
    private static MetadataPollingService _pollingService;
    private static MetadataCache _metadataCache;
    private static bool _initialized = false;
    private static readonly object _lockObject = new object();
    
    // Public accessors
    public static MetadataCache MetadataCache { get; }
    public static MetadataPollingService PollingService { get; }
}
```

### Initialization Pattern

```csharp
public void Init(HttpApplication context)
{
    if (_initialized) return;  // Prevent re-initialization
    
    lock (_lockObject)
    {
        if (_initialized) return;  // Double-check
        
        // Initialize services
        _metadataCache = new MetadataCache();
        _pollingService = new MetadataPollingService(...);
        _initialized = true;
    }
}
```

## Diagnostics and Tracing

### System.Diagnostics Integration

```csharp
// Trace initialization
System.Diagnostics.Trace.TraceInformation(
    $"Module initialized with {config.Issuers.Count} issuers");

// Trace polling events
System.Diagnostics.Trace.TraceInformation(
    $"Polling completed: Success {count}, Failures {count}");

// Trace errors
System.Diagnostics.Trace.TraceWarning(
    $"Error polling {issuer}: {error}");

// Trace metadata updates
System.Diagnostics.Trace.TraceInformation(
    $"Metadata updated: {issuer} at {timestamp}");
```

### Enabling Tracing

In Web.config:

```xml
<configuration>
  <system.diagnostics>
    <trace autoflush="true">
      <listeners>
        <add name="textWriterTraceListener" 
             type="System.Diagnostics.TextWriterTraceListener" 
             initializeData="app_trace.log" />
      </listeners>
    </trace>
  </system.diagnostics>
</configuration>
```

## Error Handling Strategy

### Configuration Validation

```csharp
try
{
    var config = ConfigurationManager.GetSection("samlMetadataPolling");
    ValidateConfiguration(config);
    InitializeServices(config);
}
catch (ConfigurationErrorsException ex)
{
    Trace.TraceError($"Configuration error: {ex.Message}");
    throw;  // Fail fast - configuration errors prevent app start
}
```

### Polling Resilience

```csharp
// Endpoint-level error handling
foreach (var result in results)
{
    if (result.IsSuccess)
    {
        // Update cache
    }
    else
    {
        // Log error, continue with next endpoint
        OnPollingError(...);
    }
}
```

### Concurrent Access Safety

```csharp
// Cache operations are thread-safe
lock (_lockObject)
{
    // Modify cache
}

// Multiple readers and writers can coexist safely
```

## Performance Characteristics

### Memory Usage

| Component | Memory |
|-----------|--------|
| Per metadata entry | 5-50 KB |
| Cache overhead | ~1 KB per entry |
| Service infrastructure | ~50 KB |
| **Typical (5 issuers)** | **~150-300 KB** |

### CPU Usage

- Polling is async/non-blocking
- Cache operations are O(1)
- No busy-wait loops
- Timer-based scheduling

### Network Usage

- One HTTP request per endpoint per polling interval
- Configurable timeouts prevent hanging connections
- Retry logic handles transient failures

## Testing Considerations

### Unit Testing

```csharp
[Test]
public void Configuration_IsLoaded()
{
    var config = ConfigurationManager.GetSection("samlMetadataPolling");
    Assert.IsNotNull(config);
}

[Test]
public void Cache_IsThreadSafe()
{
    var cache = new MetadataCache();
    // Concurrent read/write test
}

[Test]
public async Task PollingService_FetchesMetadata()
{
    var service = new MetadataPollingService(...);
    await service.PollNowAsync();
    Assert.Greater(cache.Count, 0);
}
```

### Integration Testing

```csharp
[Test]
public void Module_InitializesInWebContext()
{
    var module = new MetadataPollingHttpModule();
    var app = new HttpApplication();
    module.Init(app);
    
    Assert.IsNotNull(MetadataPollingHttpModule.MetadataCache);
    Assert.IsNotNull(MetadataPollingHttpModule.PollingService);
}
```

---

For usage details, see IIS_MODULE_USAGE.md
