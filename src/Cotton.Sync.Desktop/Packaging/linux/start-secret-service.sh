#!/usr/bin/env bash

if [ "${BASH_SOURCE[0]}" = "$0" ]; then
  echo "start-secret-service.sh must be sourced so GNOME keyring environment variables stay in the current shell." >&2
  exit 2
fi

command -v gnome-keyring-daemon >/dev/null
command -v secret-tool >/dev/null

if [ -z "${DBUS_SESSION_BUS_ADDRESS:-}" ]; then
  echo "DBUS_SESSION_BUS_ADDRESS is required before starting Linux Secret Service." >&2
  return 1
fi

keyring_password="${COTTON_SYNC_TEST_KEYRING_PASSWORD:-cotton-sync-ci}"

unlock_output="$(printf '%s\n' "$keyring_password" | gnome-keyring-daemon --unlock --components=secrets)"
if [ -n "$unlock_output" ]; then
  eval "$unlock_output"
fi

start_output="$(gnome-keyring-daemon --start --components=secrets)"
if [ -n "$start_output" ]; then
  eval "$start_output"
fi

probe_id="cotton-sync-ci-$$-${RANDOM:-0}"
probe_secret="cotton-sync-secret-service-probe"
printf '%s' "$probe_secret" | secret-tool store \
  --label="Cotton Sync CI Secret Service probe" \
  application cotton-sync-desktop \
  purpose ci-secret-service-probe \
  id "$probe_id"

lookup_secret="$(secret-tool lookup \
  application cotton-sync-desktop \
  purpose ci-secret-service-probe \
  id "$probe_id")"

secret-tool clear \
  application cotton-sync-desktop \
  purpose ci-secret-service-probe \
  id "$probe_id" >/dev/null

if [ "$lookup_secret" != "$probe_secret" ]; then
  echo "Linux Secret Service probe returned an unexpected value." >&2
  return 1
fi
