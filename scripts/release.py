#!/usr/bin/env python3
"""
Tag a release on main and open a draft GitHub release for it.

Nothing is pushed to nuget.org from here. Publishing the draft (after you edit its title and
notes) triggers .github/workflows/release.yml, which packs the tagged commit and pushes it with
NuGet trusted publishing. That push cannot be undone - nuget.org only unlists - so the script
checks everything it can first: main is clean and in sync, CI passed on HEAD, the version is
new on nuget.org, and the packages build and the CLI tool installs and runs.

Usage:
    python scripts/release.py                  # current version, commits since it, next candidates
    python scripts/release.py rc               # v1.0.0-rc.7 -> v1.0.0-rc.8
    python scripts/release.py patch            # v1.0.0 -> v1.0.1; from v1.0.0-rc.7 -> v1.0.0
    python scripts/release.py minor            # v1.0.0 -> v1.1.0
    python scripts/release.py major            # v1.0.0 -> v2.0.0
    python scripts/release.py v1.1.0-rc.1      # explicit; a version with a suffix is a pre-release

Rerunning with the same version after a failure skips the tag that is already pushed.
Needs git, gh (signed in, with push access to the repository) and the .NET 10 SDK.
"""

import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import urllib.error
import urllib.request
from pathlib import Path
from typing import List, Optional, Tuple

REPO = Path(__file__).resolve().parent.parent
GITHUB_REPO = "xping-dev/sdk-dotnet"
CI_WORKFLOW = "ci.yml"
PACKAGES = ["Xping.Sdk.Core", "Xping.Sdk.NUnit", "Xping.Sdk.XUnit", "Xping.Sdk.XUnit.V3",
            "Xping.Sdk.MSTest", "Xping.Cli"]

# Tagged before the vX.Y.Z-rc.N scheme. SemVer ranks "rc1" above "rc.8", so counting them would
# block every later release candidate; their GitHub releases stay as they are.
LEGACY_TAGS = {"v1.0.0-rc1", "v1.0.0-rc2"}

SEMVER = re.compile(r"^(\d+)\.(\d+)\.(\d+)(?:-([0-9A-Za-z.-]+))?$")


class Colors:
    """ANSI color codes for terminal output."""
    RED = '\033[91m'
    BLUE = '\033[94m'
    RESET = '\033[0m'
    BOLD = '\033[1m'


class ReleaseError(Exception):
    pass


class Version:
    """A SemVer version, ordered by SemVer precedence."""

    def __init__(self, text: str):
        match = SEMVER.match(text[1:] if text.startswith("v") else text)
        if not match:
            raise ValueError(text)
        self.major, self.minor, self.patch = (int(g) for g in match.groups()[:3])
        self.prerelease = match.group(4) or ""

    @property
    def tag(self) -> str:
        return f"v{self}"

    def bump(self, part: str) -> "Version":
        # From a pre-release, a bump first lands on the release it leads to (1.0.0-rc.7 -> 1.0.0).
        pre = bool(self.prerelease)
        if part == "major":
            if pre and self.minor == 0 and self.patch == 0:
                return Version(f"{self.major}.0.0")
            return Version(f"{self.major + 1}.0.0")
        if part == "minor":
            if pre and self.patch == 0:
                return Version(f"{self.major}.{self.minor}.0")
            return Version(f"{self.major}.{self.minor + 1}.0")
        if part == "patch":
            if pre:
                return Version(f"{self.major}.{self.minor}.{self.patch}")
            return Version(f"{self.major}.{self.minor}.{self.patch + 1}")
        # rc: the next release candidate of the same version.
        match = re.match(r"^rc\.(\d+)$", self.prerelease)
        if not match:
            raise ReleaseError(f"{self.tag} is not a release candidate: give the version, e.g. "
                               f"{self.bump('minor').tag}-rc.1")
        return Version(f"{self.major}.{self.minor}.{self.patch}-rc.{int(match.group(1)) + 1}")

    def _key(self) -> Tuple:
        # A release ranks above its pre-releases; numeric identifiers rank below alphanumeric ones.
        pre: Tuple = (1,)
        if self.prerelease:
            pre = (0, tuple((0, int(p), "") if p.isdigit() else (1, 0, p) for p in self.prerelease.split(".")))
        return (self.major, self.minor, self.patch, pre)

    def __lt__(self, other: "Version") -> bool:
        return self._key() < other._key()

    def __eq__(self, other: object) -> bool:
        return isinstance(other, Version) and self._key() == other._key()

    def __str__(self) -> str:
        core = f"{self.major}.{self.minor}.{self.patch}"
        return f"{core}-{self.prerelease}" if self.prerelease else core


def run(*args: str, capture: bool = True, check: bool = True,
        env: Optional[dict] = None) -> subprocess.CompletedProcess:
    """Run a command in the repository root; captured output is stripped text."""
    result = subprocess.run(args, cwd=REPO, text=True, capture_output=capture,
                            env={**os.environ, **env} if env else None)
    if check and result.returncode != 0:
        detail = (result.stderr or result.stdout or "").strip() if capture else ""
        raise ReleaseError(f"{' '.join(args)} failed" + (f":\n{detail}" if detail else ""))
    if capture:
        result.stdout = result.stdout.strip()
    return result


