#!/usr/bin/env python3
"""
Local NuGet Package Builder

This script automates the creation of NuGet packages locally by executing
the same dotnet pack commands used in the GitHub Actions release workflow.

Usage:
    python build_nuget.py <version>

Example:
    python build_nuget.py 1.0.0-rc.9
"""

import argparse
import os
import subprocess
import sys
from pathlib import Path


def run_command(command: str, cwd: str = None) -> bool:
    """
    Execute a shell command and return True if successful, False otherwise.

    Args:
        command: The command to execute
        cwd: The working directory to run the command in

    Returns:
        bool: True if command succeeded, False otherwise
    """
    print(f"Executing: {command}")
    if cwd:
        print(f"Working directory: {cwd}")

    try:
        result = subprocess.run(
            command,
            shell=True,
            cwd=cwd,
            check=True,
            capture_output=True,
            text=True
        )
        print(f"✓ Command succeeded")
        if result.stdout:
            print(f"Output: {result.stdout}")
        return True
    except subprocess.CalledProcessError as e:
        print(f"✗ Command failed with exit code {e.returncode}")
        if e.stdout:
            print(f"Stdout: {e.stdout}")
        if e.stderr:
            print(f"Stderr: {e.stderr}")
        return False


def ensure_output_directory(output_dir: Path) -> bool:
    """
    Ensure the output directory exists.

    Args:
        output_dir: Path to the output directory

    Returns:
        bool: True if directory exists or was created successfully
    """
    try:
        output_dir.mkdir(parents=True, exist_ok=True)
        print(f"✓ Output directory ready: {output_dir}")
        return True
    except Exception as e:
        print(f"✗ Failed to create output directory {output_dir}: {e}")
        return False


def build_nuget_packages(version: str) -> bool:
    """
    Build NuGet packages for both Xping.Sdk.Core and Xping.Sdk projects.

    Args:
        version: The version string to use for the packages

    Returns:
        bool: True if all packages were built successfully
    """
    # Get the script directory and find the repository root
    script_dir = Path(__file__).parent.resolve()

    # If we're in a scripts folder, go up one level to find the repo root
    if script_dir.name == "scripts":
        repo_root = script_dir.parent
    else:
        repo_root = script_dir

    # Define paths relative to repo root
    core_project = repo_root / "src" / "Xping.Sdk.Core" / "Xping.Sdk.Core.csproj"
    sdk_project = repo_root / "src" / "Xping.Sdk" / "Xping.Sdk.csproj"
    output_dir = repo_root / ".artifcats" / "nuget"
    
    # Verify project files exist
    if not core_project.exists():
        print(f"✗ Core project file not found: {core_project}")
        return False

    if not sdk_project.exists():
        print(f"✗ SDK project file not found: {sdk_project}")
        return False

    print(f"Building NuGet packages with version: {version}")
    print(f"Output directory: {output_dir}")

    # Ensure output directory exists
    if not ensure_output_directory(output_dir):
        return False

    # Build Core package
    print("\n" + "="*50)
    print("Building Xping.Sdk.Core package...")
    print("="*50)

    core_command = (
        f"dotnet pack {core_project} "
        f"-c Release "
        f"-p:NuspecProperties=\"version={version}\" "
        f"-o {output_dir}"
    )

    if not run_command(core_command, cwd=str(repo_root)):
        print("✗ Failed to build Xping.Sdk.Core package")
        return False

    # Build SDK package
    print("\n" + "="*50)
    print("Building Xping.Sdk package...")
    print("="*50)

    sdk_command = (
        f"dotnet pack {sdk_project} "
        f"-c Release "
        f"-p:NuspecProperties=\"version={version}\" "
        f"-o {output_dir}"
    )

    if not run_command(sdk_command, cwd=str(repo_root)):
        print("✗ Failed to build Xping.Sdk package")
        return False

    # List the created packages
    print("\n" + "="*50)
    print("Build completed successfully!")
    print("="*50)

    expected_core_package = output_dir / f"Xping.Sdk.Core.{version}.nupkg"
    expected_sdk_package = output_dir / f"Xping.Sdk.{version}.nupkg"

    if expected_core_package.exists():
        print(f"✓ Created: {expected_core_package}")
    else:
        print(f"⚠ Expected package not found: {expected_core_package}")

    if expected_sdk_package.exists():
        print(f"✓ Created: {expected_sdk_package}")
    else:
        print(f"⚠ Expected package not found: {expected_sdk_package}")

    # List all .nupkg files in the output directory
    nupkg_files = list(output_dir.glob("*.nupkg"))
    if nupkg_files:
        print(f"\nAll packages in {output_dir}:")
        for pkg in sorted(nupkg_files):
            print(f"  - {pkg.name}")

    return True


def validate_version(version: str) -> bool:
    """
    Basic validation of the version string.

    Args:
        version: The version string to validate

    Returns:
        bool: True if version appears valid
    """
    if not version:
        print("✗ Version cannot be empty")
        return False

    # Basic check - version should not contain spaces or invalid characters
    if " " in version:
        print("✗ Version should not contain spaces")
        return False

    print(f"✓ Version format appears valid: {version}")
    return True


def main():
    """Main entry point of the script."""
    parser = argparse.ArgumentParser(
        description="Build NuGet packages locally using the same commands from release.yml",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python build_nuget.py 1.0.0
  python build_nuget.py 1.0.0-rc.9
  python build_nuget.py 2.1.3-beta.1
        """
    )

    parser.add_argument(
        "version",
        help="Version string for the NuGet packages (e.g., 1.0.0-rc.9)"
    )

    args = parser.parse_args()

    # Validate version
    if not validate_version(args.version):
        sys.exit(1)

    # Find the repository root directory
    script_dir = Path(__file__).parent.resolve()

    # If we're in a scripts folder, go up one level to find the repo root
    if script_dir.name == "scripts":
        repo_root = script_dir.parent
    else:
        repo_root = script_dir

    # Check if we can find the solution file in the repo root
    sln_file = repo_root / "xping-sdk.sln"

    if not sln_file.exists():
        print(f"✗ Solution file not found: {sln_file}")
        print("Cannot locate the repository root directory.")
        print("Make sure the script is placed in the repository or its scripts folder.")
        sys.exit(1)

    print("Local NuGet Package Builder")
    print("="*50)
    print(f"Script location: {script_dir}")
    print(f"Repository root: {repo_root}")
    print(f"Target version: {args.version}")

    # Build packages
    if build_nuget_packages(args.version):
        print(f"\n🎉 Successfully built NuGet packages for version {args.version}")
        sys.exit(0)
    else:
        print(f"\n💥 Failed to build NuGet packages")
        sys.exit(1)


if __name__ == "__main__":
    main()
