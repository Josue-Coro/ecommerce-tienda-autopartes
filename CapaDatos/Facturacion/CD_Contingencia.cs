using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

using CapaEntidad.Facturacion;

namespace CapaDatos.Facturacion
{
    /// <summary>Acceso a datos de eventos significativos, paquetes y facturas fuera de línea.</summary>
    public class CD_Contingencia
    {
        public bool GuardarSimulacionSinConexion(bool valor, out string Mensaje)
        {
            Mensaje = string.Empty;
            try
            {
                using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
                {
                    SqlCommand cmd = new SqlCommand("FACTURACION.GuardarSimulacionSinConexion", oconexion);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@SimularSinConexion", valor);
                    oconexion.Open();
                    cmd.ExecuteNonQuery();
                }
                return true;
            }
            catch (Exception ex)
            {
                Mensaje = "Error al guardar la simulación: " + ex.Message;
                return false;
            }
        }

        /// <summary>Crea el evento. Devuelve el id (> 0), o el id negativo del evento que ya estaba abierto, o 0 si falla.</summary>
        public int IniciarEvento(EventoSignificativo e, out string Mensaje)
        {
            Mensaje = string.Empty;
            try
            {
                using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
                {
                    SqlCommand cmd = new SqlCommand("FACTURACION.IniciarEvento", oconexion);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@IdTienda", e.id_tienda);
                    cmd.Parameters.AddWithValue("@CodigoEvento", e.codigo_evento);
                    cmd.Parameters.AddWithValue("@Descripcion", e.descripcion);
                    cmd.Parameters.Add("@FechaInicio", SqlDbType.DateTime2).Value = e.fecha_inicio;
                    cmd.Parameters.AddWithValue("@CufdEvento", e.cufd_evento);
                    cmd.Parameters.AddWithValue("@CodigoControlEvento", e.codigo_control_evento);
                    cmd.Parameters.AddWithValue("@Automatico", e.automatico);
                    cmd.Parameters.Add("@Mensaje", SqlDbType.VarChar, 500).Direction = ParameterDirection.Output;
                    cmd.Parameters.Add("@Resultado", SqlDbType.Int).Direction = ParameterDirection.Output;
                    oconexion.Open();
                    cmd.ExecuteNonQuery();
                    Mensaje = cmd.Parameters["@Mensaje"].Value.ToString();
                    return Convert.ToInt32(cmd.Parameters["@Resultado"].Value);
                }
            }
            catch (Exception ex)
            {
                Mensaje = "Error al iniciar la contingencia: " + ex.Message;
                return 0;
            }
        }

