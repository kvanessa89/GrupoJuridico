# Grupo Jurídico Gestión

Implementación del diseño exportado desde Claude Design (`project/` y `chats/`): página pública de inicio y el sistema
**Grupo Jurídico Gestión** (antes "Mi Cartera") para manejar prospectos, ventas, primas y clientes oficiales.

```
backend/    API ASP.NET Core 8 (Clean Architecture, PostgreSQL, Identity + JWT)
frontend/   React 19 + TypeScript + Vite (solo interfaz)
project/    Prototipo HTML de Claude Design (referencia, ver HANDOFF.md)
chats/      Conversaciones de diseño (referencia)
```

## Fase 1 (esta entrega)

- **Inicio** (`/`): página pública con el enlace discreto "Grupo Jurídico Gestión" y el pie con el logo de Kiub.
- **Login** con roles fijos: Administrador, Asistente de Ventas y Cobros.
- **Tabla de ventas**: filtros (vendedor, estado de prima, procedencia, mes de pago, que abre en el mes actual),
  búsqueda, orden por columna, paginación y tarjetas de totales. El monto total de ventas solo lo ve el Administrador.
  Asistente de Ventas y Cobros no ven primas pagadas. El Administrador puede eliminar una persona con todo lo relacionado.
- **Registrar prospecto**: se guarda solo con "Guardar prospecto" y valida los campos requeridos (al menos un teléfono o WhatsApp).
- **Ficha de la persona** con autoguardado por sección ("Cambios guardados"): datos, venta, prima (saldo y estado
  calculados), familiares y comentarios. Si la prima está pagada, la fecha de pago es obligatoria para volver.
- **Convertir en cliente**: origen requerido, estado (Al Día por defecto) y expediente opcional.
- **Clientes** (Administrador y Cobros): columnas según el rol, filtros por año, procedencia, prima, estado y origen.
  La ficha del cliente oficial usa pestañas General, Ventas, Familiares y Actividad, con teléfonos (Teléfono/WhatsApp
  con principal por tipo) y correos editables.
- **Configuración** (Administrador): vendedores (código visible), procedencias, métodos, nombres de los estados de prima,
  interés de morosidad, orígenes y estados del cliente; roles solo lectura.
- **Usuarios del sistema** (Administrador): alta, edición en línea, cambio de contraseña y eliminación.

**Pendiente para la fase 2:** módulo de Cobros (planes de pago, cuotas, pagos con comprobante, promesas, mora,
recordatorios, letra de cambio) y archivos por tipo de documento con permisos por rol.

## Requisitos

- .NET SDK 8
- Node.js 22
- PostgreSQL 14 o superior

## Backend

```bash
cd backend
dotnet tool restore
dotnet run --project src/GrupoJuridico.Gestion.Api     # http://localhost:5015, Swagger en /swagger
dotnet test
```

Al iniciar aplica las migraciones y carga los catálogos. En **Development** y **Testing** también carga la cartera
de ejemplo del prototipo y estos usuarios:

| Usuario | Rol |
| --- | --- |
| admin | Administrador |
| ventas | Asistente de Ventas |
| cobros | Cobros |

Las contraseñas de demostración se definen en la configuración de datos de prueba; no se publican en esta tabla.

Configuración por entorno (`appsettings.{Development,Testing,Staging,Production}.json`). En Staging y Production
definí los secretos por variables de entorno:

```bash
ConnectionStrings__Gestion="Host=...;Database=...;Username=...;Password=..."
Jwt__Key="<clave de al menos 32 caracteres>"
Seed__AdminContrasena="<contraseña inicial del usuario admin>"
```

Nueva migración:

```bash
dotnet ef migrations add Nombre -p src/GrupoJuridico.Gestion.Infrastructure -s src/GrupoJuridico.Gestion.Api -o Persistence/Migrations
```

Proyectos:

- `Domain`: entidades (Persona → Venta → Prima → Cliente, Numeros, Correos, Fincas, Familiares, Comentarios) y catálogos.
- `Application`: servicios por módulo, DTOs y validaciones con FluentValidation.
- `Infrastructure`: EF Core + Npgsql, ASP.NET Core Identity, JWT y datos semilla.
- `Api`: controladores, autenticación JWT Bearer, manejo de errores (ProblemDetails) y Swagger.

## Frontend

```bash
cd frontend
npm install
npm run dev            # http://localhost:5173 (envía /api al backend en :5015)
npm run lint
npm run build          # producción (.env.production)
npm run build:staging  # staging (.env.staging)
```

`VITE_API_URL` define la URL de la API por entorno.

En IIS, el build incluye `web.config` (desde `public/`). Con él, las direcciones de la aplicación (por ejemplo
`/gestion/cobros`) se pueden abrir directamente o recargar, `index.html` no se guarda en caché y `/assets` sí, por
un año. Requiere el módulo URL Rewrite de IIS.

