# CommonServices — Guía de Despliegue

Stack de servicios comunes: autenticación centralizada (Kauthen), email relay y observabilidad (Seq).

---

## Contenido

- [Arquitectura](#arquitectura)
- [Requisitos](#requisitos)
- [Implementación en PC local (dev)](#implementación-en-pc-local-dev)
- [Despliegue en server homelab](#despliegue-en-server-homelab)
- [Gestión de secretos](#gestión-de-secretos)
- [Logs con Seq](#logs-con-seq)
- [Backups](#backups)
- [Comandos útiles](#comandos-útiles)
- [Solución de problemas](#solución-de-problemas)

---

## Arquitectura

```
┌─────────────────────────── kauthen-net ──────────────────────────────┐
│                                                                       │
│   auth-service :8080 ──→ postgres :5432                              │
│        │                                                              │
│        └─────────────→ email-service :8080 ──→ Gmail SMTP :587       │
│                                                                       │
│   seq :5341 (ingestion) :80 (UI)  [solo en dev]                      │
│                                                                       │
└───────────────────────────────────────────────────────────────────────┘

Puertos expuestos al host:
  dev:    auth :8080  email :8081  postgres :5434  seq :8003
  server: auth :8080  (resto solo interno)
```

### Estrategia de secrets

| Entorno | Fuente              | Cuándo migrar                         |
|---------|---------------------|---------------------------------------|
| Dev     | `.env` local        | Siempre                               |
| Server  | `.env` en el server | Ahora y hasta tener múltiples stacks  |
| Futuro  | Infisical           | Cuando gestionar secretos a mano escale mal |

### Puertos expuestos

| Servicio        | URL local                  | Descripción                     |
|-----------------|----------------------------|---------------------------------|
| AuthService     | http://localhost:8080      | API + Swagger (dev)             |
| EmailService    | http://localhost:8081      | Solo en dev, interno en server  |
| PostgreSQL      | localhost:5434             | Solo en dev, interno en server  |
| Seq UI          | http://localhost:8003      | Dashboard de logs               |
| Infisical       | http://localhost:8888      | Gestión de secretos             |

---

## Requisitos

### Local (dev)
- Docker Desktop >= 4.x
- Docker Compose >= 2.x

### Server
- Docker Engine >= 24.x
- Docker Compose >= 2.x
- Disco montado en `/mnt/data`

---

## Implementación en PC local (dev)

### 1. Clonar el repositorio

```bash
git clone <repo-url> CommonServices
cd CommonServices/Docker
```

### 2. Configurar el `.env`

```bash
cp .env.server.example .env
```

Editar `.env` con los valores de desarrollo:

```env
ASPNETCORE_ENVIRONMENT=Development
POSTGRES_PASSWORD=postgres123
JWT_SECRET_KEY=dev-secret-key-min-32-chars-aqui

# Infisical local (opcional en dev)
INFISICAL_ENABLED=false

# Email Infisical (opcional en dev)
EMAIL_INFISICAL_ENABLED=false
```

> Si quieres usar Infisical en dev, levanta primero el stack de Infisical (paso 4) y pon `INFISICAL_ENABLED=true` con las credenciales correspondientes.

### 3. Levantar el stack

```bash
# Desde la carpeta Docker/
docker compose up -d
```

Docker Compose carga automáticamente `docker-compose.yml` + `docker-compose.override.yml`.

### 4. (Opcional) Levantar Infisical

```bash
docker compose -f docker-compose.infisical.yml --env-file .env.infisical up -d
```

> Infisical requiere su propio `.env.infisical` con `INFISICAL_ENCRYPTION_KEY`, `INFISICAL_AUTH_SECRET` y `INFISICAL_DB_PASSWORD`.

### 5. Verificar que todo esté corriendo

```bash
docker ps
curl http://localhost:8080/health
curl http://localhost:8081/health
```

### 6. Acceder a los servicios

| Servicio   | URL                               |
|------------|-----------------------------------|
| Swagger    | http://localhost:8080/swagger     |
| Seq        | http://localhost:8003             |
| Infisical  | http://localhost:8888             |

### Flujo de arranque en dev

```
docker compose up -d
        ↓
  kauthen-net creada
        ↓
  postgres arranca → healthcheck ok
        ↓
  auth-service arranca → aplica migraciones automáticamente
  email-service arranca
  seq arranca
```

> **Migraciones**: `AuthService` aplica `Database.Migrate()` automáticamente al iniciar.
> No se requiere ningún paso manual. Si falla, el error sale en `docker logs auth-service`.

### Detener el stack

```bash
docker compose down
# Con volúmenes (borra la BD):
docker compose down -v
```

---

## Despliegue en server homelab

### Estructura de directorios en el server

```
/srv/apps/common-services/     ← repo clonado
  Docker/
    docker-compose.yml
    docker-compose.server.yml
    .env                       ← NO en el repo
    deploy.sh
    backup.sh

/mnt/data/
  apps/
    common-services/
      postgres/                ← datos de PostgreSQL
  platform/
    seq/                       ← datos de Seq
  backups/
    common-services/
      postgres/                ← backups .sql.gz

/srv/platform/                 ← servicios compartidos
  docker-compose.platform.yml
  docker-compose.infisical.yml
  .env.platform                ← NO en el repo
```

### Paso 1 — Preparar directorios

```bash
mkdir -p /mnt/data/apps/common-services/postgres
mkdir -p /mnt/data/platform/seq
mkdir -p /mnt/data/backups/common-services/postgres
mkdir -p /srv/platform
```

### Paso 2 — Levantar servicios de plataforma

```bash
cd /srv/platform

# Copiar los compose files de plataforma
cp /srv/apps/common-services/Docker/docker-compose.platform.yml .
cp /srv/apps/common-services/Docker/docker-compose.infisical.yml .

# Crear .env.platform
cat > .env.platform << EOF
SEQ_ADMIN_PASSWORD=TU_PASSWORD_SEGURO
SEQ_DATA_PATH=/mnt/data/platform/seq
INFISICAL_ENCRYPTION_KEY=...
INFISICAL_AUTH_SECRET=...
INFISICAL_DB_PASSWORD=...
EOF

# Levantar Seq (crea platform-net)
SEQ_DATA_PATH=/mnt/data/platform/seq docker compose -f docker-compose.platform.yml up -d

# Levantar Infisical (se conecta a kauthen-net — la crea el app stack)
# Nota: kauthen-net debe existir antes. Ver paso 4.
```

### Paso 3 — Configurar el `.env` del app stack

```bash
cd /srv/apps/common-services/Docker
cp .env.server.example .env
```

Editar `.env`:

```env
ASPNETCORE_ENVIRONMENT=Production
POSTGRES_PASSWORD=PASSWORD_MUY_SEGURO

# Solo necesario si Infisical no está disponible al arrancar
JWT_SECRET_KEY=CLAVE_MIN_32_CHARS

# URL pública del servicio
APP_BASE_URL=https://kauthen.tudominio.com

# Infisical — credenciales de la machine identity
INFISICAL_ENABLED=true
INFISICAL_SITE_URL=http://infisical:8080
INFISICAL_CLIENT_ID=...
INFISICAL_CLIENT_SECRET=...
INFISICAL_PROJECT_ID=...
INFISICAL_ENVIRONMENT=prod

EMAIL_INFISICAL_ENABLED=true
EMAIL_INFISICAL_CLIENT_ID=...
EMAIL_INFISICAL_CLIENT_SECRET=...
EMAIL_INFISICAL_PROJECT_ID=...
EMAIL_INFISICAL_ENVIRONMENT=prod
```

### Paso 4 — Levantar el app stack

```bash
cd /srv/apps/common-services/Docker
chmod +x deploy.sh backup.sh

./deploy.sh
```

Que internamente ejecuta:
```bash
docker compose -f docker-compose.yml -f docker-compose.server.yml up -d
```

### Paso 5 — Levantar Infisical (se conecta a kauthen-net ya existente)

```bash
cd /srv/platform
docker compose -f docker-compose.infisical.yml --env-file .env.platform up -d
```

> **Nota**: El orden importa. `kauthen-net` debe existir antes de levantar Infisical porque la referencia como red externa. El app stack la crea en el paso 4.

### Paso 6 — Verificar

```bash
# Estado de contenedores
docker ps

# Health checks
curl http://localhost:8080/health
curl http://localhost:8080/version

# Logs en vivo
docker logs auth-service -f
docker logs email-service -f
```

### Actualizar a nueva versión

```bash
cd /srv/apps/common-services

# Bajar código
git pull

# Reconstruir y redeployar
cd Docker
./deploy.sh
```

---

## Gestión de secretos

### Estrategia actual: archivo `.env`

Todos los secrets van en el archivo `.env` del server. Simple, directo y sin dependencias externas.

```
/srv/apps/common-services/Docker/.env
```

Permisos correctos:
```bash
chmod 600 .env
chown andersxn:andersxn .env
```

### Variables requeridas

Usar `.env.server.example` como plantilla:
```bash
cp .env.server.example .env
nano .env
```

Variables obligatorias:

| Variable | Descripción |
|---|---|
| `POSTGRES_PASSWORD` | Password de PostgreSQL |
| `JWT_SECRET_KEY` | Clave de firma JWT (min 32 chars) |
| `APP_BASE_URL` | URL pública del servicio |
| `EMAIL_SMTP_PASSWORD` | App Password de Gmail |
| `EMAIL_SMTP_USERNAME` | Cuenta de Gmail |
| `EMAIL_FROM_ADDRESS` | Dirección remitente |

Generar un JWT secret seguro:
```bash
openssl rand -base64 32
```

### Migración futura a Infisical

Cuando tengas múltiples servicios y gestionar el `.env` a mano se vuelva incómodo, Infisical ya está disponible como stack separado en `docker-compose.infisical.yml`. La integración en el código ya está implementada — solo hay que habilitarla.

---

## Logs con Seq

### Acceso

- **Dev**: http://localhost:8003
- **Server**: http://server-ip:8003 (o via Cloudflare Tunnel)

### Filtros útiles en Seq

```
# Solo logs de negocio (sin ruido de ASP.NET)
@Logger like 'AuthService%' or @Logger like 'EmailService%'

# Solo warnings y errores
@Level in ['Warning', 'Error', 'Fatal']

# Logs de un usuario específico
@Message like '%userId%'

# Errores de email
@Logger like '%EmailNotification%' and @Level = 'Error'

# Intentos de login fallidos
@Message like '%Login denegado%' or @Message like '%Intento de login%'
```

### Configurar retención

1. Ir a Seq UI → `Administration` → `Settings` → `Storage`
2. Configurar retención recomendada:
   - Dev: 7 días
   - Server: 30 días

---

## Backups

### Manual

```bash
cd /srv/apps/common-services/Docker
./backup.sh
```

Genera: `/mnt/data/backups/common-services/postgres/authservice_YYYYMMDD_HHMMSS.sql.gz`

### Automático con cron

```bash
crontab -e
```

Agregar:
```cron
# Backup diario a las 2am
0 2 * * * /srv/apps/common-services/Docker/backup.sh >> /var/log/common-services-backup.log 2>&1
```

### Restaurar un backup

```bash
# Ver backups disponibles
ls -lh /mnt/data/backups/common-services/postgres/

# Restaurar
gunzip -c /mnt/data/backups/common-services/postgres/authservice_20260329_020000.sql.gz \
  | docker exec -i auth-postgres psql -U postgres -d authservice
```

---

## Comandos útiles

### Stack completo

```bash
# Levantar (dev)
docker compose up -d

# Levantar (server)
docker compose -f docker-compose.yml -f docker-compose.server.yml up -d

# Bajar
docker compose down

# Reconstruir imágenes
docker compose build

# Estado
docker compose ps
```

### Logs

```bash
docker logs auth-service -f
docker logs email-service -f
docker logs auth-postgres -f
docker logs seq -f

# Últimas 100 líneas
docker logs auth-service --tail 100
```

### Base de datos

```bash
# Conectar a PostgreSQL
docker exec -it auth-postgres psql -U postgres -d authservice

# Borrar todos los usuarios (dev)
docker exec auth-postgres psql -U postgres -d authservice -c "DELETE FROM \"Users\";"

# Ver tablas
docker exec auth-postgres psql -U postgres -d authservice -c "\dt"
```

### Health checks

```bash
curl http://localhost:8080/health
curl http://localhost:8080/version
curl http://localhost:8081/health
```

---

## Solución de problemas

### El servicio no arranca y hay error de migración

```bash
docker logs auth-service
```

Si el error es de migración, verificar:
1. PostgreSQL está corriendo y sano: `docker ps`
2. La connection string es correcta: revisar `.env`
3. Conectar manualmente: `docker exec -it auth-postgres psql -U postgres`

### No llegan correos

```bash
docker logs email-service --tail 50
```

Verificar en Seq: filtro `@Logger like '%EmailSender%'`

Causas comunes:
- Credenciales SMTP incorrectas en Infisical
- `EmailConfiguration__Password` vacío (no cargó de Infisical)
- Puerto 587 bloqueado en el server

### Infisical no conecta

```bash
docker logs auth-service | grep -i infisical
```

Si dice `Connection refused`:
1. Verificar que Infisical esté corriendo: `docker ps | grep infisical`
2. Verificar que comparten red: `docker network inspect kauthen-net`
3. Si kauthen-net no existe: `docker network create kauthen-net` y relanzar Infisical

### Seq no recibe logs

Verificar que los servicios están en la misma red que Seq:
```bash
docker network inspect kauthen-net   # dev
docker network inspect platform-net  # server
```

Si el servicio no aparece en la red, reiniciarlo:
```bash
docker compose restart auth-service
```
