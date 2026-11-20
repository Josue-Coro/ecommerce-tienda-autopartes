using CapaDatos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CapaEntidad;

namespace CapaNegocio
{
    public class CN_Roles
    {
        private CD_Roles objRol = new CD_Roles();

        public List<Roles> Listar()
        {
            return objRol.ListarRoles();
        }

        public int Registrar(Roles obj, out string Mensaje)
        {
            Mensaje = string.Empty;
            if (string.IsNullOrEmpty(obj.nombre) || string.IsNullOrWhiteSpace(obj.nombre))
            {
                Mensaje = "El nombre del rol no puede ser vacio";
            }
            else if (string.IsNullOrEmpty(obj.descripcion) || string.IsNullOrWhiteSpace(obj.descripcion))
            {
                Mensaje = "La descripcion del rol no puede ser vacio";
            }
            else if (CN_Autorizacion.EsNombreReservado(obj.nombre))
            {
                Mensaje = "El nombre ADMINISTRADOR está reservado.";
            }
            if (string.IsNullOrEmpty(Mensaje))
            {
                int id = objRol.Registrar(obj, out Mensaje);
                CN_Autorizacion.Invalidar();
                return id;
            }
            else
            {
                return 0;
            }
        }

        public bool Editar(Roles obj, out string Mensaje)
        {
            Mensaje = string.Empty;
            if (string.IsNullOrEmpty(obj.nombre) || string.IsNullOrWhiteSpace(obj.nombre))
            {
                Mensaje = "El nombre del rol no puede ser vacio";
            }
            else if (string.IsNullOrEmpty(obj.descripcion) || string.IsNullOrWhiteSpace(obj.descripcion))
            {
                Mensaje = "La descripcion del rol no puede ser vacio";
            }
            if (string.IsNullOrEmpty(Mensaje))
            {
                // El rol ADMINISTRADOR (superusuario) no se renombra ni se desactiva, y ningún otro rol puede tomar ese nombre.
                Roles actual = ObtenerPorId(obj.id_rol);
                bool eraAdmin = actual != null && CN_Autorizacion.EsNombreReservado(actual.nombre);
                if (eraAdmin && (!CN_Autorizacion.EsNombreReservado(obj.nombre) || !obj.estado))
                    Mensaje = "El rol ADMINISTRADOR no se puede renombrar ni desactivar.";
                else if (!eraAdmin && CN_Autorizacion.EsNombreReservado(obj.nombre))
                    Mensaje = "El nombre ADMINISTRADOR está reservado.";
            }
            if (string.IsNullOrEmpty(Mensaje))
            {
                bool ok = objRol.Editar(obj, out Mensaje);
                CN_Autorizacion.Invalidar();
                return ok;
            }
            else
            {
                return false;
            }
        }

        public bool Eliminar(int id_rol, out string Mensaje)
        {
            Mensaje = string.Empty;

            Roles actual = ObtenerPorId(id_rol);
            if (actual != null && CN_Autorizacion.EsNombreReservado(actual.nombre))
            {
                Mensaje = "El rol ADMINISTRADOR no se puede eliminar.";
                return false;
            }
            bool eliminado = objRol.Eliminar(id_rol, out Mensaje);
            CN_Autorizacion.Invalidar();
            if (!eliminado)
            {
                Mensaje = "El rol se encuentra relacionado a un usuario. No se puede eliminar.";
            }
            return eliminado;
        }

        /// <summary>
        /// Obtiene un objeto Rol de la base de datos por su ID.
        /// </summary>
        /// <param name="idRol">El ID del rol a obtener.</param>
        /// <returns>Un objeto Rol si se encuentra, null en caso contrario.</returns>
        public Roles ObtenerPorId(int idRol)
        {
            if (idRol <= 0)
            {
                // Manejo de error o log si el ID es inválido
                Console.WriteLine("CN_Rol: ID de Rol inválido para ObtenerPorId.");
                return null;
            }
            return objRol.ObtenerPorId(idRol);
        }


    }
}
