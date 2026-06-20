#!/usr/bin/env bash
#
# Najde a ukončí procesy běžící na portech demo API a demo WASM.
#
#   API : 5100 (https) + 5101 (http)
#   WASM: 7106 (https) + 5010 (http)
#
# Nejdřív zkusí SIGTERM (graceful), po krátké pauze zbylé dorazí SIGKILL.
#
set -uo pipefail

PORTS=(7106 5010 5100 5101)

# Vrátí PIDy naslouchající na daném portu (lsof, fallback fuser).
pids_on_port() {
    local port="$1"
    if command -v lsof >/dev/null 2>&1; then
        lsof -ti "tcp:${port}" -sTCP:LISTEN 2>/dev/null || true
    elif command -v fuser >/dev/null 2>&1; then
        fuser "${port}/tcp" 2>/dev/null || true
    fi
}

# Posbírej unikátní PIDy přes všechny porty.
all_pids=()
for port in "${PORTS[@]}"; do
    for pid in $(pids_on_port "$port"); do
        all_pids+=("$pid")
        echo "==> Port ${port}: PID ${pid} ($(ps -p "$pid" -o comm= 2>/dev/null || echo '?'))"
    done
done

if [ "${#all_pids[@]}" -eq 0 ]; then
    echo "==> Na portech ${PORTS[*]} nic neběží."
    exit 0
fi

# Deduplikace.
mapfile -t unique_pids < <(printf '%s\n' "${all_pids[@]}" | sort -u)

echo "==> Posílám SIGTERM: ${unique_pids[*]}"
for pid in "${unique_pids[@]}"; do
    kill "$pid" 2>/dev/null || true
done

# Dej procesům chvíli na čistý odchod.
alive=()
for _ in 1 2 3 4 5 6; do
    sleep 0.5
    alive=()
    for pid in "${unique_pids[@]}"; do
        kill -0 "$pid" 2>/dev/null && alive+=("$pid")
    done
    [ "${#alive[@]}" -eq 0 ] && break
done

# Co zbylo, dorazit.
if [ "${#alive[@]}" -ne 0 ]; then
    echo "==> Stále běží, posílám SIGKILL: ${alive[*]}"
    for pid in "${alive[@]}"; do
        kill -9 "$pid" 2>/dev/null || true
    done
fi

echo "==> Hotovo."
