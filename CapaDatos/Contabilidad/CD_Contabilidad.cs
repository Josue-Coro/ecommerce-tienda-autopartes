using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

using CapaDatos.Facturacion;
using CapaEntidad.Contabilidad;

namespace CapaDatos.Contabilidad
{
    /// <summary>Acceso a datos del módulo de contabilidad.</summary>
    public class CD_Contabilidad
    {
        // ------------------------------------------------------------------ utilidades

        private static SqlCommand Comando(SqlConnection cn, string sp)
        {
            return new SqlCommand(sp, cn) { CommandType = CommandType.StoredProcedure };
        }

        private static bool EjecutarConResultado(SqlCommand cmd, out string Mensaje)
        {
            cmd.Parameters.Add("@Mensaje", SqlDbType.VarChar, 500).Direction = ParameterDirection.Output;
            cmd.Parameters.Add("@Resultado", SqlDbType.Bit).Direction = ParameterDirection.Output;
            cmd.Connection.Open();
            cmd.ExecuteNonQuery();
            Mensaje = cmd.Parameters["@Mensaje"].Value.ToString();
            return Convert.ToBoolean(cmd.Parameters["@Resultado"].Value);
        }

        // ------------------------------------------------------------------ plan de cuentas

        public List<Cuenta> ListarCuentas()
        {
            List<Cuenta> lista = new List<Cuenta>();
            using (SqlConnection cn = new SqlConnection(Conexion.cn))
            {
                SqlCommand cmd = Comando(cn, "CONTABILIDAD.ListarCuentas");
                cn.Open();
                using (SqlDataReader dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        lista.Add(new Cuenta
                        {
                            id_cuenta = LectorDatos.Entero(dr, "id_cuenta"),
                            codigo = LectorDatos.Texto(dr, "codigo"),
                            nombre = LectorDatos.Texto(dr, "nombre"),
                            nivel = LectorDatos.Entero(dr, "nivel"),
                            id_padre = LectorDatos.EnteroNulo(dr, "id_padre"),
                            codigo_padre = LectorDatos.Texto(dr, "codigo_padre"),
                            tipo = LectorDatos.Texto(dr, "tipo"),
                            naturaleza = LectorDatos.Texto(dr, "naturaleza"),
                            imputable = LectorDatos.Booleano(dr, "imputable"),
                            estado = LectorDatos.Booleano(dr, "estado"),
                            tiene_movimientos = LectorDatos.Booleano(dr, "tiene_movimientos")
                        });
                    }
                }
            }
            return lista;
        }

        public bool GuardarCuenta(Cuenta c, out string Mensaje)
        {
            try
            {
                using (SqlConnection cn = new SqlConnection(Conexion.cn))
                {
                    SqlCommand cmd = Comando(cn, "CONTABILIDAD.GuardarCuenta");
                    cmd.Parameters.AddWithValue("@IdCuenta", c.id_cuenta);
                    cmd.Parameters.AddWithValue("@Codigo", c.codigo ?? "");
                    cmd.Parameters.AddWithValue("@Nombre", c.nombre);
                    cmd.Parameters.AddWithValue("@Imputable", c.imputable);
                    cmd.Parameters.AddWithValue("@Estado", c.estado);
                    return EjecutarConResultado(cmd, out Mensaje);
                }
            }
            catch (Exception ex)
            {
                Mensaje = "Error al guardar la cuenta: " + ex.Message;
                return false;
            }
        }

