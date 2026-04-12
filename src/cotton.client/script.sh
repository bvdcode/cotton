#!/usr/bin/env bash

set -euo pipefail

SCRIPT_NAME="$(basename "$0")"

STATE_DIR="${XDG_STATE_HOME:-$HOME/.local/state}/cotton-upload"
COOKIE_JAR=""
TMP_DIR=""

ACCESS_TOKEN=""
API_BASE=""
BASE_URL=""
DEST_HOST=""
DEST_PATH=""
SOURCE_PATH=""
ROOT_NODE_ID=""
MAX_CHUNK_BYTES=0
SERVER_HASH_ALGORITHM=""
HASH_CMD=""
CHUNK_MIN_BYTES=$((256 * 1024))
PARALLEL_CHUNKS=4

CLI_USERNAME=""
CLI_PASSWORD=""
CLI_TWO_FACTOR=""
PERSIST_AUTH=0
COOKIE_JAR_PATH=""

MANIFEST_FILE=""
LAST_FILE_HASH=""
LAST_FILE_SKIPPED=0

TOTAL_FILES=0
COMPLETED_FILES=0
SKIPPED_FILES=0
TOTAL_BYTES_ALL=0
UPLOADED_BYTES_ALL=0
PROCESSED_BYTES_ALL=0
UPLOAD_STARTED_AT=0
CURRENT_FILE_INDEX=0
PROGRESS_ACTIVE=0

API_STATUS=""
API_BODY=""

declare -A PATH_ID_CACHE=()
declare -A NODE_CHILDREN_LOADED=()
declare -A NODE_CHILD_ENTRY_NAMES=()

usage() {
  cat <<EOF
Usage:
  $SCRIPT_NAME --copy <local_path> <host/path> [options]

Example:
  $SCRIPT_NAME --copy /data/archive cotton.your.domain/backups

Options:
  --username <value>      Login username
  --password <value>      Login password
  --two-factor <value>    Optional 2FA code
  --parallel <num>        Parallel chunk workers per file (default: 4)
  --persist-auth          Persist refresh cookie between runs
  --cookie-jar <path>     Custom cookie file path (implies --persist-auth)

Notes:
  - The script automatically uses https://<host>
  - host/path means path from remote root (e.g. /backups)
  - Login is interactive if username/password are not provided
  - Access token is never persisted to disk
EOF
}

log() {
  if [[ "$PROGRESS_ACTIVE" -eq 1 ]]; then
    printf '\n'
    PROGRESS_ACTIVE=0
  fi
  printf '[%s] %s\n' "$(date '+%H:%M:%S')" "$*"
}

die() {
  echo "Error: $*" >&2
  exit 1
}

require_cmd() {
  local cmd
  for cmd in "$@"; do
    command -v "$cmd" >/dev/null 2>&1 || die "Required command not found: $cmd"
  done
}

ensure_state_dirs() {
  umask 077
  TMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/cotton-upload.XXXXXX")"

  if (( PERSIST_AUTH == 1 )); then
    local target_cookie_jar
    target_cookie_jar="${COOKIE_JAR_PATH:-$STATE_DIR/cookies.txt}"
    mkdir -p "$(dirname "$target_cookie_jar")"
    touch "$target_cookie_jar"
    chmod 600 "$target_cookie_jar"
    COOKIE_JAR="$target_cookie_jar"
  else
    COOKIE_JAR="$TMP_DIR/cookies.txt"
    : > "$COOKIE_JAR"
    chmod 600 "$COOKIE_JAR"
  fi
}

cleanup_runtime() {
  if [[ -n "$TMP_DIR" && -d "$TMP_DIR" ]]; then
    rm -rf "$TMP_DIR"
  fi
}

normalize_remote_path() {
  local p="$1"
  p="${p#/}"
  p="${p%/}"
  printf '%s' "$p"
}

human_bytes() {
  local bytes="$1"
  local sign=""

  if (( bytes < 0 )); then
    sign="-"
    bytes=$(( -bytes ))
  fi

  if (( bytes < 1024 )); then
    printf '%s%d B' "$sign" "$bytes"
    return 0
  fi

  if (( bytes < 1024 * 1024 )); then
    awk -v b="$bytes" -v s="$sign" 'BEGIN { printf "%s%.1f KiB", s, b/1024 }'
    return 0
  fi

  if (( bytes < 1024 * 1024 * 1024 )); then
    awk -v b="$bytes" -v s="$sign" 'BEGIN { printf "%s%.1f MiB", s, b/(1024*1024) }'
    return 0
  fi

  if (( bytes < 1024 * 1024 * 1024 * 1024 )); then
    awk -v b="$bytes" -v s="$sign" 'BEGIN { printf "%s%.1f GiB", s, b/(1024*1024*1024) }'
    return 0
  fi

  awk -v b="$bytes" -v s="$sign" 'BEGIN { printf "%s%.1f TiB", s, b/(1024*1024*1024*1024) }'
}

human_seconds() {
  local total="$1"

  if (( total < 0 )); then
    total=0
  fi

  local h=$(( total / 3600 ))
  local m=$(( (total % 3600) / 60 ))
  local s=$(( total % 60 ))

  if (( h > 0 )); then
    printf '%02d:%02d:%02d' "$h" "$m" "$s"
  else
    printf '%02d:%02d' "$m" "$s"
  fi
}

percent_1dp() {
  local done="$1"
  local total="$2"
  if (( total <= 0 )); then
    printf '0.0'
    return 0
  fi
  awk -v d="$done" -v t="$total" 'BEGIN { printf "%.1f", (d * 100.0) / t }'
}

