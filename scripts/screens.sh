#!/usr/bin/env bash
# Regenerates the screenshots of the README (docs/screens/*.jpg) and of docs/Portal.md (docs/screens/portal/*.jpg)
# from the e2e run: the real app on its own PostgreSQL and RustFS (Testcontainers) with the seed. Needs Docker;
# installs Chromium on the first run.
set -euo pipefail
cd "$(dirname "$0")/.."

dotnet build Halcyon.slnx --nologo -v quiet
HALCYON_SCREENS_TO_DOCS=1 dotnet test --project tests/Halcyon.E2E/Halcyon.E2E.csproj --no-build \
  -- --filter-class 'Halcyon.E2E.ScreenshotTests' --filter-class 'Halcyon.E2E.PortalTourTests'
ls -1 docs/screens docs/screens/portal
