using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

using CapaEntidad.Facturacion;

namespace CapaDatos.Facturacion
{
    /// <summary>Acceso a datos de facturas, numeración y datos fiscales de clientes.</summary>
    public class CD_Factura
    {
        public VentaParaFacturar ObtenerVentaParaFacturar(int idVenta, out string Mensaje)
        {
            Mensaje = string.Empty;
            VentaParaFacturar venta = null;
            try
            {
                using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
                {
                    SqlCommand cmd = new SqlCommand("FACTURACION.ObtenerVentaParaFacturar", oconexion);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@IdVenta", idVenta);
                    oconexion.Open();
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            venta = new VentaParaFacturar()
                            {
                                id_venta = LectorDatos.Entero(dr, "id_venta"),
                                fecha = LectorDatos.Fecha(dr, "fecha"),
                                monto_total = LectorDatos.Decimal(dr, "monto_total"),
                                id_cliente = LectorDatos.Entero(dr, "id_cliente"),
                                id_tipo_pago = LectorDatos.Entero(dr, "id_tipo_pago"),
                                tipo_pago = LectorDatos.Texto(dr, "tipo_pago"),
                                codigo_metodo_pago = LectorDatos.EnteroNulo(dr, "codigo_metodo_pago"),
                                estado_factura_vigente = LectorDatos.Texto(dr, "estado_factura_vigente")
                            };
                        }

                        if (venta != null && dr.NextResult())
                        {
                            while (dr.Read())
                            {
                                venta.oLineas.Add(new LineaVentaParaFacturar()
                                {
                                    tipo_item = LectorDatos.Texto(dr, "tipo_item"),
                                    id_producto = LectorDatos.EnteroNulo(dr, "id_producto"),
                                    id_servicio = LectorDatos.EnteroNulo(dr, "id_servicio"),
                                    descripcion = LectorDatos.Texto(dr, "descripcion"),
                                    cantidad = LectorDatos.Decimal(dr, "cantidad"),
                                    precio = LectorDatos.Decimal(dr, "precio"),
                                    sub_total = LectorDatos.Decimal(dr, "sub_total"),
                                    codigo_producto_sin = LectorDatos.EnteroNulo(dr, "codigo_producto_sin"),
                                    codigo_unidad_medida = LectorDatos.EnteroNulo(dr, "codigo_unidad_medida")
                                });
                            }
                        }
                    }
                }
                if (venta == null)
                    Mensaje = "No se encontró la venta #" + idVenta + ".";
            }
            catch (Exception ex)
            {
                venta = null;
                Mensaje = "Error al obtener la venta: " + ex.Message;
            }
            return venta;
        }

        public List<VentaPendienteFactura> ListarVentasPendientes(DateTime? fechaInicio, DateTime? fechaFin)
        {
            List<VentaPendienteFactura> lista = new List<VentaPendienteFactura>();
            try
            {
                using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
                {
                    SqlCommand cmd = new SqlCommand("FACTURACION.ListarVentasPendientes", oconexion);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@FechaInicio", (object)fechaInicio ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@FechaFin", (object)fechaFin ?? DBNull.Value);
                    oconexion.Open();
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            lista.Add(new VentaPendienteFactura()
                            {
                                id_venta = LectorDatos.Entero(dr, "id_venta"),
                                fecha = LectorDatos.Fecha(dr, "fecha"),
                                monto_total = LectorDatos.Decimal(dr, "monto_total"),
                                id_cliente = LectorDatos.Entero(dr, "id_cliente"),
                                cliente = LectorDatos.Texto(dr, "cliente"),
                                tipo_pago = LectorDatos.Texto(dr, "tipo_pago")
                            });
                        }
                    }
                }
            }
            catch
            {
                lista = new List<VentaPendienteFactura>();
            }
            return lista;
        }

        public DatosFiscalesCliente ObtenerDatosFiscalesCliente(int idCliente)
        {
            DatosFiscalesCliente obj = null;
            try
            {
                using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
                {
                    SqlCommand cmd = new SqlCommand("FACTURACION.ObtenerDatosFiscalesCliente", oconexion);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@IdCliente", idCliente);
                    oconexion.Open();
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            obj = new DatosFiscalesCliente()
                            {
                                id_cliente = LectorDatos.Entero(dr, "id_cliente"),
                                codigo_tipo_documento = LectorDatos.Entero(dr, "codigo_tipo_documento", 1),
                                numero_documento = LectorDatos.Texto(dr, "numero_documento"),
                                complemento = LectorDatos.Texto(dr, "complemento"),
                                razon_social = LectorDatos.Texto(dr, "razon_social"),
                                correo = LectorDatos.Texto(dr, "correo")
                            };
                        }
                    }
                }
            }
            catch
            {
                obj = null;
            }
            return obj;
        }

        public bool GuardarDatosFiscalesCliente(DatosFiscalesCliente obj, out string Mensaje)
        {
            Mensaje = string.Empty;
            try
            {
                using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
                {
                    SqlCommand cmd = new SqlCommand("FACTURACION.GuardarDatosFiscalesCliente", oconexion);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@IdCliente", obj.id_cliente);
                    cmd.Parameters.AddWithValue("@CodigoTipoDocumento", obj.codigo_tipo_documento);
                    cmd.Parameters.AddWithValue("@NumeroDocumento", obj.numero_documento);
                    cmd.Parameters.AddWithValue("@Complemento", LectorDatos.Nulo(obj.complemento));
                    cmd.Parameters.AddWithValue("@RazonSocial", obj.razon_social);
                    cmd.Parameters.AddWithValue("@Correo", LectorDatos.Nulo(obj.correo));
                    oconexion.Open();
                    cmd.ExecuteNonQuery();
                }
                return true;
            }
            catch (Exception ex)
            {
                Mensaje = "Error al guardar los datos fiscales del cliente: " + ex.Message;
                return false;
            }
        }

        public long ObtenerSiguienteNumero(int idTienda, out string Mensaje)
        {
            Mensaje = string.Empty;
            try
            {
                using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
                {
                    SqlCommand cmd = new SqlCommand("FACTURACION.ObtenerSiguienteNumeroFactura", oconexion);
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
                Mensaje = "Error al obtener el número de factura: " + ex.Message;
                return 0;
            }
        }

        public int Registrar(Factura f, out string Mensaje)
        {
            Mensaje = string.Empty;
            int idFactura = 0;
            try
            {
                DataTable detalle = new DataTable();
                detalle.Columns.Add("actividad_economica", typeof(string));
                detalle.Columns.Add("codigo_producto_sin", typeof(int));
                detalle.Columns.Add("codigo_producto", typeof(string));
                detalle.Columns.Add("descripcion", typeof(string));
                detalle.Columns.Add("cantidad", typeof(decimal));
                detalle.Columns.Add("unidad_medida", typeof(int));
                detalle.Columns.Add("precio_unitario", typeof(decimal));
                detalle.Columns.Add("monto_descuento", typeof(decimal));
                detalle.Columns.Add("sub_total", typeof(decimal));
                detalle.Columns.Add("numero_serie", typeof(string));
                foreach (FacturaDetalle d in f.oDetalle)
                {
                    detalle.Rows.Add(d.actividad_economica, d.codigo_producto_sin, d.codigo_producto, d.descripcion,
                        d.cantidad, d.unidad_medida, d.precio_unitario, d.monto_descuento, d.sub_total,
                        (object)d.numero_serie ?? DBNull.Value);
                }

                using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
                {
                    SqlCommand cmd = new SqlCommand("FACTURACION.RegistrarFactura", oconexion);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@IdVenta", f.id_venta);
                    cmd.Parameters.AddWithValue("@IdTienda", f.id_tienda);
                    cmd.Parameters.AddWithValue("@NitEmisor", f.nit_emisor);
                    cmd.Parameters.AddWithValue("@RazonSocialEmisor", f.razon_social_emisor);
                    cmd.Parameters.AddWithValue("@Municipio", f.municipio);
                    cmd.Parameters.AddWithValue("@Telefono", LectorDatos.Nulo(f.telefono));
                    cmd.Parameters.AddWithValue("@Direccion", f.direccion);
                    cmd.Parameters.AddWithValue("@CodigoSucursal", f.codigo_sucursal);
                    cmd.Parameters.AddWithValue("@CodigoPuntoVenta", f.codigo_punto_venta);
                    cmd.Parameters.AddWithValue("@NumeroFactura", f.numero_factura);
                    cmd.Parameters.AddWithValue("@Cuf", f.cuf);
                    cmd.Parameters.AddWithValue("@Cufd", f.cufd);
                    cmd.Parameters.Add("@FechaEmision", SqlDbType.DateTime2).Value = f.fecha_emision;
                    cmd.Parameters.AddWithValue("@CodigoModalidad", f.codigo_modalidad);
                    cmd.Parameters.AddWithValue("@TipoEmision", f.tipo_emision);
                    cmd.Parameters.AddWithValue("@TipoFactura", f.tipo_factura);
                    cmd.Parameters.AddWithValue("@CodigoDocumentoSector", f.codigo_documento_sector);
                    cmd.Parameters.AddWithValue("@NombreRazonSocial", f.nombre_razon_social);
                    cmd.Parameters.AddWithValue("@CodigoTipoDocumento", f.codigo_tipo_documento);
                    cmd.Parameters.AddWithValue("@NumeroDocumento", f.numero_documento);
                    cmd.Parameters.AddWithValue("@Complemento", LectorDatos.Nulo(f.complemento));
                    cmd.Parameters.AddWithValue("@CodigoCliente", f.codigo_cliente);
                    cmd.Parameters.AddWithValue("@CorreoCliente", LectorDatos.Nulo(f.correo_cliente));
                    cmd.Parameters.AddWithValue("@CodigoMetodoPago", f.codigo_metodo_pago);
                    cmd.Parameters.AddWithValue("@NumeroTarjeta", LectorDatos.Nulo(f.numero_tarjeta));
                    cmd.Parameters.AddWithValue("@MontoTotal", f.monto_total);
                    cmd.Parameters.AddWithValue("@MontoTotalSujetoIva", f.monto_total_sujeto_iva);
                    cmd.Parameters.AddWithValue("@DescuentoAdicional", f.descuento_adicional);
                    cmd.Parameters.AddWithValue("@CodigoMoneda", f.codigo_moneda);
                    cmd.Parameters.AddWithValue("@TipoCambio", f.tipo_cambio);
                    cmd.Parameters.AddWithValue("@MontoTotalMoneda", f.monto_total_moneda);
                    cmd.Parameters.AddWithValue("@CodigoExcepcion", f.codigo_excepcion);
                    cmd.Parameters.AddWithValue("@Leyenda", f.leyenda);
                    cmd.Parameters.AddWithValue("@Usuario", f.usuario);
                    cmd.Parameters.AddWithValue("@ArchivoXml", f.archivo_xml);
                    cmd.Parameters.AddWithValue("@HashArchivo", f.hash_archivo);
                    cmd.Parameters.AddWithValue("@CodigoRecepcion", LectorDatos.Nulo(f.codigo_recepcion));
                    cmd.Parameters.AddWithValue("@Estado", f.estado);
                    cmd.Parameters.AddWithValue("@CodigoEstado", f.codigo_estado);
                    cmd.Parameters.AddWithValue("@Mensajes", LectorDatos.Nulo(f.mensajes));
                    SqlParameter p = cmd.Parameters.AddWithValue("@Detalle", detalle);
                    p.SqlDbType = SqlDbType.Structured;
                    p.TypeName = "FACTURACION.DetalleFacturaTipo";
                    cmd.Parameters.AddWithValue("@IdEvento", (object)f.id_evento ?? DBNull.Value);
                    cmd.Parameters.Add("@Mensaje", SqlDbType.VarChar, 500).Direction = ParameterDirection.Output;
                    cmd.Parameters.Add("@Resultado", SqlDbType.Int).Direction = ParameterDirection.Output;

                    oconexion.Open();
                    cmd.ExecuteNonQuery();

                    idFactura = Convert.ToInt32(cmd.Parameters["@Resultado"].Value);
                    Mensaje = cmd.Parameters["@Mensaje"].Value.ToString();
                }
            }
            catch (Exception ex)
            {
                idFactura = 0;
                Mensaje = "Error al registrar la factura: " + ex.Message;
            }
            return idFactura;
        }

        public List<Factura> Listar(DateTime? fechaInicio, DateTime? fechaFin, string estado)
        {
            List<Factura> lista = new List<Factura>();
            try
            {
                using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
                {
                    SqlCommand cmd = new SqlCommand("FACTURACION.ListarFacturas", oconexion);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@FechaInicio", (object)fechaInicio ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@FechaFin", (object)fechaFin ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Estado", LectorDatos.Nulo(estado));
                    oconexion.Open();
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            lista.Add(new Factura()
                            {
                                id_factura = LectorDatos.Entero(dr, "id_factura"),
                                id_venta = LectorDatos.Entero(dr, "id_venta"),
                                id_tienda = LectorDatos.Entero(dr, "id_tienda"),
                                numero_factura = LectorDatos.EnteroLargo(dr, "numero_factura"),
                                cuf = LectorDatos.Texto(dr, "cuf"),
                                fecha_emision = LectorDatos.Fecha(dr, "fecha_emision"),
                                nombre_razon_social = LectorDatos.Texto(dr, "nombre_razon_social"),
                                codigo_tipo_documento = LectorDatos.Entero(dr, "codigo_tipo_documento"),
                                numero_documento = LectorDatos.Texto(dr, "numero_documento"),
                                complemento = LectorDatos.Texto(dr, "complemento"),
                                monto_total = LectorDatos.Decimal(dr, "monto_total"),
                                estado = LectorDatos.Texto(dr, "estado"),
                                codigo_estado = LectorDatos.Entero(dr, "codigo_estado"),
                                codigo_recepcion = LectorDatos.Texto(dr, "codigo_recepcion"),
                                mensajes = LectorDatos.Texto(dr, "mensajes"),
                                tipo_emision = LectorDatos.Entero(dr, "tipo_emision"),
                                fecha_anulacion = LectorDatos.FechaNula(dr, "fecha_anulacion"),
                                codigo_motivo_anulacion = LectorDatos.EnteroNulo(dr, "codigo_motivo_anulacion"),
                                motivo_anulacion = LectorDatos.Texto(dr, "motivo_anulacion"),
                                codigo_sucursal = LectorDatos.Entero(dr, "codigo_sucursal"),
                                codigo_punto_venta = LectorDatos.Entero(dr, "codigo_punto_venta"),
                                correo_cliente = LectorDatos.Texto(dr, "correo_cliente"),
                                id_evento = LectorDatos.EnteroNulo(dr, "id_evento")
                            });
                        }
                    }
                }
            }
            catch
            {
                lista = new List<Factura>();
            }
            return lista;
        }

        public Factura Obtener(int idFactura, out string Mensaje)
        {
            Mensaje = string.Empty;
            Factura f = null;
            try
            {
                using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
                {
                    SqlCommand cmd = new SqlCommand("FACTURACION.ObtenerFactura", oconexion);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@IdFactura", idFactura);
                    oconexion.Open();
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            f = new Factura()
                            {
                                id_factura = LectorDatos.Entero(dr, "id_factura"),
                                id_venta = LectorDatos.Entero(dr, "VENTA_id_venta"),
                                id_tienda = LectorDatos.Entero(dr, "TIENDA_id_tienda"),
                                nit_emisor = LectorDatos.EnteroLargo(dr, "nit_emisor"),
                                razon_social_emisor = LectorDatos.Texto(dr, "razon_social_emisor"),
                                municipio = LectorDatos.Texto(dr, "municipio"),
                                telefono = LectorDatos.Texto(dr, "telefono"),
                                direccion = LectorDatos.Texto(dr, "direccion"),
                                codigo_sucursal = LectorDatos.Entero(dr, "codigo_sucursal"),
                                codigo_punto_venta = LectorDatos.Entero(dr, "codigo_punto_venta"),
                                numero_factura = LectorDatos.EnteroLargo(dr, "numero_factura"),
                                cuf = LectorDatos.Texto(dr, "cuf"),
                                cufd = LectorDatos.Texto(dr, "cufd"),
                                fecha_emision = LectorDatos.Fecha(dr, "fecha_emision"),
                                codigo_modalidad = LectorDatos.Entero(dr, "codigo_modalidad"),
                                tipo_emision = LectorDatos.Entero(dr, "tipo_emision"),
                                tipo_factura = LectorDatos.Entero(dr, "tipo_factura"),
                                codigo_documento_sector = LectorDatos.Entero(dr, "codigo_documento_sector"),
                                nombre_razon_social = LectorDatos.Texto(dr, "nombre_razon_social"),
                                codigo_tipo_documento = LectorDatos.Entero(dr, "codigo_tipo_documento"),
                                numero_documento = LectorDatos.Texto(dr, "numero_documento"),
                                complemento = LectorDatos.Texto(dr, "complemento"),
                                codigo_cliente = LectorDatos.Texto(dr, "codigo_cliente"),
                                correo_cliente = LectorDatos.Texto(dr, "correo_cliente"),
                                codigo_metodo_pago = LectorDatos.Entero(dr, "codigo_metodo_pago"),
                                numero_tarjeta = LectorDatos.Texto(dr, "numero_tarjeta"),
                                monto_total = LectorDatos.Decimal(dr, "monto_total"),
                                monto_total_sujeto_iva = LectorDatos.Decimal(dr, "monto_total_sujeto_iva"),
                                descuento_adicional = LectorDatos.Decimal(dr, "descuento_adicional"),
                                codigo_moneda = LectorDatos.Entero(dr, "codigo_moneda"),
                                tipo_cambio = LectorDatos.Decimal(dr, "tipo_cambio"),
                                monto_total_moneda = LectorDatos.Decimal(dr, "monto_total_moneda"),
                                codigo_excepcion = LectorDatos.Entero(dr, "codigo_excepcion"),
                                leyenda = LectorDatos.Texto(dr, "leyenda"),
                                usuario = LectorDatos.Texto(dr, "usuario"),
                                archivo_xml = LectorDatos.Texto(dr, "archivo_xml"),
                                hash_archivo = LectorDatos.Texto(dr, "hash_archivo"),
                                codigo_recepcion = LectorDatos.Texto(dr, "codigo_recepcion"),
                                estado = LectorDatos.Texto(dr, "estado"),
                                codigo_estado = LectorDatos.Entero(dr, "codigo_estado"),
                                mensajes = LectorDatos.Texto(dr, "mensajes"),
                                fecha_anulacion = LectorDatos.FechaNula(dr, "fecha_anulacion"),
                                codigo_motivo_anulacion = LectorDatos.EnteroNulo(dr, "codigo_motivo_anulacion"),
                                motivo_anulacion = LectorDatos.Texto(dr, "motivo_anulacion"),
                                id_evento = LectorDatos.EnteroNulo(dr, "EVENTO_id_evento")
                            };
                        }

                        if (f != null && dr.NextResult())
                        {
                            while (dr.Read())
                            {
                                f.oDetalle.Add(new FacturaDetalle()
                                {
                                    id_factura_detalle = LectorDatos.Entero(dr, "id_factura_detalle"),
                                    actividad_economica = LectorDatos.Texto(dr, "actividad_economica"),
                                    codigo_producto_sin = LectorDatos.Entero(dr, "codigo_producto_sin"),
                                    codigo_producto = LectorDatos.Texto(dr, "codigo_producto"),
                                    descripcion = LectorDatos.Texto(dr, "descripcion"),
                                    cantidad = LectorDatos.Decimal(dr, "cantidad"),
                                    unidad_medida = LectorDatos.Entero(dr, "unidad_medida"),
                                    descripcion_unidad_medida = LectorDatos.Texto(dr, "descripcion_unidad_medida"),
                                    precio_unitario = LectorDatos.Decimal(dr, "precio_unitario"),
                                    monto_descuento = LectorDatos.Decimal(dr, "monto_descuento"),
                                    sub_total = LectorDatos.Decimal(dr, "sub_total"),
                                    numero_serie = LectorDatos.Texto(dr, "numero_serie")
                                });
                            }
                        }
                    }
                }
                if (f == null)
                    Mensaje = "No se encontró la factura #" + idFactura + ".";
            }
            catch (Exception ex)
            {
                f = null;
                Mensaje = "Error al obtener la factura: " + ex.Message;
            }
            return f;
        }

        public bool RegistrarAnulacion(int idFactura, int codigoMotivo, int codigoEstado, string mensajes, out string Mensaje)
        {
            Mensaje = string.Empty;
            try
            {
                using (SqlConnection oconexion = new SqlConnection(Conexion.cn))
                {
                    SqlCommand cmd = new SqlCommand("FACTURACION.RegistrarAnulacion", oconexion);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@IdFactura", idFactura);
                    cmd.Parameters.AddWithValue("@CodigoMotivo", codigoMotivo);
                    cmd.Parameters.AddWithValue("@CodigoEstado", codigoEstado);
                    cmd.Parameters.AddWithValue("@Mensajes", LectorDatos.Nulo(mensajes));
                    oconexion.Open();
                    int filas = Convert.ToInt32(cmd.ExecuteScalar());
                    if (filas == 0)
                    {
                        Mensaje = "La factura no está en estado VALIDADA; no se registró la anulación.";
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Mensaje = "Error al registrar la anulación: " + ex.Message;
                return false;
            }
        }
    }
}
