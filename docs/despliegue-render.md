# Ambiente de pruebas en Render + Cloudflare R2

Esta guía levanta el **ambiente de pruebas** (entorno `Staging`): API, frontend y base de datos en Render, y los archivos (comprobantes de pago) en un bucket privado de Cloudflare R2. Producción se arma después igual, con nombres, base, bucket y claves propios.

```
Navegador ─► gj-web-pruebas (Render, sitio estático)
        └──► gj-api-pruebas (Render, Docker) ─┬─► gj-db-pruebas (PostgreSQL en Render)
                                              └─► gj-archivos-pruebas (bucket en Cloudflare R2)
```

Todo está descrito en [`render.yaml`](../render.yaml) (un "Blueprint" de Render). Los secretos no están en el repositorio: Render los pide al crear el Blueprint.

## Qué datos tiene el ambiente de pruebas

- Arranca **sin datos de demostración** (`Seed__DatosDemo=false`; el código no permite activarlos fuera de Development).
- Al iniciar, la API aplica las migraciones y crea solo los **roles**, los **catálogos** y el **usuario administrador inicial** (`admin`, con la contraseña que se defina en `Seed__AdminContrasena`).
- Los clientes, ventas y planes de pago se cargan a mano desde la aplicación para probar.
- Swagger queda habilitado en `/swagger` (solo en pruebas).

## 1. Cloudflare R2 (archivos)

1. Cree una cuenta en [cloudflare.com](https://dash.cloudflare.com/sign-up) o entre con una existente.
2. En el menú, abra **R2 Object Storage** y actívelo. Cloudflare pide un método de pago aunque el uso quede dentro de la capa gratuita.
3. **Create bucket** con el nombre `gj-archivos-pruebas`. Déjelo **privado**: no active el acceso público ni un dominio público. La API es la única que lee y escribe.
4. Anote el **Account ID**, que aparece en la página principal de R2.
5. Cree una llave de acceso: **Manage R2 API Tokens** → **Create API token**.
   - Permiso: **Object Read & Write**.
   - Alcance: **solo el bucket `gj-archivos-pruebas`**.
   - Al crearla, Cloudflare muestra el **Access Key ID** y el **Secret Access Key** **una sola vez**. Guárdelos en un lugar seguro, como un gestor de contraseñas. No los envíe por chat ni por correo.

## 2. Render (API, frontend y base de datos)

1. Cree una cuenta en [render.com](https://dashboard.render.com/register) y conecte su cuenta de GitHub, dando acceso al repositorio `kvanessa89/GrupoJuridico`.
2. **New** → **Blueprint** → elija el repositorio y la rama:
   - mientras los cambios no estén en `main`, use la rama `feat/despliegue-render`;
   - después del merge, cambie a `main`.
3. Render lee `render.yaml` y propone crear `gj-db-pruebas`, `gj-api-pruebas` y `gj-web-pruebas`. Le pedirá estos valores:

| Variable | Servicio | Valor |
|---|---|---|
| `Seed__AdminContrasena` | gj-api-pruebas | Contraseña del usuario `admin`, de 12 caracteres o más. Solo de pruebas: no la reutilice en producción. |
| `Cors__Origenes__0` | gj-api-pruebas | Dirección del frontend: `https://gj-web-pruebas.onrender.com` |
| `Archivos__R2__CuentaId` | gj-api-pruebas | Account ID de Cloudflare (paso 1.4) |
| `Archivos__R2__AccessKeyId` | gj-api-pruebas | Access Key ID (paso 1.5) |
| `Archivos__R2__SecretAccessKey` | gj-api-pruebas | Secret Access Key (paso 1.5) |
| `VITE_API_URL` | gj-web-pruebas | Dirección de la API **terminada en `/api`**: `https://gj-api-pruebas.onrender.com/api` |

   La clave JWT (`Jwt__Key`) la genera Render sola. La conexión a la base (`ConnectionStrings__Gestion`) también la conecta Render; la API acepta el formato `postgresql://…` que entrega Render.

4. **Apply**. La primera publicación tarda varios minutos, porque compila la imagen Docker de la API.

### Verificar las direcciones

Render arma la dirección con el nombre del servicio (`https://<nombre>.onrender.com`). Si el nombre ya estaba tomado, le agrega un sufijo. Cuando terminen de crearse los servicios:

- Abra `gj-web-pruebas` y `gj-api-pruebas` en el panel de Render y copie la dirección real de cada uno.
- Si alguna no coincide con lo que puso en la tabla, corrija `Cors__Origenes__0` (en la API) o `VITE_API_URL` (en el frontend) en **Environment**. Después vuelva a publicar ese servicio con **Manual Deploy**. El frontend toma `VITE_API_URL` al compilar, así que necesita publicarse de nuevo para usar el valor nuevo.

## 3. Probar

1. Abra `https://gj-api-pruebas.onrender.com/api/salud`. Debe responder `{"estado":"ok"}`.
2. Abra el frontend, `https://gj-web-pruebas.onrender.com/gestion/login`, y entre con `admin` y la contraseña del paso 2.
3. Cree los usuarios de prueba en **Usuarios**, un prospecto, conviértalo en cliente, cree un plan de pago y registre un pago con comprobante.
4. En Cloudflare, abra el bucket: el comprobante aparece guardado como `aaaa/mm/<código>.pdf`.

## Publicaciones siguientes

Cada push a la rama elegida en el Blueprint vuelve a publicar la API y el frontend. La API aplica las migraciones nuevas al iniciar.

## Costos aproximados

Confirme los precios vigentes en Render y Cloudflare; cambian con el tiempo.

| Pieza | Plan | Aprox. por mes |
|---|---|---|
| gj-api-pruebas | Starter (siempre encendida) | ~US$7 |
| gj-db-pruebas | Basic 256 MB | ~US$6 |
| gj-web-pruebas | Sitio estático | Gratis |
| Bucket R2 | Capa gratuita (unos 10 GB) | US$0 |

Si el ambiente de pruebas no se va a usar por un tiempo, puede **suspender** la API desde el panel de Render.

## Cosas a tener en cuenta

- **Un solo servidor de API.** No suba la cantidad de instancias: el recálculo automático de cobros y el límite de intentos de login están pensados para una sola.
- **Límite de intentos de login por IP.** Detrás del proxy de Render, la API ve la dirección del proxy y no la del usuario. Por eso el límite de 30 intentos por minuto por IP se comparte entre todos. El bloqueo por cuenta (5 contraseñas incorrectas → 15 minutos) sí funciona por usuario. Antes de producción conviene decidir cómo obtener la IP real; ver "Protección del login" en el README.
- **Hora de Costa Rica.** El servidor corre en UTC, pero la API calcula "hoy" (vencimientos, mora, recordatorios) con la zona horaria de Costa Rica.
- **Separar pruebas y producción.** Producción debe tener su propia base, su propio bucket y una llave de R2 limitada a ese bucket, su propia contraseña de administrador y su propia clave JWT.
