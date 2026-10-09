# Grupo Jurídico Gestión — Contexto del proyecto

## Stack
- Backend: .NET 8 Web API, Clean Architecture (Domain / Application / Infrastructure / API)
- Base de datos: PostgreSQL + Entity Framework Core 8 (Npgsql)
- Auth: ASP.NET Core Identity + JWT Bearer
- Validación: FluentValidation
- Documentación API: Swagger (Swashbuckle), con botón Authorize para JWT
- Frontend: React 19 + TypeScript (Vite), React Router, Axios, ESLint
- Monorepo: backend/ y frontend/ en el mismo repositorio

## Convenciones
- Nombres de entidades, propiedades y tablas de negocio en español
- Todas las entidades heredan de BaseEntity (Id, ModificadoEn, ModificadoPorId) en GrupoJuridico.Gestion.Domain/Common; las que no tienen Id int (ConfiguracionSistema, Usuario) implementan IAuditable directamente
- Auditoría: ModificadoEn/ModificadoPorId guardan el último cambio (al crear, la creación). Los llena AuditoriaInterceptor (Infrastructure/Persistence) en cada SaveChanges; no asignarlos a mano
- Reglas de dependencias: Domain no depende de nada. Application depende solo de Domain. Infrastructure depende de Application y Domain. API depende de todas.
- appsettings.Development.json contiene secretos y está en .gitignore

## Estado actual
Recién creada la estructura base (Identity + JWT + CORS + Swagger configurados, DbContext vacío). Pendiente: definir roles, entidades de Domain, primera migración, AuthController (login) y los primeros módulos.
