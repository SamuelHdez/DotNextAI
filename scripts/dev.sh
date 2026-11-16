#!/usr/bin/env bash
# DotNextAI local development orchestrator (Git Bash on Windows, WSL, macOS, Linux; bash 3.2+).
#
#   ./scripts/dev.sh                 Start the API (http://localhost:5100, Swagger UI at /swagger)
#   ./scripts/dev.sh api [--watch]   Same; --watch runs it under dotnet watch (hot reload)
#   ./scripts/dev.sh cli [args...]   Run the terminal chatbot
#   ./scripts/dev.sh secrets         Set the LiteLLM proxy endpoint, default model and API key (user-secrets)
#   ./scripts/dev.sh build           Restore and build the backend with analyzer warnings as errors (as in CI)
#   ./scripts/dev.sh test            Build and run the unit and integration tests with coverage (as in CI)
#   ./scripts/dev.sh status          Show the .NET SDK, the LLM settings (never the key) and whether the API answers
#
# Secrets live in `dotnet user-secrets` (one store shared by the API and the CLI), never in tracked files.

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# Relative paths from the repository root: they also work with the Windows SDK called from WSL (dotnet.exe).
cd "$ROOT"

SOLUTION="backend/DotNextAI.slnx"
API_PROJECT="backend/src/DotNextAI.Api"
CLI_PROJECT="backend/src/DotNextAI.Cli"
API_URL="http://localhost:5100"

if [ -t 1 ]; then
  C_RESET=$'\033[0m' C_BOLD=$'\033[1m' C_DIM=$'\033[2m' C_RED=$'\033[31m' C_GREEN=$'\033[32m' C_YELLOW=$'\033[33m'
else
  C_RESET="" C_BOLD="" C_DIM="" C_RED="" C_GREEN="" C_YELLOW=""
fi

step() { printf '%s▸ %s%s\n' "$C_BOLD" "$*" "$C_RESET"; }
ok() { printf '  %s✓%s %s\n' "$C_GREEN" "$C_RESET" "$*"; }
warn() { printf '  %s!%s %s\n' "$C_YELLOW" "$C_RESET" "$*" >&2; }
die() {
  printf '%s✗ %s%s\n' "$C_RED" "$*" "$C_RESET" >&2
  exit 1
}

usage() { sed -n '2,13p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; }

# --- tools --------------------------------------------------------------------------------------

DOTNET=""
CURL="curl"

ensure_dotnet() {
  if command -v dotnet >/dev/null 2>&1; then
    DOTNET="dotnet"
  elif command -v dotnet.exe >/dev/null 2>&1; then
    # WSL without a Linux SDK: use the Windows one. The API then listens on Windows' localhost, which only the
    # Windows curl reaches.
    DOTNET="dotnet.exe"
    command -v curl.exe >/dev/null 2>&1 && CURL="curl.exe"
  else
    die "The .NET SDK 10 is required (https://dot.net). See README > Prerequisites."
  fi
  local sdks
  sdks="$("$DOTNET" --list-sdks 2>/dev/null | tr -d '\r')"
  printf '%s\n' "$sdks" | grep -q '^10\.' || die ".NET SDK 10 not found (installed: ${sdks:-none})."
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

cmd_build() {
  ensure_dotnet
  step "Restore"
  "$DOTNET" restore "$SOLUTION"
  step "Build (Release, warnings as errors)"
  "$DOTNET" build "$SOLUTION" --configuration Release --no-restore -warnaserror
}

cmd_test() {
  cmd_build
  step "Tests (unit + integration, with coverage)"
  "$DOTNET" test --solution "$SOLUTION" \
    --configuration Release \
    --no-build \
    --coverage \
    --coverage-output-format cobertura \
    --results-directory backend/TestResults
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
  if ! llm_configured; then
    warn "The LiteLLM proxy is not configured: the API starts, but model calls stay disabled. Run ./scripts/dev.sh secrets."
  fi

  step "API on $API_URL (Swagger UI: $API_URL/swagger). Ctrl+C stops it."
  if [ "$watch" = true ]; then
    exec "$DOTNET" watch run --project "$API_PROJECT" --launch-profile http
  fi
  exec "$DOTNET" run --project "$API_PROJECT" --launch-profile http
}

cmd_cli() {
  ensure_dotnet
  exec "$DOTNET" run --project "$CLI_PROJECT" -- "$@"
}

cmd_status() {
  ensure_dotnet
  step "Tools"
  ok ".NET SDK $("$DOTNET" --version | tr -d '\r') ($DOTNET)"

  step "LiteLLM proxy settings (user-secrets)"
  local value
  for key in Llm:Endpoint Llm:DefaultModel; do
    value="$(user_secret_get "$key")"
    if [ -n "$value" ]; then ok "$key = $value"; else warn "$key is missing"; fi
  done
  if [ -n "$(user_secret_get Llm:ApiKey)" ]; then ok "Llm:ApiKey is set"; else warn "Llm:ApiKey is missing"; fi

  step "API"
  local health
  if health="$("$CURL" -fsS --max-time 3 "$API_URL/api/health" 2>/dev/null)"; then
    ok "$API_URL answers: $health"
  else
    printf '  %s-%s %s is not running\n' "$C_DIM" "$C_RESET" "$API_URL"
  fi
}

main() {
  local command="${1:-api}"
  [ $# -eq 0 ] || shift
  case "$command" in
    api) cmd_api "$@" ;;
    cli) cmd_cli "$@" ;;
    secrets) cmd_secrets ;;
    build) cmd_build ;;
    test) cmd_test ;;
    status) cmd_status ;;
    -h | --help | help) usage ;;
    *)
      usage >&2
      die "Unknown command: $command"
      ;;
  esac
}

main "$@"