progress_bar() {
  local done="$1"
  local total="$2"
  local width="${3:-24}"
  local filled=0

  if (( total > 0 )); then
    filled=$(( done * width / total ))
  fi

  if (( filled < 0 )); then
    filled=0
  fi
  if (( filled > width )); then
    filled="$width"
  fi

  local empty=$(( width - filled ))
  local left right
  left="$(printf '%*s' "$filled" '' | tr ' ' '#')"
  right="$(printf '%*s' "$empty" '' | tr ' ' '-')"
  printf '%s%s' "$left" "$right"
}

shorten_label() {
  local label="$1"
  local max_len="${2:-52}"
  local len
  len=${#label}

  if (( len <= max_len )); then
    printf '%s' "$label"
    return 0
  fi

  local head_len=$(( (max_len - 3) / 2 ))
  local tail_len=$(( max_len - 3 - head_len ))
  local tail_start=$(( len - tail_len ))

  printf '%s...%s' "${label:0:head_len}" "${label:tail_start:tail_len}"
}

finish_progress_line() {
  if [[ "$PROGRESS_ACTIVE" -eq 1 ]]; then
    printf '\n'
    PROGRESS_ACTIVE=0
  fi
}

render_progress() {
  local file_label="$1"
  local file_done="$2"
  local file_total="$3"
  local chunk_bytes="$4"

  local bar
  local file_pct
  local total_pct
  local elapsed
  local speed=0
  local eta=0
  local processed_h
  local total_h
  local file_done_h
  local file_total_h
  local speed_h
  local eta_h
  local short_label

  file_pct="$(percent_1dp "$file_done" "$file_total")"
  total_pct="$(percent_1dp "$PROCESSED_BYTES_ALL" "$TOTAL_BYTES_ALL")"
  bar="$(progress_bar "$PROCESSED_BYTES_ALL" "$TOTAL_BYTES_ALL" 16)"

  elapsed=$(( $(date +%s) - UPLOAD_STARTED_AT ))
  if (( elapsed > 0 )); then
    speed=$(( PROCESSED_BYTES_ALL / elapsed ))
  fi

  if (( speed > 0 && TOTAL_BYTES_ALL > PROCESSED_BYTES_ALL )); then
    eta=$(( (TOTAL_BYTES_ALL - PROCESSED_BYTES_ALL) / speed ))
  fi

  processed_h="$(human_bytes "$PROCESSED_BYTES_ALL")"
  total_h="$(human_bytes "$TOTAL_BYTES_ALL")"
  file_done_h="$(human_bytes "$file_done")"
  file_total_h="$(human_bytes "$file_total")"
  speed_h="$(human_bytes "$speed")/s"
  eta_h="$(human_seconds "$eta")"
  short_label="$(shorten_label "$file_label" 52)"

  printf '\r\033[2K[%s] [%s] %s%% %s/%s | f %s%% | %d/%d | %s | eta %s | %s' \
    "$(date '+%H:%M:%S')" \
    "$bar" \
    "$total_pct" \
    "$processed_h" \
    "$total_h" \
    "$file_pct" \
    "$CURRENT_FILE_INDEX" \
    "$TOTAL_FILES" \
    "$speed_h" \
    "$eta_h" \
    "$short_label"

  PROGRESS_ACTIVE=1
}

json_escape() {
  local s="$1"
  s="${s//\\/\\\\}"
  s="${s//\"/\\\"}"
  s="${s//$'\n'/\\n}"
  s="${s//$'\r'/\\r}"
  s="${s//$'\t'/\\t}"
  printf '%s' "$s"
}

json_get_string() {
  local json="$1"
  local key="$2"
  printf '%s' "$json" \
    | tr -d '\n' \
    | sed -n "s/.*\"$key\"[[:space:]]*:[[:space:]]*\"\\([^\"]*\\)\".*/\\1/p" \
    | head -n 1
}

json_get_number() {
  local json="$1"
  local key="$2"
  printf '%s' "$json" \
    | tr -d '\n' \
    | sed -n "s/.*\"$key\"[[:space:]]*:[[:space:]]*\\([0-9][0-9]*\\).*/\\1/p" \
    | head -n 1
}

urlencode() {
  local raw="$1"
  local out=""
  local i c hex
  local LC_ALL=C

  for ((i = 0; i < ${#raw}; i += 1)); do
    c="${raw:i:1}"
    case "$c" in
      [a-zA-Z0-9.~_-])
        out+="$c"
        ;;
      *)
        printf -v hex '%%%02X' "'$c"
        out+="$hex"
        ;;
    esac
  done

  printf '%s' "$out"
}

encode_resolver_path() {
  local path="$1"
  local out=""
  local seg
  local enc
  local IFS='/'
  read -r -a parts <<< "$path"

  for seg in "${parts[@]}"; do
    [[ -z "$seg" ]] && continue
    enc="$(urlencode "$seg")"
    if [[ -n "$out" ]]; then
      out+="/"
    fi
    out+="$enc"
  done

  printf '%s' "$out"
}

status_allowed() {
  local status="$1"
  shift
  local allowed
  for allowed in "$@"; do
    if [[ "$status" == "$allowed" ]]; then
      return 0
    fi
  done
  return 1
}

perform_json_request() {
  local method="$1"
  local path="$2"
  local data="${3:-}"
  local include_auth="${4:-1}"
  local url="${API_BASE}/${path#/}"
  local body_file
  body_file="$(mktemp "$TMP_DIR/body.XXXXXX")"

  local -a args
  args=(
    -sS
    -o "$body_file"
    -w "%{http_code}"
    -X "$method"
    "$url"
    -H "Accept: application/json"
    -b "$COOKIE_JAR"
    -c "$COOKIE_JAR"
  )

  if [[ "$include_auth" == "1" && -n "$ACCESS_TOKEN" ]]; then
    args+=( -H "Authorization: Bearer $ACCESS_TOKEN" )
  fi

  if [[ -n "$data" ]]; then
    args+=( -H "Content-Type: application/json" --data "$data" )
  fi

  local status
  if ! status="$(curl "${args[@]}")"; then
    rm -f "$body_file"
    die "Network error: ${method} ${path}"
  fi

  API_STATUS="$status"
  API_BODY="$(cat "$body_file")"
  rm -f "$body_file"
}

refresh_access_token() {
  perform_json_request "POST" "auth/refresh" "{}" "0"

  if ! status_allowed "$API_STATUS" 200; then
    return 1
  fi

  local token
  token="$(json_get_string "$API_BODY" "accessToken")"
  if [[ -z "$token" ]]; then
    return 1
  fi

  ACCESS_TOKEN="$token"
  return 0
}

restore_session_if_possible() {
  if [[ ! -s "$COOKIE_JAR" ]]; then
    return 1
  fi

  if refresh_access_token; then
    return 0
  fi

  ACCESS_TOKEN=""
  return 1
}

call_api_json() {
  local method="$1"
  local path="$2"
  local data="${3:-}"
  shift 3 || true

  local -a allowed=("$@")
  if [[ ${#allowed[@]} -eq 0 ]]; then
    allowed=(200 201 204)
  fi

  perform_json_request "$method" "$path" "$data" "1"

  if [[ "$API_STATUS" == "401" ]]; then
    if ! refresh_access_token; then
      die "Authorization failed and refresh token is invalid. Please login again."
    fi
    perform_json_request "$method" "$path" "$data" "1"
  fi

  if ! status_allowed "$API_STATUS" "${allowed[@]}"; then
    die "API ${method} ${path} failed with status ${API_STATUS}. Response: ${API_BODY}"
  fi
}

parse_destination() {
  local raw="$1"

  raw="${raw#https://}"
  raw="${raw#http://}"

  if [[ "$raw" == */* ]]; then
    DEST_HOST="${raw%%/*}"
    DEST_PATH="${raw#*/}"
  else
    DEST_HOST="$raw"
    DEST_PATH=""
  fi

  DEST_PATH="$(normalize_remote_path "$DEST_PATH")"

  [[ -n "$DEST_HOST" ]] || die "Destination host is empty"

  BASE_URL="https://${DEST_HOST}"
  API_BASE="${BASE_URL}/api/v1"
}

prompt_login() {
  local username="$CLI_USERNAME"
  local password="$CLI_PASSWORD"
  local two_factor="$CLI_TWO_FACTOR"

  if [[ -z "$username" ]]; then
    read -r -p "Username: " username
  fi

  if [[ -z "$password" ]]; then
    read -r -s -p "Password: " password
    echo
  fi

  if [[ -z "$two_factor" && ( -z "$CLI_USERNAME" || -z "$CLI_PASSWORD" ) ]]; then
    read -r -p "2FA code (optional): " two_factor
  fi

  [[ -n "$username" ]] || die "Username is required"
  [[ -n "$password" ]] || die "Password is required"

  local payload
  payload="{\"username\":\"$(json_escape "$username")\",\"password\":\"$(json_escape "$password")\",\"trustDevice\":true"
  if [[ -n "$two_factor" ]]; then
    payload+=",\"twoFactorCode\":\"$(json_escape "$two_factor")\""
  fi
  payload+="}"

  perform_json_request "POST" "auth/login" "$payload" "0"

  if ! status_allowed "$API_STATUS" 200; then
    die "Login failed (${API_STATUS}): ${API_BODY}"
  fi

  ACCESS_TOKEN="$(json_get_string "$API_BODY" "accessToken")"
  [[ -n "$ACCESS_TOKEN" ]] || die "Login succeeded but accessToken was not found"
}

load_upload_settings() {
  call_api_json "GET" "server/settings" "" 200

  local chunk_size
  local algorithm

  chunk_size="$(json_get_number "$API_BODY" "maxChunkSizeBytes")"
  [[ -n "$chunk_size" ]] || die "server/settings does not contain maxChunkSizeBytes"

  algorithm="$(json_get_string "$API_BODY" "supportedHashAlgorithm")"
  if [[ -z "$algorithm" ]]; then
    algorithm="$(json_get_string "$API_BODY" "SupportedHashAlgorithm")"
  fi
  if [[ -z "$algorithm" ]]; then
    algorithm="SHA-256"
  fi

  MAX_CHUNK_BYTES="$chunk_size"
  SERVER_HASH_ALGORITHM="$algorithm"

  local normalized
  normalized="$(printf '%s' "$SERVER_HASH_ALGORITHM" | tr '[:lower:]' '[:upper:]')"

  case "$normalized" in
    SHA-1|SHA1)
      HASH_CMD="sha1sum"
      SERVER_HASH_ALGORITHM="SHA-1"
      ;;
    SHA-256|SHA256)
      HASH_CMD="sha256sum"
      SERVER_HASH_ALGORITHM="SHA-256"
      ;;
    SHA-384|SHA384)
      HASH_CMD="sha384sum"
      SERVER_HASH_ALGORITHM="SHA-384"
      ;;
    SHA-512|SHA512)
      HASH_CMD="sha512sum"
      SERVER_HASH_ALGORITHM="SHA-512"
      ;;
    *)
      HASH_CMD="sha256sum"
      SERVER_HASH_ALGORITHM="SHA-256"
      log "Unknown server hash algorithm '${algorithm}', fallback to SHA-256"
      ;;
  esac

  require_cmd "$HASH_CMD"
}

resolve_root_node() {
  call_api_json "GET" "layouts/resolver" "" 200
  ROOT_NODE_ID="$(json_get_string "$API_BODY" "id")"
  [[ -n "$ROOT_NODE_ID" ]] || die "Failed to resolve root node id"
}

resolve_path_node_id_maybe() {
  local path="$1"
  path="$(normalize_remote_path "$path")"

  if [[ -z "$path" ]]; then
    printf '%s' "$ROOT_NODE_ID"
    return 0
  fi

  local encoded
  encoded="$(encode_resolver_path "$path")"

  call_api_json "GET" "layouts/resolver/${encoded}" "" 200 404

  if [[ "$API_STATUS" == "404" ]]; then
    return 1
  fi

  local id
  id="$(json_get_string "$API_BODY" "id")"
  [[ -n "$id" ]] || die "Resolver returned 200 but no id for path: $path"
  printf '%s' "$id"
}

create_remote_folder() {
  local parent_id="$1"
  local name="$2"
  local payload
  payload="{\"parentId\":\"$(json_escape "$parent_id")\",\"name\":\"$(json_escape "$name")\"}"

  call_api_json "PUT" "layouts/nodes" "$payload" 200 201 409

  if [[ "$API_STATUS" == "409" ]]; then
    printf ''
    return 0
  fi

  local id
  id="$(json_get_string "$API_BODY" "id")"
  [[ -n "$id" ]] || die "Failed to parse created folder id for '$name'"
  printf '%s' "$id"
}

ensure_remote_path() {
  local requested="$1"
  local path
  path="$(normalize_remote_path "$requested")"

  if [[ -z "$path" ]]; then
    printf '%s' "$ROOT_NODE_ID"
    return 0
  fi

  if [[ -n "${PATH_ID_CACHE[$path]:-}" ]]; then
    printf '%s' "${PATH_ID_CACHE[$path]}"
    return 0
  fi

  local current_path=""
  local current_id="$ROOT_NODE_ID"
  local seg
  local next_path
  local resolved
  local created
  local IFS='/'

  read -r -a parts <<< "$path"
  for seg in "${parts[@]}"; do
    [[ -z "$seg" ]] && continue

    next_path="${current_path:+$current_path/}${seg}"

    if [[ -n "${PATH_ID_CACHE[$next_path]:-}" ]]; then
      current_id="${PATH_ID_CACHE[$next_path]}"
      current_path="$next_path"
      continue
    fi

    if resolved="$(resolve_path_node_id_maybe "$next_path")"; then
      current_id="$resolved"
      PATH_ID_CACHE["$next_path"]="$current_id"
      current_path="$next_path"
      continue
    fi

    created="$(create_remote_folder "$current_id" "$seg")"
    if [[ -z "$created" ]]; then
      if resolved="$(resolve_path_node_id_maybe "$next_path")"; then
        current_id="$resolved"
      else
        die "Folder conflict (409) for '$next_path', but failed to resolve existing folder"
      fi
    else
      current_id="$created"
    fi
    PATH_ID_CACHE["$next_path"]="$current_id"
    current_path="$next_path"
  done

  printf '%s' "$current_id"
}

load_node_children_entries() {
  local node_id="$1"

  if [[ -n "${NODE_CHILDREN_LOADED[$node_id]:-}" ]]; then
    return 0
  fi

  call_api_json "GET" "layouts/nodes/${node_id}/children?page=1&pageSize=1000000" "" 200

  local encoded_name
  while IFS= read -r encoded_name; do
    [[ -z "$encoded_name" ]] && continue
    NODE_CHILD_ENTRY_NAMES["${node_id}::${encoded_name}"]=1
  done < <(
    printf '%s' "$API_BODY" \
      | grep -oE '"name":"([^"\\]|\\.)*"' \
      | sed -E 's/^"name":"(.*)"$/\1/'
  )

  NODE_CHILDREN_LOADED["$node_id"]=1
}

node_has_entry_name() {
  local node_id="$1"
  local entry_name="$2"
  local encoded_name
  local key

  encoded_name="$(json_escape "$entry_name")"
  key="${node_id}::${encoded_name}"

  if [[ -n "${NODE_CHILD_ENTRY_NAMES[$key]:-}" ]]; then
    return 0
  fi

  load_node_children_entries "$node_id"

  if [[ -n "${NODE_CHILD_ENTRY_NAMES[$key]:-}" ]]; then
    return 0
  fi

  return 1
}

hash_file() {
  local file_path="$1"
  "$HASH_CMD" "$file_path" | awk '{print $1}'
}

compact_api_body_for_log() {
  printf '%s' "$API_BODY" \
    | tr '\r\n' ' ' \
    | tr '|' '/' \
    | sed 's/[[:space:]]\+/ /g' \
    | cut -c1-220
}

decode_exists_body() {
  local body_norm
  body_norm="$(printf '%s' "$API_BODY" | tr -d '\r\n\t ' | tr '[:upper:]' '[:lower:]')"

  # Server contract: endpoint returns plain true/false.
  # Also accept quoted variants for safety.
  case "$body_norm" in
    true|"true")
      return 0
      ;;
    false|"false")
      return 1
      ;;
  esac

  return 2
}

