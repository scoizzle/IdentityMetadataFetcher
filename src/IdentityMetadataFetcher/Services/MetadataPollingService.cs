using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdentityMetadataFetcher.Models;

namespace IdentityMetadataFetcher.Services
{
    /// <summary>
    /// Background service that periodically polls issuer endpoints for metadata updates.
    /// Can be used in any .NET Framework application (console, Windows Service, web, etc.)
    /// </summary>
    public class MetadataPollingService : IDisposable
    {
        private readonly IMetadataFetcher _metadataFetcher;
        private readonly MetadataCache _metadataCache;
        private IEnumerable<IssuerEndpoint> _endpoints;
        private readonly int _pollingIntervalMs;
        private readonly int _minimumPollIntervalMinutes;
        private readonly Dictionary<string, DateTime> _lastPollTimestamps;
        private readonly object _pollLockObject = new object();

        private Timer _pollingTimer;
        private bool _isPolling;
        private bool _disposed;
        private DateTime? _lastGlobalPoll;

        public event EventHandler<PollingEventArgs> PollingStarted;
        public event EventHandler<PollingEventArgs> PollingCompleted;
        public event EventHandler<PollingErrorEventArgs> PollingError;
        public event EventHandler<MetadataUpdatedEventArgs> MetadataUpdated;

        public MetadataPollingService(
            IMetadataFetcher metadataFetcher,
            MetadataCache metadataCache,
            IEnumerable<IssuerEndpoint> endpoints,
            int pollingIntervalMinutes)
            : this(metadataFetcher, metadataCache, endpoints, pollingIntervalMinutes, 0)
        {
        }

        public MetadataPollingService(
            IMetadataFetcher metadataFetcher,
            MetadataCache metadataCache,
            IEnumerable<IssuerEndpoint> endpoints,
            int pollingIntervalMinutes,
            int minimumPollIntervalMinutes)
        {
            if (metadataFetcher == null)
                throw new ArgumentNullException(nameof(metadataFetcher));

            if (metadataCache == null)
                throw new ArgumentNullException(nameof(metadataCache));

            if (endpoints == null)
                throw new ArgumentNullException(nameof(endpoints));

            if (pollingIntervalMinutes < 1)
                throw new ArgumentException("Polling interval must be at least 1 minute", nameof(pollingIntervalMinutes));

            _metadataFetcher = metadataFetcher;
            _metadataCache = metadataCache;
            _endpoints = endpoints.ToList();
            _pollingIntervalMs = pollingIntervalMinutes * 60 * 1000;
            _minimumPollIntervalMinutes = minimumPollIntervalMinutes;
            _lastPollTimestamps = new Dictionary<string, DateTime>();
            _isPolling = false;
        }

        /// <summary>
        /// Starts the background polling service.
        /// </summary>
        public void Start()
        {
            if (_disposed)
                throw new ObjectDisposedException("MetadataPollingService");

            if (_pollingTimer != null)
                return; // Already started

            // Perform initial poll immediately
            _ = PollMetadataAsync();

            // Schedule periodic polling
            _pollingTimer = new Timer(
                callback: state => _ = PollMetadataAsync(),
                state: null,
                dueTime: TimeSpan.FromMilliseconds(_pollingIntervalMs),
                period: TimeSpan.FromMilliseconds(_pollingIntervalMs));
        }

        /// <summary>
        /// Stops the background polling service.
        /// </summary>
        public void Stop()
        {
            if (_pollingTimer != null)
            {
                _pollingTimer.Dispose();
                _pollingTimer = null;
            }
        }

        /// <summary>
        /// Manually triggers a metadata poll operation.
        /// </summary>
        public async Task PollNowAsync()
        {
            await PollMetadataAsync();
        }

        /// <summary>
        /// Manually triggers a metadata poll for a specific issuer if the minimum interval has elapsed.
        /// </summary>
        /// <param name="issuerId">The issuer identifier to poll.</param>
        /// <returns>True if polling was performed; false if throttled.</returns>
        public async Task<bool> PollIssuerNowAsync(string issuerId)
        {
            return await PollIssuerNowAsync(issuerId, false);
        }

        /// <summary>
        /// Manually triggers a metadata poll for a specific issuer.
        /// </summary>
        /// <param name="issuerId">The issuer identifier to poll.</param>
        /// <param name="force">If true, bypasses throttling and polls immediately.</param>
        /// <returns>True if polling was performed; false if throttled and not forced.</returns>
        public async Task<bool> PollIssuerNowAsync(string issuerId, bool force)
        {
            if (string.IsNullOrEmpty(issuerId))
                throw new ArgumentNullException(nameof(issuerId));

            // Check if we should throttle this poll
            if (!force && !ShouldPollIssuer(issuerId))
            {
                System.Diagnostics.Trace.TraceInformation(
                    $"MetadataPollingService: Skipping throttled poll for issuer '{issuerId}'");
                return false;
            }

            // Find the endpoint
            var endpoint = _endpoints.FirstOrDefault(e => e.Id.Equals(issuerId, StringComparison.OrdinalIgnoreCase));
            if (endpoint == null)
            {
                System.Diagnostics.Trace.TraceWarning(
                    $"MetadataPollingService: Issuer '{issuerId}' not found in configured endpoints");
                return false;
            }

            // Poll just this endpoint
            await PollSingleEndpointAsync(endpoint);
            return true;
        }