        public List<EventoSignificativo> ListarEventos(int? idTienda, int? idEvento, bool soloAbiertos)
        {
            List<EventoSignificativo> lista = new List<EventoSignificativo>();
            try
            {
                using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
                {
                    SqlCommand cmd = new SqlCommand("FACTURACION.ListarEventos", oconexion);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@IdTienda", (object)idTienda ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@IdEvento", (object)idEvento ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SoloAbiertos", soloAbiertos);
                    oconexion.Open();
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            lista.Add(new EventoSignificativo
                            {
                                id_evento = LectorDatos.Entero(dr, "id_evento"),
                                id_tienda = LectorDatos.Entero(dr, "id_tienda"),
                                nombre_tienda = LectorDatos.Texto(dr, "nombre_tienda"),
                                codigo_evento = LectorDatos.Entero(dr, "codigo_evento"),
                                descripcion_evento = LectorDatos.Texto(dr, "descripcion_evento"),
                                descripcion = LectorDatos.Texto(dr, "descripcion"),
                                fecha_inicio = LectorDatos.Fecha(dr, "fecha_inicio"),
                                fecha_fin = LectorDatos.FechaNula(dr, "fecha_fin"),
                                cufd_evento = LectorDatos.Texto(dr, "cufd_evento"),
                                codigo_control_evento = LectorDatos.Texto(dr, "codigo_control_evento"),
                                automatico = LectorDatos.Booleano(dr, "automatico"),
                                estado = LectorDatos.Texto(dr, "estado"),
                                codigo_recepcion_evento = LectorDatos.Texto(dr, "codigo_recepcion_evento"),
                                mensajes = LectorDatos.Texto(dr, "mensajes"),
                                cantidad_facturas = LectorDatos.Entero(dr, "cantidad_facturas"),
                                facturas_pendientes = LectorDatos.Entero(dr, "facturas_pendientes")
                            });
                        }
                    }
                }
            }
            catch
            {
                lista = new List<EventoSignificativo>();
            }
            return lista;
        }

        public bool ActualizarEvento(int idEvento, string estado, DateTime? fechaFin, string codigoRecepcion, string mensajes, out string Mensaje)
        {
            Mensaje = string.Empty;
            try
            {
                using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
                {
                    SqlCommand cmd = new SqlCommand("FACTURACION.ActualizarEvento", oconexion);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@IdEvento", idEvento);
                    cmd.Parameters.AddWithValue("@Estado", estado);
                    cmd.Parameters.Add("@FechaFin", SqlDbType.DateTime2).Value = (object)fechaFin ?? DBNull.Value;
                    cmd.Parameters.AddWithValue("@CodigoRecepcionEvento", LectorDatos.Nulo(codigoRecepcion));
                    cmd.Parameters.AddWithValue("@Mensajes", LectorDatos.Nulo(mensajes));
                    oconexion.Open();
                    cmd.ExecuteNonQuery();
                }
                return true;
            }
            catch (Exception ex)
            {
                Mensaje = "Error al actualizar el evento: " + ex.Message;
                return false;
            }
        }

        public List<FacturaPendienteEnvio> ListarFacturasPendientes(int idEvento)
        {
            List<FacturaPendienteEnvio> lista = new List<FacturaPendienteEnvio>();
            using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
            {
                SqlCommand cmd = new SqlCommand("FACTURACION.ListarFacturasPendientesEvento", oconexion);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@IdEvento", idEvento);
                oconexion.Open();
                using (SqlDataReader dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        lista.Add(new FacturaPendienteEnvio
                        {
                            id_factura = LectorDatos.Entero(dr, "id_factura"),
                            numero_factura = LectorDatos.EnteroLargo(dr, "numero_factura"),
                            archivo_xml = LectorDatos.Texto(dr, "archivo_xml")
                        });
                    }
                }
            }
            return lista;
        }

        public void RegistrarPaquete(int idEvento, int cantidad, string hash, string codigoRecepcion, int codigoEstado, string estado, string mensajes)
        {
            using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
            {
                SqlCommand cmd = new SqlCommand("FACTURACION.RegistrarPaquete", oconexion);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@IdEvento", idEvento);
                cmd.Parameters.AddWithValue("@CantidadFacturas", cantidad);
                cmd.Parameters.AddWithValue("@HashArchivo", hash);
                cmd.Parameters.AddWithValue("@CodigoRecepcion", LectorDatos.Nulo(codigoRecepcion));
                cmd.Parameters.AddWithValue("@CodigoEstado", codigoEstado);
                cmd.Parameters.AddWithValue("@Estado", estado);
                cmd.Parameters.AddWithValue("@Mensajes", LectorDatos.Nulo(mensajes));
                oconexion.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public void ActualizarEstadoFactura(int idFactura, string estado, int codigoEstado, string codigoRecepcion, string mensajes)
        {
            using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
            {
                SqlCommand cmd = new SqlCommand("FACTURACION.ActualizarEstadoFactura", oconexion);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@IdFactura", idFactura);
                cmd.Parameters.AddWithValue("@Estado", estado);
                cmd.Parameters.AddWithValue("@CodigoEstado", codigoEstado);
                cmd.Parameters.AddWithValue("@CodigoRecepcion", LectorDatos.Nulo(codigoRecepcion));
                cmd.Parameters.AddWithValue("@Mensajes", LectorDatos.Nulo(mensajes));
                oconexion.Open();
                cmd.ExecuteNonQuery();
            }
        }
    }
}
