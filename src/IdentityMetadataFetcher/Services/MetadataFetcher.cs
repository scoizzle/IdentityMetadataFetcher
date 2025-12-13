using System;
using System.Collections.Generic;
using Microsoft.IdentityModel.Protocols.WsFederation;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using IdentityMetadataFetcher.Exceptions;
using IdentityMetadataFetcher.Models;

namespace IdentityMetadataFetcher.Services
{
    /// <summary>
    /// Service for fetching WSFED and SAML metadata from issuing authorities.
    /// </summary>
    public class MetadataFetcher : IMetadataFetcher
    {
        private readonly MetadataFetchOptions _options;
        private readonly HttpClientHandler _httpClientHandler;

        public MetadataFetcher() : this(new MetadataFetchOptions())
        {
        }

        public MetadataFetcher(MetadataFetchOptions options)
        {
            _options = options ?? new MetadataFetchOptions();
            
            _httpClientHandler = new HttpClientHandler();
            if (!_options.ValidateServerCertificate)
            {
                // WARNING: Only disable certificate validation for development/testing
                ServicePointManager.ServerCertificateValidationCallback = 
                    (sender, certificate, chain, sslPolicyErrors) => true;
            }
        }

        /// <summary>
        /// Fetches metadata from a single issuer endpoint synchronously.
        /// </summary>
        public MetadataFetchResult FetchMetadata(IssuerEndpoint endpoint)
        {
            if (endpoint == null)
                throw new ArgumentNullException(nameof(endpoint));

            if (string.IsNullOrWhiteSpace(endpoint.Endpoint))
                throw new ArgumentException("Endpoint URL cannot be null or empty", nameof(endpoint));

            var result = new MetadataFetchResult
            {
                Endpoint = endpoint
            };

            try
            {
                var timeout = endpoint.Timeout ?? _options.DefaultTimeoutMs;
                var metadataXml = DownloadMetadataXml(endpoint.Endpoint, timeout);
                var metadata = ParseMetadata(metadataXml, endpoint.MetadataType);
                result.IsSuccess = true;
                result.Metadata = metadata;
                result.RawMetadata = metadataXml;
                return result;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Exception = ex;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        /// <summary>
        /// Asynchronously fetches metadata from a single issuer endpoint.
        /// </summary>
        public async Task<MetadataFetchResult> FetchMetadataAsync(IssuerEndpoint endpoint)
        {
            if (endpoint == null)
                throw new ArgumentNullException(nameof(endpoint));

            if (string.IsNullOrWhiteSpace(endpoint.Endpoint))
                throw new ArgumentException("Endpoint URL cannot be null or empty", nameof(endpoint));

            var result = new MetadataFetchResult
            {
                Endpoint = endpoint
            };

            try
            {
                var timeout = endpoint.Timeout ?? _options.DefaultTimeoutMs;
                var metadataXml = await DownloadMetadataXmlAsync(endpoint.Endpoint, timeout);
                var metadata = ParseMetadata(metadataXml, endpoint.MetadataType);
                result.IsSuccess = true;
                result.Metadata = metadata;
                result.RawMetadata = metadataXml;
                return result;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Exception = ex;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        /// <summary>
        /// Fetches metadata from multiple issuer endpoints synchronously.
        /// </summary>
        public IEnumerable<MetadataFetchResult> FetchMetadataFromMultipleEndpoints(IEnumerable<IssuerEndpoint> endpoints)
        {
            if (endpoints == null)
                throw new ArgumentNullException(nameof(endpoints));

            var results = new List<MetadataFetchResult>();

            foreach (var endpoint in endpoints)
            {
                try
                {
                    var result = FetchMetadata(endpoint);
                    results.Add(result);
                }
                catch (Exception ex) when (_options.ContinueOnError)
                {
                    results.Add(new MetadataFetchResult
                    {
                        Endpoint = endpoint,
                        IsSuccess = false,
                        Exception = ex,
                        ErrorMessage = ex.Message
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// Asynchronously fetches metadata from multiple issuer endpoints.
        /// </summary>
        public async Task<IEnumerable<MetadataFetchResult>> FetchMetadataFromMultipleEndpointsAsync(IEnumerable<IssuerEndpoint> endpoints)
        {
            if (endpoints == null)
                throw new ArgumentNullException(nameof(endpoints));

            var tasks = new List<Task<MetadataFetchResult>>();

            foreach (var endpoint in endpoints)
            {
                tasks.Add(FetchMetadataAsync(endpoint));
            }

            var completedResults = await Task.WhenAll(tasks);
            return completedResults;
        }

        private WsFederationConfiguration FetchMetadataInternal(IssuerEndpoint endpoint)
        {
            var timeout = endpoint.Timeout ?? _options.DefaultTimeoutMs;
            var metadataXml = DownloadMetadataXml(endpoint.Endpoint, timeout);
            
            return ParseMetadata(metadataXml, endpoint.MetadataType);
        }

        private async Task<WsFederationConfiguration> FetchMetadataInternalAsync(IssuerEndpoint endpoint)
        {
            var timeout = endpoint.Timeout ?? _options.DefaultTimeoutMs;
            var metadataXml = await DownloadMetadataXmlAsync(endpoint.Endpoint, timeout);
            
            return ParseMetadata(metadataXml, endpoint.MetadataType);
        }

        private string DownloadMetadataXml(string endpoint, int timeoutMs)
        {
            using var client = new HttpClient(_httpClientHandler, false);
            client.Timeout = TimeSpan.FromMilliseconds(timeoutMs);

            for (int i = 0; i <= _options.MaxRetries; i++)
            {
                try
                {
                    var response = client.GetAsync(endpoint).Result;

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new MetadataFetchException(
                            $"HTTP request failed with status code {response.StatusCode}",
                            endpoint,
                            (int)response.StatusCode,
                            null
                        );
                    }

                    return response.Content.ReadAsStringAsync().Result;
                }
                catch (HttpRequestException) when (i < _options.MaxRetries)
                {
                    // Retry on failure
                    continue;
                }
                catch (HttpRequestException ex)
                {
                    throw new MetadataFetchException(
                        $"Failed to download metadata from {endpoint}",
                        endpoint,
                        ex
                    );
                }
            }

            throw new MetadataFetchException(
                $"Failed to download metadata from {endpoint} after {_options.MaxRetries + 1} attempts",
                endpoint
            );
        }

        private async Task<string> DownloadMetadataXmlAsync(string endpoint, int timeoutMs)
        {
            using var client = new HttpClient(_httpClientHandler, false);
            client.Timeout = TimeSpan.FromMilliseconds(timeoutMs);

            for (int i = 0; i <= _options.MaxRetries; i++)
            {
                try
                {
                    var response = await client.GetAsync(endpoint);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new MetadataFetchException(
                            $"HTTP request failed with status code {response.StatusCode}",
                            endpoint,
                            (int)response.StatusCode,
                            null
                        );
                    }

                    return await response.Content.ReadAsStringAsync();
                }
                catch (HttpRequestException) when (i < _options.MaxRetries)
                {
                    // Retry on failure
                    await Task.Delay(100 * (i + 1)); // Exponential backoff
                    continue;
                }
                catch (HttpRequestException ex)
                {
                    throw new MetadataFetchException(
                        $"Failed to download metadata from {endpoint}",
                        endpoint,
                        ex
                    );
                }
            }

            throw new MetadataFetchException(
                $"Failed to download metadata from {endpoint} after {_options.MaxRetries + 1} attempts",
                endpoint
            );
        }

        private WsFederationConfiguration ParseMetadata(string metadataXml, MetadataType metadataType)
        {
            if (string.IsNullOrWhiteSpace(metadataXml))
                throw new MetadataFetchException("Downloaded metadata is empty or null");

            try
            {
                using var reader = XmlReader.Create(new StringReader(metadataXml));
                var serializer = new WsFederationMetadataSerializer();
                var metadata = serializer.ReadMetadata(reader);
                return metadata;
            }
            catch (Exception ex)
            {
                throw new MetadataFetchException(
                    $"Failed to parse metadata as {metadataType}: {ex.Message}",
                    null,
                    ex
                );
            }
        }
    }
}
