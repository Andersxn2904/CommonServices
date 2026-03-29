# AuthService — Requerimientos

## Stack y arquitectura

- .NET 8 / ASP.NET Core Web API
- Monolito simple con separación por carpetas (sin capas formales)
- Base de datos propia e independiente
- Containerizado con Docker / Docker Compose
- Entrada externa vía Cloudflare Tunnel → API Gateway → AuthService

## Estructura de proyecto


```
AuthService/
  Controllers/       ← endpoints REST
  Entities/          ← modelos de BD
  Dtos/              ← request/response
  Data/              ← DbContext, migraciones
  Services/          ← lógica de negocio
  Auth/              ← JWT, hashing, token helpers
  Configurations/    ← opciones tipadas (JWT, app settings)
  Common/            ← utilidades compartidas
```

Servicios mínimos a implementar:

- `TokenService` — generación y validación de JWT
- `AuthService` — login, logout, refresh
- `UserService` — CRUD de usuarios
- `RoleService` — roles globales y por aplicación
- `PermissionService` — permisos por aplicación
- `RefreshTokenService` — emisión, rotación, revocación

---

## Objetivo

Proveedor central de identidad para todas las apps del ecosistema (Notes, Prestapp, Dashboard, etc.).

- Una identidad única por usuario en toda la plataforma
- Autenticación centralizada con JWT
- Refresh tokens rotativos
- Roles y permisos globales y por aplicación
- Sin duplicar usuarios entre apps

---

## Modelo de datos

> Completo desde el inicio. Las fases no agregan tablas nuevas, solo activan funcionalidad sobre este esquema.

```
Users
  Id            UUID PK
  Email         string unique
  DisplayName   string
  PasswordHash  string
  IsActive      bool
  CreatedAt     datetime
  UpdatedAt     datetime

Applications
  Id            UUID PK
  Code          string unique
  Name          string
  IsActive      bool
  Description   string?

Roles
  Id              UUID PK
  Name            string
  ApplicationId   UUID? FK → Applications
  IsActive        bool

Permissions
  Id              UUID PK
  Code            string unique por alcance
  ApplicationId   UUID? FK → Applications
  IsActive        bool

UserRoles
  UserId    UUID FK → Users
  RoleId    UUID FK → Roles

RolePermissions
  RoleId        UUID FK → Roles
  PermissionId  UUID FK → Permissions

RefreshTokens
  Id                    UUID PK
  UserId                UUID FK → Users
  TokenHash             string
  ExpiresAt             datetime
  CreatedAt             datetime
  RevokedAt             datetime?
  ReplacedByTokenHash   string?
  CreatedByIp           string?
  RevokedByIp           string?
  DeviceInfo            string?
```

---

## Requerimientos transversales

Aplican a todas las fases desde el inicio.

| ID | Descripción |
|----|-------------|
| RNF-01 | .NET 8 / ASP.NET Core Web API |
| RNF-02 | Monolito simple con separación por carpetas y servicios |
| RNF-03 | BD propia, sin compartir tablas con otras apps |
| RNF-04 | Deployable con Docker y Docker Compose |
| RNF-05 | Configuración sensible por variables de entorno o archivos por ambiente, sin secretos en código |
| RNF-06 | Soporte de múltiples apps consumidoras de forma concurrente |
| RA-03 | Identidad única y centralizada; no se duplica la tabla de usuarios en apps consumidoras |
| RA-04 | Apps consumidoras pueden tener tablas locales por usuario solo para preferencias o metadatos propios — nunca para credenciales |
| RI-03 | Múltiples apps consumidoras sin duplicación de usuarios |
| RI-04 | Los servicios destino validan JWT de forma independiente; el gateway hace validación perimetral adicional |

---

## Fase 1 — Auth Core

**Objetivo:** el servicio puede autenticar usuarios y emitir tokens. Es el mínimo para integrar con otras apps.

### Funcional

| ID | Descripción |
|----|-------------|
| RF-02 | CRUD de usuarios con estado activo/inactivo |
| RF-03 | Autenticación por email + contraseña, emite access token + refresh token |
| RF-04 | Emisión de JWT firmado con claims: id, email, nombre, jti, iss, aud, exp |
| RF-05 | Refresh token rotativo: al renovar se invalida el anterior y se emite uno nuevo |
| RF-06 | Logout: revoca el refresh token de la sesión activa |
| RF-11 | Endpoint `/auth/me` con id, email, nombre y estado del usuario autenticado |
| RF-14 | Endpoints técnicos: `/health`, `/version` |

