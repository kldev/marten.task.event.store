#!/usr/bin/env bash
# Stops Halcyon started by ./up.sh.
#
#   ./down.sh           stop and remove the containers; the database stays
#   ./down.sh --purge   also drop the database volume - the next ./up.sh migrates and seeds from scratch
set -euo pipefail
cd "$(dirname "$0")"

case "${1:-}" in
  "")
    docker compose --profile app down --remove-orphans ;;
  --purge)
    docker compose --profile app down --volumes --remove-orphans
    echo "Database volume removed." ;;
  -h|--help)
    sed -n '2,/^set /{/^#/p}' "$0" | sed 's/^# \{0,1\}//' ;;
  *)
    echo "Unknown option: $1 (see --help)" >&2; exit 2 ;;
esac
