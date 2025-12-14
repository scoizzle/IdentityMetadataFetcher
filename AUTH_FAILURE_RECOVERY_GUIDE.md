# Authentication Failure Recovery - Implementation Guide

## Overview

The IdentityMetadataFetcher IIS module now includes automatic recovery from authentication failures caused by issuer certificate rotation. This feature intercepts `System.IdentityModel` authentication failures, detects when they're due to untrusted certificates, and automatically refreshes metadata from the identity provider to obtain updated certificates.

## How It Works

### 1. Normal Operation

```
???????????????????????????????????????????????????????????????
? Scheduled Polling (e.g., every 60 minutes)                  ?
? ?? Fetch metadata from all configured issuers               ?
? ?? Update cache with latest certificates                    ?
? ?? Apply to System.IdentityModel (if autoApplyIdentityModel=true) ?
???????????????????????????????????????????????????????????????
```

### 2. Recovery from Certificate Rotation

```
??????????????????????????????????????????????????????????????
? 1. User attempts authentication                            ?
?    ?                                                        ?
? 2. System.IdentityModel validates token                    ?
?    ?                                                        ?
? 3. Certificate trust failure (ID4037, ID4175, etc.)        ?
?    ?                                                        ?
? 4. HttpApplication.Error event fires                       ?
?    ?                                                        ?
? 5. MetadataPollingHttpModule.OnApplicationError()          ?
?    ?? Checks if autoApplyIdentityModel is enabled          ?
?    ?? Analyzes exception for certificate trust issues      ?
?    ?? Calls AuthenticationFailureRecoveryService           ?
?       ?                                                     ?
?       6. AuthenticationFailureRecoveryService               ?
?          ?? Extracts issuer from exception                  ?
?          ?? Matches to configured endpoint                  ?
?          ?? Checks if minimum interval elapsed             ?
?          ?? Triggers immediate metadata poll               ?
?          ?? Applies updated certificates                    ?
?             ?                                               ?
?             7. Subsequent requests succeed                  ?
??????????????????????????????????????????????????????????????
```

## Configuration

The `authFailureRecoveryIntervalMinutes` setting controls throttling at the **`MetadataPollingService`** level, which prevents excessive metadata polling from any source (scheduled polling or recovery attempts).

### Basic Configuration

```xml
<samlMetadataPolling enabled="true"
                     autoApplyIdentityModel="true"
                     pollingIntervalMinutes="60"
                     httpTimeoutSeconds="30"
                     authFailureRecoveryIntervalMinutes="5"
                     enableSynchronousRecovery="false">
  <!-- authFailureRecoveryIntervalMinutes: minimum time (1-60 minutes) between 
       ANY metadata polls for the same issuer, whether from scheduled polling,
       manual refresh, or authentication failure recovery.
       Default: 5 minutes -->
  <issuers>
    <add id="azure-ad" 
         endpoint="https://login.microsoftonline.com/your-tenant/federationmetadata/2007-06/federationmetadata.xml" 
         name="Azure Active Directory" 
         metadataType="WSFED" />
  </issuers>
</samlMetadataPolling>
```

### Configuration Options

| Option | Default | Range | Description |
|--------|---------|-------|-------------|
| `autoApplyIdentityModel` | false | true/false | Must be enabled for recovery to work |
| `authFailureRecoveryIntervalMinutes` | 5 | 1-60 | Minimum time between forced polls for same issuer |
| `pollingIntervalMinutes` | 60 | 1-10080 | Normal scheduled polling interval |

### Tuning Guidelines

#### Production Environment

```xml
<!-- Balanced configuration for production -->
<samlMetadataPolling 
    pollingIntervalMinutes="120"           <!-- Poll every 2 hours normally -->
    authFailureRecoveryIntervalMinutes="5" <!-- Allow recovery every 5 minutes -->
    autoApplyIdentityModel="true">
```

**Rationale:**
- 2-hour polling is sufficient for most identity providers
- 5-minute recovery interval allows quick response to certificate rotation
- Won't cause excessive load during authentication storms

#### High-Security Environment

```xml
<!-- More frequent polling, slower recovery -->
<samlMetadataPolling 
    pollingIntervalMinutes="30"            <!-- Poll every 30 minutes -->
    authFailureRecoveryIntervalMinutes="10" <!-- Recovery every 10 minutes -->
    autoApplyIdentityModel="true">
```

**Rationale:**
- More frequent polling reduces reliance on recovery mechanism
- Longer recovery interval prevents excessive polling during issues

#### Development Environment