def succeeds(*args: str) -> bool:
    return run(*args, check=False).returncode == 0


def git(*args: str) -> str:
    return run("git", *args).stdout


def step(message: str) -> None:
    print(f"\n{Colors.BLUE}{Colors.BOLD}==> {message}{Colors.RESET}", flush=True)


def version_tags() -> List[Version]:
    versions = []
    for tag in git("tag", "--list", "v*").splitlines():
        if tag in LEGACY_TAGS:
            continue
        try:
            versions.append(Version(tag))
        except ValueError:
            pass
    return versions


def newest_tag_on_main() -> Optional[str]:
    exclude = [arg for tag in sorted(LEGACY_TAGS) for arg in ("--exclude", tag)]
    result = run("git", "describe", "--tags", "--abbrev=0", "--match", "v*", *exclude, "origin/main",
                 check=False)
    return result.stdout if result.returncode == 0 else None


def commits_since(tag: Optional[str], ref: str) -> str:
    log = git("log", "--oneline", f"{tag}..{ref}" if tag else ref)
    return "\n".join(f"  {line}" for line in log.splitlines()) or "  (none)"


def resolve(target: str, current: Version) -> Version:
    if target in ("rc", "patch", "minor", "major"):
        return current.bump(target)
    try:
        return Version(target)
    except ValueError:
        raise ReleaseError(f"'{target}' is not rc, patch, minor, major or a version like v1.2.3") from None


def show_status(current: Version) -> None:
    newest = newest_tag_on_main()
    print(f"Latest version: {current.tag}")
    if newest:
        print(f"\nCommits on origin/main since {newest}:\n{commits_since(newest, 'origin/main')}")
    candidates = []
    for part in ("rc", "patch", "minor", "major"):
        try:
            candidates.append(f"{part} {current.bump(part).tag}")
        except ReleaseError:
            pass
    print(f"\nNext: {', '.join(candidates)}")
    print("Run:  python scripts/release.py rc|patch|minor|major|vX.Y.Z[-suffix]")


def check_repository() -> None:
    url = git("remote", "get-url", "origin")
    if not re.search(rf"github\.com[:/]{re.escape(GITHUB_REPO)}(\.git)?$", url):
        raise ReleaseError(f"origin is {url}, not {GITHUB_REPO}: releases are cut from the main repository")
    if not succeeds("gh", "auth", "status"):
        raise ReleaseError("gh is not signed in: run gh auth login")


def check_working_copy(version: Version, versions: List[Version]) -> bool:
    """Fail unless main is releasable; return True when the tag is already pushed at HEAD (a rerun)."""
    step("Checking the working copy")
    if git("branch", "--show-current") != "main":
        raise ReleaseError("not on main")
    if git("status", "--porcelain"):
        raise ReleaseError("the working tree has uncommitted changes")
    head = git("rev-parse", "HEAD")
    if head != git("rev-parse", "origin/main"):
        raise ReleaseError("main is not in sync with origin/main (git pull / git push first)")

    tag = version.tag
    if succeeds("git", "rev-parse", "-q", "--verify", f"refs/tags/{tag}"):
        if git("rev-list", "-n", "1", tag) != head:
            raise ReleaseError(f"{tag} already exists on another commit")
        if not succeeds("git", "ls-remote", "--exit-code", "--tags", "origin", f"refs/tags/{tag}"):
            raise ReleaseError(f"{tag} exists locally but not on origin: push it or delete it")
        return True

    # A pre-release tag on HEAD may be promoted (v1.0.0-rc.8 -> v1.0.0); a release may not be repeated.
    released = [t for t in git("tag", "--points-at", "HEAD", "--list", "v*").splitlines() if "-" not in t]
    if released:
        raise ReleaseError(f"HEAD is already released as {released[0]}")
    highest = max(versions) if versions else None
    if highest and not highest < version:
        raise ReleaseError(f"{tag} is not greater than the existing {highest.tag}")
    return False


def check_ci(head: str) -> None:
    step("Checking CI on HEAD")
    runs = json.loads(run("gh", "run", "list", "--repo", GITHUB_REPO, "--workflow", CI_WORKFLOW,
                          "--commit", head, "--json", "status,conclusion,url").stdout or "[]")
    if not runs:
        raise ReleaseError(f"no {CI_WORKFLOW} run for {head[:7]} yet")
    if any(r["conclusion"] == "success" for r in runs):
        print(f"  passed: {next(r['url'] for r in runs if r['conclusion'] == 'success')}")
        return
    latest = runs[0]
    if latest["status"] != "completed":
        raise ReleaseError(f"CI is still running on {head[:7]}: {latest['url']}")
    raise ReleaseError(f"CI {latest['conclusion']} on {head[:7]}: {latest['url']}")


