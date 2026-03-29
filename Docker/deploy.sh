#!/bin/bash
# ─────────────────────────────────────────────────────────
# deploy.sh — despliegue para el server homelab
#
# Uso:
#   chmod +x deploy.sh && chmod +x backup.sh
#   ./deploy.sh           # primera vez o actualización
#   ./deploy.sh --down    # bajar el stack
#   ./deploy.sh --logs    # ver logs en vivo
#
# ── Migraciones ───────────────────────────────────────────
#   AuthService aplica migraciones automáticamente al arrancar
#   vía Database.Migrate() en Program.cs.
#   No se requiere ningún paso manual.
#   Si hay una migración fallida, el contenedor no arranca
#   y el error aparece en: docker logs auth-service
#
# ── Orden de arranque ─────────────────────────────────────
#   1. docker-compose.platform.yml  → crea platform-net + Seq
#   2. docker-compose.infisical.yml → Infisical en kauthen-net + infisical-net
#   3. Este script                  → app stack
#
# ── Prerequisitos ─────────────────────────────────────────
#   - platform-net creada (por docker-compose.platform.yml)
#   - .env configurado (cp .env.server.example .env)
# ─────────────────────────────────────────────────────────

set -e

COMPOSE_CMD="docker compose -f docker-compose.yml -f docker-compose.server.yml"
DATA_DIR="/mnt/data/apps/common-services"

case "$1" in
  --down)
    echo "Bajando stack common-services..."
    $COMPOSE_CMD down
    ;;
  --logs)
    $COMPOSE_CMD logs -f
    ;;
  *)
    echo "Preparando directorios de datos..."
    mkdir -p "$DATA_DIR/postgres"

    echo "Construyendo imágenes..."
    $COMPOSE_CMD build

    echo "Levantando servicios..."
    $COMPOSE_CMD up -d

    echo ""
    echo "Estado del stack:"
    $COMPOSE_CMD ps

    echo ""
    echo "Health checks:"
    sleep 5
    docker inspect --format='{{.Name}} → {{.State.Health.Status}}' \
      $(docker compose -f docker-compose.yml -f docker-compose.server.yml ps -q) 2>/dev/null || true
    ;;
esac
