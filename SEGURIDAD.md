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

## Control de acceso (permisos)

El acceso se decide por los **permisos** asignados a cada rol (`SEGURIDAD.PERMISOS` / `ROLES_PERMISOS`), desde
*Administración > Roles por permisos*. Los cambios tienen efecto inmediato, sin volver a iniciar sesión. El filtro
`[ValidarSesion(Permisos = "...")]` protege vistas **y** endpoints JSON, y el menú solo muestra lo que el usuario puede usar.

| Permiso | Qué habilita | Roles que lo reciben al instalarse (además de ADMINISTRADOR) |
|---|---|---|
| ADMIN_USUARIOS | Usuarios y contraseñas | ADMINISTRADOR |
| ADMIN_ROLES | Roles, permisos y su asignación | ADMINISTRADOR |
| MANTENEDOR | Categorías, marcas, productos, servicios, tipos de pago, tiendas, unidades | GERENTE |
| VENTAS | Registrar ventas (incluye leer productos, servicios, tipos de pago y clientes) | GERENTE, VENDEDOR |
| COMPRAS | Registrar compras | GERENTE, VENDEDOR |
| CLIENTES / PROVEEDORES / STOCK | Gestión de cada área | GERENTE, VENDEDOR |
| REPORTES | Reportes, historial y exportación a Excel | GERENTE |
| FACTURAS_EMITIR | Emitir y consultar facturas | GERENTE, VENDEDOR |
| FACTURAS_ANULAR | Anular facturas, notas crédito-débito | GERENTE |
| FACTURACION_CONFIG | Configuración SIAT, homologación, contingencia | GERENTE |
| CONTABILIDAD | Plan de cuentas, libros, estados | GERENTE, CONTADOR |
| CONTABILIDAD_CIERRE | Cerrar y reabrir meses | GERENTE |

- El catálogo se instala solo al arrancar la aplicación. A un permiso nuevo se le asignan los roles por defecto **una
  sola vez**; después manda lo que el administrador configure.
- Los permisos del sistema no se pueden renombrar, desactivar ni eliminar (solo cambiar su descripción).
- **ADMINISTRADOR** es superusuario: tiene todos los permisos, su nombre está reservado y el rol no se puede renombrar,
  desactivar ni eliminar. Solo un ADMINISTRADOR puede crear, editar o cambiar la contraseña de otra cuenta ADMINISTRADOR,
  y nadie puede eliminar su propia cuenta.
- Un usuario desactivado (o con el rol desactivado) pierde la sesión en menos de un minuto.

Sin sesión, las vistas redirigen al inicio de sesión y los endpoints responden 401; sin permiso, 403 o "Acceso denegado".

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
- **XSS:** las tablas (DataTables) escapan por defecto todo texto (`Scripts/seguridad.js`, función `esc()`), y las
  vistas que arman HTML con datos de la base (incluida la tienda pública) los escapan.
- **Subida de imágenes:** solo JPG, PNG, GIF o WEBP, hasta 5 MB, verificando el contenido real del archivo.
- No se muestran detalles de errores a usuarios remotos (`customErrors`); cookies `HttpOnly`; sin cabeceras de versión.

## Pendiente

- Regenerar el identificador de sesión al iniciar sesión y forzar HTTPS en producción (`requireSSL`).
- El alta de usuario y el restablecimiento por el administrador envían la contraseña temporal por correo; lo ideal es
  un enlace de un solo uso.
