using IdentityMetadataFetcher.Models;
using IdentityMetadataFetcher.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IdentityMetadataFetcher.Examples
{
    /// <summary>
    /// Example console application demonstrating the usage of the MetadataFetcher library.
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("SAML Metadata Fetcher - Examples");
            Console.WriteLine("================================\n");

            // Example 1: Single endpoint fetch
            Example1_SingleEndpointFetch();

            Console.WriteLine("\n" + new string('-', 50) + "\n");

            // Example 2: Multiple endpoints fetch
            Example2_MultipleEndpointsFetch();

            Console.WriteLine("\n" + new string('-', 50) + "\n");

            // Example 3: Async fetch
            Example3_AsyncFetch();

            Console.WriteLine("\n" + new string('-', 50) + "\n");

            // Example 4: Custom configuration
            Example4_CustomConfiguration();

            Console.WriteLine("\nAll examples completed.");
            Console.ReadLine();
        }

        /// <summary>
        /// Example 1: Fetching metadata from a single endpoint synchronously.
        /// </summary>
        static void Example1_SingleEndpointFetch()
        {
            Console.WriteLine("Example 1: Single Endpoint Fetch (Synchronous)");
            Console.WriteLine("-----------------------------------------------");

            var fetcher = new MetadataFetcher();

            // Create an issuer endpoint
            var endpoint = new IssuerEndpoint
            {
                Id = "azure-ad",
                Endpoint = "https://login.microsoftonline.com/common/federationmetadata/2007-06/federationmetadata.xml",
                Name = "Azure Active Directory",
                MetadataType = MetadataType.WSFED
            };

            Console.WriteLine($"Fetching metadata from: {endpoint.Name}");
            Console.WriteLine($"URL: {endpoint.Endpoint}");

            var result = fetcher.FetchMetadata(endpoint);

            if (result.IsSuccess)
            {
                Console.WriteLine("✓ Success!");
                Console.WriteLine($"  Fetched at: {result.FetchedAt:yyyy-MM-dd HH:mm:ss} UTC");
                Console.WriteLine($"  Metadata Type: {endpoint.MetadataType}");
            }
            else
            {
                Console.WriteLine("✗ Failed!");
                Console.WriteLine($"  Error: {result.ErrorMessage}");
                if (result.Exception != null)
                {
                    Console.WriteLine($"  Exception: {result.Exception.GetType().Name}");
                }
            }
        }

        /// <summary>
        /// Example 2: Fetching metadata from multiple endpoints.
        /// </summary>
        static void Example2_MultipleEndpointsFetch()
        {
            Console.WriteLine("Example 2: Multiple Endpoints Fetch (Synchronous)");
            Console.WriteLine("-------------------------------------------------");

            var options = new MetadataFetchOptions
            {
                DefaultTimeoutMs = 30000,
                ContinueOnError = true,
                ValidateServerCertificate = true,
                MaxRetries = 1
            };

            var fetcher = new MetadataFetcher(options);

            var endpoints = new List<IssuerEndpoint>
            {
                new IssuerEndpoint
                {
                    Id = "issuer1",
                    Endpoint = "https://login.microsoftonline.com/common/federationmetadata/2007-06/federationmetadata.xml",
                    Name = "Azure AD",
                    MetadataType = MetadataType.WSFED
                },
                new IssuerEndpoint
                {
                    Id = "issuer2",
                    Endpoint = "https://example.auth0.com/samlp/metadata",
                    Name = "Auth0",
                    MetadataType = MetadataType.SAML
                },
                new IssuerEndpoint
                {
                    Id = "invalid",
                    Endpoint = "https://invalid-issuer-endpoint-does-not-exist.example.com/metadata",
                    Name = "Invalid Endpoint",
                    MetadataType = MetadataType.SAML
                }
            };

            Console.WriteLine($"Fetching metadata from {endpoints.Count} endpoints...\n");

            var results = fetcher.FetchMetadataFromMultipleEndpoints(endpoints);

            int successCount = 0;
            int failureCount = 0;

            foreach (var result in results)
            {
                if (result.IsSuccess)
                {
                    successCount++;
                    Console.WriteLine($"✓ {result.Endpoint.Name}");
                    Console.WriteLine($"  Type: {result.Endpoint.MetadataType}");
                    Console.WriteLine($"  Fetched: {result.FetchedAt:yyyy-MM-dd HH:mm:ss}");
                }
                else
                {
                    failureCount++;
                    Console.WriteLine($"✗ {result.Endpoint.Name}");
                    Console.WriteLine($"  Error: {result.ErrorMessage}");
                }
            }

            Console.WriteLine($"\nSummary: {successCount} succeeded, {failureCount} failed");
        }

        /// <summary>
        /// Example 3: Fetching metadata asynchronously.
        /// </summary>
        static void Example3_AsyncFetch()
        {
            Console.WriteLine("Example 3: Async Fetch (Task-based)");
            Console.WriteLine("-----------------------------------");

            var task = Example3_AsyncFetchInternal();
            task.Wait();
        }

        static async Task Example3_AsyncFetchInternal()
        {
            var fetcher = new MetadataFetcher();

            var endpoints = new List<IssuerEndpoint>
            {
                new IssuerEndpoint
                {
                    Id = "ep1",
                    Endpoint = "https://login.microsoftonline.com/common/federationmetadata/2007-06/federationmetadata.xml",
                    Name = "Endpoint 1",
                    MetadataType = MetadataType.WSFED
                },
                new IssuerEndpoint
                {
                    Id = "ep2",
                    Endpoint = "https://example.auth0.com/samlp/metadata",
                    Name = "Endpoint 2",
                    MetadataType = MetadataType.SAML
                }
            };

            Console.WriteLine("Fetching metadata asynchronously...\n");

            var results = await fetcher.FetchMetadataFromMultipleEndpointsAsync(endpoints);

            foreach (var result in results)
            {
                string status = result.IsSuccess ? "✓ Success" : "✗ Failed";
                Console.WriteLine($"{status}: {result.Endpoint.Name}");
            }
        }

        /// <summary>
        /// Example 4: Using custom configuration options.
        /// </summary>
        static void Example4_CustomConfiguration()
        {
            Console.WriteLine("Example 4: Custom Configuration");
            Console.WriteLine("-------------------------------");

            var options = new MetadataFetchOptions
            {
                DefaultTimeoutMs = 15000,              // 15 second timeout
                ContinueOnError = true,                // Keep going if one fails
                ValidateServerCertificate = true,      // Validate certificates
                MaxRetries = 2,                        // Retry up to 2 times on failure
                CacheMetadata = false,                 // Don't cache
                CacheDurationMinutes = 120             // If caching, cache for 2 hours
            };

            var fetcher = new MetadataFetcher(options);

            Console.WriteLine("MetadataFetcher created with custom options:");
            Console.WriteLine($"  Default Timeout: {options.DefaultTimeoutMs}ms");
            Console.WriteLine($"  Continue on Error: {options.ContinueOnError}");
            Console.WriteLine($"  Validate Server Certificate: {options.ValidateServerCertificate}");
            Console.WriteLine($"  Max Retries: {options.MaxRetries}");
            Console.WriteLine($"  Cache Metadata: {options.CacheMetadata}");
            Console.WriteLine($"  Cache Duration: {options.CacheDurationMinutes}min");

            // Per-endpoint timeout override
            var endpoint = new IssuerEndpoint
            {
                Id = "slow-endpoint",
                Endpoint = "https://slow-issuer.example.com/metadata",
                Name = "Slow Issuer",
                MetadataType = MetadataType.SAML,
                Timeout = 60000  // 60 second timeout for this specific endpoint
            };

            Console.WriteLine($"\nEndpoint with custom timeout:");
            Console.WriteLine($"  Name: {endpoint.Name}");
            Console.WriteLine($"  Custom Timeout: {endpoint.Timeout}ms (overrides default)");
        }
    }
}
