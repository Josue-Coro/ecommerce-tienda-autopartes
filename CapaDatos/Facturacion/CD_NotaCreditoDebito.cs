using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

using CapaEntidad.Facturacion;

namespace CapaDatos.Facturacion
{
    /// <summary>Acceso a datos de las notas de crédito-débito.</summary>
    public class CD_NotaCreditoDebito
    {
        public List<LineaDevolvible> ObtenerLineasDevolvibles(int idFactura)
        {
            List<LineaDevolvible> lista = new List<LineaDevolvible>();
            using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
            {
                SqlCommand cmd = new SqlCommand("FACTURACION.ObtenerFacturaParaNota", oconexion);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@IdFactura", idFactura);
                oconexion.Open();
                using (SqlDataReader dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        lista.Add(new LineaDevolvible
                        {
                            oDetalle = new FacturaDetalle
                            {
                                id_factura_detalle = LectorDatos.Entero(dr, "id_factura_detalle"),
                                actividad_economica = LectorDatos.Texto(dr, "actividad_economica"),
                                codigo_producto_sin = LectorDatos.Entero(dr, "codigo_producto_sin"),
                                codigo_producto = LectorDatos.Texto(dr, "codigo_producto"),
                                descripcion = LectorDatos.Texto(dr, "descripcion"),
                                cantidad = LectorDatos.Decimal(dr, "cantidad"),
                                unidad_medida = LectorDatos.Entero(dr, "unidad_medida"),
                                precio_unitario = LectorDatos.Decimal(dr, "precio_unitario"),
                                monto_descuento = LectorDatos.Decimal(dr, "monto_descuento"),
                                sub_total = LectorDatos.Decimal(dr, "sub_total")
                            },
                            cantidad_devuelta = LectorDatos.Decimal(dr, "cantidad_devuelta")
                        });
                    }
                }
            }
            return lista;
        }

        public long ObtenerSiguienteNumero(int idTienda, out string Mensaje)
        {
            Mensaje = string.Empty;
            try
            {
                using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
                {
                    SqlCommand cmd = new SqlCommand("FACTURACION.ObtenerSiguienteNumeroNota", oconexion);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@IdTienda", idTienda);
                    cmd.Parameters.Add("@Numero", SqlDbType.BigInt).Direction = ParameterDirection.Output;
                    oconexion.Open();
                    cmd.ExecuteNonQuery();
                    return Convert.ToInt64(cmd.Parameters["@Numero"].Value);
                }
            }
            catch (Exception ex)
            {
                Mensaje = "Error al obtener el número de nota: " + ex.Message;
                return 0;
            }
        }

