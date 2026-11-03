#!/usr/bin/env bash
# The three test levels, separately or together (ADR-0007). Integration and e2e need Docker (Testcontainers).
#   scripts/test.sh            domain + integration
#   scripts/test.sh domain     only the domain
#   scripts/test.sh integration
#   scripts/test.sh e2e        Playwright (installs Chromium on first run); HEADED=1 to watch
#   scripts/test.sh all        all three
set -euo pipefail
cd "$(dirname "$0")/.."

level="${1:-default}"
run() { echo "==> $1"; dotnet test --project "tests/$1/$1.csproj" --no-build; }

dotnet build Halcyon.slnx --nologo -v quiet
case "$level" in
  domain) run Halcyon.Domain.Tests ;;
  integration) run Halcyon.IntegrationTests ;;
  e2e) run Halcyon.E2E ;;
  all) run Halcyon.Domain.Tests; run Halcyon.IntegrationTests; run Halcyon.E2E ;;
  default) run Halcyon.Domain.Tests; run Halcyon.IntegrationTests ;;
  *) echo "unknown level: $level" >&2; exit 2 ;;
esac
