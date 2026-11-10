#!/usr/bin/env bash
#
# import-prod-db.sh — copy the production GenoCRM PostgreSQL database into the
# local development database (docker-compose.dev.yml).
#
# How it works
#   1. Runs `pg_dump -Fc` INSIDE the production postgres container over SSH and
#      streams the dump to ./dumps/ (no local postgres client needed, dump and
#      server versions always match).
#   2. Makes sure the local dev postgres container is running and healthy.
#   3. Drops and recreates the local `genocrm` database.
#   4. Restores the dump with pg_restore inside the local container.
#
# On the next app start, EF Core applies any migrations that are newer than
# production (Program.cs calls MigrateAsync on startup).
#
# Configuration (flags override env vars, env vars override .env in repo root):
#   PROD_SSH_HOST       user@host of the production machine         (required)
#   PROD_DB_CONTAINER   production postgres container name           (default: genocrm-postgres)
#   PROD_DB_NAME        production database name                     (default: genocrm)
#   PROD_DB_USER        production database user                     (default: genocrm)
#   PROD_DOCKER_CMD     how to call docker on prod, e.g. "sudo docker" (default: docker)
#   PROD_SSH_OPTS       extra ssh options, e.g. "-o IdentitiesOnly=yes -o IdentityFile=.local/key.pub"
#   LOCAL_DB_CONTAINER  local postgres container                     (default: genocrm-dev-postgres)
#   LOCAL_DB_NAME / LOCAL_DB_USER                                     (default: genocrm / genocrm)
#   LOCAL_COMPOSE_FILE  compose file for the local DB                (default: docker-compose.dev.yml)
#   DUMP_DIR            where dumps are stored                        (default: ./dumps)
#
# Usage
#   scripts/import-prod-db.sh --host deploy@genocrm.example.org
#   scripts/import-prod-db.sh --host deploy@prod --docker-cmd "sudo docker"
#   scripts/import-prod-db.sh --from-file dumps/genocrm-20261110-073000.dump   # restore an existing dump
#   scripts/import-prod-db.sh --dump-only --host deploy@prod                    # just fetch the dump
#
# NOTE: the production database contains real member data (names, addresses,
# bank details). Treat ./dumps/ accordingly: it is git-ignored, but delete the
# files when you no longer need them and never copy them elsewhere.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

# ---------- load .env (only for variables not already set) -------------------
if [ -f .env ]; then
  while IFS='=' read -r key value || [ -n "$key" ]; do
    [[ -z "$key" || "$key" =~ ^[[:space:]]*# ]] && continue
    key="${key// /}"
    case "$key" in
      PROD_SSH_HOST|PROD_DB_CONTAINER|PROD_DB_NAME|PROD_DB_USER|PROD_DOCKER_CMD|PROD_SSH_OPTS|\
      LOCAL_DB_CONTAINER|LOCAL_DB_NAME|LOCAL_DB_USER|LOCAL_COMPOSE_FILE|DUMP_DIR)
        value="${value%\"}"; value="${value#\"}"; value="${value%\'}"; value="${value#\'}"
        [ -z "${!key:-}" ] && export "$key=$value"
        ;;
    esac
  done < .env
fi

PROD_SSH_HOST="${PROD_SSH_HOST:-}"
PROD_DB_CONTAINER="${PROD_DB_CONTAINER:-genocrm-postgres}"
PROD_DB_NAME="${PROD_DB_NAME:-genocrm}"
PROD_DB_USER="${PROD_DB_USER:-genocrm}"
PROD_DOCKER_CMD="${PROD_DOCKER_CMD:-docker}"
PROD_SSH_OPTS="${PROD_SSH_OPTS:-}"
LOCAL_DB_CONTAINER="${LOCAL_DB_CONTAINER:-genocrm-dev-postgres}"
LOCAL_DB_NAME="${LOCAL_DB_NAME:-genocrm}"
LOCAL_DB_USER="${LOCAL_DB_USER:-genocrm}"
LOCAL_COMPOSE_FILE="${LOCAL_COMPOSE_FILE:-docker-compose.dev.yml}"
DUMP_DIR="${DUMP_DIR:-$REPO_ROOT/dumps}"

FROM_FILE=""
DUMP_ONLY=0
KEEP_DUMP=1
ASSUME_YES=0

usage() { sed -n '2,/^$/p' "$0" | sed 's/^# \{0,1\}//'; exit "${1:-0}"; }

while [ $# -gt 0 ]; do
  case "$1" in
    --host)        PROD_SSH_HOST="$2"; shift 2 ;;
    --container)   PROD_DB_CONTAINER="$2"; shift 2 ;;
    --db)          PROD_DB_NAME="$2"; shift 2 ;;
    --user)        PROD_DB_USER="$2"; shift 2 ;;
    --docker-cmd)  PROD_DOCKER_CMD="$2"; shift 2 ;;
    --from-file)   FROM_FILE="$2"; shift 2 ;;
    --dump-only)   DUMP_ONLY=1; shift ;;
    --delete-dump) KEEP_DUMP=0; shift ;;
    -y|--yes)      ASSUME_YES=1; shift ;;
    -h|--help)     usage 0 ;;
    *) echo "Unknown argument: $1" >&2; usage 1 ;;
  esac
done

log()  { printf '\033[1;34m==>\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33mWARN:\033[0m %s\n' "$*" >&2; }
die()  { printf '\033[1;31mERROR:\033[0m %s\n' "$*" >&2; exit 1; }

# ---------- docker / podman -------------------------------------------------
command -v docker >/dev/null || die "docker CLI not found (install docker or podman-docker)"
_podman_sock="/run/user/$(id -u)/podman/podman.sock"
if [ -z "${DOCKER_HOST:-}" ] && [ -S "$_podman_sock" ]; then
  export DOCKER_HOST="unix://$_podman_sock"
