#!/usr/bin/env bash
# The test levels, separately or together (ADR-0007). Persistence, integration and e2e need Docker (Testcontainers).
#   scripts/test.sh            domain + persistence + integration
#   scripts/test.sh domain     only the domain
#   scripts/test.sh persistence  the store alone (indexes, projections, stream locks), no host
#   scripts/test.sh integration
#   scripts/test.sh e2e        Playwright (installs Chromium on first run); HEADED=1 to watch
#   scripts/test.sh all        all four
set -euo pipefail
cd "$(dirname "$0")/.."

level="${1:-default}"
run() { echo "==> $1"; dotnet test --project "tests/$1/$1.csproj" --no-build; }

dotnet build Halcyon.slnx --nologo -v quiet
case "$level" in
  domain) run Halcyon.Domain.Tests ;;
  persistence) run Halcyon.PersistenceTests ;;
  integration) run Halcyon.IntegrationTests ;;
  e2e) run Halcyon.E2E ;;
  all) run Halcyon.Domain.Tests; run Halcyon.PersistenceTests; run Halcyon.IntegrationTests; run Halcyon.E2E ;;
  default) run Halcyon.Domain.Tests; run Halcyon.PersistenceTests; run Halcyon.IntegrationTests ;;
  *) echo "unknown level: $level" >&2; exit 2 ;;
esac
