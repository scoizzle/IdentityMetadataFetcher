using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdentityMetadataFetcher.Models;
using IdentityMetadataFetcher.Services;

namespace IdentityMetadataFetcher.Iis.Services
{
    /// <summary>
    /// Background service that periodically polls issuer endpoints for metadata updates.
    /// </summary>
    public class MetadataPollingService : IDisposable
    {
        private readonly IMetadataFetcher _metadataFetcher;
        private readonly MetadataCache _metadataCache;
        private readonly IEnumerable<IssuerEndpoint> _endpoints;
        private readonly int _pollingIntervalMs;

        private Timer _pollingTimer;
        private bool _isPolling;
        private bool _disposed;

        public event EventHandler<PollingEventArgs> PollingStarted;
        public event EventHandler<PollingEventArgs> PollingCompleted;
        public event EventHandler<PollingErrorEventArgs> PollingError;
        public event EventHandler<MetadataUpdatedEventArgs> MetadataUpdated;

        public MetadataPollingService(
            IMetadataFetcher metadataFetcher,
            MetadataCache metadataCache,
            IEnumerable<IssuerEndpoint> endpoints,
            int pollingIntervalMinutes)
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
            PollMetadataAsync();

            // Schedule periodic polling
            _pollingTimer = new Timer(
                callback: state => PollMetadataAsync(),
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
