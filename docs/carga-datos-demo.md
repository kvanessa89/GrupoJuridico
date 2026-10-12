# Protección de datos de demostración

La opción `Seed:DatosDemo` solo puede activarse cuando el entorno efectivo de la aplicación es `Development` o `Testing`.

Si se activa en `Production`, `Staging` o cualquier otro entorno, `DbSeeder.EjecutarAsync` lanza una excepción antes de migrar, crear roles, cargar catálogos o crear usuarios. La API no completa su inicio. El mensaje identifica la opción y el entorno, sin incluir credenciales.

Para corregir una configuración inválida, desactivar `Seed:DatosDemo` (o `Seed__DatosDemo` si se configura mediante variables de entorno). No cambiar el entorno de producción a Testing para eludir el control.

Con `DatosDemo=false`, la carga normal de roles, catálogos y administrador inicial sigue funcionando según las opciones existentes. En Testing autorizado, la carga demo conserva su comportamiento e idempotencia.

Este cambio no necesita migración de base de datos. Tampoco elimina usuarios o datos que se hayan creado previamente. Antes del lanzamiento, revisar en un canal privado si existen cuentas de demostración y acordar su tratamiento; no publicar sus contraseñas en reportes. No reutilizar credenciales ni claves del ambiente de pruebas en producción.

Las pruebas cubren entornos rechazados sin escrituras, bloqueo antes de acceder a una base relacional, carga demo autorizada e idempotente, y carga normal sin demo en producción.