chunk_exists() {
  local chunk_hash="$1"
  local encoded

  chunk_exists_once() {
    local target_hash="$1"
    local body_file
    body_file="$(mktemp "$TMP_DIR/chunk-exists.XXXXXX")"

    local status
    if ! status="$(curl \
        -sS \
        -o "$body_file" \
        -w "%{http_code}" \
        -X GET "${API_BASE}/chunks/${target_hash}/exists" \
        -b "$COOKIE_JAR" \
        -c "$COOKIE_JAR" \
        -H "Accept: application/json" \
        -H "Authorization: Bearer ${ACCESS_TOKEN}")"; then
      rm -f "$body_file"
      return 1
    fi

    API_STATUS="$status"
    API_BODY="$(cat "$body_file")"
    rm -f "$body_file"
    return 0
  }

  encoded="$(urlencode "$chunk_hash")"

  if ! chunk_exists_once "$encoded"; then
    return 2
  fi

  if [[ "$API_STATUS" == "401" ]]; then
    return 3
  fi

  if [[ "$API_STATUS" == "404" ]]; then
    return 1
  fi

  if [[ "$API_STATUS" != "200" ]]; then
    return 2
  fi

  decode_exists_body
  return $?
}

upload_chunk_once() {
  local chunk_file="$1"
  local original_file_name="$2"
  local chunk_hash="$3"
  local body_file
  body_file="$(mktemp "$TMP_DIR/chunk-upload.XXXXXX")"

  local status
  if ! status="$(curl \
      -sS \
      -o "$body_file" \
      -w "%{http_code}" \
      -X POST "${API_BASE}/chunks" \
      -b "$COOKIE_JAR" \
      -c "$COOKIE_JAR" \
      -H "Accept: application/json" \
      -H "Authorization: Bearer ${ACCESS_TOKEN}" \
      -F "file=@${chunk_file};filename=${original_file_name}" \
      -F "hash=${chunk_hash}")"; then
    rm -f "$body_file"
    return 1
  fi

  API_STATUS="$status"
  API_BODY="$(cat "$body_file")"
  rm -f "$body_file"
  return 0
}