```xml
<!-- Rapid polling for testing -->
<samlMetadataPolling 
    pollingIntervalMinutes="5"             <!-- Poll every 5 minutes -->
    authFailureRecoveryIntervalMinutes="1" <!-- Recovery every 1 minute -->
    autoApplyIdentityModel="true">
```

**Rationale:**
- Quick feedback during development
- Test recovery mechanism frequently

## Exception Types Detected

The recovery service detects the following System.IdentityModel exception patterns:

### WIF Error IDs

- **ID4037**: The key needed to verify the signature could not be resolved
- **ID4022**: The key needed to decrypt the token could not be resolved
- **ID4175**: The issuer of the security token was not recognized
- **ID4257**: The key wrap token provided is not a X509SecurityToken
- **ID4252**: X509SecurityToken cannot be validated

### Message Patterns

- Contains "signature"
- Contains "certificate"
- Contains "X509"
- Contains "key needed"
- Contains "issuer"

## Monitoring and Diagnostics

### Enable Trace Logging

Add to `Web.config`:

```xml
<system.diagnostics>
  <trace autoflush="true">
    <listeners>
      <add name="textWriter" 
           type="System.Diagnostics.TextWriterTraceListener" 
           initializeData="App_Data\metadata_recovery.log" />
    </listeners>
  </trace>
</system.diagnostics>
```

### Log Messages

#### Successful Recovery

```
SamlMetadataPollingModule: Authentication error detected: SecurityTokenValidationException
AuthenticationFailureRecoveryService: Detected certificate trust failure in authentication
AuthenticationFailureRecoveryService: Attempting metadata refresh for issuer 'Azure Active Directory' (azure-ad)
AuthenticationFailureRecoveryService: Successfully refreshed and applied metadata for 'Azure Active Directory'
SamlMetadataPollingModule: Successfully recovered from authentication failure by refreshing metadata
```

#### Skipped Recovery (Too Soon)

```
AuthenticationFailureRecoveryService: Detected certificate trust failure in authentication
AuthenticationFailureRecoveryService: Skipping recent poll for issuer 'Azure Active Directory' (azure-ad)
SamlMetadataPollingModule: Could not recover from authentication failure
```

#### No Matching Issuer

```
AuthenticationFailureRecoveryService: Detected certificate trust failure in authentication
AuthenticationFailureRecoveryService: No matching configured endpoints found for issuer 'https://unknown-issuer.com'
```

### Performance Counters

Monitor these metrics:

1. **Authentication Failure Rate**: Track how often recovery is triggered
2. **Recovery Success Rate**: % of recoveries that succeed
3. **Time Between Recoveries**: Track `authFailureRecoveryIntervalMinutes` effectiveness
4. **Metadata Fetch Duration**: How long recovery polling takes

### Custom Monitoring

```csharp
// In Global.asax or application startup
protected void Application_Start()
{
    var service = MetadataPollingHttpModule.RecoveryService;
    if (service != null)
    {
        // Access recovery service for custom monitoring
        // Note: Service is only available if autoApplyIdentityModel is enabled
    }
}
```

## Troubleshooting

### Issue: Recovery Not Triggering

**Symptoms:**
- Authentication failures continue
- No recovery log messages

**Checks:**
1. Verify `autoApplyIdentityModel="true"`
2. Check trace logs are enabled
3. Confirm exception is certificate-related
4. Verify endpoints are configured

**Solution:**
```xml
<!-- Ensure both settings are enabled -->
<samlMetadataPolling enabled="true"
                     autoApplyIdentityModel="true">
```

### Issue: Excessive Polling

**Symptoms:**
- Too many metadata fetch requests
- High network traffic to identity provider

**Checks:**
1. Review `authFailureRecoveryIntervalMinutes`
2. Check for authentication storms
3. Review exception patterns

**Solution:**
```xml
<!-- Increase recovery interval -->
<samlMetadataPolling authFailureRecoveryIntervalMinutes="15">
```

### Issue: Recovery Succeeds But Authentication Still Fails

**Symptoms:**
- Recovery logs show success
- Subsequent authentications still fail

**Possible Causes:**
1. **Multiple Certificates**: Identity provider uses multiple signing certificates and only one was rotated
2. **Caching Issues**: Application or IIS has cached old configuration
3. **Load Balancer**: Different servers in farm have inconsistent metadata
4. **Clock Skew**: Certificate validity periods don't account for time differences

**Solutions:**

#### 1. Force Application Pool Recycle
```powershell
# Restart application pool to clear all caches
Restart-WebAppPool -Name "YourAppPoolName"
```

