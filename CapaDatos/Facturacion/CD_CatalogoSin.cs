using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

using CapaEntidad.Facturacion;

namespace CapaDatos.Facturacion
{
    /// <summary>Acceso a los catálogos del SIN y a las homologaciones.</summary>
    public class CD_CatalogoSin
    {
        public bool ReemplazarCatalogo(string tipo, List<CatalogoSin> registros, out string Mensaje)
        {
            Mensaje = string.Empty;
            try
            {
                DataTable tabla = new DataTable();
                tabla.Columns.Add("codigo", typeof(string));
                tabla.Columns.Add("descripcion", typeof(string));
                tabla.Columns.Add("codigo_actividad", typeof(string));
                foreach (CatalogoSin r in registros)
                    tabla.Rows.Add(r.codigo, r.descripcion, (object)r.codigo_actividad ?? DBNull.Value);

                using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
                {
                    SqlCommand cmd = new SqlCommand("FACTURACION.ReemplazarCatalogo", oconexion);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Tipo", tipo);
                    SqlParameter p = cmd.Parameters.AddWithValue("@Registros", tabla);
                    p.SqlDbType = SqlDbType.Structured;
                    p.TypeName = "FACTURACION.CatalogoTipo";
                    oconexion.Open();
                    cmd.ExecuteNonQuery();
                }
                return true;
            }
            catch (Exception ex)
            {
                Mensaje = "Error al guardar el catálogo " + tipo + ": " + ex.Message;
                return false;
            }
        }

        public List<CatalogoSin> Listar(string tipo)
        {
            List<CatalogoSin> lista = new List<CatalogoSin>();
            try
            {
                using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
                {
                    SqlCommand cmd = new SqlCommand("FACTURACION.ListarCatalogo", oconexion);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Tipo", tipo);
                    oconexion.Open();
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            lista.Add(new CatalogoSin()
                            {
                                tipo = LectorDatos.Texto(dr, "tipo"),
                                codigo = LectorDatos.Texto(dr, "codigo"),
                                descripcion = LectorDatos.Texto(dr, "descripcion"),
                                codigo_actividad = LectorDatos.Texto(dr, "codigo_actividad"),
                                fecha_sincronizacion = LectorDatos.FechaNula(dr, "fecha_sincronizacion")
                            });
                        }
                    }
                }
            }
            catch
            {
                lista = new List<CatalogoSin>();
            }
            return lista;
        }

        public List<ResumenCatalogo> Resumen()
        {
            List<ResumenCatalogo> lista = new List<ResumenCatalogo>();
            try
            {
                using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
                {
                    SqlCommand cmd = new SqlCommand("FACTURACION.ResumenCatalogos", oconexion);
                    cmd.CommandType = CommandType.StoredProcedure;
                    oconexion.Open();
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            lista.Add(new ResumenCatalogo()
                            {
                                tipo = LectorDatos.Texto(dr, "tipo"),
                                cantidad = LectorDatos.Entero(dr, "cantidad"),
                                ultima_sincronizacion = LectorDatos.FechaNula(dr, "ultima_sincronizacion")
                            });
                        }
                    }
                }
            }
            catch
            {
                lista = new List<ResumenCatalogo>();
            }
            return lista;
        }

        public List<Homologacion> ListarHomologacion(string tipo)
        {
            List<Homologacion> lista = new List<Homologacion>();
            try
            {
                using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
                {
                    SqlCommand cmd = new SqlCommand("FACTURACION.ListarHomologacion", oconexion);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Tipo", tipo);
                    oconexion.Open();
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            lista.Add(new Homologacion()
                            {
                                tipo = LectorDatos.Texto(dr, "tipo"),
                                id_registro = LectorDatos.Entero(dr, "id_registro"),
                                descripcion = LectorDatos.Texto(dr, "descripcion"),
                                codigo_sin = LectorDatos.Texto(dr, "codigo_sin"),
                                descripcion_sin = LectorDatos.Texto(dr, "descripcion_sin")
                            });
                        }
                    }
                }
            }
            catch
            {
                lista = new List<Homologacion>();
            }
            return lista;
        }

        public bool GuardarHomologacion(string tipo, int idRegistro, int? codigoSin, out string Mensaje)
        {
            bool resultado = false;
            Mensaje = string.Empty;
            try
            {
                using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
                {
                    SqlCommand cmd = new SqlCommand("FACTURACION.GuardarHomologacion", oconexion);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Tipo", tipo);
                    cmd.Parameters.AddWithValue("@IdRegistro", idRegistro);
                    cmd.Parameters.AddWithValue("@CodigoSin", (object)codigoSin ?? DBNull.Value);
                    cmd.Parameters.Add("@Mensaje", SqlDbType.VarChar, 500).Direction = ParameterDirection.Output;
                    cmd.Parameters.Add("@Resultado", SqlDbType.Bit).Direction = ParameterDirection.Output;
                    oconexion.Open();
                    cmd.ExecuteNonQuery();

                    resultado = Convert.ToBoolean(cmd.Parameters["@Resultado"].Value);
                    Mensaje = cmd.Parameters["@Mensaje"].Value.ToString();
                }
            }
            catch (Exception ex)
            {
                resultado = false;
                Mensaje = "Error al guardar la homologación: " + ex.Message;
            }
            return resultado;
        }
    }
}
