#!/usr/bin/env bash
# Ejecuta Compile, Test/Smoke y Fuzz-lean de RESTler contra la API efímera.
# Las operaciones destructivas (logout y borrado de cuenta) se ejecutan en
# pasadas separadas para que no invaliden el token de las demás operaciones.
# No escribe secretos en artefactos: el reporte solo copia logs saneados.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$ROOT"

WORK="$ROOT/.restler-work"
STATE="$ROOT/.restler-state"
ARTIFACTS="$ROOT/artifacts/restler"
RESTLER_BIN="${RESTLER_BIN:-${RUNNER_TEMP:-/tmp}/restler-bin}"
RESTLER_DLL="$RESTLER_BIN/restler/Restler.dll"
PYTHON="${PYTHON:-python3}"

mkdir -p "$WORK" "$STATE" "$ARTIFACTS" "$ROOT/.dast-mail"
rm -rf "$WORK" "$STATE"
mkdir -p "$WORK" "$STATE"

fail_fast() {
  local message="$1"
  printf '%s\n' "$message" > "$WORK/prepare-error.txt"
  "$PYTHON" "$ROOT/tools/ci/restler/restler_report.py" || true
  exit 1
}

if ! command -v "$PYTHON" >/dev/null 2>&1; then
  fail_fast "No se encontró el intérprete Python configurado."
fi
if [[ ! -f "$RESTLER_DLL" ]]; then
  fail_fast "No se encontró RESTler en $RESTLER_DLL."
fi

if ! "$PYTHON" "$ROOT/tools/ci/restler/restler_prepare.py" > "$WORK/prepare-console.txt" 2>&1; then
  cp "$WORK/prepare-console.txt" "$WORK/prepare-error.txt"
  "$PYTHON" "$ROOT/tools/ci/restler/restler_report.py" || true
  exit 1
fi

run_restler() {
  local name="$1"
  local duration="$2"
  shift 2
  local code=0
  (
    cd "$WORK"
    timeout --signal=INT --kill-after=30s "$duration" \
      dotnet "$RESTLER_DLL" "$@" \
      > "$WORK/${name}-console.txt" 2>&1 || code=$?
    printf '%s\n' "$code" > "$WORK/${name}-exit-code.txt"
  )
  return 0
}

move_results() {
  local name="$1"
  if [[ -d "$WORK/Test" ]]; then
    rm -rf "$WORK/$name"
    mv "$WORK/Test" "$WORK/$name"
  fi
}

run_restler "compile" "5m" compile "$STATE/compile-config.json"

if [[ -f "$WORK/Compile/grammar.py" ]]; then
  rm -rf "$WORK/Test"
  run_restler "test-main" "10m" test \
    --grammar_file "$WORK/Compile/grammar.py" \
    --dictionary_file "$WORK/Compile/dict.json" \
    --settings "$STATE/test-settings.json" \
    --target_ip 127.0.0.1 --target_port 5088 --no_ssl
  move_results "TestMain"

  rm -rf "$WORK/Test"
  run_restler "test-logout" "5m" test \
    --grammar_file "$WORK/Compile/grammar.py" \
    --dictionary_file "$WORK/Compile/dict.json" \
    --settings "$STATE/logout-settings.json" \
    --target_ip 127.0.0.1 --target_port 5088 --no_ssl
  move_results "TestLogout"

  rm -rf "$WORK/Test"
  run_restler "test-delete" "5m" test \
    --grammar_file "$WORK/Compile/grammar.py" \
    --dictionary_file "$WORK/Compile/dict.json" \
    --settings "$STATE/delete-settings.json" \
    --target_ip 127.0.0.1 --target_port 5088 --no_ssl
  move_results "TestDelete"

  rm -rf "$WORK/FuzzLean"
  run_restler "fuzz-lean" "15m" fuzz-lean \
    --grammar_file "$WORK/Compile/grammar.py" \
    --dictionary_file "$WORK/Compile/dict.json" \
    --settings "$STATE/fuzz-settings.json" \
    --target_ip 127.0.0.1 --target_port 5088 --no_ssl
else
  printf '1\n' > "$WORK/test-main-exit-code.txt"
  printf '1\n' > "$WORK/test-logout-exit-code.txt"
  printf '1\n' > "$WORK/test-delete-exit-code.txt"
  printf '1\n' > "$WORK/fuzz-lean-exit-code.txt"
  printf 'RESTler compile no generó grammar.py.\n' > "$WORK/prepare-error.txt"
fi

"$PYTHON" "$ROOT/tools/ci/restler/restler_report.py"