#### 2. Check All Certificates in Metadata
```csharp
// Diagnostic code to list all certificates
var cache = MetadataPollingHttpModule.MetadataCache;
var entry = cache.GetCacheEntry("azure-ad");
var config = entry.Metadata;

foreach (var key in config.SigningKeys)
{
    if (key is Microsoft.IdentityModel.Tokens.X509SecurityKey x509Key)
    {
        // Log each certificate thumbprint
        var thumbprint = x509Key.Certificate.Thumbprint;
    }
}
```

#### 3. Verify Certificate Chain Trust
```xml
<!-- Ensure certificate validation is enabled -->
<samlMetadataPolling validateServerCertificate="true">
```

### Issue: First Request After Rotation Always Fails

**This is expected behavior**

**Why:**
- Recovery is triggered by the failure
- Current request completes before metadata is refreshed
- Subsequent requests succeed

**Mitigation Options:**

1. **Shorter Polling Interval**: Reduce chance of being caught by rotation
```xml
<samlMetadataPolling pollingIntervalMinutes="30">
```

2. **Custom Error Page**: Provide user-friendly message
```xml
<customErrors mode="On">
  <error statusCode="401" redirect="~/auth-error.aspx" />
</customErrors>
```

3. **Client-Side Retry**: Implement automatic retry in client application
```javascript
// Pseudo-code
fetch('/api/secured-endpoint')
    .catch(error => {
        if (error.status === 401) {
            // Wait briefly for recovery
            setTimeout(() => {
                fetch('/api/secured-endpoint'); // Retry
            }, 2000);
        }
    });
```

## Best Practices

### 1. Monitor Identity Provider Notifications

Subscribe to your identity provider's notifications about certificate rotation:
- **Azure AD**: Microsoft 365 Admin Center notifications
- **Okta**: Admin console notifications
- **Auth0**: Dashboard notifications

### 2. Test Recovery Regularly

Simulate certificate rotation in test environment:
```powershell
# Force immediate polling to test recovery
$service = [MetadataPollingHttpModule]::PollingService
$service.PollNowAsync().Wait()
```

### 3. Implement Health Checks

```csharp
public class MetadataHealthCheck
{
    public bool CheckCertificateExpiry()
    {
        var cache = MetadataPollingHttpModule.MetadataCache;
        var entry = cache.GetCacheEntry("azure-ad");
        
        // Check certificate expiry dates
        // Alert if expiring within 7 days
        return true;
    }
}
```

### 4. Load Balancer Considerations

For load-balanced environments:
- Ensure all servers have synchronized time (NTP)
- Consider shared cache solution (Redis, SQL)
- Monitor recovery across all servers

### 5. Graceful Degradation

Implement fallback authentication:
```csharp
public void ConfigureAuth(IAppBuilder app)
{
    app.Use((context, next) =>
    {
        try
        {
            return next.Invoke();
        }
        catch (SecurityTokenValidationException)
        {
            // Fallback to alternative authentication
            return FallbackAuth(context);
        }
    });
}
```

## API Reference

### AuthenticationFailureRecoveryService

#### Methods

```csharp
// Attempt recovery from authentication failure
Task<bool> TryRecoverFromAuthenticationFailureAsync(
    Exception exception, 
    IEnumerable<IssuerEndpoint> endpoints)

// Get timestamp of last poll for an issuer
DateTime? GetLastPollTimestamp(string issuerId)

// Clear all poll timestamps (testing only)
void ClearPollTimestamps()
```

### AuthenticationFailureInterceptor

#### Methods

```csharp
// Check if exception is certificate-related
bool IsCertificateTrustFailure(Exception exception)

// Extract issuer identifier from exception
string ExtractIssuerFromException(Exception exception)
```

## Security Considerations

### Rate Limiting

The `authFailureRecoveryIntervalMinutes` setting provides built-in rate limiting:
- Prevents DoS from excessive polling
- Protects identity provider endpoints
- Prevents application resource exhaustion

### Trust Validation

- Always keep `validateServerCertificate="true"` in production
- Only fetch metadata from HTTPS endpoints
- Monitor for certificate validation failures

### Audit Trail

All recovery operations are logged:
- Timestamp of recovery attempt
- Issuer identifier
- Success/failure status
- Exception details (sanitized)

### Principle of Least Privilege

Recovery service:
- Only polls configured issuers
- Only applies metadata when explicitly enabled
- Cannot modify authentication policies
- Cannot bypass certificate validation

---

## Summary

The authentication failure recovery feature provides automatic resilience to certificate rotation events while maintaining security and preventing abuse. By properly configuring the recovery interval and monitoring the logs, you can ensure seamless authentication even when identity provider certificates are rotated between polling intervals.
