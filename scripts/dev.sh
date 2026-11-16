#!/usr/bin/env bash
# DotNextAI local development orchestrator (Git Bash on Windows, WSL, macOS, Linux; bash 3.2+).
#
#   ./scripts/dev.sh                       Start the API (:5100) and the web app (:3030) together; Ctrl+C stops both
#   ./scripts/dev.sh api [--watch]         Only the API (Swagger UI at /swagger); --watch runs it under dotnet watch
#   ./scripts/dev.sh web                   Only the web app (next dev on http://localhost:3030)
#   ./scripts/dev.sh cli [args...]         Run the terminal app (chat by default; `cli ask "..."` for one question)
#   ./scripts/dev.sh secrets               Set the LiteLLM proxy endpoint, default model and API key (user-secrets)
#   ./scripts/dev.sh build [backend|frontend]  Build as in CI (.NET with warnings as errors, Next.js production build)
#   ./scripts/dev.sh test [backend|frontend]   Run the checks of CI (.NET tests; ESLint, type-check and Vitest)
#   ./scripts/dev.sh status                Show the tools, the LLM settings (never the key) and what is running
#
# Secrets live in `dotnet user-secrets` (one store shared by the API and the CLI), never in tracked files.

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# Relative paths from the repository root: they also work with the Windows tools called from WSL (dotnet.exe, npm).
cd "$ROOT"

SOLUTION="backend/DotNextAI.slnx"
API_PROJECT="backend/src/DotNextAI.Api"
CLI_PROJECT="backend/src/DotNextAI.Cli"
FRONTEND_DIR="frontend"
FRONTEND_ENV_FILE="$FRONTEND_DIR/.env.local"
API_URL="http://localhost:5100"
WEB_URL="http://localhost:3030"

if [ -t 1 ]; then
  C_RESET=$'\033[0m' C_BOLD=$'\033[1m' C_DIM=$'\033[2m' C_RED=$'\033[31m' C_GREEN=$'\033[32m' C_YELLOW=$'\033[33m'
  C_BLUE=$'\033[34m' C_MAGENTA=$'\033[35m'
else
  C_RESET="" C_BOLD="" C_DIM="" C_RED="" C_GREEN="" C_YELLOW="" C_BLUE="" C_MAGENTA=""
fi

step() { printf '%s▸ %s%s\n' "$C_BOLD" "$*" "$C_RESET"; }
ok() { printf '  %s✓%s %s\n' "$C_GREEN" "$C_RESET" "$*"; }
warn() { printf '  %s!%s %s\n' "$C_YELLOW" "$C_RESET" "$*" >&2; }
die() {
  printf '%s✗ %s%s\n' "$C_RED" "$*" "$C_RESET" >&2
  exit 1
}

