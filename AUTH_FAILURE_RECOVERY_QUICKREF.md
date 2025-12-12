# Authentication Failure Recovery - Quick Reference

## Enable Feature

```xml
<samlMetadataPolling enabled="true"
                     autoApplyIdentityModel="true"
                     enableSynchronousRecovery="false"
                     authFailureRecoveryIntervalMinutes="5">
  <issuers>
    <add id="azure-ad" 
         endpoint="https://login.microsoftonline.com/tenant-id/federationmetadata/2007-06/federationmetadata.xml" 
         name="Azure AD" 
         metadataType="WSFED" />
  </issuers>
</samlMetadataPolling>
```

## Recovery Modes

### Asynchronous (Default - `enableSynchronousRecovery="false"`)
? **Current request fails**, subsequent requests succeed  
? Fast response (no blocking)  
? Best for high-traffic sites  

### Synchronous (`enableSynchronousRecovery="true"`)
? **Current request can succeed** (with redirect)  
?? Adds up to 10 seconds to request time  
?? Best for low-traffic or internal applications  

## What It Does

? Detects authentication failures caused by untrusted certificates  
? Automatically polls issuer metadata for updated certificates  
? Applies new certificates to System.IdentityModel  
? Allows subsequent authentication attempts to succeed  
? (Sync mode) Redirects current request to retry with new certificates

## Configuration Quick Guide

| Setting | Default | Values | Purpose |
|---------|---------|--------|---------|
| `autoApplyIdentityModel` | false | true/false | Enable recovery feature |
| `enableSynchronousRecovery` | false | true/false | Make current request succeed |
| `authFailureRecoveryIntervalMinutes` | 5 | 1-60 | Minimum time between forced polls |
| `pollingIntervalMinutes` | 60 | 1-10080 | Normal polling interval |

## Important Notes

### Asynchronous Mode (Default)
?? **Current request fails** - Recovery happens asynchronously  
?? **Subsequent requests succeed** - After metadata refresh  
?? **Rate limited** - By `authFailureRecoveryIntervalMinutes`  
?? **Requires autoApplyIdentityModel** - Must be enabled  

### Synchronous Mode
? **Current request redirects and retries** - May succeed  
?? **10 second delay** - While waiting for metadata  
?? **Risk of timeout** - If metadata endpoint is slow  
?? **Higher resource usage** - Blocks thread during recovery  
?? **Only once per request** - Prevents infinite loops

## Detected Exceptions

- **ID4037**: Signature verification key not found
- **ID4175**: Issuer not recognized
- **ID4022**: Decryption key not found
- **ID4257**: X509 token issue
- **ID4252**: X509 validation failed

## Enable Logging

```xml
<system.diagnostics>
  <trace autoflush="true">
    <listeners>
      <add name="textWriter" 
           type="System.Diagnostics.TextWriterTraceListener" 
           initializeData="App_Data\recovery.log" />
    </listeners>
  </trace>
</system.diagnostics>
```

## Log Messages to Monitor

### Success
```
? Detected certificate trust failure
? Attempting metadata refresh for issuer 'Azure AD'
? Successfully refreshed and applied metadata
? Successfully recovered from authentication failure
```

### Rate Limited
```
? Skipping recent poll for issuer 'Azure AD'
```

### No Match
```
? No matching configured endpoints found
```

## Troubleshooting Checklist

- [ ] Is `autoApplyIdentityModel="true"`?
- [ ] Is issuer configured in `<issuers>` collection?
- [ ] Has `authFailureRecoveryIntervalMinutes` elapsed?
- [ ] Is metadata endpoint accessible?
- [ ] Are trace logs enabled?
- [ ] Is exception certificate-related?

## Testing

```csharp
// Verify recovery service is available
var recoveryService = MetadataPollingHttpModule.RecoveryService;
if (recoveryService != null)
{
    // Recovery is enabled
    var lastPoll = recoveryService.GetLastPollTimestamp("azure-ad");
    Console.WriteLine($"Last poll: {lastPoll}");
}
```

## Production Settings

```xml
<!-- Recommended for production -->
<samlMetadataPolling enabled="true"
                     autoApplyIdentityModel="true"
                     pollingIntervalMinutes="120"
                     authFailureRecoveryIntervalMinutes="5"
                     httpTimeoutSeconds="30"
                     validateServerCertificate="true"
                     maxRetries="2">
  <issuers>
    <add id="azure-ad" 
         endpoint="https://login.microsoftonline.com/your-tenant-id/federationmetadata/2007-06/federationmetadata.xml" 
         name="Azure Active Directory" 
         metadataType="WSFED" />
  </issuers>
</samlMetadataPolling>
```

## Key Benefits

?? **Automatic resilience** to certificate rotation  
? **Zero downtime** - No manual intervention needed  
??? **Rate limited** - Prevents excessive polling  
?? **Full diagnostics** - Complete audit trail  
?? **Background operation** - Non-blocking recovery  

## Limitations

- Current request always fails (recovery is async)
- Requires `autoApplyIdentityModel` to be enabled
- Only works for configured issuers
- Subject to `authFailureRecoveryIntervalMinutes` rate limiting
- Does not prevent initial failure after certificate rotation

---

For complete documentation, see: `AUTH_FAILURE_RECOVERY_GUIDE.md`
