# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2025-12-11

### Added

- Initial release of SAML Metadata Fetcher library
- Support for .NET Framework 4.5 and higher
- **Metadata Fetching**:
  - Fetch metadata from single WSFED issuer endpoints
  - Fetch metadata from single SAML issuer endpoints
  - Batch fetch from multiple issuer endpoints simultaneously
  - Synchronous and asynchronous APIs for all operations

- **Metadata Processing**:
  - Integration with System.IdentityModel.Metadata.MetadataSerializer
  - Automatic XML parsing and deserialization
  - Support for both WSFED and SAML metadata formats
  - Returns parsed MetadataBase objects from System.IdentityModel

- **Configuration Options**:
  - Configurable HTTP request timeouts (global and per-endpoint)
  - Optional retry mechanism with exponential backoff
  - Control error handling behavior in batch operations (continue on error)
  - SSL/TLS certificate validation control
  - Reserved configuration for future caching support

- **Resilience**:
  - Automatic retry logic for failed HTTP requests
  - Continue-on-error mode for batch operations
  - Comprehensive error information in results
  - Custom MetadataFetchException with context details

- **Models**:
  - `IssuerEndpoint` - Encapsulates endpoint configuration
  - `MetadataFetchResult` - Contains fetch results with success/failure info
  - `MetadataFetchOptions` - Configures fetcher behavior
  - `MetadataType` enum - Distinguishes WSFED vs SAML

- **Services**:
  - `IMetadataFetcher` interface - Service contract
  - `MetadataFetcher` implementation - Full implementation

- **Exception Handling**:
  - `MetadataFetchException` - Custom exception for metadata operations
  - Includes endpoint information and optional HTTP status codes

- **Testing**:
  - Comprehensive unit test suite with NUnit 3.x
  - Tests for input validation and error handling
  - Tests for single and multiple endpoint fetching
  - Tests for configuration options
  - Tests for model serialization

- **Documentation**:
  - README.md - Complete feature documentation and usage guide
  - QUICKREF.md - Quick reference for common patterns
  - DESIGN.md - Architecture, design principles, and implementation details
  - DEVELOPER.md - Developer guide for building and testing
  - Examples.cs - Runnable code examples
  - Comprehensive XML documentation on all public types

### Technical Details

- **Framework Target**: .NET Framework 4.5+
- **Dependencies**: Framework assemblies only (System.IdentityModel.Metadata, System.Net.Http, System.Xml)
- **No external NuGet dependencies**
- **Thread-safe** - stateless service design
- **Language**: C# 5.0+ compatible
- **Solution Structure**:
  - IdentityMetadataFetcher.csproj - Main library
  - IdentityMetadataFetcher.Tests.csproj - Unit tests
  - IdentityMetadataFetcher.sln - Solution file

### API Surface

**Public Types** (6):
- IMetadataFetcher (interface)
- MetadataFetcher (class)
- IssuerEndpoint (class)
- MetadataFetchResult (class)
- MetadataFetchOptions (class)
- MetadataFetchException (exception)

**Public Enums** (1):
- MetadataType (WSFED, SAML)

**Public Methods** (4 on IMetadataFetcher):
- FetchMetadata(IssuerEndpoint)
- FetchMetadataAsync(IssuerEndpoint)
- FetchMetadataFromMultipleEndpoints(IEnumerable<IssuerEndpoint>)
- FetchMetadataFromMultipleEndpointsAsync(IEnumerable<IssuerEndpoint>)

### Testing Coverage

- Input validation (null, empty, invalid)
- Single endpoint operations (sync & async)
- Multiple endpoint operations (sync & async)
- Error handling and exceptions
- Configuration options
- Model construction and properties

**Test Count**: 12 test methods

### Future Enhancements (Not in 1.0)

- Metadata caching with TTL support
- HttpClientFactory integration
- Performance metrics and tracing
- WIF metadata validation
- LINQ-to-Metadata support
- Parallel batch fetching optimization
- Event notifications
- Polly policy integration for advanced retries

---

## [Unreleased]

### Planned for Future Releases

- Metadata caching infrastructure
- Performance improvements
- Extended validation
- Logging integration options
- Rate limiting support

---

## Version Information

- **Current Version**: 1.0.0
- **Release Date**: December 11, 2025
- **Target Framework**: .NET Framework 4.5+
- **Status**: Production Ready

---

## How to Upgrade

### From 0.x to 1.0.0
No prior versions exist. This is the initial release.

### Future Upgrade Guidelines
- Follow semantic versioning
- Minor version upgrades will be backwards compatible
- Major version upgrades may include breaking changes
- Patch upgrades are safe to apply

---

## Breaking Changes History

**None** - First release, no prior versions to break compatibility with.

---

## Support and Reporting

For issues, questions, or enhancement suggestions:
1. Check documentation in README.md
2. Review examples in Examples.cs
3. Consult quick reference in QUICKREF.md
4. Review design documentation in DESIGN.md

## Contributors

- Initial development: 2025

## License

See LICENSE file for details.
