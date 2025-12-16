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
                    Endpoint = url
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
                        Console.WriteLine(result.RawMetadata.TrimStart().StartsWith("{") ? "Raw JSON:" : "Raw XML:");
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

        private static void PrintMetadataSummary(MetadataDocument metadata)
        {
            if (metadata == null)
            {
                Console.WriteLine("No metadata received.");
                return;
            }

            // Handle WsFederationMetadataDocument (the actual concrete type we now use)
            if (metadata is WsFederationMetadataDocument wsFedDoc)
            {
                Console.WriteLine();
                Console.WriteLine("Summary:");
                Console.WriteLine(new string('-', 80));
                
                // Issuer
                if (!string.IsNullOrEmpty(wsFedDoc.Issuer))
                {
                    Console.WriteLine($"Issuer: {wsFedDoc.Issuer}");
                }

                // Configuration info
                if (wsFedDoc.Configuration != null)
                {
                    // Token endpoint
                    if (!string.IsNullOrEmpty(wsFedDoc.Configuration.TokenEndpoint))
                    {
                        Console.WriteLine($"Token Endpoint: {wsFedDoc.Configuration.TokenEndpoint}");
                    }
                }

                // Additional endpoints dictionary
                if (wsFedDoc.Endpoints != null && wsFedDoc.Endpoints.Any())
                {
                    Console.WriteLine();
                    Console.WriteLine($"Endpoints ({wsFedDoc.Endpoints.Count}):");
                    foreach (var ep in wsFedDoc.Endpoints)
                    {
                        Console.WriteLine($"  {ep.Key}: {ep.Value}");
                    }
                }

                // Signing certificates
                if (wsFedDoc.SigningCertificates != null && wsFedDoc.SigningCertificates.Any())
                {
                    Console.WriteLine();
                    Console.WriteLine($"Signing Certificates ({wsFedDoc.SigningCertificates.Count}):");
                    PrintCertificateInformation(wsFedDoc.SigningCertificates);
                }

                // Signing keys from configuration
                if (wsFedDoc.Configuration?.SigningKeys != null && wsFedDoc.Configuration.SigningKeys.Any())
                {
                    Console.WriteLine();
                    Console.WriteLine($"Signing Keys ({wsFedDoc.Configuration.SigningKeys.Count}):");
                    PrintKeyInformation(wsFedDoc.Configuration.SigningKeys);
                }

                // Key infos
                if (wsFedDoc.Configuration?.KeyInfos != null && wsFedDoc.Configuration.KeyInfos.Any())
                {
                    Console.WriteLine();
                    Console.WriteLine($"Key Infos: {wsFedDoc.Configuration.KeyInfos.Count}");
                }

                // Created timestamp
                Console.WriteLine();
                Console.WriteLine($"Created At: {wsFedDoc.CreatedAt:u}");
            }
            // Handle OpenIdConnectMetadataDocument
            else if (metadata is OpenIdConnectMetadataDocument oidcDoc)
            {
                Console.WriteLine();
                Console.WriteLine("OIDC Summary:");
                Console.WriteLine(new string('-', 80));
                
                // Issuer
                if (!string.IsNullOrEmpty(oidcDoc.Issuer))
                {
                    Console.WriteLine($"Issuer: {oidcDoc.Issuer}");
                }

                // Endpoints
                if (oidcDoc.Endpoints != null && oidcDoc.Endpoints.Any())
                {
                    Console.WriteLine();
                    Console.WriteLine($"Endpoints ({oidcDoc.Endpoints.Count}):");
                    foreach (var ep in oidcDoc.Endpoints)
                    {
                        Console.WriteLine($"  {ep.Key}: {ep.Value}");
                    }
                }

                // Signing certificates
                if (oidcDoc.SigningCertificates != null && oidcDoc.SigningCertificates.Any())
                {
                    Console.WriteLine();
                    Console.WriteLine($"Signing Certificates ({oidcDoc.SigningCertificates.Count}):");
                    PrintCertificateInformation(oidcDoc.SigningCertificates);
                }

                // Signing keys from configuration
                if (oidcDoc.Configuration?.SigningKeys != null && oidcDoc.Configuration.SigningKeys.Any())
                {
                    Console.WriteLine();
                    Console.WriteLine($"Signing Keys ({oidcDoc.Configuration.SigningKeys.Count}):");
                    PrintKeyInformation(oidcDoc.Configuration.SigningKeys);
                }

                // Created timestamp
                Console.WriteLine();
                Console.WriteLine($"Created At: {oidcDoc.CreatedAt:u}");
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine("Summary:");
                Console.WriteLine(new string('-', 80));
                Console.WriteLine($"Unknown metadata type: {metadata.GetType().Name}");
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
            PrintCertificateInfo(cert, "    ");
        }

        private static void PrintCertificateInfo(X509Certificate2 cert, string indent)
        {
            Console.WriteLine($"{indent}Subject: {cert.Subject}");
            Console.WriteLine($"{indent}Issuer: {cert.Issuer}");
            Console.WriteLine($"{indent}Thumbprint: {cert.Thumbprint}");
            Console.WriteLine($"{indent}Serial Number: {cert.SerialNumber}");
            Console.WriteLine($"{indent}Valid From: {cert.NotBefore:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"{indent}Valid To: {cert.NotAfter:yyyy-MM-dd HH:mm:ss}");
            
            var now = DateTime.Now;
            if (now < cert.NotBefore)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"{indent}Status: Not yet valid");
                Console.ResetColor();
            }
            else if (now > cert.NotAfter)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"{indent}Status: EXPIRED");
                Console.ResetColor();
            }
            else
            {
                var daysRemaining = (cert.NotAfter - now).Days;
                if (daysRemaining < 30)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"{indent}Status: Expires in {daysRemaining} days");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"{indent}Status: Valid (expires in {daysRemaining} days)");
                    Console.ResetColor();
                }
            }

            // Display public key info
            try
            {
                var publicKey = cert.PublicKey;
                Console.WriteLine($"{indent}Key Algorithm: {publicKey.Oid?.FriendlyName ?? publicKey.Oid?.Value}");
                Console.WriteLine($"{indent}Key Size: {cert.PublicKey.Key?.KeySize} bits");
            }
            catch
            {
                // Some certs may not expose key info
            }
        }

        private static void PrintCertificateInformation(IReadOnlyList<X509Certificate2> certificates)
        {
            var certIndex = 1;
            foreach (var cert in certificates)
            {
                Console.WriteLine($"  Certificate #{certIndex}:");
                PrintCertificateInfo(cert, "    ");
                certIndex++;
            }
        }

    }
}
