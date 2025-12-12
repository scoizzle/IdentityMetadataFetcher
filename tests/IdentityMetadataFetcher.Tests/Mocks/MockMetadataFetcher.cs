using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdentityMetadataFetcher.Models;
using IdentityMetadataFetcher.Services;

namespace IdentityMetadataFetcher.Tests.Mocks
{
    /// <summary>
    /// Mock implementation of IMetadataFetcher for testing purposes
    /// </summary>
    public class MockMetadataFetcher : IMetadataFetcher
    {
        private readonly Dictionary<string, bool> _failureMap = new Dictionary<string, bool>();
        private readonly Dictionary<string, string> _errorMap = new Dictionary<string, string>();

        /// <summary>
        /// Set an endpoint to fail with the specified error message
        /// </summary>
        public void SetFailure(string endpointId, string errorMessage)
        {
            _failureMap[endpointId] = true;
            _errorMap[endpointId] = errorMessage;
        }

        /// <summary>
        /// Clear all failure settings
        /// </summary>
        public void ClearFailures()
        {
            _failureMap.Clear();
            _errorMap.Clear();
        }

        public MetadataFetchResult FetchMetadata(IssuerEndpoint endpoint)
        {
            if (_failureMap.ContainsKey(endpoint.Id))
            {
                return MetadataFetchResult.Failure(endpoint, _errorMap[endpoint.Id]);
            }

            var metadata = new MockMetadata();
            var rawXml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<EntityDescriptor xmlns=""urn:oasis:names:tc:SAML:2.0:metadata"" ID=""{Guid.NewGuid()}"">
    <SPSSODescriptor protocolSupportEnumeration=""urn:oasis:names:tc:SAML:2.0:protocol"">
        <SingleLogoutService Binding=""urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST"" Location=""https://example.com/logout"" />
        <AssertionConsumerService Binding=""urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST"" Location=""https://example.com/acs"" index=""0"" isDefault=""true"" />
    </SPSSODescriptor>
</EntityDescriptor>";

            return MetadataFetchResult.Success(endpoint, metadata, rawXml);
        }

        public async Task<MetadataFetchResult> FetchMetadataAsync(IssuerEndpoint endpoint)
        {
            await Task.Delay(10); // Simulate async work
            return FetchMetadata(endpoint);
        }

        public IEnumerable<MetadataFetchResult> FetchMetadataFromMultipleEndpoints(IEnumerable<IssuerEndpoint> endpoints)
        {
            return endpoints.Select(ep => FetchMetadata(ep)).ToList();
        }

        public async Task<IEnumerable<MetadataFetchResult>> FetchMetadataFromMultipleEndpointsAsync(IEnumerable<IssuerEndpoint> endpoints)
        {
            var tasks = endpoints.Select(ep => FetchMetadataAsync(ep)).ToList();
            var results = await Task.WhenAll(tasks);
            return results;
        }
    }
}