        /// <summary>
        /// Checks if sufficient time has elapsed since the last poll for a specific issuer.
        /// </summary>
        /// <param name="issuerId">The issuer identifier.</param>
        /// <returns>True if polling should proceed; false if throttled.</returns>
        public bool ShouldPollIssuer(string issuerId)
        {
            if (_minimumPollIntervalMinutes <= 0)
                return true; // No throttling configured

            lock (_pollLockObject)
            {
                if (!_lastPollTimestamps.ContainsKey(issuerId))
                    return true;

                var lastPoll = _lastPollTimestamps[issuerId];
                var elapsed = DateTime.UtcNow - lastPoll;

                return elapsed.TotalMinutes >= _minimumPollIntervalMinutes;
            }
        }

        /// <summary>
        /// Gets the timestamp of the last poll for a specific issuer.
        /// </summary>
        /// <param name="issuerId">The issuer identifier.</param>
        /// <returns>The last poll timestamp, or null if never polled.</returns>
        public DateTime? GetLastPollTimestamp(string issuerId)
        {
            lock (_pollLockObject)
            {
                return _lastPollTimestamps.ContainsKey(issuerId)
                    ? _lastPollTimestamps[issuerId]
                    : (DateTime?)null;
            }
        }

        /// <summary>
        /// Gets the timestamp of the last global poll (all endpoints).
        /// </summary>
        public DateTime? GetLastGlobalPollTimestamp()
        {
            return _lastGlobalPoll;
        }

        private async Task PollMetadataAsync()
        {
            // Prevent concurrent polling operations
            if (_isPolling)
                return;

            try
            {
                _isPolling = true;

                var now = DateTime.UtcNow;
                OnPollingStarted(new PollingEventArgs { StartTime = now });

                // Fetch metadata from all endpoints asynchronously
                var results = await _metadataFetcher.FetchMetadataFromMultipleEndpointsAsync(_endpoints);

                var successCount = 0;
                var failureCount = 0;

                foreach (var result in results)
                {
                    if (result.IsSuccess && result.Metadata != null)
                    {
                        // Update cache with new metadata
                        _metadataCache.AddOrUpdateMetadata(
                            result.Endpoint.Id,
                            result.Metadata,
                            result.RawMetadata);

                        // Update poll timestamp for this issuer
                        UpdatePollTimestamp(result.Endpoint.Id, result.FetchedAt);

                        successCount++;

                        OnMetadataUpdated(new MetadataUpdatedEventArgs
                        {
                            IssuerId = result.Endpoint.Id,
                            IssuerName = result.Endpoint.Name,
                            UpdatedAt = result.FetchedAt
                        });
                    }
                    else
                    {
                        failureCount++;
                        OnPollingError(new PollingErrorEventArgs
                        {
                            IssuerId = result.Endpoint.Id,
                            IssuerName = result.Endpoint.Name,
                            ErrorMessage = result.ErrorMessage,
                            Exception = result.Exception,
                            OccurredAt = DateTime.UtcNow
                        });
                    }
                }

                _lastGlobalPoll = now;

                OnPollingCompleted(new PollingEventArgs
                {
                    StartTime = now,
                    EndTime = DateTime.UtcNow,
                    SuccessCount = successCount,
                    FailureCount = failureCount
                });
            }
            finally
            {
                _isPolling = false;
            }
        }

        private async Task PollSingleEndpointAsync(IssuerEndpoint endpoint)
        {
            var now = DateTime.UtcNow;

            System.Diagnostics.Trace.TraceInformation(
                $"MetadataPollingService: Polling single endpoint '{endpoint.Name}' ({endpoint.Id})");

            var result = await _metadataFetcher.FetchMetadataAsync(endpoint);

            if (result.IsSuccess && result.Metadata != null)
            {
                // Update cache with new metadata
                _metadataCache.AddOrUpdateMetadata(
                    result.Endpoint.Id,
                    result.Metadata,
                    result.RawMetadata);

                // Update poll timestamp for this issuer
                UpdatePollTimestamp(result.Endpoint.Id, result.FetchedAt);

                OnMetadataUpdated(new MetadataUpdatedEventArgs
                {
                    IssuerId = result.Endpoint.Id,
                    IssuerName = result.Endpoint.Name,
                    UpdatedAt = result.FetchedAt
                });
            }
            else
            {
                OnPollingError(new PollingErrorEventArgs
                {
                    IssuerId = result.Endpoint.Id,
                    IssuerName = result.Endpoint.Name,
                    ErrorMessage = result.ErrorMessage,
                    Exception = result.Exception,
                    OccurredAt = DateTime.UtcNow
                });
            }
        }

