# Polling Throttling Architecture

## Overview

Polling throttling is implemented at the **`MetadataPollingService`** level, not in individual consumers like the `AuthenticationFailureRecoveryService`. This provides several benefits:

## Benefits of Centralized Throttling

### 1. **Single Source of Truth**
- One place manages all polling timestamps
- Consistent throttling across all polling sources
- Easier to reason about and debug

### 2. **Prevents Redundant Polls**
- Scheduled polling won't poll if recovery just did
- Recovery won't poll if scheduled polling just did
- Manual `PollNowAsync()` calls respect throttling
- API calls to poll specific issuers are throttled

### 3. **Resource Protection**
- Protects metadata endpoints from excessive requests
- Prevents thread pool exhaustion
- Reduces network traffic
- Protects against accidental DoS

### 4. **Flexibility**
- Can disable throttling (set to 0) for testing
- Per-issuer throttling (not global)
- Transparent to consumers

## Architecture

```
???????????????????????????????????????????????????
? Consumers (Multiple Sources)                    ?
?                                                  ?
?  ????????????????????????  ??????????????????? ?
?  ? Scheduled Polling    ?  ? Recovery Service? ?
?  ? (Timer)              ?  ? (Auth Failure)  ? ?
?  ????????????????????????  ??????????????????? ?
?             ?                       ?          ?
?             ?????????????????????????          ?
?                         ?                      ?
??????????????????????????????????????????????????
                          ?
                          ?
        ???????????????????????????????????????
        ? MetadataPollingService              ?
        ?                                     ?
        ?  ?????????????????????????????????  ?
        ?  ? Throttling Logic              ?  ?
        ?  ? - Per-issuer timestamps       ?  ?
        ?  ? - ShouldPollIssuer()          ?  ?
        ?  ? - GetLastPollTimestamp()      ?  ?
        ?  ?????????????????????????????????  ?
        ?                                     ?
        ?  ?????????????????????????????????  ?
        ?  ? Polling Operations            ?  ?
        ?  ? - PollNowAsync() (all)        ?  ?
        ?  ? - PollIssuerNowAsync(id)      ?  ?
        ?  ?????????????????????????????????  ?
        ???????????????????????????????????????
```

## API Design

### MetadataPollingService

```csharp
public class MetadataPollingService
{
    // Constructor includes throttling interval
    public MetadataPollingService(
        IMetadataFetcher fetcher,
        MetadataCache cache,
        IEnumerable<IssuerEndpoint> endpoints,
        int pollingIntervalMinutes,
        int minimumPollIntervalMinutes = 0) // Throttling

    // Check if an issuer should be polled (respects throttling)
    public bool ShouldPollIssuer(string issuerId)

    // Poll a specific issuer (throttled)
    public async Task<bool> PollIssuerNowAsync(string issuerId)

    // Poll all issuers (updates all timestamps)
    public async Task PollNowAsync()

    // Get last poll time for specific issuer
    public DateTime? GetLastPollTimestamp(string issuerId)

    // Get last global poll time
    public DateTime? GetLastGlobalPollTimestamp()
}
```

### AuthenticationFailureRecoveryService

```csharp
public class AuthenticationFailureRecoveryService
{
    // Simplified - no throttling logic needed
    public AuthenticationFailureRecoveryService(
        MetadataPollingService pollingService,  // Handles throttling
        MetadataCache cache,
        IdentityModelConfigurationUpdater updater)

    // Just delegates to polling service
    private async Task<bool> RefreshMetadataForEndpointAsync(IssuerEndpoint endpoint)
    {
        // Polling service handles throttling internally
        return await _pollingService.PollIssuerNowAsync(endpoint.Id);
    }
}
```

## Configuration

The `authFailureRecoveryIntervalMinutes` is passed to `MetadataPollingService`:

```csharp
// In MetadataPollingHttpModule.cs
_pollingService = new MetadataPollingService(
    fetcher,
    cache,
    endpoints,
    config.PollingIntervalMinutes,
    config.AuthFailureRecoveryIntervalMinutes); // Throttling interval
```

