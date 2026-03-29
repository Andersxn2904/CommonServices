#!/bin/bash
# ─────────────────────────────────────────────────────────
# backup.sh — backup lógico de la base de datos AuthService
#
# Uso manual:
#   ./backup.sh
#
# Para programarlo (cron — cada día a las 2am):
#   0 2 * * * /srv/apps/common-services/Docker/backup.sh >> /var/log/common-services-backup.log 2>&1
# ─────────────────────────────────────────────────────────

set -e

# ── Config ────────────────────────────────────────────────
BACKUP_DIR="/mnt/data/backups/common-services/postgres"
CONTAINER="auth-postgres"          # nombre del contenedor según el entorno
DB_NAME="authservice"
DB_USER="postgres"
RETENTION_DAYS=7
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
BACKUP_FILE="$BACKUP_DIR/authservice_$TIMESTAMP.sql.gz"

# ── Crear directorio si no existe ─────────────────────────
mkdir -p "$BACKUP_DIR"

# ── Dump comprimido ───────────────────────────────────────
echo "[$TIMESTAMP] Iniciando backup de $DB_NAME..."

docker exec "$CONTAINER" pg_dump -U "$DB_USER" "$DB_NAME" | gzip > "$BACKUP_FILE"

echo "[$TIMESTAMP] Backup guardado: $BACKUP_FILE ($(du -sh "$BACKUP_FILE" | cut -f1))"

# ── Rotación — eliminar backups más viejos que RETENTION_DAYS ──
echo "[$TIMESTAMP] Limpiando backups de más de $RETENTION_DAYS días..."
find "$BACKUP_DIR" -name "authservice_*.sql.gz" -mtime +$RETENTION_DAYS -delete

TOTAL=$(ls "$BACKUP_DIR"/authservice_*.sql.gz 2>/dev/null | wc -l)
echo "[$TIMESTAMP] Backups disponibles: $TOTAL"
echo "[$TIMESTAMP] Backup completado."
