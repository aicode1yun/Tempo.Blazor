#!/usr/bin/env bash
#
# Spustí HTTPS demo API a HTTPS demo WASM zároveň.
#
#   API : https://localhost:5100  (src/Tempo.Blazor.Demo.Api)
#   WASM: https://localhost:7106  (src/Tempo.Blazor.Demo)
#
# Ctrl+C ukončí oba procesy.
#
set -euo pipefail

# Kořen repozitáře (skript funguje odkudkoli).
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

API_PROJECT="src/Tempo.Blazor.Demo.Api"
API_PROFILE="Tempo.Blazor.Demo.Api"
WASM_PROJECT="src/Tempo.Blazor.Demo"
WASM_PROFILE="https"

pids=()

cleanup() {
    echo
    echo "==> Ukončuji demo procesy…"
    for pid in "${pids[@]}"; do
        kill "$pid" 2>/dev/null || true
    done
    wait 2>/dev/null || true
}
trap cleanup EXIT INT TERM

echo "==> Spouštím HTTPS demo API   (https://localhost:5100)"
dotnet run --project "$API_PROJECT" --launch-profile "$API_PROFILE" &
pids+=("$!")

echo "==> Spouštím HTTPS demo WASM  (https://localhost:7106)"
dotnet run --project "$WASM_PROJECT" --launch-profile "$WASM_PROFILE" &
pids+=("$!")

echo "==> Oba procesy běží. Ctrl+C je ukončí."

# Čekej, dokud kterýkoli z procesů neskončí; pak trap ukončí zbytek.
wait -n
