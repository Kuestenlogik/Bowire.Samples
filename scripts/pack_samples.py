#!/usr/bin/env python3
# Copyright 2026 Küstenlogik
# SPDX-License-Identifier: Apache-2.0
"""
Pack every sample as its own ready-to-run download (#92).

  python scripts/pack_samples.py --version v0.1.16 [--out artifacts/samples] [--smoke]

Which samples exist is read off Bowire.Samples.slnx, never from a list: every
project that runs (the Web SDK, or OutputType Exe) is a sample. Libraries
(Shared) are not, and neither is the Aspire AppHost — it orchestrates the
others and means nothing on its own; `combined` is the whole harbor demo in
one process and is the download for it.

Each archive, bowire-samples-<sample>.zip, holds one top-level folder with:

  app/           the framework-dependent publish output — needs the .NET
                 runtime, not the SDK
  src/           the sample's source and the projects it references, with
                 the repo's build files, so it also builds on its own
  run.sh/.cmd    start it on the ports its appsettings.json names
  README.md      the sample's README, plus how to run the download

The samples listen on https://localhost:<port>. A machine with only the
runtime has no developer certificate (that comes with the SDK), so the run
scripts make a self-signed localhost certificate on first start and hand it
to Kestrel, and turn on Bowire:TrustLocalhostCert so the workbench inside the
sample can call its own endpoints — a setting that only ever trusts
localhost.

Names carry no version, so releases/latest/download/<name> stays a stable
link; the release tag and the README say which version it is.

--smoke starts every packed sample with its own run.sh and waits for its
first port to answer: what a user downloads must start, not just build.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import signal
import ssl
import subprocess
import sys
import tempfile
import time
import urllib.request
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PREFIX = "bowire-samples"
SKIP_DIRS = {"bin", "obj", ".vs", "node_modules", "__pycache__"}
BUILD_FILES = ["Directory.Build.props", "Directory.Build.targets", "nuget.config", "global.json"]


def projects() -> list[Path]:
    slnx = (ROOT / "Bowire.Samples.slnx").read_text(encoding="utf-8")
    return [ROOT / p for p in re.findall(r'Project Path="([^"]+\.csproj)"', slnx)]


def is_sample(csproj: Path) -> bool:
    text = csproj.read_text(encoding="utf-8")
    if "Aspire.AppHost.Sdk" in text:
        return False
    return 'Sdk="Microsoft.NET.Sdk.Web"' in text or "<OutputType>Exe</OutputType>" in text


def sample_name(csproj: Path) -> str:
    return csproj.stem.rsplit(".", 1)[-1].lower()


def references(csproj: Path, seen: set[Path] | None = None) -> set[Path]:
    """The project and every project it references, transitively."""
    seen = seen if seen is not None else set()
    csproj = csproj.resolve()
    if csproj in seen:
        return seen
    seen.add(csproj)
    for ref in re.findall(r'ProjectReference Include="([^"]+)"', csproj.read_text(encoding="utf-8")):
        references((csproj.parent / ref.replace("\\", "/")).resolve(), seen)
    return seen


def urls(csproj: Path) -> list[str]:
    settings = csproj.parent / "appsettings.json"
    if not settings.is_file():
        return []
    try:
        data = json.loads(settings.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        return []
    endpoints = (data.get("Kestrel") or {}).get("Endpoints") or {}
    return [e["Url"] for e in endpoints.values() if isinstance(e, dict) and "Url" in e]


def copy_tree(src: Path, dest: Path) -> None:
    for path in sorted(src.rglob("*")):
        rel = path.relative_to(src)
        if any(part in SKIP_DIRS for part in rel.parts):
            continue
        target = dest / rel
        if path.is_dir():
            target.mkdir(parents=True, exist_ok=True)
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, target)


RUN_SH = """#!/bin/sh
# Starts the {name} sample on {urls}.
# Needs the .NET runtime (https://dotnet.microsoft.com/download) and, for the
# first start, openssl to make a self-signed localhost certificate.
set -e
cd "$(dirname "$0")"
if [ ! -f cert/localhost.pem ] && command -v openssl >/dev/null 2>&1; then
  mkdir -p cert
  openssl req -x509 -newkey rsa:2048 -nodes -days 825 -subj "/CN=localhost" \\
    -addext "subjectAltName=DNS:localhost,IP:127.0.0.1" \\
    -keyout cert/localhost.key -out cert/localhost.pem >/dev/null 2>&1