fi
dkr() { docker "$@" 2> >(grep -v -i 'emulate docker cli\|nodocker' >&2); }

# ---------- 1. obtain dump ---------------------------------------------------
mkdir -p "$DUMP_DIR"
if [ -n "$FROM_FILE" ]; then
  [ -f "$FROM_FILE" ] || die "dump file not found: $FROM_FILE"
  DUMP_FILE="$FROM_FILE"
  log "Using existing dump: $DUMP_FILE"
else
  [ -n "$PROD_SSH_HOST" ] || die "PROD_SSH_HOST not set. Use --host user@host or put PROD_SSH_HOST=... into .env"
  DUMP_FILE="$DUMP_DIR/${PROD_DB_NAME}-$(date +%Y%m%d-%H%M%S).dump"
  log "Dumping ${PROD_DB_NAME} from ${PROD_SSH_HOST} (container ${PROD_DB_CONTAINER}) -> $DUMP_FILE"
  # -Fc: custom format (compressed, restorable with pg_restore, supports --no-owner etc.)
  # shellcheck disable=SC2086  (PROD_SSH_OPTS is intentionally word-split)
  ssh -o BatchMode=yes $PROD_SSH_OPTS "$PROD_SSH_HOST" \
      "$PROD_DOCKER_CMD exec -i '$PROD_DB_CONTAINER' pg_dump -U '$PROD_DB_USER' -d '$PROD_DB_NAME' -Fc --no-owner --no-privileges" \
      > "$DUMP_FILE" \
    || { rm -f "$DUMP_FILE"; die "remote pg_dump failed"; }
  [ -s "$DUMP_FILE" ] || { rm -f "$DUMP_FILE"; die "dump is empty"; }
  chmod 600 "$DUMP_FILE"
  log "Dump size: $(du -h "$DUMP_FILE" | cut -f1)"
  if [ "$DUMP_ONLY" = 1 ]; then
    log "--dump-only given, stopping here."
    exit 0
  fi
fi

# ---------- 2. local database container -------------------------------------
if ! dkr inspect -f '{{.State.Running}}' "$LOCAL_DB_CONTAINER" 2>/dev/null | grep -q true; then
  log "Starting local database via $LOCAL_COMPOSE_FILE"
  dkr compose -f "$LOCAL_COMPOSE_FILE" up -d
fi
log "Waiting for $LOCAL_DB_CONTAINER to become healthy"
for _ in $(seq 1 60); do
  status="$(dkr inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}nohealth{{end}}' "$LOCAL_DB_CONTAINER" 2>/dev/null || echo missing)"
  [ "$status" = healthy ] && break
  if [ "$status" = nohealth ] && dkr exec "$LOCAL_DB_CONTAINER" pg_isready -U "$LOCAL_DB_USER" >/dev/null 2>&1; then status=healthy; break; fi
  sleep 1
done
[ "$status" = healthy ] || die "local postgres container '$LOCAL_DB_CONTAINER' did not become healthy (status: $status)"

psql_local() { dkr exec -i "$LOCAL_DB_CONTAINER" psql -v ON_ERROR_STOP=1 -U "$LOCAL_DB_USER" "$@"; }

# ---------- 3. drop & recreate -----------------------------------------------
if [ "$ASSUME_YES" != 1 ]; then
  printf 'This will DROP the local database "%s" in container "%s" and replace it with the dump. Continue? [y/N] ' "$LOCAL_DB_NAME" "$LOCAL_DB_CONTAINER"
  read -r answer
  [[ "$answer" =~ ^[Yy]$ ]] || die "aborted"
fi

log "Dropping and recreating local database $LOCAL_DB_NAME"
psql_local -d postgres -q -o /dev/null <<SQL
SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '$LOCAL_DB_NAME' AND pid <> pg_backend_pid();
DROP DATABASE IF EXISTS "$LOCAL_DB_NAME";
CREATE DATABASE "$LOCAL_DB_NAME" OWNER "$LOCAL_DB_USER";
SQL

# ---------- 4. restore -------------------------------------------------------
log "Restoring dump into $LOCAL_DB_NAME"
# pg_restore exits 1 on harmless warnings (e.g. missing extensions comments); capture and show.
if ! dkr exec -i "$LOCAL_DB_CONTAINER" pg_restore -U "$LOCAL_DB_USER" -d "$LOCAL_DB_NAME" \
      --no-owner --no-privileges --exit-on-error < "$DUMP_FILE"; then
  die "pg_restore failed"
fi

# ---------- 5. summary -------------------------------------------------------
log "Restore complete. Row counts:"
psql_local -d "$LOCAL_DB_NAME" -tA -F $'\t' <<'SQL' | column -t -s $'\t'
SELECT relname AS "table", n_live_tup AS "rows"
FROM pg_stat_user_tables
ORDER BY relname;
SQL
log "Applied EF migrations in dump:"
psql_local -d "$LOCAL_DB_NAME" -tAc 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY 1 DESC LIMIT 3;' 2>/dev/null || warn "no __EFMigrationsHistory table found"

if [ "$KEEP_DUMP" = 0 ] && [ -z "$FROM_FILE" ]; then
  rm -f "$DUMP_FILE"; log "Deleted dump file."
else
  log "Dump kept at $DUMP_FILE (git-ignored). Delete it when no longer needed."
fi
log "Done. Start the app (Rider or 'dotnet run --project GenoCRM/GenoCRM/GenoCRM.csproj') — pending migrations are applied on startup."