## Archivos del expediente

La ficha del cliente tiene la pestaña **Archivos**: se elige el tipo de documento y se suben uno o varios archivos (PDF, imágenes, Word o Excel; hasta 10 MB cada uno y 25 MB por envío). En **Configuración → Archivos** se administran los tipos de documento y la matriz de permisos: cada rol puede **subir** (y quitar) y/o **ver** cada tipo. Subir implica ver. Un tipo nuevo empieza habilitado solo para el Administrador.

Los archivos (también los comprobantes de Cobros) se guardan con una clave interna en el almacenamiento elegido con `Archivos__Proveedor`:

- `Local` (por defecto): disco del servidor, en `Archivos__Ruta` (`App_Data/archivos`). Hay que respaldar esa carpeta y excluirla al publicar.
- `R2`: bucket **privado** de Cloudflare R2. Requiere `Archivos__R2__CuentaId`, `Archivos__R2__AccessKeyId`, `Archivos__R2__SecretAccessKey` y `Archivos__R2__Bucket`. Use una llave de R2 limitada a ese bucket. Si falta alguno, la API no arranca y nombra las variables que faltan.

Las credenciales de R2 son secretos: van en variables de entorno del servidor (en IIS, `environmentVariables` del `web.config` publicado) y nunca en el repositorio. Los usuarios no acceden al bucket: la API revisa los permisos y entrega cada archivo.

## Diferencias con el prototipo

- Las contraseñas se guardan cifradas (Identity), así que en "Usuarios del sistema" no se pueden mostrar:
  el campo aparece con puntos y escribir una nueva la reemplaza.
- Los estados de prima tienen un significado fijo (pendiente, incompleta, pagada) porque se calculan de los montos;
  en Configuración solo se puede cambiar su nombre.

## Protección del login

- Cinco contraseñas incorrectas bloquean la cuenta durante 15 minutos. Un login correcto reinicia el contador; mientras hay bloqueo tampoco se acepta la contraseña correcta.
- Las contraseñas nuevas y reemplazadas requieren 12 caracteres. Las existentes siguen funcionando; cambiar las contraseñas demo solo afecta usuarios creados desde ahora, no usuarios ya guardados.
- POST /api/auth/login permite inicialmente 30 solicitudes por minuto por IP y por instancia, sin cola. El exceso devuelve HTTP 429 con Retry-After. Se puede ajustar con LoginRateLimit__PermitLimit y LoginRateLimit__WindowSeconds (por defecto 30 y 60).
- La IP proviene de Connection.RemoteIpAddress. No se confía directamente en X-Forwarded-For. En hosting detrás de proxy, configurar forwarded headers únicamente para proxies/redes confiables antes del limitador, o aplicar el límite en el proxy. Sin eso, clientes detrás del proxy pueden compartir el mismo cupo. Varias instancias necesitan coordinación en el gateway para un límite agregado.
- Los bloqueos se registran con ID interno y fecha; no se registran contraseñas ni JWT. La respuesta de credenciales inválidas y bloqueo es genérica.
- El bloqueo por intentos fallidos protege nuevos logins. La revocación de JWT por cambios de cuenta se describe a continuación.

## Revocación de sesiones JWT

Cada token incluye el SecurityStamp de Identity. Después de validar firma, emisor, audiencia y vencimiento, cada petición consulta la cuenta y el rol actuales en PostgreSQL, sin caché de revocación. Si falta la cuenta, el identificador no coincide o el rol ya no pertenece al usuario, se rechaza con HTTP 401.

- ResetPasswordAsync cambia el SecurityStamp al reemplazar la contraseña.
- Un cambio de rol cambia explícitamente el SecurityStamp antes de quitar/asignar roles.
- Eliminar una cuenta invalida sus tokens en la siguiente solicitud.
- Cambiar el nombre de usuario también cambia el SecurityStamp mediante Identity. Editar solo el nombre completo no obliga a iniciar sesión nuevamente.
- Los access tokens nuevos duran 480 minutos (8 horas), configurable con Jwt__ExpiraMinutos. No hay refresh tokens: al vencer, el usuario inicia sesión nuevamente. La interfaz ya procesa respuestas 401.
- El despliegue rechaza tokens anteriores sin session_stamp: los usuarios deberán iniciar sesión nuevamente una vez.
- No requiere nuevas columnas ni migraciones: SecurityStamp ya existe en AspNetUsers.
- El control agrega consultas por petición; medir su costo con PostgreSQL antes de producción. No hay una ventana de caché que mantenga permisos anteriores. Una solicitud que ya fue autorizada antes del cambio puede terminar; esto no cancela operaciones en curso.
- Todas las operaciones administrativas que modifiquen permisos fuera de IdentityService deben rotar SecurityStamp. La validación de pertenencia al rol también rechaza tokens cuyo rol ya fue retirado directamente.
