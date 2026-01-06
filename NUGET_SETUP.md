# NuGet Package Publishing Setup

This document explains how to configure NuGet package publishing for the IdentityMetadataFetcher library.

## Overview

The release workflow automatically builds and publishes NuGet packages to NuGet.org when tags are pushed to the repository. The workflow extracts the version from the git tag (e.g., `v1.2.3` → `1.2.3`) and uses it for the package version.

## Initial Setup

### 1. Create a NuGet API Key

1. Go to https://www.nuget.org/account/apikeys
2. Sign in with your NuGet.org account (create one if you don't have it)
3. Click "Create" to generate a new API key
4. Configure the API key:
   - **Key Name**: `IdentityMetadataFetcher-GitHub-Actions` (or any descriptive name)
   - **Expiration**: Choose an expiration period (recommended: 365 days)
   - **Scopes**: Select "Push new packages and package versions"
   - **Select Packages**: 
     - Choose "Push only new package versions for matching patterns"
     - Add glob pattern: `IdentityMetadataFetcher*`
5. Click "Create" and copy the generated API key immediately (it will only be shown once)

### 2. Add API Key as GitHub Secret

1. Navigate to the repository settings: https://github.com/scoizzle/IdentityMetadataFetcher/settings/secrets/actions
2. Click "New repository secret"
3. Configure the secret:
   - **Name**: `NUGET_API_KEY` (must match exactly as used in the workflow)
   - **Value**: Paste the API key you copied from NuGet.org
4. Click "Add secret"

## Publishing a Release

Once the setup is complete, publishing is automatic:

1. Create and push a tag following semantic versioning:
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```

2. The release workflow will:
   - Extract the version from the tag (`1.0.0`)
   - Build the solution with the version
   - Run tests
   - Create NuGet packages (`.nupkg` and `.snupkg` symbol packages)
   - Publish packages to NuGet.org
   - Create a GitHub release with binary artifacts

3. The packages will be available on NuGet.org:
   - https://www.nuget.org/packages/IdentityMetadataFetcher
   - https://www.nuget.org/packages/IdentityMetadataFetcher.Iis

## Troubleshooting

### Workflow fails on publish step

**Issue**: The workflow runs successfully until the NuGet push step, where it fails with an authentication error.

**Solution**: Verify that:
- The `NUGET_API_KEY` secret is correctly set in GitHub
- The API key hasn't expired on NuGet.org
- The API key has the correct permissions ("Push new packages and package versions")

### Duplicate package version

**Issue**: Attempting to publish a version that already exists on NuGet.org.

**Solution**: The workflow uses `--skip-duplicate` flag, so this should be handled gracefully. If you need to republish:
1. Delete the tag locally and remotely
2. Increment the version number
3. Create a new tag with the new version

### First-time publishing

When publishing a package for the first time, you may need to:
1. Verify your email address on NuGet.org
2. Accept any terms of service
3. Wait a few minutes for the package to appear in search results

## Package Metadata

Both packages include:
- **License**: MIT (specified via SPDX identifier)
- **Project URL**: Repository homepage
- **Repository URL**: GitHub repository
- **README**: Automatically included from repository root
- **Symbol Packages**: `.snupkg` files for debugging support
- **Multi-targeting**: net462, net472, and net481 frameworks

## Security Best Practices

1. **API Key Rotation**: Rotate your NuGet API key periodically (every 6-12 months)
2. **Minimal Permissions**: Use scoped API keys that only have push permissions
3. **Secret Management**: Never commit API keys to the repository
4. **Access Control**: Limit who has access to GitHub secrets in the repository settings

## Manual Publishing (Alternative)

If you need to publish packages manually:

```bash
# Build the solution
msbuild IdentityMetadataFetcher.sln /p:Configuration=Release /p:Version=1.0.0

# Create packages
dotnet pack src/IdentityMetadataFetcher/IdentityMetadataFetcher.csproj --configuration Release --no-build /p:Version=1.0.0 --output ./nupkgs
dotnet pack src/IdentityMetadataFetcher.Iis/IdentityMetadataFetcher.Iis.csproj --configuration Release --no-build /p:Version=1.0.0 --output ./nupkgs

# Push to NuGet.org
dotnet nuget push ./nupkgs/*.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json
```

## Additional Resources

- [NuGet Package Publishing Documentation](https://docs.microsoft.com/en-us/nuget/quickstart/create-and-publish-a-package-using-the-dotnet-cli)
- [GitHub Actions Secrets](https://docs.github.com/en/actions/security-guides/encrypted-secrets)
- [Semantic Versioning](https://semver.org/)