        private void UpdatePollTimestamp(string issuerId, DateTime timestamp)
        {
            lock (_pollLockObject)
            {
                _lastPollTimestamps[issuerId] = timestamp;
            }
        }

        /// <summary>
        /// Adds a new issuer to the running polling service.
        /// </summary>
        public bool AddIssuer(IssuerEndpoint endpoint)
        {
            if (endpoint == null || string.IsNullOrWhiteSpace(endpoint.Id))
                return false;

            lock (_pollLockObject)
            {
                // Check if issuer already exists
                var endpointsList = _endpoints as List<IssuerEndpoint>;
                if (endpointsList == null)
                {
                    // Convert to list if needed
                    endpointsList = _endpoints.ToList();
                }
                else if (endpointsList.Any(e => e.Id == endpoint.Id))
                {
                    return false; // Already exists
                }

                endpointsList.Add(endpoint);
                _endpoints = endpointsList;

                System.Diagnostics.Trace.TraceInformation(
                    $"MetadataPollingService: Added issuer '{endpoint.Id}' ({endpoint.Name})");

                return true;
            }
        }

        /// <summary>
        /// Updates an existing issuer in the running polling service.
        /// </summary>
        public bool UpdateIssuer(IssuerEndpoint endpoint)
        {
            if (endpoint == null || string.IsNullOrWhiteSpace(endpoint.Id))
                return false;

            lock (_pollLockObject)
            {
                var endpointsList = _endpoints as List<IssuerEndpoint>;
                if (endpointsList == null)
                {
                    endpointsList = _endpoints.ToList();
                }

                var existingIndex = endpointsList.FindIndex(e => e.Id == endpoint.Id);
                if (existingIndex < 0)
                    return false; // Not found

                endpointsList[existingIndex] = endpoint;
                _endpoints = endpointsList;

                System.Diagnostics.Trace.TraceInformation(
                    $"MetadataPollingService: Updated issuer '{endpoint.Id}' ({endpoint.Name})");

                return true;
            }
        }

        /// <summary>
        /// Removes an issuer from the running polling service.
        /// </summary>
        public bool RemoveIssuer(string issuerId)
        {
            if (string.IsNullOrWhiteSpace(issuerId))
                return false;

            lock (_pollLockObject)
            {
                var endpointsList = _endpoints as List<IssuerEndpoint>;
                if (endpointsList == null)
                {
                    endpointsList = _endpoints.ToList();
                }

                var removed = endpointsList.RemoveAll(e => e.Id == issuerId) > 0;
                if (removed)
                {
                    _endpoints = endpointsList;
                    _lastPollTimestamps.Remove(issuerId);

                    System.Diagnostics.Trace.TraceInformation(
                        $"MetadataPollingService: Removed issuer '{issuerId}'");
                }

                return removed;
            }
        }

        /// <summary>
        /// Gets the current list of issuer endpoints being polled.
        /// </summary>
        public IEnumerable<IssuerEndpoint> GetCurrentEndpoints()
        {
            lock (_pollLockObject)
            {
                return _endpoints.ToList();
            }
        }

        /// <summary>
        /// Gets the metadata cache entry for the specified issuer.
        /// </summary>
        public MetadataCacheEntry GetMetadataCacheEntry(string issuerId)
        {
            return _metadataCache.GetCacheEntry(issuerId);
        }

        protected virtual void OnPollingStarted(PollingEventArgs e)
        {
            PollingStarted?.Invoke(this, e);
        }

        protected virtual void OnPollingCompleted(PollingEventArgs e)
        {
            PollingCompleted?.Invoke(this, e);
        }

        protected virtual void OnPollingError(PollingErrorEventArgs e)
        {
            PollingError?.Invoke(this, e);
        }

        protected virtual void OnMetadataUpdated(MetadataUpdatedEventArgs e)
        {
            MetadataUpdated?.Invoke(this, e);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Stop();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Event arguments for polling start/completion.
    /// </summary>
    public class PollingEventArgs : EventArgs
    {
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }

        public int TotalCount
        {
            get { return SuccessCount + FailureCount; }
        }

        public TimeSpan? Duration
        {
            get
            {
                if (EndTime.HasValue)
                    return EndTime.Value - StartTime;
                return null;
            }
        }
    }

    /// <summary>
    /// Event arguments for polling errors.
    /// </summary>
    public class PollingErrorEventArgs : EventArgs
    {
        public string IssuerId { get; set; }
        public string IssuerName { get; set; }
        public string ErrorMessage { get; set; }
        public Exception Exception { get; set; }
        public DateTime OccurredAt { get; set; }
    }

    /// <summary>
    /// Event arguments for successful metadata updates.
    /// </summary>
    public class MetadataUpdatedEventArgs : EventArgs
    {
        public string IssuerId { get; set; }
        public string IssuerName { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