        public List<CuentaConfig> ListarCuentaConfig()
        {
            List<CuentaConfig> lista = new List<CuentaConfig>();
            using (SqlConnection cn = new SqlConnection(Conexion.cn))
            {
                SqlCommand cmd = Comando(cn, "CONTABILIDAD.ListarCuentaConfig");
                cn.Open();
                using (SqlDataReader dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        lista.Add(new CuentaConfig
                        {
                            clave = LectorDatos.Texto(dr, "clave"),
                            descripcion = LectorDatos.Texto(dr, "descripcion"),
                            id_cuenta = LectorDatos.Entero(dr, "id_cuenta"),
                            codigo = LectorDatos.Texto(dr, "codigo"),
                            nombre = LectorDatos.Texto(dr, "nombre")
                        });
                    }
                }
            }
            return lista;
        }

        public bool GuardarCuentaConfig(string clave, int idCuenta, out string Mensaje)
        {
            try
            {
                using (SqlConnection cn = new SqlConnection(Conexion.cn))
                {
                    SqlCommand cmd = Comando(cn, "CONTABILIDAD.GuardarCuentaConfig");
                    cmd.Parameters.AddWithValue("@Clave", clave);
                    cmd.Parameters.AddWithValue("@IdCuenta", idCuenta);
                    return EjecutarConResultado(cmd, out Mensaje);
                }
            }
            catch (Exception ex)
            {
                Mensaje = "Error al asignar la cuenta: " + ex.Message;
                return false;
            }
        }

        // ------------------------------------------------------------------ asientos

        public int RegistrarAsiento(Asiento a, out string Mensaje)
        {
            try
            {
                DataTable detalle = new DataTable();
                detalle.Columns.Add("CUENTA_id", typeof(int));
                detalle.Columns.Add("debe", typeof(decimal));
                detalle.Columns.Add("haber", typeof(decimal));
                detalle.Columns.Add("glosa", typeof(string));
                foreach (AsientoDetalle d in a.oDetalle)
                    detalle.Rows.Add(d.id_cuenta, d.debe, d.haber, (object)d.glosa ?? DBNull.Value);

                using (SqlConnection cn = new SqlConnection(Conexion.cn))
                {
                    SqlCommand cmd = Comando(cn, "CONTABILIDAD.RegistrarAsiento");
                    cmd.Parameters.Add("@Fecha", SqlDbType.Date).Value = a.fecha.Date;
                    cmd.Parameters.AddWithValue("@Glosa", a.glosa);
                    cmd.Parameters.AddWithValue("@Tipo", a.tipo);
                    cmd.Parameters.AddWithValue("@OrigenTipo", LectorDatos.Nulo(a.origen_tipo));
                    cmd.Parameters.AddWithValue("@OrigenId", (object)a.origen_id ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Usuario", a.usuario);
                    SqlParameter p = cmd.Parameters.AddWithValue("@Detalle", detalle);
                    p.SqlDbType = SqlDbType.Structured;
                    p.TypeName = "CONTABILIDAD.DetalleAsientoTipo";
                    cmd.Parameters.Add("@Mensaje", SqlDbType.VarChar, 500).Direction = ParameterDirection.Output;
                    cmd.Parameters.Add("@Resultado", SqlDbType.Int).Direction = ParameterDirection.Output;
                    cn.Open();
                    cmd.ExecuteNonQuery();
                    Mensaje = cmd.Parameters["@Mensaje"].Value.ToString();
                    return Convert.ToInt32(cmd.Parameters["@Resultado"].Value);
                }
            }
            catch (Exception ex)
            {
                Mensaje = "Error al registrar el asiento: " + ex.Message;
                return 0;
            }
        }

        public bool AnularAsiento(int idAsiento, out string Mensaje)
        {
            try
            {
                using (SqlConnection cn = new SqlConnection(Conexion.cn))
                {
                    SqlCommand cmd = Comando(cn, "CONTABILIDAD.AnularAsiento");
                    cmd.Parameters.AddWithValue("@IdAsiento", idAsiento);
                    return EjecutarConResultado(cmd, out Mensaje);
                }
            }
            catch (Exception ex)
            {
                Mensaje = "Error al anular el asiento: " + ex.Message;
                return false;
            }
        }

        public List<Asiento> ListarAsientos(DateTime fechaInicio, DateTime fechaFin, int? idAsiento)
        {
            List<Asiento> lista = new List<Asiento>();
            using (SqlConnection cn = new SqlConnection(Conexion.cn))
            {
                SqlCommand cmd = Comando(cn, "CONTABILIDAD.ListarAsientos");
                cmd.Parameters.Add("@FechaInicio", SqlDbType.Date).Value = fechaInicio.Date;
                cmd.Parameters.Add("@FechaFin", SqlDbType.Date).Value = fechaFin.Date;
                cmd.Parameters.AddWithValue("@IdAsiento", (object)idAsiento ?? DBNull.Value);
                cn.Open();
                using (SqlDataReader dr = cmd.ExecuteReader())
                {
                    Asiento actual = null;
                    while (dr.Read())
                    {
                        int id = LectorDatos.Entero(dr, "id_asiento");
                        if (actual == null || actual.id_asiento != id)
                        {
                            actual = new Asiento
                            {
                                id_asiento = id,
                                gestion = LectorDatos.Entero(dr, "gestion"),
                                numero = LectorDatos.Entero(dr, "numero"),
                                fecha = LectorDatos.Fecha(dr, "fecha"),
                                glosa = LectorDatos.Texto(dr, "glosa"),
                                tipo = LectorDatos.Texto(dr, "tipo"),
                                origen_tipo = LectorDatos.Texto(dr, "origen_tipo"),
                                origen_id = LectorDatos.EnteroNulo(dr, "origen_id"),
                                estado = LectorDatos.Texto(dr, "estado"),
                                usuario = LectorDatos.Texto(dr, "usuario")
                            };
                            lista.Add(actual);
                        }
                        actual.oDetalle.Add(new AsientoDetalle
                        {
                            id_cuenta = LectorDatos.Entero(dr, "id_cuenta"),
                            codigo = LectorDatos.Texto(dr, "codigo"),
                            nombre = LectorDatos.Texto(dr, "nombre"),
                            debe = LectorDatos.Decimal(dr, "debe"),
                            haber = LectorDatos.Decimal(dr, "haber"),
                            glosa = LectorDatos.Texto(dr, "glosa_linea")
                        });
                    }
                }
            }
            return lista;
        }

        /// <summary>Asiento vigente generado por un documento (para armar su reverso).</summary>
        public Asiento ObtenerAsientoPorOrigen(string origenTipo, int origenId)
        {
            int? id = null;
            using (SqlConnection cn = new SqlConnection(Conexion.cn))
            {
                SqlCommand cmd = new SqlCommand(
                    "SELECT id_asiento FROM CONTABILIDAD.ASIENTO WHERE origen_tipo = @t AND origen_id = @i AND estado = 'VIGENTE'", cn);
                cmd.Parameters.AddWithValue("@t", origenTipo);
                cmd.Parameters.AddWithValue("@i", origenId);
                cn.Open();
                object r = cmd.ExecuteScalar();
                if (r != null && r != DBNull.Value) id = Convert.ToInt32(r);
            }
            if (id == null) return null;
            List<Asiento> l = ListarAsientos(DateTime.MinValue.AddYears(1800), DateTime.MaxValue.AddYears(-1), id);
            return l.Count > 0 ? l[0] : null;
        }

        public LibroMayor LibroMayor(int idCuenta, DateTime fechaInicio, DateTime fechaFin, out decimal debeAnterior, out decimal haberAnterior)
        {
            LibroMayor libro = new LibroMayor();
            debeAnterior = 0; haberAnterior = 0;
            using (SqlConnection cn = new SqlConnection(Conexion.cn))
            {
                SqlCommand cmd = Comando(cn, "CONTABILIDAD.LibroMayor");
                cmd.Parameters.AddWithValue("@IdCuenta", idCuenta);
                cmd.Parameters.Add("@FechaInicio", SqlDbType.Date).Value = fechaInicio.Date;
                cmd.Parameters.Add("@FechaFin", SqlDbType.Date).Value = fechaFin.Date;
                cn.Open();
                using (SqlDataReader dr = cmd.ExecuteReader())
                {
                    if (dr.Read())
                    {
                        debeAnterior = LectorDatos.Decimal(dr, "debe");
                        haberAnterior = LectorDatos.Decimal(dr, "haber");
                    }
                    dr.NextResult();
                    while (dr.Read())
                    {
                        libro.movimientos.Add(new MovimientoMayor
                        {
                            id_asiento = LectorDatos.Entero(dr, "id_asiento"),
                            numero = LectorDatos.Entero(dr, "numero"),
                            fecha = LectorDatos.Fecha(dr, "fecha"),
                            glosa = LectorDatos.Texto(dr, "glosa_linea") ?? LectorDatos.Texto(dr, "glosa"),
                            debe = LectorDatos.Decimal(dr, "debe"),
                            haber = LectorDatos.Decimal(dr, "haber")
                        });
                    }
                }
            }
            return libro;
        }

        public List<SumaCuenta> SumasPorCuenta(DateTime fechaInicio, DateTime fechaFin)
        {
            List<SumaCuenta> lista = new List<SumaCuenta>();
            using (SqlConnection cn = new SqlConnection(Conexion.cn))
            {
                SqlCommand cmd = Comando(cn, "CONTABILIDAD.SumasPorCuenta");
                cmd.Parameters.Add("@FechaInicio", SqlDbType.Date).Value = fechaInicio.Date;
                cmd.Parameters.Add("@FechaFin", SqlDbType.Date).Value = fechaFin.Date;
                cn.Open();
                using (SqlDataReader dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        lista.Add(new SumaCuenta
                        {
                            id_cuenta = LectorDatos.Entero(dr, "id_cuenta"),
                            codigo = LectorDatos.Texto(dr, "codigo"),
                            nombre = LectorDatos.Texto(dr, "nombre"),
                            tipo = LectorDatos.Texto(dr, "tipo"),
                            naturaleza = LectorDatos.Texto(dr, "naturaleza"),
                            debe = LectorDatos.Decimal(dr, "debe"),
                            haber = LectorDatos.Decimal(dr, "haber")
                        });
                    }
                }
            }
            return lista;
        }

        // ------------------------------------------------------------------ periodos

        public List<PeriodoCerrado> ListarPeriodosCerrados()
        {
            List<PeriodoCerrado> lista = new List<PeriodoCerrado>();
            using (SqlConnection cn = new SqlConnection(Conexion.cn))
            {
                SqlCommand cmd = Comando(cn, "CONTABILIDAD.ListarPeriodosCerrados");
                cn.Open();
                using (SqlDataReader dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        lista.Add(new PeriodoCerrado
                        {
                            gestion = LectorDatos.Entero(dr, "gestion"),
                            mes = LectorDatos.Entero(dr, "mes"),
                            usuario = LectorDatos.Texto(dr, "usuario"),
                            fecha_cierre = LectorDatos.Fecha(dr, "fecha_cierre")
                        });
                    }
                }
            }
            return lista;
        }

        public void CambiarCierrePeriodo(int gestion, int mes, bool cerrar, string usuario)
        {
            using (SqlConnection cn = new SqlConnection(Conexion.cn))
            {
                SqlCommand cmd = Comando(cn, "CONTABILIDAD.CambiarCierrePeriodo");
                cmd.Parameters.AddWithValue("@Gestion", gestion);
                cmd.Parameters.AddWithValue("@Mes", mes);
                cmd.Parameters.AddWithValue("@Cerrar", cerrar);
                cmd.Parameters.AddWithValue("@Usuario", usuario);
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        // ------------------------------------------------------------------ documentos pendientes

        public DocumentosPendientes DocumentosPendientes(DateTime fechaInicio, DateTime fechaFin)
        {
            DocumentosPendientes d = new DocumentosPendientes();
            using (SqlConnection cn = new SqlConnection(Conexion.cn))
            {
                SqlCommand cmd = Comando(cn, "CONTABILIDAD.DocumentosPendientes");
                cmd.Parameters.Add("@FechaInicio", SqlDbType.Date).Value = fechaInicio.Date;
                cmd.Parameters.Add("@FechaFin", SqlDbType.Date).Value = fechaFin.Date;
                cn.Open();
                using (SqlDataReader dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                        d.facturas.Add(new FacturaAContabilizar
                        {
                            id = LectorDatos.Entero(dr, "id"),
                            fecha = LectorDatos.Fecha(dr, "fecha"),
                            numero = LectorDatos.EnteroLargo(dr, "numero"),
                            tercero = LectorDatos.Texto(dr, "tercero"),
                            monto_total = LectorDatos.Decimal(dr, "monto_total"),
                            codigo_metodo_pago = LectorDatos.Entero(dr, "codigo_metodo_pago"),
                            monto_productos = LectorDatos.Decimal(dr, "monto_productos"),
                            costo_productos = LectorDatos.Decimal(dr, "costo_productos")
                        });
                    dr.NextResult();
                    while (dr.Read())
                        d.anulaciones_factura.Add(LeerAnulacion(dr));
                    dr.NextResult();
                    while (dr.Read())
                        d.notas.Add(new NotaAContabilizar
                        {
                            id = LectorDatos.Entero(dr, "id"),
                            fecha = LectorDatos.Fecha(dr, "fecha"),
                            numero = LectorDatos.EnteroLargo(dr, "numero"),
                            numero_factura = LectorDatos.EnteroLargo(dr, "numero_factura"),
                            tercero = LectorDatos.Texto(dr, "tercero"),
                            monto_total_devuelto = LectorDatos.Decimal(dr, "monto_total_devuelto"),
                            monto_efectivo_credito_debito = LectorDatos.Decimal(dr, "monto_efectivo_credito_debito"),
                            codigo_metodo_pago = LectorDatos.Entero(dr, "codigo_metodo_pago"),
                            reingreso_stock = LectorDatos.Booleano(dr, "reingreso_stock"),
                            monto_productos = LectorDatos.Decimal(dr, "monto_productos"),
                            costo_productos = LectorDatos.Decimal(dr, "costo_productos")
                        });
                    dr.NextResult();
                    while (dr.Read())
                        d.anulaciones_nota.Add(LeerAnulacion(dr));
                    dr.NextResult();
                    while (dr.Read())
                        d.compras.Add(new CompraAContabilizar
                        {
                            id = LectorDatos.Entero(dr, "id"),
                            fecha = LectorDatos.Fecha(dr, "fecha"),
                            tercero = LectorDatos.Texto(dr, "tercero"),
                            monto_total = LectorDatos.Decimal(dr, "monto_total"),
                            codigo_metodo_pago = LectorDatos.EnteroNulo(dr, "codigo_metodo_pago"),
                            con_factura = LectorDatos.Booleano(dr, "con_factura"),
                            numero_factura = dr["numero_factura"] == DBNull.Value ? (long?)null : Convert.ToInt64(dr["numero_factura"]),
                            tipo_compra = LectorDatos.Entero(dr, "tipo_compra"),
                            base_credito_fiscal = LectorDatos.Decimal(dr, "base_credito_fiscal")
                        });
                }
            }
            return d;
        }

        private static AnulacionAContabilizar LeerAnulacion(IDataRecord dr)
        {
            return new AnulacionAContabilizar
            {
                id = LectorDatos.Entero(dr, "id"),
                fecha = LectorDatos.Fecha(dr, "fecha"),
                numero = LectorDatos.EnteroLargo(dr, "numero")
            };
        }

        // ------------------------------------------------------------------ compras fiscales

        public List<CompraFiscal> ListarCompras(DateTime fechaInicio, DateTime fechaFin)
        {
            List<CompraFiscal> lista = new List<CompraFiscal>();
            using (SqlConnection cn = new SqlConnection(Conexion.cn))
            {
                SqlCommand cmd = Comando(cn, "CONTABILIDAD.ListarCompras");
                cmd.Parameters.Add("@FechaInicio", SqlDbType.Date).Value = fechaInicio.Date;
                cmd.Parameters.Add("@FechaFin", SqlDbType.Date).Value = fechaFin.Date;
                cn.Open();
                using (SqlDataReader dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        bool registrada = dr["codigo_autorizacion"] != DBNull.Value;
                        lista.Add(new CompraFiscal
                        {
                            id_compra = LectorDatos.Entero(dr, "id_compra"),
                            fecha_compra = LectorDatos.Fecha(dr, "fecha"),
                            monto_compra = LectorDatos.Decimal(dr, "monto_total"),
                            proveedor = LectorDatos.Texto(dr, "proveedor"),
                            nit_proveedor_registrado = LectorDatos.Texto(dr, "nit_proveedor_registrado"),
                            registrada = registrada,
                            contabilizada = LectorDatos.Booleano(dr, "contabilizada"),
                            nit_proveedor = LectorDatos.EnteroLargo(dr, "nit_proveedor"),
                            razon_social = LectorDatos.Texto(dr, "razon_social"),
                            codigo_autorizacion = LectorDatos.Texto(dr, "codigo_autorizacion"),
                            numero_factura = LectorDatos.EnteroLargo(dr, "numero_factura"),
                            fecha_factura = LectorDatos.FechaNula(dr, "fecha_factura"),
                            importe_total = LectorDatos.Decimal(dr, "importe_total"),
                            importe_no_sujeto = LectorDatos.Decimal(dr, "importe_no_sujeto"),
                            descuentos = LectorDatos.Decimal(dr, "descuentos"),
                            tipo_compra = LectorDatos.Entero(dr, "tipo_compra", 1),
                            codigo_control = LectorDatos.Texto(dr, "codigo_control")
                        });
                    }
                }
            }
            return lista;
        }

        public bool GuardarCompraFiscal(CompraFiscal c, out string Mensaje)
        {
            try
            {
                using (SqlConnection cn = new SqlConnection(Conexion.cn))
                {
                    SqlCommand cmd = Comando(cn, "CONTABILIDAD.GuardarCompraFiscal");
                    cmd.Parameters.AddWithValue("@IdCompra", c.id_compra);
                    cmd.Parameters.AddWithValue("@NitProveedor", c.nit_proveedor);
                    cmd.Parameters.AddWithValue("@RazonSocial", c.razon_social);
                    cmd.Parameters.AddWithValue("@CodigoAutorizacion", c.codigo_autorizacion);
                    cmd.Parameters.AddWithValue("@NumeroFactura", c.numero_factura);
                    cmd.Parameters.Add("@FechaFactura", SqlDbType.Date).Value = c.fecha_factura.Value.Date;
                    cmd.Parameters.AddWithValue("@ImporteTotal", c.importe_total);
                    cmd.Parameters.AddWithValue("@ImporteNoSujeto", c.importe_no_sujeto);
                    cmd.Parameters.AddWithValue("@Descuentos", c.descuentos);
                    cmd.Parameters.AddWithValue("@TipoCompra", c.tipo_compra);
                    cmd.Parameters.AddWithValue("@CodigoControl", LectorDatos.Nulo(c.codigo_control));
                    return EjecutarConResultado(cmd, out Mensaje);
                }
            }
            catch (Exception ex)
            {
                Mensaje = "Error al guardar los datos de la factura del proveedor: " + ex.Message;
                return false;
            }
        }

        // ------------------------------------------------------------------ libros fiscales e impuestos

        public DataTable LibroVentas(int gestion, int mes) { return Tabla("CONTABILIDAD.LibroVentas", gestion, mes); }

        public DataTable LibroCompras(int gestion, int mes) { return Tabla("CONTABILIDAD.LibroCompras", gestion, mes); }

        public List<TotalesMes> TotalesImpositivos(int gestion, int mes)
        {
            List<TotalesMes> lista = new List<TotalesMes>();
            foreach (DataRow r in Tabla("CONTABILIDAD.TotalesImpositivos", gestion, mes).Rows)
            {
                lista.Add(new TotalesMes
                {
                    gestion = Convert.ToInt32(r["gestion"]),
                    mes = Convert.ToInt32(r["mes"]),
                    base_debito_fiscal = Convert.ToDecimal(r["base_debito_fiscal"]),
                    devoluciones = Convert.ToDecimal(r["devoluciones"]),
                    credito_fiscal_devoluciones = Convert.ToDecimal(r["credito_fiscal_devoluciones"]),
                    base_credito_fiscal = Convert.ToDecimal(r["base_credito_fiscal"])
                });
            }
            return lista;
        }

        private static DataTable Tabla(string sp, int gestion, int mes)
        {
            DataTable t = new DataTable();
            using (SqlConnection cn = new SqlConnection(Conexion.cn))
            {
                SqlCommand cmd = Comando(cn, sp);
                cmd.Parameters.AddWithValue("@Gestion", gestion);
                cmd.Parameters.AddWithValue("@Mes", mes);
                using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                    da.Fill(t);
            }
            return t;
        }
    }
}
