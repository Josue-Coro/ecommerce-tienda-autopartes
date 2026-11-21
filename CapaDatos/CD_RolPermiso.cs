using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using CapaEntidad;

namespace CapaDatos
{
    public class CD_RolPermiso
    {
        /// <summary>
        /// Obtiene una lista de IDs de permisos asignados a un rol específico.
        /// </summary>
        /// <param name="idRol">El ID del rol para el cual se quieren obtener los permisos.</param>
        /// <returns>Una lista de enteros con los IDs de permisos.</returns>
        /// <summary>
        /// Códigos (nombres) de los permisos activos de un rol activo. Si el rol está inactivo no devuelve ninguno.
        /// </summary>
        public HashSet<string> ObtenerCodigosPorRol(int idRol)
        {
            HashSet<string> codigos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
            {
                SqlCommand cmd = new SqlCommand(@"
                    SELECT P.nombre
                    FROM SEGURIDAD.ROLES_PERMISOS RP
                    INNER JOIN SEGURIDAD.PERMISOS P ON P.id_permiso = RP.PERMISOS_id_permiso AND P.estado = 1
                    INNER JOIN SEGURIDAD.ROLES R ON R.id_rol = RP.ROLES_id_rol AND R.estado = 1
                    WHERE RP.ROLES_id_rol = @IdRol", oconexion);
                cmd.Parameters.AddWithValue("@IdRol", idRol);
                oconexion.Open();
                using (SqlDataReader dr = cmd.ExecuteReader())
                    while (dr.Read())
                        codigos.Add(dr["nombre"].ToString().Trim());
            }
            return codigos;
        }

        /// <summary>
        /// Crea el permiso si no existe (por nombre) y, solo en ese caso, lo asigna a los roles indicados que existan.
        /// Devuelve true si lo creó. No modifica permisos ni asignaciones existentes.
        /// </summary>
        public bool InstalarPermiso(string codigo, string descripcion, IEnumerable<string> rolesPorDefecto)
        {
            using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
            {
                oconexion.Open();
                using (SqlTransaction tx = oconexion.BeginTransaction())
                {
                    SqlCommand existe = new SqlCommand("SELECT COUNT(*) FROM SEGURIDAD.PERMISOS WITH (UPDLOCK, HOLDLOCK) WHERE nombre = @Nombre", oconexion, tx);
                    existe.Parameters.AddWithValue("@Nombre", codigo);
                    if (Convert.ToInt32(existe.ExecuteScalar()) > 0)
                    {
                        tx.Commit();
                        return false;
                    }

                    SqlCommand insertar = new SqlCommand(
                        "INSERT INTO SEGURIDAD.PERMISOS (nombre, descripcion, estado) VALUES (@Nombre, @Descripcion, 1); SELECT CAST(SCOPE_IDENTITY() AS INT);", oconexion, tx);
                    insertar.Parameters.AddWithValue("@Nombre", codigo);
                    insertar.Parameters.AddWithValue("@Descripcion", descripcion.Length > 150 ? descripcion.Substring(0, 150) : descripcion);
                    int idPermiso = Convert.ToInt32(insertar.ExecuteScalar());

                    foreach (string rol in rolesPorDefecto)
                    {
                        SqlCommand asignar = new SqlCommand(@"
                            INSERT INTO SEGURIDAD.ROLES_PERMISOS (ROLES_id_rol, PERMISOS_id_permiso)
                            SELECT R.id_rol, @IdPermiso FROM SEGURIDAD.ROLES R
                            WHERE UPPER(LTRIM(RTRIM(R.nombre))) = @Rol
                              AND NOT EXISTS (SELECT 1 FROM SEGURIDAD.ROLES_PERMISOS X WHERE X.ROLES_id_rol = R.id_rol AND X.PERMISOS_id_permiso = @IdPermiso)",
                            oconexion, tx);
                        asignar.Parameters.AddWithValue("@IdPermiso", idPermiso);
                        asignar.Parameters.AddWithValue("@Rol", rol.ToUpperInvariant());
                        asignar.ExecuteNonQuery();
                    }
                    tx.Commit();
                    return true;
                }
            }
        }

        /// <summary>Estado del usuario y su rol actual (para detectar cambios después de iniciar sesión).</summary>
        public bool ObtenerRolActual(int idUsuario, out bool activo, out int idRol, out string nombreRol)
        {
            activo = false; idRol = 0; nombreRol = null;
            using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
            {
                SqlCommand cmd = new SqlCommand(@"
                    SELECT U.estado, R.id_rol, R.nombre
                    FROM SEGURIDAD.USUARIO U
                    INNER JOIN SEGURIDAD.ROLES R ON R.id_rol = U.ROLES_id_rol
                    WHERE U.id_usuario = @IdUsuario", oconexion);
                cmd.Parameters.AddWithValue("@IdUsuario", idUsuario);
                oconexion.Open();
                using (SqlDataReader dr = cmd.ExecuteReader())
                {
                    if (!dr.Read()) return false;
                    activo = Convert.ToBoolean(dr["estado"]);
                    idRol = Convert.ToInt32(dr["id_rol"]);
                    nombreRol = dr["nombre"].ToString().Trim();
                    return true;
                }
            }
        }

        public List<int> ObtenerPermisosPorRol(int idRol)
        {
            List<int> listaIdPermisos = new List<int>();

            try
            {
                using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
                {
                    oconexion.Open();
                    SqlCommand cmd = new SqlCommand("SELECT PERMISOS_id_permiso FROM SEGURIDAD.ROLES_PERMISOS WHERE ROLES_id_rol = @IdRol", oconexion);
                    cmd.Parameters.AddWithValue("@IdRol", idRol);
                    cmd.CommandType = CommandType.Text;

                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            listaIdPermisos.Add(Convert.ToInt32(dr["PERMISOS_id_permiso"]));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al obtener permisos por rol: " + ex.Message);
                listaIdPermisos = new List<int>();
            }

            return listaIdPermisos;
        }

        /// <summary>
        /// Asigna un nuevo conjunto de permisos a un rol, eliminando los anteriores.
        /// Esta operación se realiza dentro de una transacción para asegurar la integridad.
        /// </summary>
        /// <param name="idRol">El ID del rol al que se asignarán los permisos.</param>
        /// <param name="idsPermisos">Una lista de IDs de permisos que deben ser asignados.</param>
        /// <returns>Verdadero si la operación fue exitosa, falso en caso contrario.</returns>
        public bool AsignarPermisosARol(int idRol, List<int> idsPermisos)
        {
            bool resultado = false;

            // Usamos Conexion.cn directamente aquí
            using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
            {
                oconexion.Open();
                SqlTransaction objTransaccion = null;
                try
                {
                    objTransaccion = oconexion.BeginTransaction();

                    SqlCommand cmdDelete = new SqlCommand("DELETE FROM SEGURIDAD.ROLES_PERMISOS WHERE ROLES_id_rol = @IdRol", oconexion, objTransaccion);
                    cmdDelete.Parameters.AddWithValue("@IdRol", idRol);
                    cmdDelete.CommandType = CommandType.Text;
                    cmdDelete.ExecuteNonQuery();

                    foreach (int idPermiso in idsPermisos)
                    {
                        SqlCommand cmdInsert = new SqlCommand("INSERT INTO SEGURIDAD.ROLES_PERMISOS (ROLES_id_rol, PERMISOS_id_permiso) VALUES (@IdRol, @IdPermiso)", oconexion, objTransaccion);
                        cmdInsert.Parameters.AddWithValue("@IdRol", idRol);
                        cmdInsert.Parameters.AddWithValue("@IdPermiso", idPermiso);
                        cmdInsert.CommandType = CommandType.Text;
                        cmdInsert.ExecuteNonQuery();
                    }

                    objTransaccion.Commit();
                    resultado = true;
                }
                catch (Exception ex)
                {
                    if (objTransaccion != null)
                    {
                        objTransaccion.Rollback();
                    }
                    Console.WriteLine("Error en la asignación de permisos a rol: " + ex.Message);
                    resultado = false;
                }
            }
            return resultado;
        }
    }
}
