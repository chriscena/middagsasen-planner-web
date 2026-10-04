#!/usr/bin/env bash
# Kjører sqlpackage med retry KUN når Azure SQL avviser runnerens IP (feil 40615),
# typisk fordi en nyopprettet brannmurregel ikke er aktiv ennå.
# Alle andre feil avslutter straks med sqlpackage sin exit-kode.
# Bruk: bash .github/scripts/sqlpackage-retry.sh <sqlpackage-argumenter...>
# Argumentene skrives aldri ut (de inneholder access token).
set -euo pipefail

attempts="${SQLPACKAGE_RETRY_ATTEMPTS:-8}"
delay="${SQLPACKAGE_RETRY_DELAY:-15}"

log="$(mktemp)"
trap 'rm -f "$log"' EXIT

attempt=1
while true; do
  set +e
  sqlpackage "$@" 2>&1 | tee "$log"
  code="${PIPESTATUS[0]}"
  set -e

  if [ "$code" -eq 0 ]; then
    exit 0
  fi

  if ! grep -qE '40615|is not allowed to access the server' "$log"; then
    exit "$code"
  fi

  if [ "$attempt" -ge "$attempts" ]; then
    echo "::error::sqlpackage fikk fortsatt brannmurfeil (40615) etter $attempts forsøk."
    exit "$code"
  fi

  echo "::warning::Brannmurregelen er ikke aktiv ennå (40615). Forsøk $attempt av $attempts feilet, prøver igjen om ${delay}s."
  sleep "$delay"
  attempt=$((attempt + 1))
done