fi
if [ -f cert/localhost.pem ]; then
  export Kestrel__Certificates__Default__Path="$PWD/cert/localhost.pem"
  export Kestrel__Certificates__Default__KeyPath="$PWD/cert/localhost.key"
fi
# Only ever trusts localhost: lets the workbench inside the sample call its own endpoints.
export Bowire__TrustLocalhostCert=true
echo "{name}: {urls}  (Ctrl+C to stop)"
# From app/: ASP.NET reads appsettings.json, and with it the ports, from the directory it starts in.
cd app
exec dotnet {dll} "$@"
"""

RUN_CMD = """@echo off
rem Starts the {name} sample on {urls}.
rem Needs the .NET runtime (https://dotnet.microsoft.com/download).
setlocal
cd /d "%~dp0"
rem A PSModulePath inherited from PowerShell 7 keeps Windows PowerShell from
rem loading its certificate modules; clearing it lets it use its own default.
set "PSModulePath="
if not exist cert\\localhost.pfx (
  powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "$c = New-SelfSignedCertificate -DnsName localhost -CertStoreLocation Cert:\\CurrentUser\\My -NotAfter (Get-Date).AddYears(2);" ^
    "New-Item -ItemType Directory -Force cert | Out-Null;" ^
    "Export-PfxCertificate -Cert $c -FilePath cert\\localhost.pfx -Password (ConvertTo-SecureString 'bowire-sample' -Force -AsPlainText) | Out-Null;" ^
    "Remove-Item $c.PSPath"
)
if exist cert\\localhost.pfx (
  set "Kestrel__Certificates__Default__Path=%~dp0cert\\localhost.pfx"
  set "Kestrel__Certificates__Default__Password=bowire-sample"
)
rem Only ever trusts localhost: lets the workbench inside the sample call its own endpoints.
set "Bowire__TrustLocalhostCert=true"
echo {name}: {urls}  (Ctrl+C to stop)
rem From app\\: ASP.NET reads appsettings.json, and with it the ports, from the directory it starts in.
cd app
dotnet {dll} %*
"""


def readme(csproj: Path, name: str, version: str, sample_urls: list[str]) -> str:
    own = csproj.parent / "README.md"
    text = own.read_text(encoding="utf-8").rstrip() if own.is_file() else f"# {name}\n\nThe `{csproj.stem}` sample."
    where = ", ".join(sample_urls) if sample_urls else "the URLs in app/appsettings.json"
    return text + f"""

---

## Running this download

This is the `{name}` sample from Bowire.Samples {version}, ready to run. You need the [.NET runtime](https://dotnet.microsoft.com/download) — not the SDK.

```sh
./run.sh        # Linux / macOS
run.cmd         # Windows
```

It listens on {where}; the workbench is at `/bowire` there. On first start the script makes a self-signed certificate for localhost (in `cert/`), so the browser asks you to accept it once.

`src/` has the source, with the projects it references and the repository's build files: `dotnet run --project src/{csproj.relative_to(ROOT).as_posix()}` builds and runs it with the SDK. Links to other samples point into the repository: https://github.com/Kuestenlogik/Bowire.Samples
"""


def pack(version: str, out: Path, only: set[str] | None) -> list[tuple[Path, list[str]]]:
    out.mkdir(parents=True, exist_ok=True)
    packed = []
    with tempfile.TemporaryDirectory() as scratch:
        for csproj in projects():
            if not is_sample(csproj):
                continue
            name = sample_name(csproj)
            if only and name not in only:
                continue
            folder = f"{PREFIX}-{name}"
            tree = Path(scratch) / folder
            app = tree / "app"
            print(f"::group::publish {name}", flush=True)
            result = subprocess.run(["dotnet", "publish", str(csproj), "-c", "Release", "-o", str(app), "-nologo"])
            print("::endgroup::", flush=True)
            if result.returncode != 0:
                raise SystemExit(f"publish failed: {csproj}")
            for project in references(csproj):
                copy_tree(project.parent, tree / "src" / project.parent.relative_to(ROOT))
            for f in BUILD_FILES:
                if (ROOT / f).is_file():
                    shutil.copy2(ROOT / f, tree / "src" / f)
            sample_urls = urls(csproj)
            shown = ", ".join(sample_urls) or "the URLs in app/appsettings.json"
            dll = csproj.stem + ".dll"
            (tree / "run.sh").write_text(RUN_SH.format(name=name, urls=shown, dll=dll), encoding="utf-8", newline="\n")
            (tree / "run.cmd").write_text(RUN_CMD.format(name=name, urls=shown, dll=dll), encoding="utf-8", newline="\r\n")
            (tree / "README.md").write_text(readme(csproj, name, version, sample_urls), encoding="utf-8")
            archive = out / f"{folder}.zip"
            with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as z:
                for f in sorted(tree.rglob("*")):
                    if f.is_file():
                        info = zipfile.ZipInfo.from_file(f, f.relative_to(Path(scratch)).as_posix())
                        if f.name == "run.sh":
                            info.external_attr = (0o755 & 0xFFFF) << 16
                        with open(f, "rb") as fh:
                            z.writestr(info, fh.read(), zipfile.ZIP_DEFLATED)
            packed.append((archive, sample_urls))
            print(f"packed {archive.name}", flush=True)
    return packed


def smoke(packed: list[tuple[Path, list[str]]], timeout: float) -> list[str]:
    """Start each archive with its own run.sh; its first URL has to answer."""
    failed = []
    insecure = ssl.create_default_context()
    insecure.check_hostname = False
    insecure.verify_mode = ssl.CERT_NONE
    for archive, sample_urls in packed:
        if not sample_urls:
            print(f"smoke {archive.name}: no URL in appsettings.json, started only")
        with tempfile.TemporaryDirectory() as scratch:
            with zipfile.ZipFile(archive) as z:
                z.extractall(scratch)
            run = next(Path(scratch).glob("*/run.sh"))
            run.chmod(0o755)
            log = open(Path(scratch) / "run.log", "w+")
            proc = subprocess.Popen(["sh", str(run)], stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
            ok, deadline = False, time.time() + timeout
            while time.time() < deadline and proc.poll() is None:
                if not sample_urls:
                    ok = time.time() > deadline - timeout + 10  # stayed up 10 s
                    if ok:
                        break
                else:
                    try:
                        with urllib.request.urlopen(sample_urls[0].rstrip("/") + "/bowire", timeout=3, context=insecure) as r:
                            ok = r.status < 500
                    except urllib.error.HTTPError as e:
                        ok = e.code < 500
                    except OSError:
                        ok = False
                    if ok:
                        break
                time.sleep(1)
            if proc.poll() is None:
                os.killpg(proc.pid, signal.SIGTERM)
                try:
                    proc.wait(10)
                except subprocess.TimeoutExpired:
                    os.killpg(proc.pid, signal.SIGKILL)
            print(f"smoke {archive.name}: {'ok' if ok else 'FAILED'}", flush=True)
            if not ok:
                log.seek(0)
                print(log.read()[-3000:])
                failed.append(archive.name)
            log.close()
    return failed


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--version", required=True, help="release tag, e.g. v0.1.16 (or 'pr' for a CI dry run)")
    parser.add_argument("--out", default=str(ROOT / "artifacts" / "samples"))
    parser.add_argument("--only", nargs="*", help="pack only these samples (by name)")
    parser.add_argument("--smoke", action="store_true", help="start every archive with run.sh and wait for it to answer")
    parser.add_argument("--smoke-timeout", type=float, default=60)
    args = parser.parse_args()

    packed = pack(args.version, Path(args.out), set(args.only) if args.only else None)
    if not packed:
        print("::error::no samples found", file=sys.stderr)
        return 1
    if args.smoke:
        failed = smoke(packed, args.smoke_timeout)
        if failed:
            print("::error::samples that do not start from their download: " + ", ".join(failed), file=sys.stderr)
            return 1
    print(f"{len(packed)} archives in {args.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
