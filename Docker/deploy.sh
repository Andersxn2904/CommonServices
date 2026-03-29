#!/bin/bash
# ─────────────────────────────────────────────────────────
# deploy.sh — despliegue para el server homelab
#
# Uso:
#   chmod +x deploy.sh
#   ./deploy.sh           # primera vez o actualización
#   ./deploy.sh --down    # bajar el stack
#   ./deploy.sh --logs    # ver logs en vivo
#
# ── Migraciones ───────────────────────────────────────────
#   AuthService aplica migraciones automáticamente al arrancar
#   vía Database.Migrate() en Program.cs. No hay pasos manuales.
#   Si falla: ./deploy.sh --logs → buscar error en auth-service
#
# ── Prerequisitos ─────────────────────────────────────────
#   - .env configurado: cp .env.server.example .env && chmod 600 .env
#   - Directorio de datos: mkdir -p /mnt/data/apps/common-services/postgres
# ─────────────────────────────────────────────────────────

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

COMPOSE_CMD="docker compose -f docker-compose.yml -f docker-compose.server.yml"
DATA_DIR="/mnt/data/apps/common-services"

# ── Validaciones ──────────────────────────────────────────
if [ ! -f .env ]; then
  echo "ERROR: no existe .env. Crea uno con:"
  echo "  cp .env.server.example .env && chmod 600 .env"
  exit 1
fi

# ── Comandos ──────────────────────────────────────────────
case "$1" in
  --down)
    echo "Bajando stack common-services..."
    $COMPOSE_CMD down
    ;;

  --logs)
    $COMPOSE_CMD logs -f
    ;;

  --status)
    $COMPOSE_CMD ps
    ;;

  *)
    echo "Preparando directorios de datos..."
    mkdir -p "$DATA_DIR/postgres"

    echo "Levantando servicios..."
    $COMPOSE_CMD up -d --build

    echo ""
    echo "Estado del stack:"
    $COMPOSE_CMD ps

    echo ""
    echo "Health checks (esperando 10s que arranquen)..."
    sleep 10
    docker inspect \
      --format='{{.Name}} → {{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' \
      $($COMPOSE_CMD ps -q) 2>/dev/null || true

    echo ""
    echo "Logs de auth-service (últimas 20 líneas):"
    $COMPOSE_CMD logs --tail 20 auth-service
    ;;
esac
