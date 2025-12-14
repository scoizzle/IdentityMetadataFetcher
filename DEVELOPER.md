# SAML Metadata Fetcher - Developer Guide

## Building the Project

### Prerequisites

- Visual Studio 2015 or later (or MSBuild 14.0+)
- .NET Framework 4.5 or later
- NUnit 3.x for running tests (optional)

### Build Commands

#### Using Visual Studio

1. Open `IdentityMetadataFetcher.sln` in Visual Studio
2. Select Build > Build Solution (Ctrl+Shift+B)
3. Output binaries will be in `bin/Debug` or `bin/Release`

#### Using MSBuild (Command Line)

```bash
# Debug build
msbuild IdentityMetadataFetcher.sln /p:Configuration=Debug /p:Platform="Any CPU"

# Release build
msbuild IdentityMetadataFetcher.sln /p:Configuration=Release /p:Platform="Any CPU"

# Clean build
msbuild IdentityMetadataFetcher.sln /t:Clean
```

## Project Structure

```
IdentityMetadataFetcher/
│
├── IdentityMetadataFetcher.csproj           # Main library project
├── IdentityMetadataFetcher.sln              # Solution file
│
├── Properties/
│   └── AssemblyInfo.cs                  # Assembly metadata
│
├── Models/                               # Data models
│   ├── IssuerEndpoint.cs                # Endpoint configuration
│   ├── MetadataFetchResult.cs           # Fetch result container
│   └── MetadataFetchOptions.cs          # Configuration options
│
├── Services/                             # Service classes
│   ├── IMetadataFetcher.cs              # Service interface
│   └── MetadataFetcher.cs               # Service implementation
│
├── Exceptions/                           # Custom exceptions
│   └── MetadataFetchException.cs        # Metadata fetch exception
│
├── IdentityMetadataFetcher.Tests/           # Unit test project
│   ├── IdentityMetadataFetcher.Tests.csproj
│   ├── Properties/
│   │   └── AssemblyInfo.cs
│   ├── MetadataFetcherTests.cs          # Service tests
│   └── IssuerEndpointTests.cs           # Model tests
│
├── Examples.cs                          # Usage examples
│
├── README.md                            # Main documentation
├── QUICKREF.md                          # Quick reference guide
├── DESIGN.md                            # Architecture & design
└── (this file)
```

## Running Tests

### Prerequisites for Tests

- NUnit 3.12.0 (specified in test project)
- Optional: NUnit Test Adapter for Visual Studio

### Run Tests in Visual Studio

1. Open Test Explorer (Test > Windows > Test Explorer)
2. Click "Run All Tests" or select specific tests
3. Results appear in Test Explorer window

### Run Tests from Command Line

#### Using NUnit3 Console Runner

```bash
# Install NUnit console runner (if not already installed)
nuget install NUnit.ConsoleRunner

# Run all tests
./NUnit.ConsoleRunner.3.x.x/tools/nunit3-console.exe IdentityMetadataFetcher.Tests/bin/Debug/IdentityMetadataFetcher.Tests.dll

# Run specific test fixture
./NUnit.ConsoleRunner.3.x.x/tools/nunit3-console.exe IdentityMetadataFetcher.Tests/bin/Debug/IdentityMetadataFetcher.Tests.dll --fixture=IdentityMetadataFetcher.Tests.MetadataFetcherTests

# Run with XML output
./NUnit.ConsoleRunner.3.x.x/tools/nunit3-console.exe IdentityMetadataFetcher.Tests/bin/Debug/IdentityMetadataFetcher.Tests.dll --result=TestResults.xml
```

#### Using PowerShell (Windows)

```powershell
# Run tests
dotnet test IdentityMetadataFetcher.Tests.csproj
```

### Test Coverage

**Unit Tests Included:**

1. **MetadataFetcherTests.cs**
   - Constructor validation
   - Single endpoint fetch (sync and async)
   - Multiple endpoint fetch (sync and async)
   - Error handling
   - Configuration options

2. **IssuerEndpointTests.cs**
   - Model construction
   - Property get/set
   - Enum values
   - Null tolerance

### Test Patterns

The test suite uses NUnit 3.x with the following patterns:

```csharp
[TestFixture]
public class MyTests
{
    [SetUp]
    public void Setup() { }  // Before each test
    
    [TearDown]
    public void Teardown() { }  // After each test
    
    [Test]
    public void TestName() { }  // Synchronous test
    
    [Test]
    public async Task TestNameAsync() { }  // Asynchronous test
    
    [TestCase(arg1)]
    public void TestName(string arg) { }  // Parameterized test
}
```

## Development Workflow

### Adding New Features

1. **Create Feature Branch**
   ```bash
   git checkout -b feature/new-feature
   ```

2. **Implement in Main Project**
   - Add classes to appropriate namespace
   - Follow existing code style
   - Add XML documentation comments
   - Reference Microsoft.IdentityModel NuGet packages as needed

3. **Write Unit Tests**
   - Add test cases to IdentityMetadataFetcher.Tests project
   - Test success and failure scenarios
   - Verify edge cases

4. **Update Documentation**
   - Update README.md with new usage examples
   - Update DESIGN.md if architecture changes
   - Update QUICKREF.md if adding public API

5. **Build & Test**
   ```bash
   msbuild IdentityMetadataFetcher.sln
   nunit3-console IdentityMetadataFetcher.Tests/bin/Debug/IdentityMetadataFetcher.Tests.dll
   ```

6. **Create Pull Request**
   - Document changes
   - Reference related issues
   - Ensure all tests pass

### Code Style Guidelines

