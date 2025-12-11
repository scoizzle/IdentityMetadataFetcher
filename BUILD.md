# SAML Metadata Fetcher - Build Instructions

Quick reference for building and testing the SAML Metadata Fetcher library.

## Prerequisites

- **.NET Framework 4.5 or higher** installed
- **Visual Studio 2015+** or **MSBuild 14.0+**
- **NUnit 3.x** (for running tests, optional)

## Quick Build

### Option 1: Visual Studio (Easiest)

```powershell
# 1. Open the solution
start IdentityMetadataFetcher.sln

# 2. In Visual Studio:
#    - Select Build > Build Solution (Ctrl+Shift+B)
#    - Output appears in bin/Debug or bin/Release
```

### Option 2: MSBuild Command Line

```bash
# Debug build
msbuild IdentityMetadataFetcher.sln /p:Configuration=Debug

# Release build
msbuild IdentityMetadataFetcher.sln /p:Configuration=Release

# Clean build
msbuild IdentityMetadataFetcher.sln /t:Clean /p:Configuration=Debug
msbuild IdentityMetadataFetcher.sln /p:Configuration=Debug
```

## Running Tests

### In Visual Studio

1. Open Test Explorer: **Test > Windows > Test Explorer**
2. Tests appear in the list
3. Click **Run All** or select individual tests
4. View results in the Test Explorer window

### Command Line with NUnit

```bash
# Install NUnit console runner (first time)
nuget install NUnit.ConsoleRunner

# Run all tests
./NUnit.ConsoleRunner.3.x.x/tools/nunit3-console.exe \
  IdentityMetadataFetcher.Tests/bin/Debug/IdentityMetadataFetcher.Tests.dll

# Run specific test class
./NUnit.ConsoleRunner.3.x.x/tools/nunit3-console.exe \
  IdentityMetadataFetcher.Tests/bin/Debug/IdentityMetadataFetcher.Tests.dll \
  --fixture=IdentityMetadataFetcher.Tests.MetadataFetcherTests

# Generate XML report
./NUnit.ConsoleRunner.3.x.x/tools/nunit3-console.exe \
  IdentityMetadataFetcher.Tests/bin/Debug/IdentityMetadataFetcher.Tests.dll \
  --result=TestResults.xml
```

## Using the Built Library

After building, the library DLL is at:

```
IdentityMetadataFetcher/bin/Debug/IdentityMetadataFetcher.dll      (Debug)
IdentityMetadataFetcher/bin/Release/IdentityMetadataFetcher.dll    (Release)
```

**To use in your project:**

1. Add reference to `IdentityMetadataFetcher.dll`
2. Add using statement: `using IdentityMetadataFetcher.Services;`
3. Create instance: `var fetcher = new MetadataFetcher();`

## Troubleshooting Builds

| Issue | Solution |
|-------|----------|
| "Project file could not be loaded" | Ensure .csproj files are well-formed XML |
| "Missing Microsoft.CSharp" | Install .NET Framework 4.5 Developer Pack |
| "Cannot find type System.IdentityModel.Metadata" | Add reference to System.IdentityModel.Metadata |
| Tests don't appear | Right-click project > Properties > Framework 4.5+; Rebuild |
| "Could not resolve" errors | Close and reopen solution; Clean then Rebuild |

## Build Output Structure

```
IdentityMetadataFetcher/
├── bin/
│   ├── Debug/
│   │   ├── IdentityMetadataFetcher.dll
│   │   ├── IdentityMetadataFetcher.pdb
│   │   ├── IdentityMetadataFetcher.Tests.dll
│   │   └── IdentityMetadataFetcher.Tests.pdb
│   └── Release/
│       ├── IdentityMetadataFetcher.dll
│       ├── IdentityMetadataFetcher.pdb
│       ├── IdentityMetadataFetcher.Tests.dll
│       └── IdentityMetadataFetcher.Tests.pdb
└── obj/
    └── (compiler intermediate files)
```

