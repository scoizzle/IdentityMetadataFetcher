using IdentityMetadataFetcher.Iis.Configuration;
using IdentityMetadataFetcher.Iis.Services;
using IdentityMetadataFetcher.Models;
using IdentityMetadataFetcher.Services;
using System;
using System.Configuration;
using System.Web;

namespace IdentityMetadataFetcher.Iis.Modules
{
    /// <summary>
    /// IIS HTTP Module that manages SAML/WSFED metadata polling and caching.
    /// 
    /// Installation in Web.config:
    /// <code>
    /// <![CDATA[
    /// <configuration>
    ///   <system.webServer>
    ///     <modules>
    ///       <add name="SamlMetadataPollingModule" 
    ///            type="IdentityMetadataFetcher.Iis.Modules.MetadataPollingHttpModule, IdentityMetadataFetcher.Iis" />
    ///     </modules>
    ///   </system.webServer>
    /// </configuration>
    /// ]]>
    /// </code>
    /// </summary>
    public class MetadataPollingHttpModule : IHttpModule
    {
        private static MetadataPollingService _pollingService;
        private static MetadataCache _metadataCache;
        private static IdentityModelConfigurationUpdater _identityUpdater;
        private static AuthenticationFailureRecoveryService _recoveryService;
        private static System.Collections.Generic.IEnumerable<IssuerEndpoint> _endpoints;
        private static readonly object _lockObject = new object();
        private static bool _initialized = false;
        private static bool _autoApplyEnabled = false;
        private static bool _enableSynchronousRecovery = false;
        private static bool _eventsSubscribed = false;

        /// <summary>
        /// Gets the current metadata cache instance.
        /// </summary>
        public static MetadataCache MetadataCache
        {
            get
            {
                if (_metadataCache == null)
                {
                    lock (_lockObject)
                    {
                        if (_metadataCache == null)
                        {
                            _metadataCache = new MetadataCache();
                        }
                    }
                }
                return _metadataCache;
            }
        }

        /// <summary>
        /// Gets the current metadata polling service instance.
        /// </summary>
        public static MetadataPollingService PollingService
        {
            get { return _pollingService; }
        }

        /// <summary>
        /// Gets the authentication failure recovery service instance.
        /// </summary>
        public static AuthenticationFailureRecoveryService RecoveryService
        {
            get { return _recoveryService; }
        }

        public void Init(HttpApplication context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            // Initialize only once per application domain
            if (_initialized)
            {
                // Even if already initialized, hook up events for this HttpApplication instance
                context.AuthenticateRequest += OnAuthenticateRequest;
                context.Error += OnApplicationError;
                return;
            }

            lock (_lockObject)
            {
                if (_initialized)
                {
                    context.AuthenticateRequest += OnAuthenticateRequest;
                    context.Error += OnApplicationError;
                    return;
                }

                try
                {
                    // Load configuration
                    var config = ConfigurationManager.GetSection("samlMetadataPolling") 
                        as MetadataPollingConfigurationSection;

                    if (config == null)
                    {
                        System.Diagnostics.Trace.TraceWarning(
                            "SamlMetadataPollingModule: Configuration section 'samlMetadataPolling' not found in Web.config");
                        _initialized = true;
                        return;
                    }

                    if (!config.Enabled)
                    {
                        System.Diagnostics.Trace.TraceInformation(
                            "SamlMetadataPollingModule: Metadata polling is disabled in configuration");
                        _initialized = true;
                        return;
                    }

                    // Validate configuration
                    if (config.Issuers.Count == 0)
                    {
                        throw new ConfigurationErrorsException(
                            "SamlMetadataPollingModule: No issuers configured in samlMetadataPolling section");
                    }

                    // Create cache instance
                    if (_metadataCache == null)
                    {
                        _metadataCache = new MetadataCache();
                    }

                    // Create metadata fetcher with configured options
                    var fetcherOptions = new MetadataFetchOptions
                    {
                        DefaultTimeoutMs = config.HttpTimeoutSeconds * 1000,
                        ValidateServerCertificate = config.ValidateServerCertificate,
                        MaxRetries = config.MaxRetries,
                        ContinueOnError = true
                    };

                    var fetcher = new MetadataFetcher(fetcherOptions);

                    // Convert configuration to endpoints and store for recovery service
                    _endpoints = config.Issuers.ToIssuerEndpoints();

                    // Create and start polling service
                    _pollingService = new MetadataPollingService(
                        fetcher,
                        _metadataCache,
                        _endpoints,
                        config.PollingIntervalMinutes,
                        config.AuthFailureRecoveryIntervalMinutes); // Pass throttling interval

                    // Configure auto-apply behavior from config
                    _autoApplyEnabled = config.AutoApplyIdentityModel;
                    _enableSynchronousRecovery = config.EnableSynchronousRecovery;
                    
                    if (_autoApplyEnabled)
                    {
                        if (_identityUpdater == null)
                        {
                            _identityUpdater = new IdentityModelConfigurationUpdater();
                        }

                        // Initialize recovery service (no longer needs minimumPollInterval - handled by polling service)
                        _recoveryService = new AuthenticationFailureRecoveryService(
                            _pollingService,
                            _metadataCache,
                            _identityUpdater);

                        System.Diagnostics.Trace.TraceInformation(
                            "SamlMetadataPollingModule: auto-apply to System.IdentityModel is ENABLED");
                        System.Diagnostics.Trace.TraceInformation(
                            $"SamlMetadataPollingModule: authentication failure recovery enabled with {config.AuthFailureRecoveryIntervalMinutes} minute minimum interval");
                        System.Diagnostics.Trace.TraceInformation(
                            $"SamlMetadataPollingModule: synchronous recovery is {(_enableSynchronousRecovery ? "ENABLED" : "DISABLED")}");
                    }
                    else
                    {
                        System.Diagnostics.Trace.TraceInformation(
                            "SamlMetadataPollingModule: auto-apply to System.IdentityModel is DISABLED");
                    }

                    // Subscribe to events for diagnostics (only once)
                    if (!_eventsSubscribed)
                    {
                        _pollingService.PollingStarted += OnPollingStarted;
                        _pollingService.PollingCompleted += OnPollingCompleted;
                        _pollingService.PollingError += OnPollingError;
                        _pollingService.MetadataUpdated += OnMetadataUpdated;
                        _eventsSubscribed = true;
                    }

                    // Start the service
                    _pollingService.Start();

                    System.Diagnostics.Trace.TraceInformation(
                        $"SamlMetadataPollingModule: Initialized with {config.Issuers.Count} issuers, " +
                        $"polling interval {config.PollingIntervalMinutes} minutes");

                    _initialized = true;

                    // Hook up application events for this instance
                    context.AuthenticateRequest += OnAuthenticateRequest;
                    context.Error += OnApplicationError;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError(
                        $"SamlMetadataPollingModule: Initialization error: {ex.Message}");
                    throw;
                }
            }
        }