- **Naming**: Use PascalCase for public members, camelCase for private
- **Comments**: Use XML documentation (`///`) for all public types and members
- **Async**: Always use async/await, don't use Task.Result (except in examples)
- **Exceptions**: Throw for invalid inputs, return failure results for runtime errors
- **Null Checks**: Use explicit null checks, validate parameters early

Example:

```csharp
/// <summary>
/// Fetches metadata from the specified endpoint asynchronously.
/// </summary>
/// <param name="endpoint">The endpoint to fetch metadata from.</param>
/// <returns>A task that returns the fetch result.</returns>
/// <exception cref="ArgumentNullException">Thrown when endpoint is null.</exception>
public async Task<MetadataFetchResult> FetchMetadataAsync(IssuerEndpoint endpoint)
{
    if (endpoint == null)
        throw new ArgumentNullException(nameof(endpoint));
    
    // Implementation...
}
```

## Debugging

### Visual Studio Debugging

1. Set breakpoints by clicking in the gutter
2. Press F5 to start debugging
3. Use Debug > Step Over (F10) or Step Into (F11)
4. Watch variables in the Debug panel

### Common Debugging Scenarios

**Metadata Parsing Issues:**
```csharp
// Add to MetadataFetcher.cs for debugging
var metadata = serializer.ReadMetadata(reader);
System.Diagnostics.Debug.WriteLine($"Parsed metadata: {metadata.GetType().Name}");
```

**HTTP Request Issues:**
```csharp
// Enable HttpClient logging
var handler = new HttpClientHandler();
handler.MaxConnectionsPerServer = 5;
using (var client = new HttpClient(handler))
{
    // Configure client
}
```

## Release Process

### Version Numbering

Uses Semantic Versioning (MAJOR.MINOR.PATCH):
- MAJOR: Breaking API changes
- MINOR: New features, backwards compatible
- PATCH: Bug fixes only

### Release Checklist

- [ ] All tests passing
- [ ] Code review completed
- [ ] Documentation updated
- [ ] Version number updated in AssemblyInfo.cs
- [ ] README.md updated with changes
- [ ] CHANGELOG entry added
- [ ] Tag created in version control
- [ ] NuGet package prepared (if publishing)

### Updating Version Numbers

Edit `Properties/AssemblyInfo.cs`:

```csharp
[assembly: AssemblyVersion("1.0.0.0")]      // Major.Minor.Build.Revision
[assembly: AssemblyFileVersion("1.0.0.0")]  // Major.Minor.Build.Revision
```

## Dependencies Management

### Current Framework Dependencies

The project has no external NuGet dependencies. It uses only the .NET Framework Class Library:

- System
- System.Core
- System.IdentityModel (WIF)
- Microsoft.IdentityModel.Protocols.WsFederation
- Microsoft.IdentityModel.Tokens.Saml
- System.IdentityModel.Services
- System.Net.Http
- System.Xml

### Adding New Dependencies

If adding a NuGet package:

1. Use NuGet Package Manager Console
   ```
   Install-Package PackageName
   ```

2. Update both .csproj files if needed

3. Update README.md dependencies section

4. Document justification in DESIGN.md

## Performance Profiling

### Using Visual Studio Profiler

1. Build in Release configuration
2. Debug > Performance Profiler (or Alt+F2)
3. Select profiling tool (CPU Usage, Memory)
4. Run your code
5. Analyze results

### Memory Profiling

```csharp
// Monitor memory usage
var beforeSize = GC.GetTotalMemory(true);
var result = fetcher.FetchMetadata(endpoint);
var afterSize = GC.GetTotalMemory(true);
Console.WriteLine($"Memory used: {afterSize - beforeSize} bytes");
```

## Troubleshooting Build Issues

| Issue | Solution |
|-------|----------|
| Missing references | Ensure .NET Framework 4.5 is installed |
| Project won't load | Check .csproj file syntax |
| Test discovery fails | Rebuild solution, restart Visual Studio |
| HttpClient not found | Ensure System.Net.Http reference exists |
| WsFederationMetadataSerializer missing | Install Microsoft.IdentityModel.Protocols.WsFederation NuGet package |

## Continuous Integration

### GitHub Actions Example

```yaml
name: Build and Test

on: [push, pull_request]

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v2
      - uses: actions/setup-dotnet@v1
        with:
          dotnet-version: '4.5'
      - run: msbuild IdentityMetadataFetcher.sln
      - run: nunit3-console IdentityMetadataFetcher.Tests/bin/Release/IdentityMetadataFetcher.Tests.dll
```

## Resources

### Microsoft Documentation
- [Microsoft.IdentityModel Documentation](https://docs.microsoft.com/en-us/dotnet/api/microsoft.identitymodel)
- [WsFederationConfiguration Class](https://docs.microsoft.com/en-us/dotnet/api/microsoft.identitymodel.protocols.wsfederation.wsfederationconfiguration)
- [Windows Identity Foundation](https://docs.microsoft.com/en-us/dotnet/framework/security/windows-identity-foundation)
- [SAML 2.0 Metadata](https://docs.oasis-open.org/security/saml/v2.0/saml-metadata-2.0-os.pdf)
- [WS-Federation Metadata](https://docs.microsoft.com/en-us/dotnet/framework/security/wsfederation-metadata)

### Related Projects
- [IdentityModel](https://github.com/IdentityModel/IdentityModel)
- [Duende IdentityServer](https://duendesoftware.com/products/identityserver)

---

**For API documentation, see README.md**  
**For architecture details, see DESIGN.md**  
**For quick reference, see QUICKREF.md**