upload_chunk() {
  local chunk_file="$1"
  local original_file_name="$2"
  local chunk_hash="$3"

  if ! upload_chunk_once "$chunk_file" "$original_file_name" "$chunk_hash"; then
    return 2
  fi

  if [[ "$API_STATUS" == "401" ]]; then
    return 3
  fi

  if status_allowed "$API_STATUS" 200 201 204; then
    return 0
  fi

  return 2
}

chunk_worker() {
  local chunk_file="$1"
  local remote_name="$2"
  local chunk_hash="$3"
  local result_file="$4"

  local rc=0
  if chunk_exists "$chunk_hash"; then
    rc=0
  else
    rc=$?
  fi

  # Safety net: if exists endpoint returns 200 but parser was unsure,
  # try decoding body one more time directly in worker context.
  if [[ "$rc" -eq 2 && "$API_STATUS" == "200" ]]; then
    if decode_exists_body; then
      rc=0
    else
      rc=$?
    fi
  fi

  if [[ "$rc" -eq 0 ]]; then
    printf 'OK|EXISTS\n' > "$result_file"
    return 0
  fi

  if [[ "$rc" -eq 1 ]]; then
    if upload_chunk "$chunk_file" "$remote_name" "$chunk_hash"; then
      printf 'OK|UPLOADED\n' > "$result_file"
      return 0
    fi
    rc=$?
    if [[ "$rc" -eq 3 ]]; then
      printf 'ERR|AUTH|chunk upload unauthorized\n' > "$result_file"
      return 0
    fi
    printf 'ERR|TRANSFER|chunk upload failed status=%s body=%s\n' "$API_STATUS" "$(compact_api_body_for_log)" > "$result_file"
    return 0
  fi

  if [[ "$rc" -eq 3 ]]; then
    printf 'ERR|AUTH|chunk exists unauthorized\n' > "$result_file"
    return 0
  fi

  printf 'ERR|TRANSFER|chunk exists check failed status=%s body=%s\n' "$API_STATUS" "$(compact_api_body_for_log)" > "$result_file"
  return 0
}