        public void Dispose()
        {
            // Note: We do NOT unsubscribe from _pollingService events here because:
            // 1. _pollingService is a static field shared across all HttpApplication instances
            // 2. Multiple HttpApplication instances exist in IIS for thread pooling
            // 3. Unsubscribing in one instance's Dispose() would break polling for all others
            // 4. _pollingService disposal is handled by the application domain shutdown
            
            // Only dispose if this is the last instance (rarely happens in practice)
            // The static _pollingService will be cleaned up by the GC when the app domain unloads
        }

        private static void OnAuthenticateRequest(object sender, EventArgs e)
        {
            // This event fires before authentication - we can intercept here if recovery is enabled
            if (!_autoApplyEnabled || _recoveryService == null)
                return;

            var application = sender as HttpApplication;
            if (application == null)
                return;

            // Check if this is a protected resource that will require authentication
            var context = application.Context;
            if (context == null || context.User == null || context.User.Identity.IsAuthenticated)
                return; // Already authenticated or no user context

            // Store a flag to indicate we should attempt recovery on authentication failure
            context.Items["MetadataPolling_EnableRecovery"] = true;
        }

        private static void OnApplicationError(object sender, EventArgs e)
        {
            if (!_autoApplyEnabled || _recoveryService == null)
                return;

            var application = sender as HttpApplication;
            if (application == null)
                return;

            var exception = application.Server.GetLastError();
            if (exception == null)
                return;

            var context = application.Context;

            // Check if this is an authentication-related exception
            if (IsAuthenticationException(exception))
            {
                System.Diagnostics.Trace.TraceWarning(
                    $"SamlMetadataPollingModule: Authentication error detected: {exception.GetType().Name}");

                // Check if recovery was enabled for this request
                var enableRecovery = context.Items["MetadataPolling_EnableRecovery"] as bool?;
                
                // Attempt synchronous recovery if enabled and this is the first attempt
                var retryAttempted = context.Items["MetadataPolling_RetryAttempted"] as bool?;
                if (_enableSynchronousRecovery && enableRecovery == true && retryAttempted != true)
                {
                    try
                    {
                        System.Diagnostics.Trace.TraceInformation(
                            "SamlMetadataPollingModule: Attempting synchronous recovery for current request");

                        // Mark that we've attempted retry to prevent infinite loops
                        context.Items["MetadataPolling_RetryAttempted"] = true;

                        // Attempt synchronous recovery
                        var recoveryTask = _recoveryService.TryRecoverFromAuthenticationFailureAsync(
                            exception, 
                            _endpoints);

                        // Wait for recovery to complete (with timeout)
                        var recovered = recoveryTask.Wait(TimeSpan.FromSeconds(10)) && recoveryTask.Result;

                        if (recovered)
                        {
                            System.Diagnostics.Trace.TraceInformation(
                                "SamlMetadataPollingModule: Metadata refreshed, clearing error and retrying authentication");

                            // Clear the error so the request can continue
                            application.Server.ClearError();

                            // Force re-authentication by redirecting to self
                            // This will cause the request to restart with updated metadata
                            var currentUrl = context.Request.Url.PathAndQuery;
                            context.Response.Redirect(currentUrl, false);
                            context.ApplicationInstance.CompleteRequest();
                            return;
                        }
                        else
                        {
                            System.Diagnostics.Trace.TraceWarning(
                                "SamlMetadataPollingModule: Synchronous recovery did not resolve the issue");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.TraceError(
                            $"SamlMetadataPollingModule: Error during synchronous recovery: {ex.Message}");
                    }
                }

                // If synchronous recovery failed or wasn't attempted, fall back to async
                if (retryAttempted != true || !_enableSynchronousRecovery)
                {
                    // Attempt recovery asynchronously (fire and forget - don't block request)
                    var task = System.Threading.Tasks.Task.Run(async () =>
                    {
                        try
                        {
                            var recovered = await _recoveryService.TryRecoverFromAuthenticationFailureAsync(
                                exception, 
                                _endpoints);

                            if (recovered)
                            {
                                System.Diagnostics.Trace.TraceInformation(
                                    "SamlMetadataPollingModule: Successfully recovered from authentication failure by refreshing metadata (async)");
                            }
                            else
                            {
                                System.Diagnostics.Trace.TraceWarning(
                                    "SamlMetadataPollingModule: Could not recover from authentication failure (async)");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Trace.TraceError(
                                $"SamlMetadataPollingModule: Error during asynchronous recovery: {ex.Message}");
                        }
                    });
                }

                // Note: If synchronous recovery succeeded, the request was redirected
                // If it failed, the current request will still fail, but subsequent requests may succeed
            }
        }

        private static bool IsAuthenticationException(Exception exception)
        {
            if (exception == null)
                return false;

            // Check for common authentication exception types
            var exceptionType = exception.GetType();
            var typeName = exceptionType.FullName;

            return typeName.Contains("SecurityToken") ||
                   typeName.Contains("Authentication") ||
                   typeName.Contains("Authorization") ||
                   exception is UnauthorizedAccessException ||
                   (exception.InnerException != null && IsAuthenticationException(exception.InnerException));
        }

        private static void OnPollingStarted(object sender, PollingEventArgs e)
        {
            System.Diagnostics.Trace.TraceInformation(
                $"SamlMetadataPollingModule: Polling started at {e.StartTime:O}");
        }

        private static void OnPollingCompleted(object sender, PollingEventArgs e)
        {
            System.Diagnostics.Trace.TraceInformation(
                $"SamlMetadataPollingModule: Polling completed at {e.EndTime:O} - " +
                $"Success: {e.SuccessCount}, Failures: {e.FailureCount}, " +
                $"Duration: {e.Duration?.TotalSeconds:F2}s");
        }

        private static void OnPollingError(object sender, PollingErrorEventArgs e)
        {
            System.Diagnostics.Trace.TraceWarning(
                $"SamlMetadataPollingModule: Error polling {e.IssuerName} ({e.IssuerId}): {e.ErrorMessage}");
        }

        private static void OnMetadataUpdated(object sender, MetadataUpdatedEventArgs e)
        {
            System.Diagnostics.Trace.TraceInformation(
                $"SamlMetadataPollingModule: Metadata updated for {e.IssuerName} ({e.IssuerId}) at {e.UpdatedAt:O}");

            if (_autoApplyEnabled)
            {
                try
                {
                    IdentityModelConfigurationUpdater updater;
                    MetadataCache cache;
                    
                    lock (_lockObject)
                    {
                        updater = _identityUpdater;
                        cache = _metadataCache;
                    }
                    
                    if (updater != null && cache != null)
                    {
                        var entry = cache.GetCacheEntry(e.IssuerId);
                        if (entry != null)
                        {
                            updater.Apply(entry, e.IssuerName);
                            System.Diagnostics.Trace.TraceInformation(
                                $"SamlMetadataPollingModule: Applied metadata to System.IdentityModel for {e.IssuerName} ({e.IssuerId})");
                        }
                        else
                        {
                            System.Diagnostics.Trace.TraceWarning(
                                $"SamlMetadataPollingModule: Cache entry missing when applying metadata for {e.IssuerName} ({e.IssuerId})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError(
                        $"SamlMetadataPollingModule: Error applying metadata for {e.IssuerName} ({e.IssuerId}): {ex.Message}");
                }
            }
        }
    }
}