## Common Build Targets

```bash
# Build specific project
msbuild IdentityMetadataFetcher.csproj /p:Configuration=Debug

# Build only tests
msbuild IdentityMetadataFetcher.Tests.csproj /p:Configuration=Debug

# Clean and rebuild
msbuild IdentityMetadataFetcher.sln /t:Clean
msbuild IdentityMetadataFetcher.sln /p:Configuration=Debug

# Rebuild (clean + build)
msbuild IdentityMetadataFetcher.sln /t:Rebuild /p:Configuration=Debug
```

## Configuration Targets

```bash
# Build for specific configuration
msbuild IdentityMetadataFetcher.sln /p:Configuration=Debug /p:Platform="Any CPU"
msbuild IdentityMetadataFetcher.sln /p:Configuration=Release /p:Platform="Any CPU"
```

## Advanced Build Options

```bash
# Verbose output
msbuild IdentityMetadataFetcher.sln /v:detailed

# Parallel build (faster for large solutions)
msbuild IdentityMetadataFetcher.sln /m

# Generate full diagnostic log
msbuild IdentityMetadataFetcher.sln /v:diagnostic > build.log

# Build without dependencies
msbuild IdentityMetadataFetcher.csproj /p:BuildProjectReferences=false

# Suppress warnings
msbuild IdentityMetadataFetcher.sln /nowarn:CS0067
```

## Continuous Integration (CI)

### GitHub Actions Example

```yaml
name: Build and Test
on: [push, pull_request]
jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v2
      - name: Add msbuild to PATH
        uses: microsoft/setup-msbuild@v1
      - name: Build
        run: msbuild IdentityMetadataFetcher.sln /p:Configuration=Release
      - name: Test
        run: |
          nuget install NUnit.ConsoleRunner
          NUnit.ConsoleRunner.3.x.x\tools\nunit3-console.exe IdentityMetadataFetcher.Tests\bin\Release\IdentityMetadataFetcher.Tests.dll
```

### Azure Pipelines Example

```yaml
trigger:
  - main

pool:
  vmImage: 'windows-latest'

steps:
  - task: UseDotNet@2
    inputs:
      packageType: 'sdk'
      version: '4.5.x'
  
  - task: MSBuild@1
    inputs:
      solution: 'IdentityMetadataFetcher.sln'
      configuration: 'Release'
  
  - task: VSTest@2
    inputs:
      testAssemblyVer2: 'IdentityMetadataFetcher.Tests\bin\Release\IdentityMetadataFetcher.Tests.dll'
      searchFolder: '$(System.DefaultWorkingDirectory)'
```

## Verification After Build

After building, verify the build was successful:

```bash
# Check main library exists
ls -la IdentityMetadataFetcher/bin/Debug/IdentityMetadataFetcher.dll

# Check test assembly exists
ls -la IdentityMetadataFetcher/bin/Debug/IdentityMetadataFetcher.Tests.dll

# Run quick test
msbuild IdentityMetadataFetcher.sln /t:Test
```

## Clean Up

```bash
# Remove all build artifacts
msbuild IdentityMetadataFetcher.sln /t:Clean

# Or manually:
rm -r bin/
rm -r obj/
rm -r IdentityMetadataFetcher.Tests/bin/
rm -r IdentityMetadataFetcher.Tests/obj/

# Then rebuild
msbuild IdentityMetadataFetcher.sln /p:Configuration=Debug
```

## Next Steps

After building:

1. **Run tests** to verify everything works
2. **Reference the DLL** in your project
3. **See Examples.cs** for usage
4. **Read README.md** for complete documentation

## Need Help?

- See DEVELOPER.md for detailed build information
- See README.md for API documentation
- See QUICKREF.md for code examples
- Check Examples.cs for working code

---

**Quick Start**: `msbuild IdentityMetadataFetcher.sln && nunit3-console IdentityMetadataFetcher.Tests/bin/Debug/IdentityMetadataFetcher.Tests.dll`
