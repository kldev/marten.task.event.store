#!/usr/bin/env bash
# Starts Halcyon on docker compose (docker-compose.yml, profile `app`): PostgreSQL, the migrator (schema + seed, once)
# and the app. Waits for the app and prints its address and how to sign in.
#
#   ./up.sh              build the images and start everything (only what changed is recreated)
#   ./up.sh --no-build   start with the images already built
set -euo pipefail
cd "$(dirname "$0")"

BUILD=(--build)
for arg in "$@"; do
  case "$arg" in
    --no-build) BUILD=() ;;
    -h|--help) sed -n '2,/^set /{/^#/p}' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "Unknown option: $arg (see --help)" >&2; exit 2 ;;
  esac
done

# compose needs POSTGRES_PASSWORD, AGENT_PASSWORD and the S3 keys; the first run takes the local defaults from .env-sample.
if [[ ! -f .env ]]; then
  cp .env-sample .env
  echo "No .env - created from .env-sample (local development values)."
fi
set -a; . ./.env; set +a
PORT="${APP_PORT:-8080}"
URL="http://localhost:${PORT}"

# The commit shown in the app's footer; "-dirty" when the build has uncommitted changes.
GIT_SHA=$(git rev-parse HEAD 2>/dev/null || true)
if [[ -n "$GIT_SHA" ]] && ! git diff --quiet HEAD 2>/dev/null; then GIT_SHA="${GIT_SHA}-dirty"; fi
export GIT_SHA

docker compose --profile app up -d "${BUILD[@]}" --remove-orphans

# The migrator runs first: if it fails the app never starts, so say so and show its log.
echo "Waiting for the app (${URL})..."
deadline=$((SECONDS + 300))
while :; do
  id=$(docker compose --profile app ps -aq migrator 2>/dev/null || true)
  if [[ -n "$id" ]]; then
    state=$(docker inspect --format '{{.State.Status}} {{.State.ExitCode}}' "$id")
    if [[ "$state" == exited* && "$state" != "exited 0" ]]; then
      echo "The migrator failed:" >&2
      docker compose --profile app logs --no-log-prefix --tail=40 migrator >&2
      exit 1
    fi
  fi
  app=$(docker compose --profile app ps -aq app 2>/dev/null || true)
  if [[ -n "$app" ]]; then
    health=$(docker inspect --format '{{.State.Status}} {{if .State.Health}}{{.State.Health.Status}}{{end}}' "$app")
    case "$health" in
      "running healthy") break ;;
      exited*|dead*)
        echo "The app stopped:" >&2
        docker compose --profile app logs --no-log-prefix --tail=60 app >&2
        exit 1 ;;
    esac
  fi
  if (( SECONDS > deadline )); then
    echo "The app is not healthy after 5 minutes. ./logs.sh app" >&2
    exit 1
  fi
  sleep 2
done

echo
docker compose --profile app ps --format 'table {{.Service}}\t{{.Status}}'
cat <<EOF

Halcyon is running
  App            ${URL}
  Public form    ${URL}/zglos   (requesters, no sign-in)
  PostgreSQL     localhost:${POSTGRES_PORT:-5433}   (${POSTGRES_USER:-halcyon} / ${POSTGRES_PASSWORD}, database ${POSTGRES_DB:-halcyon})
  RustFS (S3)    http://localhost:${S3_PORT:-9010}   bucket ${S3_BUCKET:-halcyon-attachments}
  RustFS console http://localhost:${S3_CONSOLE_PORT:-9011}/rustfs/console/   (${S3_ACCESS_KEY} / ${S3_SECRET_KEY})

Sign in (person or browser agent)
  ${URL} -> any agent from the seed, e.g. anna.nowak@company.io
  Password       ${AGENT_PASSWORD}   (AGENT_PASSWORD from .env, shared by all agents)
  One click      "Zaloguj się jako domyślny agent" -> ${DEFAULT_AGENT_EMAIL:-off}
EOF
