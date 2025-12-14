using IdentityMetadataFetcher.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IdentityMetadataFetcher.Services
{
    /// <summary>
    /// Thread-safe cache for metadata documents.
    /// Can be used in any .NET Framework application (console, service, web, etc.)
    /// </summary>
    public class MetadataCache
    {
        private readonly object _lockObject = new object();
        private readonly Dictionary<string, MetadataCacheEntry> _cache;

        public MetadataCache()
        {
            _cache = new Dictionary<string, MetadataCacheEntry>();
        }

        /// <summary>
        /// Stores metadata in the cache.
        /// </summary>
        public void AddOrUpdateMetadata(string issuerId, WsFederationMetadataDocument metadata, string rawXml)
        {
            if (string.IsNullOrWhiteSpace(issuerId))
                throw new ArgumentException("issuerId cannot be null or empty", nameof(issuerId));

            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));

            lock (_lockObject)
            {
                _cache[issuerId] = new MetadataCacheEntry
                {
                    IssuerId = issuerId,
                    Metadata = metadata,
                    RawXml = rawXml,
                    CachedAt = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Retrieves metadata from the cache.
        /// </summary>
        public WsFederationMetadataDocument GetMetadata(string issuerId)
        {
            if (string.IsNullOrWhiteSpace(issuerId))
                throw new ArgumentException("issuerId cannot be null or empty", nameof(issuerId));

            lock (_lockObject)
            {
                MetadataCacheEntry entry;
                if (_cache.TryGetValue(issuerId, out entry))
                {
                    return entry.Metadata;
                }
            }

            return null;
        }

        /// <summary>
        /// Retrieves raw XML metadata from the cache.
        /// </summary>
        public string GetRawMetadata(string issuerId)
        {
            if (string.IsNullOrWhiteSpace(issuerId))
                throw new ArgumentException("issuerId cannot be null or empty", nameof(issuerId));

            lock (_lockObject)
            {
                MetadataCacheEntry entry;
                if (_cache.TryGetValue(issuerId, out entry))
                {
                    return entry.RawXml;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets a cache entry including metadata and timestamp.
        /// </summary>
        public MetadataCacheEntry GetCacheEntry(string issuerId)
        {
            if (string.IsNullOrWhiteSpace(issuerId))
                throw new ArgumentException("issuerId cannot be null or empty", nameof(issuerId));

            lock (_lockObject)
            {
                MetadataCacheEntry entry;
                if (_cache.TryGetValue(issuerId, out entry))
                {
                    return entry;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets all cached metadata entries.
        /// </summary>
        public IEnumerable<MetadataCacheEntry> GetAllEntries()
        {
            lock (_lockObject)
            {
                return _cache.Values.ToList();
            }
        }

        /// <summary>
        /// Checks if metadata is cached for the specified issuer.
        /// </summary>
        public bool HasMetadata(string issuerId)
        {
            if (string.IsNullOrWhiteSpace(issuerId))
                return false;

            lock (_lockObject)
            {
                return _cache.ContainsKey(issuerId);
            }
        }

        /// <summary>
        /// Clears all cached metadata.
        /// </summary>
        public void Clear()
        {
            lock (_lockObject)
            {
                _cache.Clear();
            }
        }

        /// <summary>
        /// Gets the count of cached entries.
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lockObject)
                {
                    return _cache.Count;
                }
            }
        }
    }

    /// <summary>
    /// Represents a single metadata cache entry.
    /// </summary>
    public class MetadataCacheEntry
    {
        /// <summary>
        /// Gets or sets the issuer identifier.
        /// </summary>
        public string IssuerId { get; set; }

        /// <summary>
        /// Gets or sets the parsed metadata document.
        /// </summary>
        public WsFederationMetadataDocument Metadata { get; set; }

        /// <summary>
        /// Gets or sets the raw XML representation of the metadata.
        /// </summary>
        public string RawXml { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the metadata was cached (UTC).
        /// </summary>
        public DateTime CachedAt { get; set; }
    }
}
