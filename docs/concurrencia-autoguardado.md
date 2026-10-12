# Concurrencia de la ficha y autoguardado

La ficha entrega `versiones` para cuatro grupos: `datos`, `venta`, `prima` y `familiares`. Datos, números, correos y conversión comparten `datos`, porque el contacto principal también puede modificarse desde Datos. Comentarios conservan sus operaciones individuales.

Cada escritura de una sección existente debe enviar `If-Match: "<uuid>"`, usando la versión del grupo obtenida en la lectura. La respuesta exitosa mantiene el cuerpo anterior y agrega `ETag: "<nuevo-uuid>"`. CORS expone ETag. El frontend serializa solicitudes del mismo grupo y usa su última versión confirmada. Las versiones de otras pestañas nunca se adoptan automáticamente para reintentar una escritura.

- 428: falta la versión; ningún cambio se guarda.
- 400: If-Match inválido, débil, múltiple o comodín.
- 409: la sección cambió desde la lectura, incluida una carrera ocurrida durante SaveChanges.
- Los permisos existentes siguen aplicándose antes de modificar los datos.

La tabla VersionesSeccion usa clave compuesta (PersonaId, Seccion) y Version como token de concurrencia de EF. Datos y cambio de versión se escriben en una sola transacción. La lectura de la ficha usa una consulta SQL única para obtener datos y versiones de una misma instantánea.

El frontend conserva el último borrador ante errores, pausa esa cola y evita reintentos automáticos. Ante 409 muestra una comparación por campos. “Usar versión guardada” descarta el borrador del grupo; “Guardar mi borrador revisado” reemplaza explícitamente ese grupo, incluidas sus listas, con la versión que el usuario revisó. Si otra sesión cambia de nuevo, vuelve a producirse un conflicto. Las otras secciones pueden seguir guardándose. La navegación y el cierre de pestaña avisan si quedan cambios sin confirmar; la ficha no envía borradores pendientes al desmontarse.

## Despliegue

1. Aplicar la migración `20261010000000_VersionesSeccion` sobre PostgreSQL. Crea e inicializa cuatro versiones para cada persona existente; no necesita extensiones de UUID.
2. Publicar backend y frontend de forma coordinada: los clientes antiguos que no envían If-Match recibirán 428. Las pestañas abiertas deben recargar la aplicación.
3. Ejecutar primero en pruebas/staging. No se ha desplegado desde este PR.

## Prueba manual en dos pestañas

1. Abrir la misma persona en A y B, con usuarios que tengan acceso.
2. En A cambiar un dato y esperar “Cambios guardados”.
3. En B modificar otro campo del mismo grupo. Debe aparecer el conflicto conservando el borrador de B; el dato de A sigue guardado.
4. Comparar ambas versiones. Elegir la guardada y verificar que B adopta los datos de A.
5. Repetir el conflicto y elegir guardar el borrador revisado. Debe guardarse esa elección; si A vuelve a editar entre la comparación y el envío, B debe recibir otro conflicto.
6. Agregar un teléfono en A y reemplazar la lista antigua desde B. El conflicto debe impedir borrar el teléfono de A.
7. Cambiar Venta en A y Prima en B desde la misma lectura. Ambos guardados deben funcionar.
8. Escribir rápido en un familiar nuevo mientras se guarda: debe conservarse una sola fila con su ID confirmado y el último texto.
9. Simular una falla de red: el borrador permanece, no aparece “Cambios guardados” y se puede reintentar explícitamente.
10. Intentar salir con un conflicto o un guardado pendiente: debe mostrarse un aviso.

## Validación automatizada

- `dotnet test backend/tests/GrupoJuridico.Gestion.Application.Tests/GrupoJuridico.Gestion.Application.Tests.csproj --configuration Release`
- Frontend, con Node 24: `npm ci`, `npm test`, `npm run lint`, `npm run build`.
- Pruebas HTTP verifican 428, 400, 409, ETag y preservación del contacto previo.
- Pruebas SQLite relacionales verifican la reversión atómica y la independencia de secciones. También se comprueba el modelo productivo, la inicialización y su coincidencia con el snapshot de migraciones.
- GitHub Actions ejecuta ambos conjuntos. La migración de datos sobre PostgreSQL y la interacción visual completa deben verificarse en staging.