### Seguridad

| ID | Descripción |
|----|-------------|
| RS-01 | Contraseñas almacenadas con hashing seguro (nunca en texto plano) |
| RS-02 | JWT firmado criptográficamente; clave de firma como secreto externo |
| RS-03 | Validación de firma, expiración, issuer y audience en cada token |
| RS-04 | Access token de corta duración — recomendado: 15 minutos |
| RS-05 | Refresh token de duración mayor configurable — recomendado: 7–15 días |
| RS-06 | Rotación de refresh token: nuevo token por cada uso válido |
| RS-08 | Usuario inactivo no puede autenticarse ni renovar sesión |
| RS-10 | Servicio expuesto únicamente por canales seguros (Cloudflare Tunnel → Gateway) |

### No funcional

| ID | Descripción |
|----|-------------|
| RNF-07 | Logs estructurados en operaciones críticas (login, logout, refresh, errores de auth) |
| RNF-08 | Swagger/OpenAPI disponible en ambiente de desarrollo |

### Endpoints

| Método | Ruta | Descripción |
|--------|------|-------------|
| POST | `/auth/login` | Login con email + contraseña |
| POST | `/auth/refresh` | Renovar access token con refresh token |
| POST | `/auth/logout` | Revocar refresh token (cerrar sesión) |
| GET | `/auth/me` | Perfil básico del usuario autenticado |
| POST | `/users` | Crear usuario |
| GET | `/users/{id}` | Detalle de usuario |
| GET | `/health` | Estado del servicio |
| GET | `/version` | Versión desplegada |

### Reglas de negocio

| ID | Regla |
|----|-------|
| RB-01 | No se puede autenticar un usuario inactivo |
| RB-02 | No se puede renovar sesión con refresh token expirado, revocado o reemplazado |
| RB-03 | Cada refresh token solo puede usarse una vez |
| RB-06 | No puede existir más de un usuario activo con el mismo email |

### Criterios de aceptación

- [ ] Un usuario puede registrarse y autenticarse
- [ ] Se emite un JWT válido con los claims mínimos
- [ ] El refresh token rota correctamente en cada renovación
- [ ] El logout invalida la sesión activa
- [ ] `/auth/me` devuelve los datos del usuario autenticado
- [ ] Un usuario inactivo no puede autenticarse ni renovar sesión
- [ ] Swagger disponible en desarrollo
- [ ] El servicio levanta con Docker Compose

---

## Fase 2 — Roles, Permisos y Aplicaciones

**Objetivo:** el servicio soporta autorización por roles y permisos, globales y por aplicación. Los JWT incluyen claims de autorización.

### Funcional

| ID | Descripción |
|----|-------------|
| RF-01 | Registro de aplicaciones consumidoras (id, nombre, código, estado, descripción) |
| RF-07 | Roles globales (ej. `PlatformAdmin`) y por aplicación (ej. `notes.Admin`) |
| RF-08 | Permisos con código único por alcance (ej. `notes.read`, `prestapp.loan.approve`) |
| RF-09 | Asignación de uno o más roles a un usuario (globales y por app) |
| RF-10 | Asignación de permisos a roles; autorización efectiva = roles + permisos derivados |
| RF-04* | JWT extendido: incluye claims de roles y permisos relevantes |
| RF-11* | `/auth/me` extendido: devuelve roles y permisos además del perfil básico |

### Autorización

| ID | Descripción |
|----|-------------|
| RA-01 | Roles y permisos globales aplicables a toda la plataforma |
| RA-02 | Roles y permisos específicos por aplicación |

### Integración

| ID | Descripción |
|----|-------------|
| RI-01 | Consumible desde el API Gateway (`/api/auth/*`) |
| RI-02 | JWT con claims que permiten a otras APIs aplicar autorización propia |

### Endpoints

