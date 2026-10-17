#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════
# پشتیبان کامل دیتابیس سامانهٔ فرش با pg_dump (قالب custom) در backups/rugs_<زمان>.dump
#
# پیش از هر دیپلوی که migration‌اش [RequiresBackup] دارد اجرا کنید؛ بعد شناسهٔ همان migration را
# در Database__ConfirmedBackupFor بگذارید تا استارت برنامه ادامه دهد.
#
# مقادیر اتصال به ترتیب از گزینه‌ها، متغیرهای استاندارد پستگرس (PGHOST، PGPORT، PGUSER، PGDATABASE،
# PGPASSWORD) و در آخر پیش‌فرض‌های محلی خوانده می‌شوند. بدون رمز، pg_dump از ~/.pgpass می‌خواند یا می‌پرسد.
#
# استفاده:
#   bash scripts/db-backup.sh
#   bash scripts/db-backup.sh --host 10.0.0.5 --db rugs_management --user postgres
#   bash scripts/db-backup.sh --container rugs_postgres          # pg_dump داخل کانتینر Docker
#   # CapRover (روی سرور):
#   bash scripts/db-backup.sh --container "$(docker ps -q -f name=srv-captain--postgres)" --password '...'
#
# بازگردانی:
#   pg_restore --clean --if-exists --no-owner -d <دیتابیس> backups/rugs_....dump
# ═══════════════════════════════════════════════════════════════════
set -euo pipefail

DB_HOST="${PGHOST:-localhost}"
DB_PORT="${PGPORT:-5432}"
DB_USER="${PGUSER:-postgres}"
DB_NAME="${PGDATABASE:-rugs_management}"
DB_PASSWORD="${PGPASSWORD:-}"
CONTAINER="${RUGS_PG_CONTAINER:-}"
PG_DUMP_BIN="${PG_DUMP:-pg_dump}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUT_DIR="$(dirname "$SCRIPT_DIR")/backups"

usage() {
  sed -n '2,22p' "${BASH_SOURCE[0]}"
  echo "Options: --host H --port P --user U --db NAME --password PW --out DIR --container NAME --pg-dump PATH"
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --host) DB_HOST="$2"; shift 2 ;;
    --port) DB_PORT="$2"; shift 2 ;;
    --user) DB_USER="$2"; shift 2 ;;
    --db|--database) DB_NAME="$2"; shift 2 ;;
    --password) DB_PASSWORD="$2"; shift 2 ;;
    --out) OUT_DIR="$2"; shift 2 ;;
    --container) CONTAINER="$2"; shift 2 ;;
    --pg-dump) PG_DUMP_BIN="$2"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
done

mkdir -p "$OUT_DIR"
# LC_ALL=C: نام فایل همیشه با رقم و تقویم میلادیِ ASCII
STAMP="$(LC_ALL=C date +%Y%m%d_%H%M%S)"
OUT_FILE="$(cd "$OUT_DIR" && pwd)/rugs_${STAMP}.dump"
# هرگز روی پشتیبان قبلی (اجرای دوم در همان ثانیه) نمی‌نویسیم
if [[ -e "$OUT_FILE" ]]; then
  echo "Backup file already exists, not overwriting: $OUT_FILE" >&2
  exit 1
fi
# فایل نیمه‌کاره نباید کنار پشتیبان‌های سالم بماند
trap 'echo "Backup failed; removing partial file $OUT_FILE" >&2; rm -f "$OUT_FILE"' ERR

if [[ -n "$CONTAINER" ]]; then
  echo "pg_dump inside container '$CONTAINER' ($DB_NAME) -> $OUT_FILE"
  # داخل کانتینر از سوکت محلی وصل می‌شود؛ host/port میزبان اینجا معنا ندارد
  docker exec -e PGPASSWORD="$DB_PASSWORD" "$CONTAINER" \
    pg_dump --format=custom --username="$DB_USER" --dbname="$DB_NAME" > "$OUT_FILE"
  # فایل نیمه‌کاره نباید «پشتیبان سالم» حساب شود
  docker exec -i "$CONTAINER" pg_restore --list < "$OUT_FILE" > /dev/null
else
  if ! command -v "$PG_DUMP_BIN" > /dev/null 2>&1; then
    echo "pg_dump not found. Install the PostgreSQL client tools, pass --pg-dump, or use --container." >&2
    exit 1
  fi
  echo "pg_dump $DB_USER@$DB_HOST:$DB_PORT/$DB_NAME -> $OUT_FILE"
  if [[ -n "$DB_PASSWORD" ]]; then export PGPASSWORD="$DB_PASSWORD"; fi
  "$PG_DUMP_BIN" --format=custom --host="$DB_HOST" --port="$DB_PORT" \
    --username="$DB_USER" --dbname="$DB_NAME" --file="$OUT_FILE"
  PG_RESTORE_BIN="$(dirname "$(command -v "$PG_DUMP_BIN")")/pg_restore"
  if [[ -x "$PG_RESTORE_BIN" || -x "$PG_RESTORE_BIN.exe" ]]; then
    "$PG_RESTORE_BIN" --list "$OUT_FILE" > /dev/null
  fi
fi

if [[ ! -s "$OUT_FILE" ]]; then
  echo "Backup file is empty: $OUT_FILE" >&2
  exit 1
fi

echo "Backup written: $OUT_FILE ($(wc -c < "$OUT_FILE" | tr -d ' ') bytes)"
echo "Restore with: pg_restore --clean --if-exists --no-owner -d <database> \"$OUT_FILE\""
