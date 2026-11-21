using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

using CapaEntidad.Facturacion;

namespace CapaDatos.Facturacion
{
    /// <summary>Acceso a datos de la configuración del emisor, sucursales, CUIS y CUFD.</summary>
    public class CD_ConfiguracionFacturacion
    {
        public ConfiguracionFacturacion ObtenerConfiguracion(out string Mensaje)
        {
            Mensaje = string.Empty;
            ConfiguracionFacturacion obj = null;
            try
            {
                using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
                {
                    SqlCommand cmd = new SqlCommand("FACTURACION.ObtenerConfiguracion", oconexion);
                    cmd.CommandType = CommandType.StoredProcedure;
                    oconexion.Open();
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            obj = new ConfiguracionFacturacion()
                            {
                                nit = LectorDatos.EnteroLargo(dr, "nit"),
                                razon_social = LectorDatos.Texto(dr, "razon_social"),
                                codigo_sistema = LectorDatos.Texto(dr, "codigo_sistema"),
                                codigo_ambiente = LectorDatos.Entero(dr, "codigo_ambiente"),
                                codigo_modalidad = LectorDatos.Entero(dr, "codigo_modalidad"),
                                actividad_economica = LectorDatos.Texto(dr, "actividad_economica"),
                                modo_simulado = LectorDatos.Booleano(dr, "modo_simulado"),
                                token_delegado = LectorDatos.Texto(dr, "token_delegado"),
                                fecha_actualizacion = LectorDatos.FechaNula(dr, "fecha_actualizacion"),
                                simular_sin_conexion = LectorDatos.Booleano(dr, "simular_sin_conexion")
                            };
                        }
                    }
                }
                if (obj == null)
                    Mensaje = "No existe la configuración de facturación. Ejecute el script BaseDatos/Facturacion/01_Esquema_Facturacion.sql.";
            }
            catch (Exception ex)
            {
                obj = null;
                Mensaje = "Error al obtener la configuración de facturación: " + ex.Message;
            }
            return obj;
        }

        public bool GuardarConfiguracion(ConfiguracionFacturacion obj, out string Mensaje)
        {
            bool resultado = false;
            Mensaje = string.Empty;
            try
            {
                using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
                {
                    SqlCommand cmd = new SqlCommand("FACTURACION.GuardarConfiguracion", oconexion);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Nit", obj.nit);
                    cmd.Parameters.AddWithValue("@RazonSocial", obj.razon_social);
                    cmd.Parameters.AddWithValue("@CodigoSistema", obj.codigo_sistema);
                    cmd.Parameters.AddWithValue("@CodigoAmbiente", obj.codigo_ambiente);
                    cmd.Parameters.AddWithValue("@CodigoModalidad", obj.codigo_modalidad);
                    cmd.Parameters.AddWithValue("@ActividadEconomica", obj.actividad_economica);
                    cmd.Parameters.AddWithValue("@ModoSimulado", obj.modo_simulado);
                    cmd.Parameters.AddWithValue("@TokenDelegado", LectorDatos.Nulo(obj.token_delegado));
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
                Mensaje = "Error al guardar la configuración: " + ex.Message;
            }
            return resultado;
        }

        public List<SucursalFacturacion> ListarSucursales(out string Mensaje)
        {
            Mensaje = string.Empty;
            List<SucursalFacturacion> lista = new List<SucursalFacturacion>();
            try
            {
                using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
                {
                    SqlCommand cmd = new SqlCommand("FACTURACION.ListarSucursales", oconexion);
                    cmd.CommandType = CommandType.StoredProcedure;
                    oconexion.Open();
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            SucursalFacturacion s = new SucursalFacturacion()
                            {
                                id_tienda = LectorDatos.Entero(dr, "id_tienda"),
                                nombre_tienda = LectorDatos.Texto(dr, "nombre_tienda"),
                                direccion_tienda = LectorDatos.Texto(dr, "direccion_tienda"),
                                configurada = LectorDatos.Booleano(dr, "configurada"),
                                codigo_sucursal = LectorDatos.Entero(dr, "codigo_sucursal"),
                                codigo_punto_venta = LectorDatos.Entero(dr, "codigo_punto_venta"),
                                municipio = LectorDatos.Texto(dr, "municipio"),
                                telefono = LectorDatos.Texto(dr, "telefono")
                            };

                            if (dr["id_cuis"] != DBNull.Value)
                            {
                                s.oCuis = new CodigoCuis()
                                {
                                    id_cuis = LectorDatos.Entero(dr, "id_cuis"),
                                    id_tienda = s.id_tienda,
                                    codigo = LectorDatos.Texto(dr, "cuis_codigo"),
                                    fecha_solicitud = LectorDatos.Fecha(dr, "cuis_fecha_solicitud"),
                                    fecha_vigencia = LectorDatos.Fecha(dr, "cuis_fecha_vigencia")
                                };
                            }

                            if (dr["id_cufd"] != DBNull.Value)
                            {
                                s.oCufd = new CodigoCufd()
                                {
                                    id_cufd = LectorDatos.Entero(dr, "id_cufd"),
                                    id_tienda = s.id_tienda,
                                    codigo = LectorDatos.Texto(dr, "cufd_codigo"),
                                    codigo_control = LectorDatos.Texto(dr, "cufd_codigo_control"),
                                    direccion = LectorDatos.Texto(dr, "cufd_direccion"),
                                    fecha_solicitud = LectorDatos.Fecha(dr, "cufd_fecha_solicitud"),
                                    fecha_vigencia = LectorDatos.Fecha(dr, "cufd_fecha_vigencia")
                                };
                            }

                            lista.Add(s);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                lista = new List<SucursalFacturacion>();
                Mensaje = "Error al listar las sucursales: " + ex.Message;
            }
            return lista;
        }

        public bool GuardarSucursal(SucursalFacturacion obj, out string Mensaje)
        {
            bool resultado = false;
            Mensaje = string.Empty;
            try
            {
                using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
                {
                    SqlCommand cmd = new SqlCommand("FACTURACION.GuardarSucursal", oconexion);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@IdTienda", obj.id_tienda);
                    cmd.Parameters.AddWithValue("@CodigoSucursal", obj.codigo_sucursal);
                    cmd.Parameters.AddWithValue("@CodigoPuntoVenta", obj.codigo_punto_venta);
                    cmd.Parameters.AddWithValue("@Municipio", obj.municipio);
                    cmd.Parameters.AddWithValue("@Telefono", LectorDatos.Nulo(obj.telefono));
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
                Mensaje = "Error al guardar la sucursal: " + ex.Message;
            }
            return resultado;
        }

        public bool RegistrarCuis(int idTienda, string codigo, DateTime fechaVigencia, out string Mensaje)
        {
            Mensaje = string.Empty;
            try
            {
                using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
                {
                    SqlCommand cmd = new SqlCommand("FACTURACION.RegistrarCuis", oconexion);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@IdTienda", idTienda);
                    cmd.Parameters.AddWithValue("@Codigo", codigo);
                    cmd.Parameters.AddWithValue("@FechaVigencia", fechaVigencia);
                    oconexion.Open();
                    cmd.ExecuteNonQuery();
                }
                return true;
            }
            catch (Exception ex)
            {
                Mensaje = "Error al registrar el CUIS: " + ex.Message;
                return false;
            }
        }

        public bool RegistrarCufd(int idTienda, string codigo, string codigoControl, string direccion, DateTime fechaVigencia, out string Mensaje)
        {
            Mensaje = string.Empty;
            try
            {
                using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
                {
                    SqlCommand cmd = new SqlCommand("FACTURACION.RegistrarCufd", oconexion);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@IdTienda", idTienda);
                    cmd.Parameters.AddWithValue("@Codigo", codigo);
                    cmd.Parameters.AddWithValue("@CodigoControl", codigoControl);
                    cmd.Parameters.AddWithValue("@Direccion", LectorDatos.Nulo(direccion));
                    cmd.Parameters.AddWithValue("@FechaVigencia", fechaVigencia);
                    oconexion.Open();
                    cmd.ExecuteNonQuery();
                }
                return true;
            }
            catch (Exception ex)
            {
                Mensaje = "Error al registrar el CUFD: " + ex.Message;
                return false;
            }
        }
    }
}