| Método | Ruta | Descripción |
|--------|------|-------------|
| GET | `/applications` | Listar aplicaciones registradas |
| POST | `/applications` | Registrar una aplicación |
| GET | `/roles` | Listar roles |
| POST | `/roles` | Crear rol (global o por app) |
| POST | `/roles/{id}/permissions` | Asignar permisos a un rol |
| POST | `/users/{id}/roles` | Asignar roles a un usuario |

### Reglas de negocio

| ID | Regla |
|----|-------|
| RB-04 | Los roles por aplicación solo pueden asignarse si la aplicación existe y está activa |
| RB-05 | Los permisos deben tener código único dentro de su alcance |

### Criterios de aceptación

- [ ] Se pueden registrar aplicaciones consumidoras
- [ ] Se pueden crear roles globales y roles asociados a una aplicación
- [ ] Se pueden crear permisos y asignarlos a roles
- [ ] Se pueden asignar roles a usuarios
- [ ] El JWT incluye los roles y permisos del usuario
- [ ] `/auth/me` devuelve roles y permisos
- [ ] No se puede asignar un rol de app a una aplicación inexistente o inactiva

---

## Fase 3 — Administración

**Objetivo:** usuarios administradores pueden gestionar usuarios, sesiones y roles desde el servicio.

### Funcional

| ID | Descripción |
|----|-------------|
| RF-12 | Cambio de contraseña autenticado; requiere contraseña actual y nueva |
| RF-13 | Administración: listar usuarios, ver detalle, activar/desactivar, asignar roles, revocar sesiones |
| RF-06* | Revocación administrativa: invalidar todas las sesiones activas de un usuario |

### Seguridad

| ID | Descripción |
|----|-------------|
| RS-07 | Mecanismo de revocación administrativa de sesiones activas por usuario |

### Endpoints

| Método | Ruta | Descripción |
|--------|------|-------------|
| GET | `/users` | Listar usuarios (admin) |
| PUT | `/users/{id}` | Actualizar datos básicos de un usuario |
| PATCH | `/users/{id}/status` | Activar o desactivar un usuario |
| POST | `/auth/change-password` | Cambio de contraseña del usuario autenticado |

### Reglas de negocio

| ID | Regla |
|----|-------|
| RB-07 | El cambio de contraseña invalida los refresh tokens vigentes del usuario según la política definida |

### Criterios de aceptación

- [ ] Un admin puede listar y ver el detalle de usuarios
- [ ] Un admin puede activar o desactivar un usuario
- [ ] Un admin puede revocar todas las sesiones de un usuario
- [ ] Un usuario autenticado puede cambiar su contraseña
- [ ] El cambio de contraseña invalida las sesiones activas si la política lo define

---

## Fase 4 — Seguridad avanzada

**Objetivo:** el servicio tiene protección activa contra abuso y trazabilidad de intentos fallidos.

### Seguridad

| ID | Descripción |
|----|-------------|
| RS-09 | Protección básica contra abuso en login: límite de intentos, bloqueo temporal opcional |
| RS-09a | Auditoría de intentos de login fallidos en logs estructurados |
| RS-09b | Bloqueo temporal de cuenta tras N intentos fallidos configurables |

### No funcional

| ID | Descripción |
|----|-------------|
| RNF-07* | Logs de seguridad: intentos fallidos, revocaciones, cambios de contraseña, bloqueos |

### Criterios de aceptación

- [ ] N intentos fallidos de login consecutivos bloquean temporalmente la cuenta
- [ ] Los intentos fallidos quedan registrados en logs estructurados
- [ ] El umbral de intentos y la duración del bloqueo son configurables por ambiente

---

## Fase 5 — Futuro (fuera de alcance actual)

Funcionalidades identificadas pero excluidas del roadmap inmediato.

| Funcionalidad | Motivo de exclusión |
|---------------|---------------------|
| MFA | Complejidad de implementación y UX |
| Login con Google/Microsoft | Requiere OAuth2 completo |
| OAuth2/OIDC completo | Fuera del alcance del ecosistema cerrado actual |
| Recuperación de contraseña por correo | Requiere integración con servicio de email |
| Confirmación de email al registrarse | Requiere integración con servicio de email |
| Gestión avanzada de dispositivos | No prioritario en V1 |
| Autorización delegada entre terceros | No hay caso de uso actual |
