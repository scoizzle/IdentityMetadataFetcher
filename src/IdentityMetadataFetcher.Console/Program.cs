using IdentityMetadataFetcher.Models;
using IdentityMetadataFetcher.Services;
using Microsoft.IdentityModel.Protocols.WsFederation;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace IdentityMetadataFetcher.ConsoleApp
{
    internal class Program
    {
        private static async Task<int> Main(string[] args)
        {
            PrintHeader();

#if DEBUG
            // If no args, in Debug build, and console input is attached, prompt for URL
            if (!args.Any(e => Uri.TryCreate(e, UriKind.Absolute, out _)) && !Console.IsInputRedirected) {
                Console.Write("Enter metadata URL: ");
                var inputUrl = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(inputUrl))
                {
                    PrintUsage();
                    return 1;
                }

                List<string> arguments = [inputUrl, .. args];
                args = arguments.ToArray();
            }
#endif

            if (args.Length == 0 || args.Contains("-h") || args.Contains("--help") || args.All(string.IsNullOrEmpty))
            {
                PrintUsage();
                return 1;
            }

            var url = args[0];
            var showRaw = args.Skip(1).Any(a => a.Equals("--raw", StringComparison.OrdinalIgnoreCase));
            var enablePolling = args.Skip(1).Any(a => a.Equals("--poll", StringComparison.OrdinalIgnoreCase));

            // Optional: polling interval in minutes
            var intervalArg = args.Skip(1)
                                  .FirstOrDefault(a => a.StartsWith("--interval-min=", StringComparison.OrdinalIgnoreCase));
            int pollingIntervalMinutes = 15; // default
            if (intervalArg != null)
            {
                var parts = intervalArg.Split('=');
                if (parts.Length == 2 && int.TryParse(parts[1], out var parsed) && parsed >= 1)
                {
                    pollingIntervalMinutes = parsed;
                }
            }

            try
            {
                var options = new MetadataFetchOptions
                {
                    DefaultTimeoutMs = 30000,
                    ValidateServerCertificate = true,
                    MaxRetries = 1,
                    ContinueOnError = false
                };

                var endpoint = new IssuerEndpoint
                {
                    Id = url,
                    Name = url,
                    Endpoint = url,
                    MetadataType = MetadataType.SAML // best-effort; parser supports both
                };

                if (enablePolling)
                {
                    // Set up cache and poller
                    var cache = new MetadataCache();
                    var fetcher = new MetadataFetcher(options);
                    var endpoints = new List<IssuerEndpoint> { endpoint };
                    var poller = new MetadataPollingService(fetcher, cache, endpoints, pollingIntervalMinutes);

                    // Wire up simple console logging for events
                    poller.PollingStarted += (s, e) =>
                    {
                        Console.WriteLine($"Polling started at {e.StartTime:u}");
                    };
                    poller.PollingCompleted += (s, e) =>
                    {
                        Console.WriteLine($"Polling completed at {e.EndTime:u} (success={e.SuccessCount}, failure={e.FailureCount})");
                    };
                    poller.PollingError += (s, e) =>
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"Polling error for '{e.IssuerName}': {e.ErrorMessage}");
                        if (e.Exception != null)
                        {
                            Console.WriteLine(e.Exception.Message);
                        }
                        Console.ResetColor();
                    };
                    poller.MetadataUpdated += (s, e) =>
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"Metadata updated for '{e.IssuerName}' at {e.UpdatedAt:u}");
                        Console.ResetColor();

                        // Print summary of latest metadata in cache
                        var entry = cache.GetCacheEntry(e.IssuerId);
                        if (entry != null && entry.Metadata != null)
                        {
                            PrintMetadataSummary(entry.Metadata);
                            if (showRaw && !string.IsNullOrWhiteSpace(entry.RawXml))
                            {
                                Console.WriteLine();
                                Console.WriteLine("Raw XML:");
                                Console.WriteLine(new string('-', 80));
                                Console.WriteLine(entry.RawXml);
                            }
                        }
                    };

                    // Start polling and keep the app alive until user quits
                    poller.Start();
                    Console.WriteLine($"Polling enabled (interval={pollingIntervalMinutes} min). Press Ctrl+C or 'q' then Enter to quit.");

                    var quit = false;
                    Console.CancelKeyPress += (sender, eventArgs) =>
                    {
                        eventArgs.Cancel = true; // prevent immediate process termination
                        quit = true;
                    };

                    while (!quit)
                    {
                        var line = Console.ReadLine();
                        if (line != null && line.Trim().Equals("q", StringComparison.OrdinalIgnoreCase))
                        {
                            quit = true;
                        }
                    }

                    poller.Stop();
                    poller.Dispose();
                    Console.WriteLine("Stopped.");

                    return 0;
                }
                else
                {
                    var fetcher = new MetadataFetcher(options);
                    Console.WriteLine($"Fetching metadata from: {url}");

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
            Console.WriteLine("  IdentityMetadataFetcher.Console <metadata-url> [--raw] [--poll] [--interval-min=N]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --raw           Print raw XML metadata after the summary");
            Console.WriteLine("  --poll          Enable continuous polling and keep the app running until quit");
            Console.WriteLine("  --interval-min  Polling interval in minutes (default 15, minimum 1)");
        }

        private static void PrintMetadataSummary(object metadata)
        {
            if (metadata == null)
            {
                Console.WriteLine("No metadata received.");
                return;
            }

            // Check if it's WsFederationConfiguration
            if (metadata is WsFederationConfiguration fedMetadata)
            {
                Console.WriteLine();
                Console.WriteLine("Summary:");
                Console.WriteLine(new string('-', 80));
                Console.WriteLine($"Issuer: {fedMetadata.Issuer}");

                // Token endpoint
                if (!string.IsNullOrEmpty(fedMetadata.TokenEndpoint))
                {
                    Console.WriteLine($"Token Endpoint: {fedMetadata.TokenEndpoint}");
                }

                // Signing keys
                if (fedMetadata.SigningKeys != null && fedMetadata.SigningKeys.Any())
                {
                    Console.WriteLine($"Signing Keys: {fedMetadata.SigningKeys.Count}");
                    PrintKeyInformation(fedMetadata.SigningKeys);
                }

                // Additional properties
                if (fedMetadata.KeyInfos != null && fedMetadata.KeyInfos.Any())
                {
                    Console.WriteLine($"Key Infos: {fedMetadata.KeyInfos.Count}");
                }
            }
            else if (metadata is System.Xml.Linq.XElement samlMetadata)
            {
                Console.WriteLine();
                Console.WriteLine("Summary:");
                Console.WriteLine(new string('-', 80));
                Console.WriteLine($"SAML EntityDescriptor: {samlMetadata.Name.LocalName}");
                
                var entityId = samlMetadata.Attribute("entityID")?.Value;
                if (!string.IsNullOrWhiteSpace(entityId))
                {
                    Console.WriteLine($"Entity ID: {entityId}");
                }
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine("Summary:");
                Console.WriteLine(new string('-', 80));
                Console.WriteLine($"Metadata type: {metadata.GetType().Name}");
            }
        }

        private static void PrintKeyInformation(ICollection<SecurityKey> keys)
        {
            var keyIndex = 1;
            foreach (var key in keys)
            {
                Console.WriteLine($"  Key #{keyIndex}:");
                Console.WriteLine($"    Key ID: {key.KeyId}");

                if (key is X509SecurityKey x509Key)
                {
                    try
                    {
                        PrintCertificateInfo(x509Key.Certificate);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"    Error reading certificate: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"    Key Type: {key.GetType().Name}");
                }

                keyIndex++;
            }
        }

        private static void PrintCertificateInfo(X509Certificate2 cert)
        {
            Console.WriteLine("    Certificate:");
            Console.WriteLine($"      Subject: {cert.Subject}");
            Console.WriteLine($"      Issuer: {cert.Issuer}");
            Console.WriteLine($"      Thumbprint: {cert.Thumbprint}");
            Console.WriteLine($"      Valid From: {cert.NotBefore:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"      Valid To: {cert.NotAfter:yyyy-MM-dd HH:mm:ss}");
            
            var now = DateTime.Now;
            if (now < cert.NotBefore)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("      Status: Not yet valid");
                Console.ResetColor();
            }
            else if (now > cert.NotAfter)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("      Status: EXPIRED");
                Console.ResetColor();
            }
            else
            {
                var daysRemaining = (cert.NotAfter - now).Days;
                if (daysRemaining < 30)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"      Status: Expires in {daysRemaining} days");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"      Status: Valid (expires in {daysRemaining} days)");
                    Console.ResetColor();
                }
            }
        }
    }
}
