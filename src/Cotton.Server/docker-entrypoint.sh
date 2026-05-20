#!/usr/bin/env sh
set -eu

log() {
    printf '%s\n' "cotton-entrypoint: $*" >&2
}

: "${COTTON_STORAGE_PATH:=/app/files}"
: "${COTTON_PERMISSION_FIX:=auto}"
: "${COTTON_RUN_AS:=app}"

if [ "$#" -eq 0 ]; then
    set -- dotnet Cotton.Server.dll
fi

if [ "$(id -u)" -ne 0 ]; then
    exec "$@"
fi

if id "$COTTON_RUN_AS" >/dev/null 2>&1; then
    run_as="$COTTON_RUN_AS"
    run_uid="$(id -u "$COTTON_RUN_AS")"
    run_gid="$(id -g "$COTTON_RUN_AS")"
else
    case "$COTTON_RUN_AS" in
        *:*)
            run_as="$COTTON_RUN_AS"
            run_uid="${COTTON_RUN_AS%%:*}"
            run_gid="${COTTON_RUN_AS#*:}"
            ;;
        *[!0-9]*|"")
            if [ "$COTTON_RUN_AS" != "app" ]; then
                log "invalid COTTON_RUN_AS=$COTTON_RUN_AS; expected an existing user, numeric uid, or uid:gid"
                exit 64
            fi
            run_uid="${APP_UID:-1654}"
            run_gid="${APP_GID:-$run_uid}"
            run_as="$run_uid:$run_gid"
            ;;
        *)
            run_uid="$COTTON_RUN_AS"
            run_gid="${APP_GID:-$run_uid}"
            run_as="$run_uid:$run_gid"
            ;;
    esac
fi

ownership="$run_uid:$run_gid"
marker="$COTTON_STORAGE_PATH/.cotton-permissions-v1"

can_write_storage() {
    gosu "$run_as" sh -c '
        set -eu
        storage_path="$1"
        tmp_path="$storage_path/tmp"
        test -d "$storage_path"
        test -d "$tmp_path"
        test_file="$tmp_path/.cotton-write-test-$$"
        : > "$test_file"
        rm -f "$test_file"
    ' sh "$COTTON_STORAGE_PATH"
}

repair_storage_permissions() {
    log "repairing storage ownership for $COTTON_STORAGE_PATH -> $ownership"
    chown -R "$ownership" "$COTTON_STORAGE_PATH"
    gosu "$run_as" sh -c '
        set -eu
        marker="$1"
        umask 077
        printf "%s\n" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" > "$marker"
    ' sh "$marker"
}

mkdir -p "$COTTON_STORAGE_PATH/tmp"

case "$COTTON_PERMISSION_FIX" in
    auto)
        if [ ! -f "$marker" ] || ! can_write_storage; then
            repair_storage_permissions
        fi
        ;;
    always)
        repair_storage_permissions
        ;;
    never)
        if ! can_write_storage; then
            log "warning: $COTTON_STORAGE_PATH is not writable by $run_as; Cotton may fail when using filesystem storage"
        fi
        ;;
    *)
        log "invalid COTTON_PERMISSION_FIX=$COTTON_PERMISSION_FIX; expected auto, always, or never"
        exit 64
        ;;
esac

exec gosu "$run_as" "$@"