This means:
- ? Scheduled polls respect the interval
- ? Recovery-triggered polls respect the interval
- ? Manual `PollIssuerNowAsync()` calls respect the interval
- ? All polling is coordinated

## Behavior Examples

### Scenario 1: Scheduled Poll Followed by Auth Failure

```
Time 0:00 - Scheduled poll runs
Time 0:02 - Auth failure occurs
Time 0:02 - Recovery attempts poll
Result: Poll is THROTTLED (too recent)
Outcome: Current request fails, next request succeeds with existing metadata
```

### Scenario 2: Auth Failure Before Scheduled Poll

```
Time 0:00 - Auth failure occurs
Time 0:00 - Recovery triggers poll
Time 0:05 - Scheduled poll would run
Result: Scheduled poll is THROTTLED (recovery just ran)
Outcome: No redundant poll, metadata already fresh
```

### Scenario 3: Multiple Concurrent Auth Failures

```
Time 0:00 - Auth failure #1 triggers poll
Time 0:00 - Auth failure #2 (concurrent)
Time 0:00 - Auth failure #3 (concurrent)
Result: Only first poll proceeds, others are THROTTLED
Outcome: Prevents DoS from authentication storm
```

## Testing Throttling

### Disable for Testing

```csharp
// Set minimumPollIntervalMinutes to 0
var service = new MetadataPollingService(
    fetcher, cache, endpoints, 
    pollingIntervalMinutes: 60,
    minimumPollIntervalMinutes: 0); // No throttling
```

### Check Throttling Status

```csharp
// Check if an issuer should be polled
bool shouldPoll = pollingService.ShouldPollIssuer("issuer-id");

// Get last poll time
DateTime? lastPoll = pollingService.GetLastPollTimestamp("issuer-id");

// Calculate time until next poll allowed
if (lastPoll.HasValue)
{
    var elapsed = DateTime.UtcNow - lastPoll.Value;
    var remaining = TimeSpan.FromMinutes(minimumInterval) - elapsed;
}
```

## Benefits Over Decentralized Throttling

### ? Old Approach (Recovery Service Throttling)

```csharp
// Each consumer manages its own timestamps
public class AuthenticationFailureRecoveryService
{
    private Dictionary<string, DateTime> _lastPollTimestamps; // Duplicated!
    
    private bool ShouldPoll(string issuerId) { ... } // Duplicated logic!
}

// Problem: Scheduled polling doesn't know about recovery timestamps
// Result: Redundant polls, no coordination
```

### ? New Approach (Centralized Throttling)

```csharp
// Single source of truth
public class MetadataPollingService
{
    private Dictionary<string, DateTime> _lastPollTimestamps; // Central!
    
    public bool ShouldPollIssuer(string issuerId) { ... } // Shared!
}

// Benefit: All consumers coordinate through one service
// Result: No redundant polls, efficient resource usage
```

## Migration Notes

If updating from an older version:

### Before (v1.0)
```csharp
// Recovery service had its own throttling
var recovery = new AuthenticationFailureRecoveryService(
    pollingService,
    cache,
    updater,
    minimumPollIntervalMinutes: 5); // Old parameter
```

### After (v1.1+)
```csharp
// Throttling moved to polling service
var pollingService = new MetadataPollingService(
    fetcher, cache, endpoints,
    pollingIntervalMinutes: 60,
    minimumPollIntervalMinutes: 5); // New parameter

var recovery = new AuthenticationFailureRecoveryService(
    pollingService,  // Handles throttling
    cache,
    updater);        // No throttling parameter
```

## Summary

**Throttling belongs in `MetadataPollingService` because:**

1. ? **Coordination**: Prevents redundant polls from any source
2. ? **Simplicity**: Consumers don't manage timestamps
3. ? **Flexibility**: Easy to adjust behavior globally
4. ? **Correctness**: Single source of truth for poll state
5. ? **Performance**: Optimal resource usage

**Consumers like `AuthenticationFailureRecoveryService` benefit from:**

1. ? **Simpler code**: No throttling logic needed
2. ? **Automatic coordination**: Respects all polling activity
3. ? **Cleaner separation**: Concerns properly separated
4. ? **Easier testing**: Can test without mocking throttling

This is a more robust and maintainable architecture! ??