usage() { sed -n '2,14p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; }

# --- tools --------------------------------------------------------------------------------------

DOTNET=""
CURL="curl"
# WSL using the Windows toolchain: .NET and Node.js run as Windows processes (node_modules installed from Windows
# do not work in WSL and vice versa).
WINDOWS_TOOLCHAIN=false

ensure_dotnet() {
  if command -v dotnet >/dev/null 2>&1; then
    DOTNET="dotnet"
  elif command -v dotnet.exe >/dev/null 2>&1; then
    # WSL without a Linux SDK: use the Windows one. The API then listens on Windows' localhost, which only the
    # Windows curl reaches.
    DOTNET="dotnet.exe"
    WINDOWS_TOOLCHAIN=true
    command -v curl.exe >/dev/null 2>&1 && CURL="curl.exe"
  else
    die "The .NET SDK 10 is required (https://dot.net). See README > Requirements."
  fi
  local sdks
  sdks="$("$DOTNET" --list-sdks 2>/dev/null | tr -d '\r')"
  printf '%s\n' "$sdks" | grep -q '^10\.' || die ".NET SDK 10 not found (installed: ${sdks:-none})."
}

npm_run() {
  if [ "$WINDOWS_TOOLCHAIN" = true ]; then
    cmd.exe /c npm "$@"
  else
    npm "$@"
  fi
}

node_version() {
  if [ "$WINDOWS_TOOLCHAIN" = true ]; then
    cmd.exe /c node --version 2>/dev/null | tr -d '\r'
  else
    node --version 2>/dev/null
  fi
}

ensure_node() {
  # The toolchain choice (Windows from WSL or native) follows the .NET SDK found.
  [ -n "$DOTNET" ] || ensure_dotnet
  if [ "$WINDOWS_TOOLCHAIN" = true ]; then
    command -v cmd.exe >/dev/null 2>&1 || die "cmd.exe is required to run the Windows Node.js from WSL."
  else
    command -v npm >/dev/null 2>&1 || die "Node.js 24 LTS and npm are required (https://nodejs.org). See README > Requirements."
  fi
  local version major
  version="$(node_version)"
  major="${version#v}"
  major="${major%%.*}"
  [ -n "$major" ] && [ "$major" -ge 22 ] 2>/dev/null || die "Node.js 22 or later is required (found: ${version:-none}); CI uses 24 LTS."
}

# npm ci only when node_modules is missing or older than the lock file.
ensure_frontend_deps() {
  ensure_node
  if [ ! -f "$FRONTEND_DIR/node_modules/.package-lock.json" ] ||
    [ "$FRONTEND_DIR/package-lock.json" -nt "$FRONTEND_DIR/node_modules/.package-lock.json" ]; then
    step "Installing frontend dependencies (npm ci, no install scripts)"
    npm_run --prefix "$FRONTEND_DIR" ci --ignore-scripts --no-fund --no-audit
  fi
  if [ ! -f "$FRONTEND_ENV_FILE" ]; then
    printf '# Written by scripts/dev.sh (git-ignored).\nNEXT_PUBLIC_API_BASE_URL=%s\n' "$API_URL" >"$FRONTEND_ENV_FILE"
    ok "Created $FRONTEND_ENV_FILE (NEXT_PUBLIC_API_BASE_URL=$API_URL)"
  fi
}

# --- user-secrets -------------------------------------------------------------------------------

user_secret_get() {
  "$DOTNET" user-secrets list --project "$API_PROJECT" 2>/dev/null | tr -d '\r' |
    awk -v key="$1" 'index($0, key " = ") == 1 { print substr($0, length(key) + 4); exit }'
}

json_escape() { printf '%s' "$1" | sed -e 's/\\/\\\\/g' -e 's/"/\\"/g'; }

# Values go through stdin as JSON (merged into the store), so the API key never appears in a process list.
user_secrets_set_llm() {
  printf '{"Llm":{"Endpoint":"%s","DefaultModel":"%s","ApiKey":"%s"}}' \
    "$(json_escape "$1")" "$(json_escape "$2")" "$(json_escape "$3")" |
    "$DOTNET" user-secrets set --project "$API_PROJECT" >/dev/null
}

llm_configured() {
  [ -n "$(user_secret_get Llm:Endpoint)" ] && [ -n "$(user_secret_get Llm:DefaultModel)" ] &&
    [ -n "$(user_secret_get Llm:ApiKey)" ]
}

warn_if_llm_missing() {
  if ! llm_configured; then
    warn "The LiteLLM proxy is not configured: the API starts, but model calls stay disabled. Run ./scripts/dev.sh secrets."
  fi
}

# --- commands -----------------------------------------------------------------------------------

cmd_secrets() {
  [ -t 0 ] || die "secrets is interactive: run it in a terminal."
  ensure_dotnet
  step "LiteLLM proxy settings (dotnet user-secrets of $API_PROJECT, shared with the CLI)"

  local endpoint model key current_endpoint current_model current_key
  current_endpoint="$(user_secret_get Llm:Endpoint)"
  current_model="$(user_secret_get Llm:DefaultModel)"
  current_key="$(user_secret_get Llm:ApiKey)"

  read -r -p "  Endpoint (base URL of the proxy)${current_endpoint:+ [$current_endpoint]}: " endpoint
  endpoint="${endpoint:-$current_endpoint}"
  case "$endpoint" in
    https://* | http://localhost* | http://127.0.0.1*) ;;
    *) die "The endpoint must be an https:// URL (http:// only for localhost)." ;;
  esac

  read -r -p "  Default model${current_model:+ [$current_model]}: " model
  model="${model:-$current_model}"
  [ -n "$model" ] || die "A default model is required."

  read -r -s -p "  API key${current_key:+ [keep the current one]}: " key
  printf '\n'
  key="${key:-$current_key}"
  [ -n "$key" ] || die "An API key is required."
  case "$endpoint$model$key" in
    *[[:cntrl:]]*) die "Values must not contain control characters." ;;
  esac

  user_secrets_set_llm "$endpoint" "$model" "$key"
  ok "Saved (the key is not shown; read the store with: dotnet user-secrets list --project $API_PROJECT)"
}

build_backend() {
  ensure_dotnet
  step "Backend: restore"
  "$DOTNET" restore "$SOLUTION"
  step "Backend: build (Release, warnings as errors)"
  "$DOTNET" build "$SOLUTION" --configuration Release --no-restore -warnaserror
}

test_backend() {
  build_backend
  step "Backend: tests (unit + integration, with coverage)"
  "$DOTNET" test --solution "$SOLUTION" \
    --configuration Release \
    --no-build \
    --coverage \
    --coverage-output-format cobertura \
    --results-directory backend/TestResults
}

