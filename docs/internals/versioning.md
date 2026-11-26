# Versioning and Release Guide

## Version Control Strategy

The Xping SDK uses **Semantic Versioning (SemVer)** with the format: `MAJOR.MINOR.PATCH[-PRERELEASE]`

- **MAJOR**: Breaking changes
- **MINOR**: New features (backward compatible)
- **PATCH**: Bug fixes (backward compatible)
- **PRERELEASE**: Optional suffix like `alpha`, `beta`, `rc1`

## How Versioning Works

### Default Version
The default version is defined in `Directory.Build.props`:
```xml
<Version Condition="'$(Version)' == ''">1.0.0</Version>
```

### Override Version Locally
You can override the version when building or packing:

```bash
# Build with specific version
dotnet build -p:Version=1.2.3

# Pack with specific version
dotnet pack -c Release -p:Version=1.2.3 -o ./artifacts

# Pack with prerelease version
dotnet pack -c Release -p:Version=1.2.3-beta -o ./artifacts
```

## GitHub Actions Release Process

### Automated Release (Recommended)

1. **Create a Git Tag** with version number:
   ```bash
   git tag v1.2.3
   git push origin v1.2.3
   ```

2. **Create a GitHub Release** from the tag:
   - Go to: https://github.com/xping-dev/sdk-dotnet/releases/new
   - Select the tag you just pushed
   - Add release notes
   - Click "Publish release"

3. **Automated Actions**:
   - GitHub Actions workflow triggers automatically
   - Extracts version from tag (removes `v` prefix)
   - Builds all SDK packages with the version
   - Publishes to NuGet.org
   - Uploads packages to GitHub release

### Version Extraction
The workflow extracts the version from the tag name:
```yaml
- name: Extract version from tag
  id: get_version
  run: echo "VERSION=${GITHUB_REF_NAME#v}" >> $GITHUB_OUTPUT

- name: Pack all SDK packages
  run: dotnet pack -c Release -p:Version=${{ steps.get_version.outputs.VERSION }}
```

**Examples:**
- Tag `v1.2.3` → Version `1.2.3`
- Tag `v2.0.0-beta` → Version `2.0.0-beta`
- Tag `v1.5.0-rc1` → Version `1.5.0-rc1`

## Release Checklist

### Pre-Release
- [ ] All tests passing
- [ ] Code coverage meets threshold (>80%)
- [ ] CHANGELOG.md updated
- [ ] Version number decided (following SemVer)
- [ ] Release notes drafted

### Release Steps
1. Update `Directory.Build.props` with new default version
2. Commit and push all changes
3. Create and push git tag: `git tag v1.2.3 && git push origin v1.2.3`
4. Create GitHub Release from tag with release notes
5. Verify GitHub Actions workflow completes successfully
6. Verify packages published to NuGet.org
7. Test installation: `dotnet add package Xping.Sdk.Core --version 1.2.3`

### Post-Release
- [ ] Verify packages on NuGet.org
- [ ] Update documentation with new version
- [ ] Announce release (blog, social media, etc.)
- [ ] Close milestone (if applicable)

## Testing Pre-Release Versions

To publish preview/beta versions:

```bash
# Tag with prerelease suffix
git tag v2.0.0-beta.1
git push origin v2.0.0-beta.1

# Or manually pack and test locally
dotnet pack -c Release -p:Version=2.0.0-beta.1 -o ./artifacts
dotnet nuget push ./artifacts/*.nupkg --api-key $API_KEY --source https://api.nuget.org/v3/index.json
```

Users can install pre-release versions:
```bash
dotnet add package Xping.Sdk.Core --version 2.0.0-beta.1
```

## Troubleshooting

### Version Not Applied
- Ensure you're using `-p:Version=X.Y.Z` (with uppercase V)
- Check that `Directory.Build.props` has the conditional version

### Wrong Version in Package
- Verify the tag format: `v1.2.3` (lowercase v)
- Check GitHub Actions logs for version extraction

### Package Already Exists on NuGet
- NuGet doesn't allow re-uploading the same version
- Increment version and create new release
- Use `--skip-duplicate` flag (already in workflow)

## Version History

| Version | Release Date     | Notes           |
|---------|------------------|-----------------|
| 1.0.0   | January 4th 2026 | Initial release |

