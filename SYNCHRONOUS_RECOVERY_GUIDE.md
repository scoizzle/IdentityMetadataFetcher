# Synchronous Recovery - Making the Current Request Succeed

## Overview

By default, authentication failure recovery happens **asynchronously** - the current request fails, but subsequent requests succeed after metadata is refreshed. With the `enableSynchronousRecovery` option, you can make the **current request succeed** by:

1. Detecting the authentication failure
2. Synchronously waiting for metadata refresh
3. Redirecting the request to retry with updated certificates

## Configuration

```xml
<samlMetadataPolling enabled="true"
                     autoApplyIdentityModel="true"
                     enableSynchronousRecovery="true"
                     authFailureRecoveryIntervalMinutes="5">
  <issuers>
    <add id="azure-ad" 
         endpoint="https://login.microsoftonline.com/tenant-id/federationmetadata/2007-06/federationmetadata.xml" 
         name="Azure AD" />
  </issuers>
</samlMetadataPolling>
```

## How It Works

### Asynchronous Recovery (Default: `enableSynchronousRecovery="false"`)

```
User Request ? Authentication Fails ? Error Logged
                                    ?
                          Recovery Starts (background)
                                    ?
User Sees Error                Metadata Refreshed
                                    ?
                          Next Request Succeeds ?
```

### Synchronous Recovery (`enableSynchronousRecovery="true"`)

```
User Request ? Authentication Fails ? Recovery Starts Immediately
                                    ?
                          Wait for Metadata Refresh (max 10 seconds)
                                    ?
                          Metadata Refreshed Successfully
                                    ?
                          Clear Error + Redirect to Self
                                    ?
                          Request Retries with New Certificates
                                    ?
                          Authentication Succeeds ?
                                    ?
User Sees Success (slightly delayed)
```

## Pros and Cons

### Asynchronous Recovery (Default)

**Pros:**
- ? Fast response time - no waiting
- ? No risk of request timeout
- ? Lower server resource usage
- ? Simpler to understand and debug

**Cons:**
- ? Current request always fails
- ? User sees error page
- ? Requires user to retry manually or via client-side code

### Synchronous Recovery

**Pros:**
- ? Current request can succeed
- ? Better user experience (transparent recovery)
- ? No error page shown to user
- ? No client-side retry logic needed

**Cons:**
- ? Adds up to 10 seconds to request time
- ? Risk of request timeout
- ? Higher server resource usage (blocking thread)
- ? Two redirects visible to user (original request + retry)
- ? May impact server performance under load

## When to Use Synchronous Recovery

### Good Use Cases

1. **Low-Traffic Applications**: Where blocking a thread for 10 seconds won't impact performance
2. **Internal Applications**: Where user experience is critical and timeout risk is acceptable
3. **API Endpoints with Retry Logic**: Where the redirect is handled automatically
4. **Mobile Apps**: Where seamless recovery is important

### Avoid If

1. **High-Traffic Public Sites**: Thread exhaustion risk
2. **Aggressive Load Balancer Timeouts**: < 15 seconds
3. **Slow Metadata Endpoints**: > 5 seconds response time
4. **Limited Server Resources**: High memory/CPU usage

## Performance Impact

### Request Timeline Comparison

**Asynchronous (default):**
```
Request 1: 0-500ms (fails)
Background: 0-5000ms (recovery)
Request 2: 0-500ms (succeeds)
Total User Time: ~1000ms (manual retry) + user reaction time
```

**Synchronous:**
```
Request 1: 0-500ms (fails) ? 500-10500ms (recovery) ? 10500-11000ms (retry succeeds)
Total User Time: ~11000ms (automatic, transparent)
```

### Server Resource Impact

| Metric | Asynchronous | Synchronous |
|--------|-------------|-------------|
| Thread Blocking | None | Up to 10s per failure |
| Concurrent Failures | Unlimited (async) | Limited by thread pool |
| Memory Usage | Low | Higher (pending requests) |
| Risk of Timeout | None | High if metadata slow |

## Configuration Recommendations

### Production - High Traffic

```xml
<!-- Optimize for performance -->
<samlMetadataPolling 
    autoApplyIdentityModel="true"
    enableSynchronousRecovery="false"    <!-- Async for performance -->
    pollingIntervalMinutes="60"
    authFailureRecoveryIntervalMinutes="5">
```

### Production - Low Traffic

```xml
<!-- Optimize for user experience -->
<samlMetadataPolling 
    autoApplyIdentityModel="true"
    enableSynchronousRecovery="true"     <!-- Sync for UX -->
    pollingIntervalMinutes="120"
    authFailureRecoveryIntervalMinutes="5">
```

### Development/Testing

```xml
<!-- Enable for testing sync recovery -->
<samlMetadataPolling 
    autoApplyIdentityModel="true"
    enableSynchronousRecovery="true"
    pollingIntervalMinutes="5"
    authFailureRecoveryIntervalMinutes="1">
```

## Monitoring Synchronous Recovery

### Log Messages

**Successful Synchronous Recovery:**
```
Authentication error detected: SecurityTokenValidationException
Attempting synchronous recovery for current request
Detected certificate trust failure in authentication
Attempting metadata refresh for issuer 'Azure AD'
Successfully refreshed and applied metadata for 'Azure AD'
Metadata refreshed, clearing error and retrying authentication
[Request redirects and retries automatically]
```

**Synchronous Recovery Timeout:**
```
Authentication error detected: SecurityTokenValidationException
Attempting synchronous recovery for current request
Detected certificate trust failure in authentication
Error during synchronous recovery: The operation has timed out
[Falls back to async recovery]
```

