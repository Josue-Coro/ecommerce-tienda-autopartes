# Seguridad del sistema

## Configuración obligatoria (credenciales fuera del repositorio)

Las credenciales ya no están en el código. Antes de ejecutar el sistema:

1. En `CapaPresentacionAdmin` y en `CapaPresentacionTienda`, copie `ConnectionStrings.config.ejemplo` como
   `ConnectionStrings.config` y complete la cadena de conexión. Sin este archivo el sitio no arranca.
2. En `CapaPresentacionAdmin`, copie `Secretos.config.ejemplo` como `Secretos.config` y complete la cuenta de correo
   (para Gmail, una *contraseña de aplicación*). Sin este archivo el sistema funciona, pero no envía correos.

Ambos archivos están en `.gitignore`. Si publica con Visual Studio, verifique que se copien al servidor.

> **Importante:** la contraseña de `sa` y la contraseña de aplicación de Gmail que estaban en el código siguen en el
> historial público de Git. **Cámbielas** (en SQL Server y en la cuenta de Google) aunque ya no estén en los archivos.
> Se recomienda además un usuario de SQL Server propio de la aplicación, con permisos solo sobre la base, en lugar de `sa`.

## Control de acceso

Todas las acciones del panel exigen sesión iniciada y, según el área, uno de estos roles (filtro `[ValidarSesion]`,
en vistas **y** en los endpoints JSON):

| Área | Roles |
|---|---|
| Administración (usuarios, roles, permisos) | ADMINISTRADOR |
| Mantenedor (categorías, marcas, productos, servicios, tipos de pago, tiendas, unidades) | ADMINISTRADOR, GERENTE — las listas de productos, servicios, tipos de pago y tiendas también VENDEDOR |
| Operaciones (ventas, compras, clientes, proveedores, stock) | ADMINISTRADOR, GERENTE, VENDEDOR |
| Reportes y exportación a Excel | ADMINISTRADOR, GERENTE |
| Facturación | ADMINISTRADOR, GERENTE, VENDEDOR (configuración, homologación, anulaciones y contingencia: ADMINISTRADOR, GERENTE) |
| Contabilidad | ADMINISTRADOR, GERENTE, CONTADOR (cierre de meses: ADMINISTRADOR, GERENTE) |

Sin sesión, las vistas redirigen al inicio de sesión y los endpoints responden 401; sin el rol, 403 o "Acceso denegado".

## Contraseñas e inicio de sesión

- Hash **PBKDF2-SHA256** con sal aleatoria y 100.000 iteraciones (`CN_Seguridad`). Los hashes SHA-256 antiguos se
  migran automáticamente la próxima vez que cada usuario inicia sesión.
- Política: mínimo 8 caracteres, con letras y números. Contraseñas temporales de 12 caracteres.
- Bloqueo de 15 minutos tras 5 intentos fallidos (por correo y por dirección IP).
- Cada usuario solo puede cambiar su propia contraseña; el aviso por correo ya no incluye la contraseña.
- La lista de usuarios ya no envía el hash de la contraseña al navegador.

## Otras protecciones

- **CSRF:** las acciones POST deben llegar por AJAX (cabecera `X-Requested-With`); los formularios de inicio de sesión
  y cambio de contraseña usan token antifalsificación.
- **Subida de imágenes:** solo JPG, PNG, GIF o WEBP, hasta 5 MB, verificando el contenido real del archivo.
- No se muestran detalles de errores a usuarios remotos (`customErrors`); cookies `HttpOnly`; sin cabeceras de versión.

## Pendiente

- Los permisos (`SEGURIDAD.PERMISOS` / `ROLES_PERMISOS`) se pueden asignar desde la pantalla, pero el sistema autoriza
  por el **nombre del rol**; la asignación de permisos todavía no tiene efecto.
- Varias pantallas antiguas muestran textos de la base sin escapar (riesgo de XSS almacenado por usuarios internos).
- Regenerar el identificador de sesión al iniciar sesión y forzar HTTPS en producción (`requireSSL`).
- El alta de usuario y el restablecimiento por el administrador envían la contraseña temporal por correo; lo ideal es
  un enlace de un solo uso.
