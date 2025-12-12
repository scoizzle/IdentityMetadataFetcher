using System;
using System.Configuration;
using System.Web;
using IdentityMetadataFetcher.Iis.Configuration;
using IdentityMetadataFetcher.Iis.Services;
using IdentityMetadataFetcher.Models;
using IdentityMetadataFetcher.Services;

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
        private static readonly object _lockObject = new object();
        private static bool _initialized = false;
        private static bool _autoApplyEnabled = false;

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

        public void Init(HttpApplication context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            // Initialize only once per application domain
            if (_initialized)
                return;

            lock (_lockObject)
            {
                if (_initialized)
                    return;

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

                    // Convert configuration to endpoints
                    var endpoints = config.Issuers.ToIssuerEndpoints();

                    // Create and start polling service
                    _pollingService = new MetadataPollingService(
                        fetcher,
                        _metadataCache,
                        endpoints,
                        config.PollingIntervalMinutes);

                    // Configure auto-apply behavior from config
                    _autoApplyEnabled = config.AutoApplyIdentityModel;
                    if (_autoApplyEnabled)
                    {
                        if (_identityUpdater == null)
                        {
                            _identityUpdater = new IdentityModelConfigurationUpdater();
                        }
                        System.Diagnostics.Trace.TraceInformation(
                            "SamlMetadataPollingModule: auto-apply to System.IdentityModel is ENABLED");
                    }
                    else
                    {
                        System.Diagnostics.Trace.TraceInformation(
                            "SamlMetadataPollingModule: auto-apply to System.IdentityModel is DISABLED");
                    }

                    // Subscribe to events for diagnostics
                    _pollingService.PollingStarted += OnPollingStarted;
                    _pollingService.PollingCompleted += OnPollingCompleted;
                    _pollingService.PollingError += OnPollingError;
                    _pollingService.MetadataUpdated += OnMetadataUpdated;

                    // Start the service
                    _pollingService.Start();

                    System.Diagnostics.Trace.TraceInformation(
                        $"SamlMetadataPollingModule: Initialized with {config.Issuers.Count} issuers, " +
                        $"polling interval {config.PollingIntervalMinutes} minutes");

                    _initialized = true;
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
            if (_pollingService != null)
            {
                _pollingService.Dispose();
                _pollingService = null;
            }
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