### Performance Metrics to Monitor

1. **Average Request Duration**: Should be < 15 seconds with sync recovery
2. **Thread Pool Saturation**: Monitor `ThreadPool.GetAvailableThreads()`
3. **Request Timeout Rate**: Should remain low (< 1%)
4. **Success Rate**: % of sync recoveries that succeed before timeout

### Custom Monitoring Code

```csharp
// In Global.asax or application startup
protected void Application_BeginRequest(object sender, EventArgs e)
{
    Context.Items["RequestStartTime"] = DateTime.UtcNow;
}

protected void Application_EndRequest(object sender, EventArgs e)
{
    var startTime = Context.Items["RequestStartTime"] as DateTime?;
    if (startTime.HasValue)
    {
        var duration = DateTime.UtcNow - startTime.Value;
        if (duration.TotalSeconds > 10)
        {
            // Log slow request - may indicate sync recovery
            logger.LogWarning($"Slow request: {duration.TotalSeconds}s - {Request.Url}");
        }
    }
}
```

## Timeout Configuration

The synchronous recovery waits up to **10 seconds** for metadata refresh. This is hardcoded to balance:

- **Success Rate**: Most metadata endpoints respond in < 5 seconds
- **User Experience**: 10 seconds is the practical limit users will wait
- **Server Impact**: Longer blocking increases thread exhaustion risk

### Adjusting Timeout (Advanced)

If you need a different timeout, modify the code:

```csharp
// In MetadataPollingHttpModule.cs, OnApplicationError method
// Change this line:
var recovered = recoveryTask.Wait(TimeSpan.FromSeconds(10)) && recoveryTask.Result;

// To a custom value (example: 15 seconds):
var recovered = recoveryTask.Wait(TimeSpan.FromSeconds(15)) && recoveryTask.Result;
```

?? **Warning**: Increasing timeout increases risk of:
- Request timeouts
- Thread pool exhaustion
- Poor user experience

## Troubleshooting

### Issue: Requests Timing Out

**Symptoms:**
- 503 Service Unavailable errors
- Thread pool exhaustion warnings
- Very slow response times

**Solution:**
```xml
<!-- Disable synchronous recovery -->
<samlMetadataPolling enableSynchronousRecovery="false">
```

### Issue: Recovery Succeeds but Redirect Fails

**Symptoms:**
- Recovery logs show success
- Redirect doesn't happen
- User still sees error

**Possible Causes:**
1. Custom error pages intercepting redirect
2. Response already written
3. HTTP modules interfering

**Solution:**
```xml
<!-- Ensure custom errors allow redirects -->
<customErrors mode="RemoteOnly" redirectMode="ResponseRedirect">
```

### Issue: Infinite Redirect Loop

**Symptoms:**
- Browser shows "too many redirects"
- Logs show repeated recovery attempts

**Cause:** The retry flag isn't being set properly

**Solution:** Verify in logs:
```
MetadataPolling_RetryAttempted flag should be set to prevent loops
```

## Security Considerations

### Redirect Safety

The synchronous recovery redirects to the **same URL** that caused the failure:

```csharp
var currentUrl = context.Request.Url.PathAndQuery;
context.Response.Redirect(currentUrl, false);
```

This is safe because:
- ? No user input in redirect URL
- ? Relative redirect (no open redirect vulnerability)
- ? Preserves query string and path
- ? Uses `false` to avoid `ThreadAbortException`

### Retry Loop Prevention

The module prevents infinite loops by:
- Setting `MetadataPolling_RetryAttempted` flag
- Only attempting recovery once per request
- Falling back to async recovery if sync fails

### Rate Limiting Still Applies

Even with synchronous recovery, the `authFailureRecoveryIntervalMinutes` rate limiting applies:
- Prevents DoS from rapid authentication failures
- Protects identity provider endpoints
- Ensures reasonable server resource usage

## Best Practices

### 1. Start with Async, Test Sync

```xml
<!-- Phase 1: Production with async -->
<samlMetadataPolling enableSynchronousRecovery="false">

<!-- Phase 2: Test sync in staging -->
<samlMetadataPolling enableSynchronousRecovery="true">

<!-- Phase 3: Enable in production if metrics look good -->
```

### 2. Monitor Performance Closely

Track these metrics when enabling sync recovery:
- Average request duration
- 95th percentile request duration
- Request timeout rate
- Thread pool available threads

### 3. Use with Appropriate Timeouts

Ensure your environment supports the delay:
```xml
<!-- IIS application pool configuration -->
<processModel idleTimeout="00:20:00" />

<!-- Load balancer timeout should be > 15 seconds -->
```

### 4. Combine with Client-Side Retry

Even with sync recovery enabled, implement client-side retry as backup:

```javascript
async function authenticatedFetch(url) {
    try {
        return await fetch(url);
    } catch (error) {
        if (error.status === 401) {
            // Wait briefly, then retry
            await new Promise(r => setTimeout(r, 2000));
            return await fetch(url);
        }
        throw error;
    }
}
```

## Summary

Synchronous recovery is **disabled by default** because:
- Most applications prioritize performance over UX
- Async recovery works well with client-side retry logic
- Lower risk of timeout and resource exhaustion

**Enable synchronous recovery** (`enableSynchronousRecovery="true"`) if:
- User experience is critical
- You have low traffic / adequate server resources
- Your metadata endpoints are fast (< 5 seconds)
- Your timeouts are configured appropriately (> 15 seconds)

**Monitor carefully** after enabling to ensure it doesn't negatively impact performance.
