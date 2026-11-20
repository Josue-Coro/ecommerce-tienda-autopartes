using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using CapaDatos;
using CapaEntidad;

namespace CapaNegocio
{
    /// <summary>
    /// Decide si un usuario puede usar una pantalla o acción según los permisos de su rol.
    ///  - El rol ADMINISTRADOR tiene siempre todos los permisos (evita quedarse sin acceso al sistema).
    ///  - Para el resto, se usan los permisos asignados al rol en SEGURIDAD.ROLES_PERMISOS (rol y permiso activos).
    ///  - Los permisos de cada rol se guardan en caché un minuto; al modificar roles o permisos la caché se vacía,
    ///    así que los cambios se aplican de inmediato, sin volver a iniciar sesión.
    /// </summary>
    public static class CN_Autorizacion
    {
        private static readonly TimeSpan DuracionCache = TimeSpan.FromMinutes(1);

        private class EntradaCache
        {
            public HashSet<string> Codigos;
            public DateTime Vence;
        }

        private static readonly ConcurrentDictionary<int, EntradaCache> _cache = new ConcurrentDictionary<int, EntradaCache>();
        private static readonly CD_RolPermiso _cd = new CD_RolPermiso();
        private static volatile bool _catalogoInstalado;
        private static readonly object _bloqueoCatalogo = new object();

        public static bool EsSuperusuario(User_activo usuario)
        {
            return usuario != null && usuario.oRol != null
                && string.Equals((usuario.oRol.nombre ?? "").Trim(), PermisoSistema.RolSuperusuario, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>true si el usuario tiene al menos uno de los permisos indicados.</summary>
        public static bool TieneAlguno(User_activo usuario, params string[] permisos)
        {
            if (usuario == null || usuario.id_user_activo == 0 || usuario.oRol == null)
                return false;
            if (EsSuperusuario(usuario))
                return true;
            if (permisos == null || permisos.Length == 0)
                return true;

            HashSet<string> propios = PermisosDelRol(usuario.oRol.id_rol);
            return permisos.Any(p => propios.Contains(p.Trim()));
        }

        /// <summary>Códigos de permiso del rol (desde la caché o la base de datos).</summary>
        public static HashSet<string> PermisosDelRol(int idRol)
        {
            AsegurarCatalogo();
            EntradaCache e;
            if (_cache.TryGetValue(idRol, out e) && e.Vence > DateTime.Now)
                return e.Codigos;

            HashSet<string> codigos;
            try
            {
                codigos = _cd.ObtenerCodigosPorRol(idRol);
            }
            catch
            {
                // Si la base no responde se deniega (no se guarda en caché para reintentar en la próxima petición).
                return new HashSet<string>();
            }
            _cache[idRol] = new EntradaCache { Codigos = codigos, Vence = DateTime.Now.Add(DuracionCache) };
            return codigos;
        }

        private class EntradaUsuario
        {
            public bool Activo;
            public int IdRol;
            public string NombreRol;
            public DateTime Vence;
        }

        private static readonly ConcurrentDictionary<int, EntradaUsuario> _usuarios = new ConcurrentDictionary<int, EntradaUsuario>();

        /// <summary>
        /// Actualiza el rol del usuario de la sesión con el de la base (por si un administrador lo cambió) y devuelve
        /// false si el usuario fue desactivado o eliminado, para cerrar su sesión.
        /// </summary>
        public static bool RefrescarUsuario(User_activo usuario)
        {
            if (usuario == null || usuario.id_user_activo == 0) return false;
            EntradaUsuario e;
            if (!_usuarios.TryGetValue(usuario.id_user_activo, out e) || e.Vence <= DateTime.Now)
            {
                bool activo; int idRol; string nombreRol;
                bool existe;
                try
                {
                    existe = _cd.ObtenerRolActual(usuario.id_user_activo, out activo, out idRol, out nombreRol);
                }
                catch
                {
                    return true;   // si la base no responde, se mantiene la sesión con los datos que tiene
                }
                e = new EntradaUsuario { Activo = existe && activo, IdRol = idRol, NombreRol = nombreRol, Vence = DateTime.Now.Add(DuracionCache) };
                _usuarios[usuario.id_user_activo] = e;
            }
            if (!e.Activo) return false;
            if (usuario.oRol == null) usuario.oRol = new Roles();
            usuario.oRol.id_rol = e.IdRol;
            usuario.oRol.nombre = e.NombreRol;
            return true;
        }

        /// <summary>Vacía la caché. Se llama al guardar usuarios, roles, permisos o asignaciones.</summary>
        public static void Invalidar()
        {
            _cache.Clear();
            _usuarios.Clear();
        }

        /// <summary>El nombre ADMINISTRADOR está reservado al superusuario.</summary>
        public static bool EsNombreReservado(string nombreRol)
        {
            return string.Equals((nombreRol ?? "").Trim(), PermisoSistema.RolSuperusuario, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Crea en la base los permisos del catálogo que falten (y los asigna a los roles por defecto que existan).
        /// Se ejecuta una vez por arranque de la aplicación; si la base no está disponible, se reintenta luego.
        /// </summary>
        public static void AsegurarCatalogo()
        {
            if (_catalogoInstalado) return;
            lock (_bloqueoCatalogo)
            {
                if (_catalogoInstalado) return;
                try
                {
                    foreach (DefinicionPermiso p in PermisoSistema.Catalogo)
                        _cd.InstalarPermiso(p.codigo, p.descripcion, p.roles_por_defecto);
                    _catalogoInstalado = true;
                    Invalidar();
                }
                catch
                {
                    // Base de datos no disponible: se reintentará en la próxima verificación.
                }
            }
        }
    }
}