def check_nuget(version: Version) -> None:
    step(f"Checking that {version} is new on nuget.org")
    for package in PACKAGES:
        url = f"https://api.nuget.org/v3-flatcontainer/{package.lower()}/index.json"
        try:
            with urllib.request.urlopen(url, timeout=30) as response:
                published = json.load(response).get("versions", [])
        except urllib.error.HTTPError as e:
            if e.code == 404:
                continue  # never published
            raise ReleaseError(f"could not read {url}: {e}") from None
        except urllib.error.URLError as e:
            raise ReleaseError(f"could not reach nuget.org: {e.reason}") from None
        if str(version).lower() in (v.lower() for v in published):
            raise ReleaseError(f"{package} {version} is already on nuget.org")


def confirm(prompt: str) -> bool:
    try:
        return input(f"{prompt} [y/N] ").strip().lower() in ("y", "yes")
    except EOFError:
        return False


def build_and_pack(version: Version) -> None:
    """Pack as release.yml does and check that the CLI tool installs and runs."""
    step(f"Building and packing {version}")
    with tempfile.TemporaryDirectory(prefix="xping-release-") as temp:
        out = Path(temp, "nuget")
        run("dotnet", "build", "-c", "Release", f"-p:Version={version}", "--nologo", "-v", "quiet",
            capture=False)
        run("dotnet", "pack", "-c", "Release", "--no-build", f"-p:Version={version}", "-o", str(out),
            "--nologo", "-v", "quiet", capture=False)
        missing = [p for p in PACKAGES if not (out / f"{p}.{version}.nupkg").exists()]
        if missing:
            raise ReleaseError(f"not packed: {', '.join(missing)}")

        step("Installing the CLI tool from the packages")
        tools = Path(temp, "tools")
        # A private package cache: a failed earlier attempt at this version may have cached an older
        # build of the same version, and the install would take that instead.
        run("dotnet", "tool", "install", "Xping.Cli", "--tool-path", str(tools), "--add-source", str(out),
            "--version", str(version), env={"NUGET_PACKAGES": str(Path(temp, "packages"))})
        xping = str(tools / "xping")
        reported = run(xping, "--version").stdout
        if not reported.startswith(str(version)):
            raise ReleaseError(f"xping --version printed {reported!r}, expected {version}")
        if "xping [command]" not in run(xping, "--help").stdout:
            raise ReleaseError("xping --help does not show the usage line 'xping [command]'")
        print(f"  xping --version: {reported}")


def create_draft_release(version: Version) -> str:
    step("Creating the draft GitHub release")
    existing = run("gh", "release", "view", version.tag, "--repo", GITHUB_REPO, "--json", "url,isDraft",
                   check=False)
    if existing.returncode == 0:
        info = json.loads(existing.stdout)
        if not info["isDraft"]:
            raise ReleaseError(f"the release {version.tag} is already published: {info['url']}")
        print(f"The draft {version.tag} already exists")
        return info["url"]
    args = ["gh", "release", "create", version.tag, "--repo", GITHUB_REPO, "--verify-tag", "--draft",
            "--generate-notes", "--title", version.tag]
    if version.prerelease:
        args.append("--prerelease")
    return run(*args).stdout


def release(target: str) -> None:
    missing = [t for t in ("git", "gh", "dotnet") if not shutil.which(t)]
    if missing:
        raise ReleaseError(f"not installed: {', '.join(missing)}")

    check_repository()
    git("fetch", "--quiet", "--tags", "origin", "main")
    versions = version_tags()
    current = max(versions) if versions else Version("0.0.0")

    if not target:
        show_status(current)
        return

    version = resolve(target, current)
    tag = version.tag

    resume = check_working_copy(version, versions)
    if resume:
        step(f"{tag} is already pushed at HEAD; continuing with the draft release")
    else:
        check_ci(git("rev-parse", "HEAD"))
        check_nuget(version)

        step(f"Releasing {tag} (from {current.tag})")
        print(commits_since(newest_tag_on_main(), "HEAD"))
        print(f"\nThis builds and packs {version}, tags and pushes {tag} and opens a draft release.")
        if not confirm("Continue?"):
            raise ReleaseError("cancelled")

        build_and_pack(version)

        step(f"Tagging and pushing {tag}")
        git("tag", "-a", tag, "-m", tag)
        run("git", "push", "origin", tag, capture=False)

    url = create_draft_release(version)

    step(f"Done: {tag} is tagged and the release is a draft")
    print(f"  {url}")
    print("Edit the title and notes, then publish: that pushes the packages to nuget.org and deploys the docs.")


def main() -> int:
    parser = argparse.ArgumentParser(
        description=__doc__,
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    parser.add_argument("target", nargs="?", default="",
                        help="rc, patch, minor, major or an explicit version; omit to show the current version")
    args = parser.parse_args()

    try:
        release(args.target)
    except ReleaseError as e:
        print(f"{Colors.RED}error: {e}{Colors.RESET}", file=sys.stderr)
        return 1
    except KeyboardInterrupt:
        print(f"\n{Colors.RED}interrupted{Colors.RESET}", file=sys.stderr)
        return 130
    return 0


if __name__ == "__main__":
    sys.exit(main())
