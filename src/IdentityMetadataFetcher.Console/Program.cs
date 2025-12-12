using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.IdentityModel.Metadata;
using System.IdentityModel.Tokens;
using System.Security.Cryptography.X509Certificates;
using System.Collections.Generic;
using IdentityMetadataFetcher.Models;
using IdentityMetadataFetcher.Services;

namespace IdentityMetadataFetcher.ConsoleApp
{
    internal class Program
    {
        private static async Task<int> Main(string[] args)
        {
            PrintHeader();

#if DEBUG
            // If no args, in Debug build, and console input is attached, prompt for URL
            if (args.Length == 0 && !Console.IsInputRedirected) {
                Console.Write("Enter metadata URL: ");
                var inputUrl = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(inputUrl))
                {
                    PrintUsage();
                    return 1;
                }

                args = [inputUrl.Trim()];
            }
#endif

            if (args.Length == 0 || args.Contains("-h") || args.Contains("--help") || args.All(string.IsNullOrEmpty))
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

            // WS-Fed STS role
            var sts = entity.RoleDescriptors?.OfType<SecurityTokenServiceDescriptor>().FirstOrDefault();
            if (sts != null)
            {
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
                    PrintKeyInformation(sts.Keys);
                }
            }

            // SAML IdP role
            var idp = entity.RoleDescriptors?.OfType<IdentityProviderSingleSignOnDescriptor>().FirstOrDefault();
            if (idp != null)
            {
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
                    PrintKeyInformation(idp.Keys);
                }
            }
        }

        private static void PrintKeyInformation(ICollection<KeyDescriptor> keys)
        {
            var keyIndex = 1;
            foreach (var key in keys)
            {
                Console.WriteLine($"  Key #{keyIndex}:");
                Console.WriteLine($"    Use: {key.Use}");

                foreach (var clause in key.KeyInfo)
                {
                    if (clause is X509RawDataKeyIdentifierClause rawDataClause)
                    {
                        try
                        {
                            var cert = new X509Certificate2(rawDataClause.GetX509RawData());
                            PrintCertificateInfo(cert);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"    Error reading certificate: {ex.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"    Key Type: {clause.GetType().Name}");
                    }
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