json_array_from_strings() {
  if [[ $# -eq 0 ]]; then
    printf '[]'
    return 0
  fi

  local out="["
  local item
  for item in "$@"; do
    out+="\"$(json_escape "$item")\","
  done
  out="${out%,}]"
  printf '%s' "$out"
}

upload_file_to_node() {
  local local_file="$1"
  local node_id="$2"
  local remote_name="$3"
  local file_label="$4"

  LAST_FILE_HASH=""
  LAST_FILE_SKIPPED=0

  local min_chunk_bytes="$CHUNK_MIN_BYTES"
  if (( min_chunk_bytes > MAX_CHUNK_BYTES )); then
    min_chunk_bytes="$MAX_CHUNK_BYTES"
  fi

  local current_chunk_bytes="$MAX_CHUNK_BYTES"
  if (( current_chunk_bytes < min_chunk_bytes )); then
    current_chunk_bytes="$min_chunk_bytes"
  fi

  local file_size
  file_size="$(stat -c%s "$local_file")"

  local attempt=1
  while :; do
    local attempt_start_uploaded="$UPLOADED_BYTES_ALL"
    local attempt_start_processed="$PROCESSED_BYTES_ALL"
    local file_uploaded=0
    local offset=0
    local index=0
    local failure_kind=""
    local failure_msg=""
    local -a chunk_hashes=()
    local -a batch_pids=()
    local -a batch_result_files=()
    local -a batch_chunk_files=()
    local -a batch_chunk_lens=()

    while (( offset < file_size )); do
      local read_bytes=$(( file_size - offset ))
      if (( read_bytes > current_chunk_bytes )); then
        read_bytes="$current_chunk_bytes"
      fi

      local chunk_file
      chunk_file="$(mktemp "$TMP_DIR/chunk.XXXXXX")"

      if ! dd \
        if="$local_file" \
        of="$chunk_file" \
        bs=64K \
        iflag=skip_bytes,count_bytes \
        skip="$offset" \
        count="$read_bytes" \
        status=none; then
        rm -f "$chunk_file"
        die "Failed to read local chunk from file: $local_file"
      fi

      local chunk_len
      chunk_len="$(stat -c%s "$chunk_file")"
      if (( chunk_len <= 0 )); then
        rm -f "$chunk_file"
        break
      fi

      local chunk_hash
      chunk_hash="$(hash_file "$chunk_file")"
      chunk_hashes[$index]="$chunk_hash"

      local result_file
      result_file="$(mktemp "$TMP_DIR/chunk-worker.XXXXXX")"

      chunk_worker "$chunk_file" "$remote_name" "$chunk_hash" "$result_file" &

      batch_pids+=("$!")
      batch_result_files+=("$result_file")
      batch_chunk_files+=("$chunk_file")
      batch_chunk_lens+=("$chunk_len")

      offset=$(( offset + chunk_len ))
      index=$(( index + 1 ))

      if (( ${#batch_pids[@]} >= PARALLEL_CHUNKS || offset >= file_size )); then
        local i
        for i in "${!batch_pids[@]}"; do
          wait "${batch_pids[$i]}" || true

          local line
          line="$(cat "${batch_result_files[$i]}" 2>/dev/null || true)"

          rm -f "${batch_result_files[$i]}" "${batch_chunk_files[$i]}"

          case "$line" in
            OK*)
              file_uploaded=$(( file_uploaded + batch_chunk_lens[$i] ))
              if (( file_uploaded > file_size )); then
                file_uploaded="$file_size"
              fi

              UPLOADED_BYTES_ALL=$(( UPLOADED_BYTES_ALL + batch_chunk_lens[$i] ))
              if (( UPLOADED_BYTES_ALL > TOTAL_BYTES_ALL )); then
                UPLOADED_BYTES_ALL="$TOTAL_BYTES_ALL"
              fi

              PROCESSED_BYTES_ALL=$(( PROCESSED_BYTES_ALL + batch_chunk_lens[$i] ))
              if (( PROCESSED_BYTES_ALL > TOTAL_BYTES_ALL )); then
                PROCESSED_BYTES_ALL="$TOTAL_BYTES_ALL"
              fi

              render_progress "$file_label" "$file_uploaded" "$file_size" "$current_chunk_bytes"
              ;;
            ERR\|AUTH\|*)
              if [[ -z "$failure_kind" ]]; then
                failure_kind="AUTH"
                failure_msg="${line#ERR|AUTH|}"
              fi
              ;;
            ERR\|TRANSFER\|*)
              if [[ -z "$failure_kind" ]]; then
                failure_kind="TRANSFER"
                failure_msg="${line#ERR|TRANSFER|}"
              fi
              ;;
            *)
              if [[ -z "$failure_kind" ]]; then
                failure_kind="TRANSFER"
                failure_msg="unknown worker result"
              fi
              ;;
          esac
        done

        batch_pids=()
        batch_result_files=()
        batch_chunk_files=()
        batch_chunk_lens=()

        if [[ -n "$failure_kind" ]]; then
          break
        fi
      fi
    done

    if [[ -n "$failure_kind" ]]; then
      UPLOADED_BYTES_ALL="$attempt_start_uploaded"
      PROCESSED_BYTES_ALL="$attempt_start_processed"
      render_progress "$file_label" 0 "$file_size" "$current_chunk_bytes"
      finish_progress_line

      if [[ "$failure_kind" == "AUTH" ]]; then
        log "[$CURRENT_FILE_INDEX/$TOTAL_FILES] Auth expired on '$file_label' (attempt $attempt). Refreshing token..."
        if ! refresh_access_token; then
          die "Unable to refresh token during parallel upload"
        fi
        attempt=$((attempt + 1))
        continue
      fi

      if (( current_chunk_bytes > min_chunk_bytes )); then
        local previous_chunk_bytes="$current_chunk_bytes"
        current_chunk_bytes=$(( current_chunk_bytes / 2 ))
        if (( current_chunk_bytes < min_chunk_bytes )); then
          current_chunk_bytes="$min_chunk_bytes"
        fi
        log "[$CURRENT_FILE_INDEX/$TOTAL_FILES] Chunk transfer issue on '$file_label' (attempt $attempt): ${failure_msg}. Reducing chunk size $(human_bytes "$previous_chunk_bytes") -> $(human_bytes "$current_chunk_bytes")"
        attempt=$((attempt + 1))
        continue
      fi

      die "Upload failed for '$file_label' at minimum chunk size ($(human_bytes "$min_chunk_bytes")): ${failure_msg}"
    fi

    local file_hash
    local chunk_hashes_json
    local payload

    file_hash="$(hash_file "$local_file")"
    chunk_hashes_json="$(json_array_from_strings "${chunk_hashes[@]}")"

    payload="{"
    payload+="\"nodeId\":\"$(json_escape "$node_id")\","
    payload+="\"chunkHashes\":${chunk_hashes_json},"
    payload+="\"name\":\"$(json_escape "$remote_name")\","
    payload+="\"contentType\":\"application/octet-stream\","
    payload+="\"hash\":\"$(json_escape "$file_hash")\","
    payload+="\"originalNodeFileId\":null"
    payload+="}"

    call_api_json "POST" "files/from-chunks" "$payload" 200 201 204 409

    local encoded_remote_name
    local node_entry_key
    encoded_remote_name="$(json_escape "$remote_name")"
    node_entry_key="${node_id}::${encoded_remote_name}"

    if [[ "$API_STATUS" == "409" ]]; then
      LAST_FILE_SKIPPED=1
      NODE_CHILD_ENTRY_NAMES["$node_entry_key"]=1
      render_progress "$file_label" "$file_size" "$file_size" "$current_chunk_bytes"
      return 0
    fi

    LAST_FILE_HASH="$file_hash"
    NODE_CHILD_ENTRY_NAMES["$node_entry_key"]=1
    render_progress "$file_label" "$file_size" "$file_size" "$current_chunk_bytes"
    return 0
  done
}

upload_source() {
  local base_remote_id
  base_remote_id="$(ensure_remote_path "$DEST_PATH")"
  if [[ -n "$DEST_PATH" ]]; then
    PATH_ID_CACHE["$DEST_PATH"]="$base_remote_id"
  fi

  SKIPPED_FILES=0

  if [[ -f "$SOURCE_PATH" ]]; then
    local source_name
    source_name="$(basename "$SOURCE_PATH")"

    TOTAL_FILES=1
    COMPLETED_FILES=0
    TOTAL_BYTES_ALL="$(stat -c%s "$SOURCE_PATH")"
    UPLOADED_BYTES_ALL=0
    PROCESSED_BYTES_ALL=0
    UPLOAD_STARTED_AT="$(date +%s)"
    CURRENT_FILE_INDEX=1

    if node_has_entry_name "$base_remote_id" "$source_name"; then
      local source_size
      source_size="$(stat -c%s "$SOURCE_PATH")"
      PROCESSED_BYTES_ALL="$source_size"
      if (( PROCESSED_BYTES_ALL > TOTAL_BYTES_ALL )); then
        PROCESSED_BYTES_ALL="$TOTAL_BYTES_ALL"
      fi
      SKIPPED_FILES=1
      COMPLETED_FILES=1
      log "Skipping existing file: $source_name"
      log "Processed 1/1 files (skipped 1), $(human_bytes "$PROCESSED_BYTES_ALL") / $(human_bytes "$TOTAL_BYTES_ALL")"
      return 0
    fi

    log "Uploading file: $SOURCE_PATH"
    log "Total: 1 file, $(human_bytes "$TOTAL_BYTES_ALL")"
    upload_file_to_node "$SOURCE_PATH" "$base_remote_id" "$source_name" "$source_name"

    if [[ "$LAST_FILE_SKIPPED" -eq 1 ]]; then
      SKIPPED_FILES=1
      log "Skipped (409 conflict): $source_name"
    else
      printf '%s\t%s\n' "$source_name" "$LAST_FILE_HASH" >> "$MANIFEST_FILE"
      log "File hash: $source_name => $LAST_FILE_HASH"
    fi

    COMPLETED_FILES=1
    finish_progress_line
    log "Uploaded 1/1 files, $(human_bytes "$UPLOADED_BYTES_ALL") / $(human_bytes "$TOTAL_BYTES_ALL")"
    return 0
  fi

  local source_norm="$SOURCE_PATH"
  source_norm="${source_norm%/}"

  local -a files=()
  local f
  while IFS= read -r -d '' f; do
    files+=("$f")
  done < <(find "$source_norm" -type f -print0 | sort -z)

  local total="${#files[@]}"
  if [[ "$total" -eq 0 ]]; then
    log "No files found in: $SOURCE_PATH"
    return 0
  fi

  TOTAL_FILES="$total"
  COMPLETED_FILES=0
  TOTAL_BYTES_ALL=0
  UPLOADED_BYTES_ALL=0
  PROCESSED_BYTES_ALL=0
  UPLOAD_STARTED_AT="$(date +%s)"

  for f in "${files[@]}"; do
    TOTAL_BYTES_ALL=$(( TOTAL_BYTES_ALL + $(stat -c%s "$f") ))
  done

  log "Total: ${TOTAL_FILES} files, $(human_bytes "$TOTAL_BYTES_ALL")"

  local index=0
  local rel_path
  local rel_dir
  local remote_dir
  local target_node_id

  for f in "${files[@]}"; do
    index=$((index + 1))
    CURRENT_FILE_INDEX="$index"
    rel_path="${f#${source_norm}/}"
    rel_dir="$(dirname "$rel_path")"

    remote_dir="$DEST_PATH"
    if [[ "$rel_dir" != "." ]]; then
      if [[ -n "$remote_dir" ]]; then
        remote_dir+="/$rel_dir"
      else
        remote_dir="$rel_dir"
      fi
    fi

    target_node_id="$(ensure_remote_path "$remote_dir")"

    local entry_name
    entry_name="$(basename "$f")"

    if node_has_entry_name "$target_node_id" "$entry_name"; then
      local skipped_size
      skipped_size="$(stat -c%s "$f")"
      PROCESSED_BYTES_ALL=$((PROCESSED_BYTES_ALL + skipped_size))
      if (( PROCESSED_BYTES_ALL > TOTAL_BYTES_ALL )); then
        PROCESSED_BYTES_ALL="$TOTAL_BYTES_ALL"
      fi
      SKIPPED_FILES=$((SKIPPED_FILES + 1))
      COMPLETED_FILES=$((COMPLETED_FILES + 1))
      render_progress "$rel_path" "$skipped_size" "$skipped_size" 0
      finish_progress_line
      log "[$index/$total] Skipped existing: $rel_path"
      log "Completed files: ${COMPLETED_FILES}/${TOTAL_FILES} | Processed: $(human_bytes "$PROCESSED_BYTES_ALL") / $(human_bytes "$TOTAL_BYTES_ALL") | Uploaded: $(human_bytes "$UPLOADED_BYTES_ALL")"
      continue
    fi

    log "[$index/$total] Uploading: $rel_path"
    upload_file_to_node "$f" "$target_node_id" "$entry_name" "$rel_path"

    if [[ "$LAST_FILE_SKIPPED" -eq 1 ]]; then
      SKIPPED_FILES=$((SKIPPED_FILES + 1))
      log "Skipped (409 conflict): $rel_path"
    else
      printf '%s\t%s\n' "$rel_path" "$LAST_FILE_HASH" >> "$MANIFEST_FILE"
      log "File hash: $rel_path => $LAST_FILE_HASH"
    fi

    COMPLETED_FILES=$((COMPLETED_FILES + 1))
    finish_progress_line
    log "Completed files: ${COMPLETED_FILES}/${TOTAL_FILES} | Uploaded: $(human_bytes "$UPLOADED_BYTES_ALL") / $(human_bytes "$TOTAL_BYTES_ALL")"
  done
}

parse_args() {
  local destination=""

  while [[ $# -gt 0 ]]; do
    case "$1" in
      --copy)
        [[ $# -ge 3 ]] || die "--copy requires <local_path> and <host/path>"
        SOURCE_PATH="$2"
        destination="$3"
        shift 3
        ;;
      --username)
        [[ $# -ge 2 ]] || die "--username requires a value"
        CLI_USERNAME="$2"
        shift 2
        ;;
      --password)
        [[ $# -ge 2 ]] || die "--password requires a value"
        CLI_PASSWORD="$2"
        shift 2
        ;;
      --two-factor)
        [[ $# -ge 2 ]] || die "--two-factor requires a value"
        CLI_TWO_FACTOR="$2"
        shift 2
        ;;
      --parallel)
        [[ $# -ge 2 ]] || die "--parallel requires a numeric value"
        PARALLEL_CHUNKS="$2"
        shift 2
        ;;
      --persist-auth)
        PERSIST_AUTH=1
        shift
        ;;
      --cookie-jar)
        [[ $# -ge 2 ]] || die "--cookie-jar requires a path"
        COOKIE_JAR_PATH="$2"
        PERSIST_AUTH=1
        shift 2
        ;;
      --help|-h)
        usage
        exit 0
        ;;
      *)
        die "Unknown argument: $1"
        ;;
    esac
  done

  if [[ -z "$SOURCE_PATH" || -z "$destination" ]]; then
    usage
    exit 1
  fi

  if ! [[ "$PARALLEL_CHUNKS" =~ ^[0-9]+$ ]] || (( PARALLEL_CHUNKS < 1 )); then
    die "--parallel must be an integer >= 1"
  fi

  parse_destination "$destination"

  if [[ ! -e "$SOURCE_PATH" ]]; then
    die "Source path does not exist: $SOURCE_PATH"
  fi

  if [[ ! -f "$SOURCE_PATH" && ! -d "$SOURCE_PATH" ]]; then
    die "Source must be a file or directory: $SOURCE_PATH"
  fi
}

main() {
  require_cmd bash curl dd stat awk sed tr mktemp find sort sha256sum
  parse_args "$@"
  ensure_state_dirs
  trap cleanup_runtime EXIT

  MANIFEST_FILE="$(mktemp "$TMP_DIR/manifest.XXXXXX")"

  log "Server: ${BASE_URL}"
  if [[ -n "$DEST_PATH" ]]; then
    log "Remote root path: /${DEST_PATH}"
  else
    log "Remote root path: /"
  fi

  if (( PERSIST_AUTH == 1 )); then
    log "Auth state: persistent refresh cookie (${COOKIE_JAR})"
  else
    log "Auth state: ephemeral (no auth state persisted)"
  fi
  log "Access token storage: disabled"

  if restore_session_if_possible; then
    log "Session restored from refresh cookie; login flow skipped"
  else
    prompt_login
  fi

  load_upload_settings
  resolve_root_node

  local effective_min_chunk="$CHUNK_MIN_BYTES"
  if (( effective_min_chunk > MAX_CHUNK_BYTES )); then
    effective_min_chunk="$MAX_CHUNK_BYTES"
  fi

  log "Chunk size: ${MAX_CHUNK_BYTES} bytes"
  log "Minimum adaptive chunk size: ${effective_min_chunk} bytes"
  log "Parallel chunk workers: ${PARALLEL_CHUNKS}"
  log "Hash algorithm: ${SERVER_HASH_ALGORITHM}"

  upload_source

  local manifest_hash
  manifest_hash="$(sha256sum "$MANIFEST_FILE" | awk '{print $1}')"
  local elapsed avg_speed
  elapsed=$(( $(date +%s) - UPLOAD_STARTED_AT ))
  if (( elapsed <= 0 )); then
    elapsed=1
  fi
  avg_speed=$(( UPLOADED_BYTES_ALL / elapsed ))
  log "Summary: files ${COMPLETED_FILES}/${TOTAL_FILES}, skipped ${SKIPPED_FILES}, uploaded $(human_bytes "$UPLOADED_BYTES_ALL") of $(human_bytes "$TOTAL_BYTES_ALL"), elapsed $(human_seconds "$elapsed"), avg $(human_bytes "$avg_speed")/s"
  log "Final upload hash (manifest SHA-256): ${manifest_hash}"
  log "Done"
}

main "$@"