build_frontend() {
  ensure_frontend_deps
  step "Frontend: production build"
  npm_run --prefix "$FRONTEND_DIR" run build
}

test_frontend() {
  ensure_frontend_deps
  step "Frontend: ESLint"
  npm_run --prefix "$FRONTEND_DIR" run lint
  step "Frontend: type-check"
  npm_run --prefix "$FRONTEND_DIR" run typecheck
  step "Frontend: unit and component tests (Vitest, with coverage)"
  npm_run --prefix "$FRONTEND_DIR" run test:coverage
}

# `build` and `test` take an optional part; without it they run both, backend first.
cmd_for_parts() {
  local backend="$1" frontend="$2" part="${3:-all}"
  case "$part" in
    all)
      "$backend"
      "$frontend"
      ;;
    backend) "$backend" ;;
    frontend) "$frontend" ;;
    *) die "Unknown part: $part (use backend or frontend)." ;;
  esac
}

api_command() {
  if [ "$1" = true ]; then
    "$DOTNET" watch run --project "$API_PROJECT" --launch-profile http
  else
    "$DOTNET" run --project "$API_PROJECT" --launch-profile http
  fi
}

cmd_api() {
  local watch=false
  for arg in "$@"; do
    case "$arg" in
      --watch) watch=true ;;
      *) die "Unknown option for api: $arg" ;;
    esac
  done

  ensure_dotnet
  warn_if_llm_missing
  step "API on $API_URL (Swagger UI: $API_URL/swagger). Ctrl+C stops it."
  api_command "$watch"
}

cmd_web() {
  ensure_dotnet
  ensure_frontend_deps
  step "Web app on $WEB_URL (calls the API on $API_URL). Ctrl+C stops it."
  npm_run --prefix "$FRONTEND_DIR" run dev
}

# Prefixes every line of a service's output, so both logs can share the terminal.
prefix() { sed -u "s/^/$1 /"; }

cmd_dev() {
  ensure_dotnet
  ensure_frontend_deps
  warn_if_llm_missing

  step "Starting the API ($API_URL) and the web app ($WEB_URL). Ctrl+C stops both."
  trap 'trap - INT TERM EXIT; kill $(jobs -p) 2>/dev/null; wait 2>/dev/null; printf "\n"; ok "Stopped"' INT TERM EXIT
  api_command false 2>&1 | prefix "${C_BLUE}[api]${C_RESET}" &
  npm_run --prefix "$FRONTEND_DIR" run dev 2>&1 | prefix "${C_MAGENTA}[web]${C_RESET}" &
  wait
}

cmd_cli() {
  ensure_dotnet
  exec "$DOTNET" run --project "$CLI_PROJECT" -- "$@"
}

answers() { "$CURL" -fsS -o /dev/null --max-time 3 "$1" 2>/dev/null; }

cmd_status() {
  ensure_dotnet
  step "Tools"
  ok ".NET SDK $("$DOTNET" --version | tr -d '\r') ($DOTNET)"
  local node
  node="$(node_version || true)"
  if [ -n "$node" ]; then ok "Node.js $node"; else warn "Node.js not found (needed for the web app)"; fi

  step "LiteLLM proxy settings (user-secrets)"
  local value
  for key in Llm:Endpoint Llm:DefaultModel; do
    value="$(user_secret_get "$key")"
    if [ -n "$value" ]; then ok "$key = $value"; else warn "$key is missing"; fi
  done
  if [ -n "$(user_secret_get Llm:ApiKey)" ]; then ok "Llm:ApiKey is set"; else warn "Llm:ApiKey is missing"; fi

  step "Services"
  local health
  if health="$("$CURL" -fsS --max-time 3 "$API_URL/api/health" 2>/dev/null)"; then
    ok "API $API_URL answers: $health"
  else
    printf '  %s-%s API %s is not running\n' "$C_DIM" "$C_RESET" "$API_URL"
  fi
  if answers "$WEB_URL"; then
    ok "Web app $WEB_URL answers"
  else
    printf '  %s-%s Web app %s is not running\n' "$C_DIM" "$C_RESET" "$WEB_URL"
  fi
}

main() {
  local command="${1:-dev}"
  [ $# -eq 0 ] || shift
  case "$command" in
    dev) cmd_dev ;;
    api) cmd_api "$@" ;;
    web) cmd_web ;;
    cli) cmd_cli "$@" ;;
    secrets) cmd_secrets ;;
    build) cmd_for_parts build_backend build_frontend "$@" ;;
    test) cmd_for_parts test_backend test_frontend "$@" ;;
    status) cmd_status ;;
    -h | --help | help) usage ;;
    *)
      usage >&2
      die "Unknown command: $command"
      ;;
  esac
}

main "$@"
