#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# Keycloak entrypoint wrapper: substitute the ${...} client-secret placeholders
# in the committed realm export (deploy/keycloak/tempo-reports-realm.json — which
# deliberately carries NO real secrets) from environment variables, write the
# result into Keycloak's import directory, then hand off to kc.sh.
#
# Kept as a mounted script (not inline compose YAML) so the shell $-handling is
# not entangled with docker-compose variable interpolation.
# ---------------------------------------------------------------------------
set -euo pipefail

SRC="/opt/keycloak/data/import-src/tempo-reports-realm.json"
DEST_DIR="/opt/keycloak/data/import"
DEST="${DEST_DIR}/tempo-reports-realm.json"

mkdir -p "${DEST_DIR}"

# The backslash keeps the LEFT-hand pattern a literal ${VAR} (the placeholder text
# in the file); the RIGHT-hand ${VAR:-default} is expanded from the container env.
sed \
  -e "s|\${TEMPO_REPORT_WEB_SECRET}|${TEMPO_REPORT_WEB_SECRET:-changeme-web-secret}|g" \
  -e "s|\${TEMPO_REPORT_M2M_SECRET}|${TEMPO_REPORT_M2M_SECRET:-changeme-m2m-secret}|g" \
  "${SRC}" > "${DEST}"

echo "keycloak-import: realm written to ${DEST} (secrets substituted from env)"

exec /opt/keycloak/bin/kc.sh "$@"
