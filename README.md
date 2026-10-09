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

| Usuario | Contraseña  | Rol                 |
|---------|-------------|---------------------|
| admin   | cartera2026 | Administrador       |
| ventas  | ventas2026  | Asistente de Ventas |
| cobros  | cobros2026  | Cobros              |

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

## Diferencias con el prototipo

- Las contraseñas se guardan cifradas (Identity), así que en "Usuarios del sistema" no se pueden mostrar:
  el campo aparece con puntos y escribir una nueva la reemplaza.
- Los estados de prima tienen un significado fijo (pendiente, incompleta, pagada) porque se calculan de los montos;
  en Configuración solo se puede cambiar su nombre.