        public int Registrar(NotaCreditoDebito n, out string Mensaje)
        {
            Mensaje = string.Empty;
            try
            {
                DataTable detalle = new DataTable();
                detalle.Columns.Add("factura_detalle_id", typeof(int));
                detalle.Columns.Add("codigo_detalle_transaccion", typeof(byte));
                detalle.Columns.Add("actividad_economica", typeof(string));
                detalle.Columns.Add("codigo_producto_sin", typeof(int));
                detalle.Columns.Add("codigo_producto", typeof(string));
                detalle.Columns.Add("descripcion", typeof(string));
                detalle.Columns.Add("cantidad", typeof(decimal));
                detalle.Columns.Add("unidad_medida", typeof(int));
                detalle.Columns.Add("precio_unitario", typeof(decimal));
                detalle.Columns.Add("monto_descuento", typeof(decimal));
                detalle.Columns.Add("sub_total", typeof(decimal));
                detalle.Columns.Add("id_producto", typeof(int));
                foreach (NotaDetalle d in n.oDetalle)
                {
                    int idProducto;
                    object producto = d.codigo_producto != null && d.codigo_producto.StartsWith("P-")
                                      && int.TryParse(d.codigo_producto.Substring(2), out idProducto) ? (object)idProducto : DBNull.Value;
                    detalle.Rows.Add(d.id_factura_detalle, (byte)d.codigo_detalle_transaccion, d.actividad_economica, d.codigo_producto_sin,
                        d.codigo_producto, d.descripcion, d.cantidad, d.unidad_medida, d.precio_unitario, d.monto_descuento, d.sub_total, producto);
                }

                using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
                {
                    SqlCommand cmd = new SqlCommand("FACTURACION.RegistrarNota", oconexion);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@IdFactura", n.id_factura);
                    cmd.Parameters.AddWithValue("@IdTienda", n.id_tienda);
                    cmd.Parameters.AddWithValue("@NumeroNota", n.numero_nota);
                    cmd.Parameters.AddWithValue("@Cuf", n.cuf);
                    cmd.Parameters.AddWithValue("@Cufd", n.cufd);
                    cmd.Parameters.Add("@FechaEmision", SqlDbType.DateTime2).Value = n.fecha_emision;
                    cmd.Parameters.AddWithValue("@CodigoSucursal", n.codigo_sucursal);
                    cmd.Parameters.AddWithValue("@CodigoPuntoVenta", n.codigo_punto_venta);
                    cmd.Parameters.AddWithValue("@Direccion", n.direccion);
                    cmd.Parameters.AddWithValue("@Municipio", n.municipio);
                    cmd.Parameters.AddWithValue("@Telefono", LectorDatos.Nulo(n.telefono));
                    cmd.Parameters.AddWithValue("@MontoTotalOriginal", n.monto_total_original);
                    cmd.Parameters.AddWithValue("@MontoTotalDevuelto", n.monto_total_devuelto);
                    cmd.Parameters.AddWithValue("@MontoEfectivoCreditoDebito", n.monto_efectivo_credito_debito);
                    cmd.Parameters.AddWithValue("@CodigoExcepcion", n.codigo_excepcion);
                    cmd.Parameters.AddWithValue("@Leyenda", n.leyenda);
                    cmd.Parameters.AddWithValue("@Usuario", n.usuario);
                    cmd.Parameters.AddWithValue("@Motivo", n.motivo);
                    cmd.Parameters.AddWithValue("@ReingresoStock", n.reingreso_stock);
                    cmd.Parameters.AddWithValue("@ArchivoXml", n.archivo_xml);
                    cmd.Parameters.AddWithValue("@HashArchivo", n.hash_archivo);
                    cmd.Parameters.AddWithValue("@CodigoRecepcion", LectorDatos.Nulo(n.codigo_recepcion));
                    cmd.Parameters.AddWithValue("@Estado", n.estado);
                    cmd.Parameters.AddWithValue("@CodigoEstado", n.codigo_estado);
                    cmd.Parameters.AddWithValue("@Mensajes", LectorDatos.Nulo(n.mensajes));
                    SqlParameter p = cmd.Parameters.AddWithValue("@Detalle", detalle);
                    p.SqlDbType = SqlDbType.Structured;
                    p.TypeName = "FACTURACION.DetalleNotaTipo";
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
                Mensaje = "Error al registrar la nota: " + ex.Message;
                return 0;
            }
        }

        public List<NotaCreditoDebito> Listar(DateTime? fechaInicio, DateTime? fechaFin, int? idFactura)
        {
            List<NotaCreditoDebito> lista = new List<NotaCreditoDebito>();
            try
            {
                using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
                {
                    SqlCommand cmd = new SqlCommand("FACTURACION.ListarNotas", oconexion);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@FechaInicio", (object)fechaInicio ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@FechaFin", (object)fechaFin ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@IdFactura", (object)idFactura ?? DBNull.Value);
                    oconexion.Open();
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            lista.Add(new NotaCreditoDebito
                            {
                                id_nota = LectorDatos.Entero(dr, "id_nota"),
                                id_factura = LectorDatos.Entero(dr, "id_factura"),
                                numero_nota = LectorDatos.EnteroLargo(dr, "numero_nota"),
                                cuf = LectorDatos.Texto(dr, "cuf"),
                                fecha_emision = LectorDatos.Fecha(dr, "fecha_emision"),
                                monto_total_original = LectorDatos.Decimal(dr, "monto_total_original"),
                                monto_total_devuelto = LectorDatos.Decimal(dr, "monto_total_devuelto"),
                                monto_efectivo_credito_debito = LectorDatos.Decimal(dr, "monto_efectivo_credito_debito"),
                                motivo = LectorDatos.Texto(dr, "motivo"),
                                reingreso_stock = LectorDatos.Booleano(dr, "reingreso_stock"),
                                estado = LectorDatos.Texto(dr, "estado"),
                                codigo_estado = LectorDatos.Entero(dr, "codigo_estado"),
                                mensajes = LectorDatos.Texto(dr, "mensajes"),
                                fecha_anulacion = LectorDatos.FechaNula(dr, "fecha_anulacion"),
                                motivo_anulacion = LectorDatos.Texto(dr, "motivo_anulacion"),
                                oFactura = new Factura
                                {
                                    id_factura = LectorDatos.Entero(dr, "id_factura"),
                                    numero_factura = LectorDatos.EnteroLargo(dr, "numero_factura"),
                                    nombre_razon_social = LectorDatos.Texto(dr, "nombre_razon_social"),
                                    numero_documento = LectorDatos.Texto(dr, "numero_documento")
                                }
                            });
                        }
                    }
                }
            }
            catch
            {
                lista = new List<NotaCreditoDebito>();
            }
            return lista;
        }

        /// <summary>Obtiene la nota con su detalle (sin la factura original; la completa la capa de negocio).</summary>
        public NotaCreditoDebito Obtener(int idNota, out string Mensaje)
        {
            Mensaje = string.Empty;
            NotaCreditoDebito n = null;
            try
            {
                using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
                {
                    SqlCommand cmd = new SqlCommand("FACTURACION.ObtenerNota", oconexion);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@IdNota", idNota);
                    oconexion.Open();
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            n = new NotaCreditoDebito
                            {
                                id_nota = LectorDatos.Entero(dr, "id_nota"),
                                id_factura = LectorDatos.Entero(dr, "FACTURA_id_factura"),
                                id_tienda = LectorDatos.Entero(dr, "TIENDA_id_tienda"),
                                numero_nota = LectorDatos.EnteroLargo(dr, "numero_nota"),
                                cuf = LectorDatos.Texto(dr, "cuf"),
                                cufd = LectorDatos.Texto(dr, "cufd"),
                                fecha_emision = LectorDatos.Fecha(dr, "fecha_emision"),
                                codigo_sucursal = LectorDatos.Entero(dr, "codigo_sucursal"),
                                codigo_punto_venta = LectorDatos.Entero(dr, "codigo_punto_venta"),
                                direccion = LectorDatos.Texto(dr, "direccion"),
                                municipio = LectorDatos.Texto(dr, "municipio"),
                                telefono = LectorDatos.Texto(dr, "telefono"),
                                monto_total_original = LectorDatos.Decimal(dr, "monto_total_original"),
                                monto_total_devuelto = LectorDatos.Decimal(dr, "monto_total_devuelto"),
                                monto_efectivo_credito_debito = LectorDatos.Decimal(dr, "monto_efectivo_credito_debito"),
                                codigo_excepcion = LectorDatos.Entero(dr, "codigo_excepcion"),
                                leyenda = LectorDatos.Texto(dr, "leyenda"),
                                usuario = LectorDatos.Texto(dr, "usuario"),
                                motivo = LectorDatos.Texto(dr, "motivo"),
                                reingreso_stock = LectorDatos.Booleano(dr, "reingreso_stock"),
                                archivo_xml = LectorDatos.Texto(dr, "archivo_xml"),
                                hash_archivo = LectorDatos.Texto(dr, "hash_archivo"),
                                codigo_recepcion = LectorDatos.Texto(dr, "codigo_recepcion"),
                                estado = LectorDatos.Texto(dr, "estado"),
                                codigo_estado = LectorDatos.Entero(dr, "codigo_estado"),
                                mensajes = LectorDatos.Texto(dr, "mensajes"),
                                fecha_anulacion = LectorDatos.FechaNula(dr, "fecha_anulacion"),
                                codigo_motivo_anulacion = LectorDatos.EnteroNulo(dr, "codigo_motivo_anulacion"),
                                motivo_anulacion = LectorDatos.Texto(dr, "motivo_anulacion_descripcion")
                            };
                        }

                        if (n != null && dr.NextResult())
                        {
                            while (dr.Read())
                            {
                                n.oDetalle.Add(new NotaDetalle
                                {
                                    id_nota_detalle = LectorDatos.Entero(dr, "id_nota_detalle"),
                                    id_factura_detalle = LectorDatos.Entero(dr, "FACTURA_DETALLE_id"),
                                    codigo_detalle_transaccion = LectorDatos.Entero(dr, "codigo_detalle_transaccion"),
                                    actividad_economica = LectorDatos.Texto(dr, "actividad_economica"),
                                    codigo_producto_sin = LectorDatos.Entero(dr, "codigo_producto_sin"),
                                    codigo_producto = LectorDatos.Texto(dr, "codigo_producto"),
                                    descripcion = LectorDatos.Texto(dr, "descripcion"),
                                    cantidad = LectorDatos.Decimal(dr, "cantidad"),
                                    unidad_medida = LectorDatos.Entero(dr, "unidad_medida"),
                                    descripcion_unidad_medida = LectorDatos.Texto(dr, "descripcion_unidad_medida"),
                                    precio_unitario = LectorDatos.Decimal(dr, "precio_unitario"),
                                    monto_descuento = LectorDatos.Decimal(dr, "monto_descuento"),
                                    sub_total = LectorDatos.Decimal(dr, "sub_total")
                                });
                            }
                        }
                    }
                }
                if (n == null)
                    Mensaje = "No se encontró la nota #" + idNota + ".";
            }
            catch (Exception ex)
            {
                n = null;
                Mensaje = "Error al obtener la nota: " + ex.Message;
            }
            return n;
        }

        public bool RegistrarAnulacion(int idNota, int codigoMotivo, int codigoEstado, out string Mensaje)
        {
            Mensaje = string.Empty;
            try
            {
                using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
                {
                    SqlCommand cmd = new SqlCommand("FACTURACION.RegistrarAnulacionNota", oconexion);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@IdNota", idNota);
                    cmd.Parameters.AddWithValue("@CodigoMotivo", codigoMotivo);
                    cmd.Parameters.AddWithValue("@CodigoEstado", codigoEstado);
                    oconexion.Open();
                    if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
                    {
                        Mensaje = "La nota no está en estado VALIDADA; no se registró la anulación.";
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Mensaje = "Error al registrar la anulación de la nota: " + ex.Message;
                return false;
            }
        }

        public int ContarNotasVigentes(int idFactura)
        {
            using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
            {
                SqlCommand cmd = new SqlCommand("FACTURACION.ContarNotasVigentes", oconexion);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@IdFactura", idFactura);
                oconexion.Open();
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }
    }
}
