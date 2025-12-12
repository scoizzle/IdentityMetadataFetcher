using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.IdentityModel.Metadata;
using IdentityMetadataFetcher.Models;
using IdentityMetadataFetcher.Services;

namespace IdentityMetadataFetcher.ConsoleApp
{
    internal class Program
    {
        private static async Task<int> Main(string[] args)
        {
            PrintHeader();

            if (args.Length == 0 || args.Contains("-h") || args.Contains("--help"))
            {
                PrintUsage();
                return 1;
            }

            var url = args[0];
            var showRaw = args.Skip(1).Any(a => a.Equals("--raw", StringComparison.OrdinalIgnoreCase));

            try
            {
                var options = new MetadataFetchOptions
                {
                    DefaultTimeoutMs = 30000,
                    ValidateServerCertificate = true,
                    MaxRetries = 1,
                    ContinueOnError = false
                };

                var fetcher = new MetadataFetcher(options);
                Console.WriteLine($"Fetching metadata from: {url}");

                var endpoint = new IssuerEndpoint
                {
                    Id = url,
                    Name = url,
                    Endpoint = url,
                    MetadataType = MetadataType.SAML // best-effort; parser supports both
                };

                var result = await fetcher.FetchMetadataAsync(endpoint);

                if (result.Metadata == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("No metadata returned.");
                    Console.ResetColor();
                    return 2;
                }

                PrintMetadataSummary(result.Metadata);

                if (showRaw && !string.IsNullOrWhiteSpace(result.RawMetadata))
                {
                    Console.WriteLine();
                    Console.WriteLine("Raw XML:");
                    Console.WriteLine(new string('-', 80));
                    Console.WriteLine(result.RawMetadata);
                }

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Done.");
                Console.ResetColor();
                return 0;
            }
            catch (WebException wex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Network error: {wex.Message}");
                Console.ResetColor();
                return 3;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
                return 4;
            }
        }

        private static void PrintHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Identity Metadata Fetcher");
            Console.ResetColor();
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  IdentityMetadataFetcher.Console <metadata-url> [--raw]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --raw    Print raw XML metadata after the summary");
        }

        private static void PrintMetadataSummary(MetadataBase metadata)
        {
            var entity = metadata as EntityDescriptor;
            if (entity == null)
            {
                Console.WriteLine("Unsupported metadata type received.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Summary:");
            Console.WriteLine(new string('-', 80));
            Console.WriteLine($"Entity ID: {entity.EntityId?.Id}");

            if (entity.SecurityTokenServiceDescriptor != null)
            {
                var sts = entity.SecurityTokenServiceDescriptor;
                Console.WriteLine("Role: Security Token Service (WS-Fed)");

                if (sts.PassiveRequestorEndpoints != null && sts.PassiveRequestorEndpoints.Any())
                {
                    Console.WriteLine("Passive Requestor Endpoints:");
                    foreach (var ep in sts.PassiveRequestorEndpoints)
                    {
                        Console.WriteLine($"  - {ep.Uri}");
                    }
                }

                if (sts.Keys != null && sts.Keys.Any())
                {
                    Console.WriteLine("Signing Keys:");
                    foreach (var key in sts.Keys)
                    {
                        Console.WriteLine($"  - Use: {key.Use}");
                        foreach (var clause in key.KeyInfo)
                        {
                            Console.WriteLine($"    * {clause.GetType().Name}");
                        }
                    }
                }
            }

            if (entity.IDPSSODescriptor != null)
            {
                var idp = entity.IDPSSODescriptor;
                Console.WriteLine("Role: Identity Provider (SAML)");

                if (idp.SingleSignOnServices != null && idp.SingleSignOnServices.Any())
                {
                    Console.WriteLine("Single Sign-On Services:");
                    foreach (var sso in idp.SingleSignOnServices)
                    {
                        Console.WriteLine($"  - {sso.Binding} -> {sso.Location}");
                    }
                }

                if (idp.Keys != null && idp.Keys.Any())
                {
                    Console.WriteLine("Signing Keys:");
                    foreach (var key in idp.Keys)
                    {
                        Console.WriteLine($"  - Use: {key.Use}");
                        foreach (var clause in key.KeyInfo)
                        {
                            Console.WriteLine($"    * {clause.GetType().Name}");
                        }
                    }
                }
            }
        }
    }
}
