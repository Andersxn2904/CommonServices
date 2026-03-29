#!/bin/bash
# ─────────────────────────────────────────────────────────
# deploy.sh — script de despliegue para el server homelab
#
# Uso:
#   chmod +x deploy.sh
#   ./deploy.sh           # primera vez o actualización
#   ./deploy.sh --down    # bajar el stack
# ─────────────────────────────────────────────────────────

set -e

COMPOSE_CMD="docker compose -f docker-compose.yml -f docker-compose.server.yml"
DATA_DIR="/mnt/data/apps/common-services"

case "$1" in
  --down)
    echo "Bajando stack common-services..."
    $COMPOSE_CMD down
    ;;
  *)
    echo "Preparando directorios de datos..."
    mkdir -p "$DATA_DIR/postgres"

    echo "Construyendo imágenes..."
    $COMPOSE_CMD build

    echo "Levantando servicios..."
    $COMPOSE_CMD up -d

    echo "Estado del stack:"
    $COMPOSE_CMD ps
    ;;
esac
