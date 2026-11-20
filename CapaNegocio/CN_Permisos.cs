using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CapaEntidad;
using CapaDatos;

namespace CapaNegocio
{
    public class CN_Permisos
    {
        private CD_Permisos objCapaDatoPermiso = new CD_Permisos();
        private CD_RolPermiso objCapaDatoRolPermiso = new CD_RolPermiso();

        public List<Permisos> Listar()
        {
            return objCapaDatoPermiso.ListarPermisos();
        }

        /// <summary>
        /// Obtiene una lista de objetos Permisos que están asignados a un rol específico.
        /// </summary>
        /// <param name="idRol">El ID del rol.</param>
        /// <returns>Una lista de objetos Permisos.</returns>
        public List<Permisos> ObtenerPermisosPorIdRol(int idRol)
        {
            List<int> idsPermisosAsignados = objCapaDatoRolPermiso.ObtenerPermisosPorRol(idRol);

            List<Permisos> todosLosPermisos = objCapaDatoPermiso.ListarPermisos(); 

            List<Permisos> permisosDelRol = todosLosPermisos
                                            .Where(p => idsPermisosAsignados.Contains(p.id_permiso))
                                            .ToList();

            return permisosDelRol;
        }

        public int Registrar(Permisos obj, out string Mensaje)
        {
            Mensaje = string.Empty; 


            if (string.IsNullOrWhiteSpace(obj.nombre))
            {
                Mensaje = "El nombre del permiso no puede estar vacío.";
                return 0; 
            }
            if (PermisoSistema.EsDelSistema(obj.nombre))
            {
                Mensaje = "Ese código pertenece a un permiso del sistema, que ya existe.";
                return 0;
            }

            int id = objCapaDatoPermiso.Registrar(obj, out Mensaje);
            CN_Autorizacion.Invalidar();
            return id;
        }

        public bool Editar(Permisos obj, out string Mensaje)
        {
            Mensaje = string.Empty; 


            

            if (string.IsNullOrWhiteSpace(obj.nombre))
            {
                Mensaje = "El nombre del permiso no puede estar vacío.";
                return false;
            }

            // Los permisos del sistema solo admiten cambiar la descripción (el código es el que verifica el sistema).
            Permisos actual = objCapaDatoPermiso.ListarPermisos().FirstOrDefault(p => p.id_permiso == obj.id_permiso);
            if (actual != null && PermisoSistema.EsDelSistema(actual.nombre)
                && (!string.Equals(actual.nombre.Trim(), obj.nombre.Trim(), StringComparison.OrdinalIgnoreCase) || !obj.estado))
            {
                Mensaje = "Es un permiso del sistema: solo puede modificar su descripción.";
                return false;
            }
            if ((actual == null || !PermisoSistema.EsDelSistema(actual.nombre)) && PermisoSistema.EsDelSistema(obj.nombre))
            {
                Mensaje = "Ese código pertenece a un permiso del sistema.";
                return false;
            }

            bool ok = objCapaDatoPermiso.Editar(obj, out Mensaje);
            CN_Autorizacion.Invalidar();
            return ok;
        }

        public bool Eliminar(int id_permiso, out string Mensaje)
        {
            Mensaje = string.Empty; 


            

            Permisos actual = objCapaDatoPermiso.ListarPermisos().FirstOrDefault(p => p.id_permiso == id_permiso);
            if (actual != null && PermisoSistema.EsDelSistema(actual.nombre))
            {
                Mensaje = "Es un permiso del sistema y no se puede eliminar. Quítelo de los roles que no deban tenerlo.";
                return false;
            }

            bool ok = objCapaDatoPermiso.Eliminar(id_permiso, out Mensaje);
            CN_Autorizacion.Invalidar();
            return ok;
        }
    }
}
