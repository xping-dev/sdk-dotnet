# Local NuGet Package Builder

This Python script automates the creation of NuGet packages locally by executing the same `dotnet pack` commands used in the GitHub Actions release workflow.

## Prerequisites

- Python 3.6 or higher
- .NET SDK (compatible with the projects)
- The script should be placed in the repository root or scripts folder

## Usage

You can run the script from either the scripts folder or the repository root:

### From the scripts folder:
```bash
cd scripts
python build_nuget.py <version>
```

### From the repository root:
```bash
python scripts/build_nuget.py <version>
```

### Examples

```bash
# Build packages with a release version
python build_nuget.py 1.0.0

# Build packages with a release candidate version
python build_nuget.py 1.0.0-rc.9

# Build packages with a beta version
python build_nuget.py 2.1.3-beta.1

# Build packages with a custom test version
python build_nuget.py 1.0.0-test
```

## What it does

The script will:

1. **Validate** the provided version string
2. **Check** that you're running it from the correct directory (repository root)
3. **Create** the output directory (`_build/nuget`) if it doesn't exist
4. **Build** the `Xping.Sdk.Core` package using:
   ```bash
   dotnet pack ./src/Xping.Sdk.Core/Xping.Sdk.Core.csproj -c Release -p:NuspecProperties="version=<your-version>" -o _build/nuget
   ```
5. **Build** the `Xping.Sdk` package using:
   ```bash
   dotnet pack ./src/Xping.Sdk/Xping.Sdk.csproj -c Release -p:NuspecProperties="version=<your-version>" -o _build/nuget
   ```
6. **List** all the created packages and provide a summary

## Output

The generated NuGet packages will be saved to:
- `_build/nuget/Xping.Sdk.Core.<version>.nupkg`
- `_build/nuget/Xping.Sdk.<version>.nupkg`

## Error Handling

The script includes comprehensive error handling and will:
- Stop execution if any command fails
- Provide clear error messages
- Return appropriate exit codes (0 for success, 1 for failure)

## Notes

- The script uses the same exact commands as defined in `.github/workflows/release.yml`
- All packages are built in Release configuration
- The version is passed through the `NuspecProperties` parameter to match the release workflow behavior
- The script will list all existing packages in the output directory after building
