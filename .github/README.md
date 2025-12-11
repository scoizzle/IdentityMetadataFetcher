# GitHub Actions CI/CD

This project uses GitHub Actions for continuous integration and deployment.

## Workflows

### Build and Test (`build.yml`)

Triggers on:
- Push to `main` or `develop` branches
- Pull requests to `main` or `develop`
- Manual workflow dispatch

**Runner**: `windows-latest` (required for .NET Framework)

**Build Matrix**:
- Configuration: Debug, Release

**Steps**:
1. Checkout code
2. Setup MSBuild and NuGet
3. Restore NuGet packages
4. Build solution using MSBuild in Debug/Release
5. Run all unit tests (net48 framework)
6. Upload test results as artifacts
7. Upload build artifacts (Release only)

### Release (`release.yml`)

Triggers on:
- Tags matching `v*.*.*` (e.g., v1.0.0)
- Manual workflow dispatch

**Runner**: `windows-latest`

**Steps**:
1. Checkout code
2. Setup MSBuild and NuGet
3. Build Release configuration using MSBuild
4. Run all tests (net48 framework)
5. Package binaries for all target frameworks (net45, net46, net47, net48)
6. Include documentation (README, QUICKREF, IIS_MODULE_USAGE)
7. Create ZIP archive
8. Create GitHub Release with artifacts

## Running Locally

These workflows require Windows. To test locally:

```bash
# Restore packages
nuget restore IdentityMetadataFetcher.sln

# Build using MSBuild
msbuild IdentityMetadataFetcher.sln /p:Configuration=Release /p:Platform="Any CPU"

# Or build using dotnet CLI (requires .NET SDK with .NET Framework support)
dotnet build IdentityMetadataFetcher.sln --configuration Release

# Test (requires .NET SDK)
dotnet test IdentityMetadataFetcher.Tests.csproj --configuration Release --framework net48
dotnet test IdentityMetadataFetcher.Iis.Tests/IdentityMetadataFetcher.Iis.Tests.csproj --configuration Release --framework net48
```

## Creating a Release

To trigger a release:

```bash
git tag v1.0.0
git push origin v1.0.0
```

The release workflow will automatically:
- Build all target frameworks
- Run tests
- Package binaries
- Create a GitHub release with downloadable ZIP

## Build Status

Add this badge to your README.md:

```markdown
![Build Status](https://github.com/YOUR_USERNAME/IdentityMetadataFetcher/workflows/Build%20and%20Test/badge.svg)
```
