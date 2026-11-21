using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;

using CapaDatos.Contabilidad;
using CapaEntidad.Contabilidad;

namespace CapaNegocio.Contabilidad
{
    /// <summary>
    /// Libros fiscales con la estructura del Registro de Compras y Ventas (RCV) del SIN, datos de las facturas
    /// de compra y liquidación mensual de IVA (13 %) e IT (3 %).
    /// </summary>
    public class CN_LibrosFiscales
    {
        private readonly CD_Contabilidad _cd = new CD_Contabilidad();
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------------ facturas de compra

        public List<CompraFiscal> ListarCompras(DateTime fechaInicio, DateTime fechaFin)
        {
            return _cd.ListarCompras(fechaInicio, fechaFin);
        }

        public bool GuardarCompraFiscal(CompraFiscal c, out string Mensaje)
        {
            Mensaje = string.Empty;
            if (c == null || c.id_compra <= 0) Mensaje = "Seleccione la compra.";
            else if (c.nit_proveedor <= 0 || c.nit_proveedor > 999999999999) Mensaje = "El NIT del proveedor debe ser numérico (máximo 12 dígitos).";
            else if (string.IsNullOrWhiteSpace(c.razon_social) || c.razon_social.Trim().Length > 240) Mensaje = "Ingrese la razón social del proveedor.";
            else if (string.IsNullOrWhiteSpace(c.codigo_autorizacion) || c.codigo_autorizacion.Trim().Length > 100 || c.codigo_autorizacion.Trim() == "0")
                Mensaje = "Ingrese el código de autorización (CUF) de la factura del proveedor, distinto de cero.";
            else if (c.numero_factura <= 0) Mensaje = "Ingrese el número de la factura del proveedor.";
            else if (c.fecha_factura == null || c.fecha_factura.Value.Date > DateTime.Today) Mensaje = "La fecha de la factura no es válida.";
            else if (c.importe_total <= 0) Mensaje = "El importe total debe ser mayor a cero.";
            else if (c.importe_no_sujeto < 0 || c.descuentos < 0 || c.importe_no_sujeto + c.descuentos > c.importe_total)
                Mensaje = "Los importes no sujetos a crédito fiscal y los descuentos no pueden superar el total.";
            else if (c.tipo_compra < 1 || c.tipo_compra > 5) Mensaje = "Seleccione el tipo de compra (1 a 5).";
            else if (!string.IsNullOrWhiteSpace(c.codigo_control) && c.codigo_control.Trim().Length > 17) Mensaje = "El código de control admite como máximo 17 caracteres.";
            if (!string.IsNullOrEmpty(Mensaje)) return false;

            c.razon_social = c.razon_social.Trim().ToUpperInvariant();
            c.codigo_autorizacion = c.codigo_autorizacion.Trim();
            c.codigo_control = string.IsNullOrWhiteSpace(c.codigo_control) ? null : c.codigo_control.Trim();
            return _cd.GuardarCompraFiscal(c, out Mensaje);
        }

        // ------------------------------------------------------------------ libros

        /// <summary>
        /// Libro de Ventas del mes. Débito fiscal = base × 13 % (por factura, como indica el RCV).
        /// Las facturas anuladas se listan con importes en cero y estado "A"; las emitidas fuera de línea con estado "C".
        /// </summary>
        public List<FilaLibroVentas> LibroVentas(int gestion, int mes)
        {
            List<FilaLibroVentas> filas = new List<FilaLibroVentas>();
            int n = 0;
            foreach (DataRow r in _cd.LibroVentas(gestion, mes).Rows)
            {
                bool anulada = r["estado"].ToString() == "ANULADA";
                decimal total = anulada ? 0m : Convert.ToDecimal(r["monto_total"]) + Convert.ToDecimal(r["descuento_adicional"]);
                decimal descuento = anulada ? 0m : Convert.ToDecimal(r["descuento_adicional"]);
                decimal baseDf = anulada ? 0m : Convert.ToDecimal(r["monto_total_sujeto_iva"]);
                filas.Add(new FilaLibroVentas
                {
                    numero = ++n,
                    fecha_factura = Convert.ToDateTime(r["fecha_emision"]),
                    numero_factura = Convert.ToInt64(r["numero_factura"]),
                    codigo_autorizacion = r["cuf"].ToString(),
                    nit_ci_cliente = r["numero_documento"].ToString(),
                    complemento = r["complemento"] == DBNull.Value ? "" : r["complemento"].ToString(),
                    razon_social = r["nombre_razon_social"].ToString(),
                    importe_total = total,
                    subtotal = total,
                    descuentos = descuento,
                    base_debito_fiscal = baseDf,
                    debito_fiscal = CN_Contabilidad.R(baseDf * CN_Contabilidad.TasaIva),
                    estado = anulada ? "A" : (Convert.ToInt32(r["tipo_emision"]) == 2 ? "C" : "V")
                });
            }
            return filas;
        }

        /// <summary>Libro de Compras del mes (facturas de proveedores registradas, por fecha de factura).</summary>
        public List<FilaLibroCompras> LibroCompras(int gestion, int mes)
        {
            List<FilaLibroCompras> filas = new List<FilaLibroCompras>();
            int n = 0;
            foreach (DataRow r in _cd.LibroCompras(gestion, mes).Rows)
            {
                decimal total = Convert.ToDecimal(r["importe_total"]);
                decimal noSujeto = Convert.ToDecimal(r["importe_no_sujeto"]);
                decimal descuentos = Convert.ToDecimal(r["descuentos"]);
                decimal subtotal = total - noSujeto;
                decimal baseCf = subtotal - descuentos;
                int tipo = Convert.ToInt32(r["tipo_compra"]);
                filas.Add(new FilaLibroCompras
                {
                    numero = ++n,
                    nit_proveedor = Convert.ToInt64(r["nit_proveedor"]),
                    razon_social = r["razon_social"].ToString(),
                    codigo_autorizacion = r["codigo_autorizacion"].ToString(),
                    numero_factura = Convert.ToInt64(r["numero_factura"]),
                    fecha_factura = Convert.ToDateTime(r["fecha_factura"]),
                    importe_total = total,
                    importe_no_sujeto = noSujeto,
                    subtotal = subtotal,
                    descuentos = descuentos,
                    base_credito_fiscal = baseCf,
                    credito_fiscal = CN_Contabilidad.R(baseCf * CN_Contabilidad.TasaIva),
                    tipo_compra = tipo,
                    codigo_control = r["codigo_control"] == DBNull.Value ? "0" : r["codigo_control"].ToString()
                });
            }
            return filas;
        }

        /// <summary>Libro de Ventas en CSV con las 24 columnas de "Ventas Estándar" del RCV.</summary>
        public string LibroVentasCsv(int gestion, int mes)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("N°,ESPECIFICACION,FECHA DE LA FACTURA,N° DE LA FACTURA,CODIGO DE AUTORIZACION,NIT / CI CLIENTE,COMPLEMENTO,"
                        + "NOMBRE O RAZON SOCIAL,IMPORTE TOTAL DE LA VENTA,IMPORTE ICE,IMPORTE IEHD,IMPORTE IPJ,TASAS,OTROS NO SUJETOS AL IVA,"
                        + "EXPORTACIONES Y OPERACIONES EXENTAS,VENTAS GRAVADAS A TASA CERO,SUBTOTAL,DESCUENTOS BONIFICACIONES Y REBAJAS SUJETAS AL IVA,"
                        + "IMPORTE GIFT CARD,IMPORTE BASE PARA DEBITO FISCAL,DEBITO FISCAL,ESTADO,CODIGO DE CONTROL,TIPO DE VENTA");
            foreach (FilaLibroVentas f in LibroVentas(gestion, mes))
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    f.numero.ToString(Inv), "2", f.fecha_factura.ToString("dd/MM/yyyy", Inv), f.numero_factura.ToString(Inv),
                    Csv(f.codigo_autorizacion), Csv(f.nit_ci_cliente), Csv(f.complemento), Csv(f.razon_social),
                    M(f.importe_total), "0.00", "0.00", "0.00", "0.00", "0.00", "0.00", "0.00",
                    M(f.subtotal), M(f.descuentos), "0.00", M(f.base_debito_fiscal), M(f.debito_fiscal), f.estado, "0", "0"
                }));
            }
            return sb.ToString();
        }

        /// <summary>Libro de Compras en CSV con las 23 columnas del Registro de Compras del RCV.</summary>
        public string LibroComprasCsv(int gestion, int mes)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("N°,ESPECIFICACION,NIT PROVEEDOR,RAZON SOCIAL PROVEEDOR,CODIGO DE AUTORIZACION,NUMERO FACTURA,NUMERO DUI/DIM,"
                        + "FECHA DE FACTURA/DUI/DIM,IMPORTE TOTAL COMPRA,IMPORTE ICE,IMPORTE IEHD,IMPORTE IPJ,TASAS,OTRO NO SUJETO A CREDITO FISCAL,"
                        + "IMPORTES EXENTOS,IMPORTE COMPRAS GRAVADAS A TASA CERO,SUBTOTAL,DESCUENTOS/BONIFICACIONES/REBAJAS SUJETAS AL IVA,"
                        + "IMPORTE GIFT CARD,IMPORTE BASE CF,CREDITO FISCAL,TIPO COMPRA,CODIGO DE CONTROL");
            foreach (FilaLibroCompras f in LibroCompras(gestion, mes))
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    f.numero.ToString(Inv), "1", f.nit_proveedor.ToString(Inv), Csv(f.razon_social), Csv(f.codigo_autorizacion),
                    f.numero_factura.ToString(Inv), "0", f.fecha_factura.ToString("dd/MM/yyyy", Inv),
                    M(f.importe_total), "0.00", "0.00", "0.00", "0.00", M(f.importe_no_sujeto), "0.00", "0.00",
                    M(f.subtotal), M(f.descuentos), "0.00", M(f.base_credito_fiscal), M(f.credito_fiscal),
                    f.tipo_compra.ToString(Inv), Csv(f.codigo_control)
                }));
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ liquidación

        /// <summary>
        /// Liquidación de IVA e IT del mes. El saldo a favor del contribuyente se arrastra mes a mes desde el primer
        /// mes con movimiento (sin actualización por UFV). El IT se calcula sobre los ingresos brutos facturados
        /// menos las devoluciones; no se considera la compensación con el IUE.
        /// </summary>
        public LiquidacionImpuestos Liquidacion(int gestion, int mes)
        {
            LiquidacionImpuestos l = null;
            decimal saldoFavor = 0m;
            foreach (TotalesMes t in _cd.TotalesImpositivos(gestion, mes))
            {
                l = new LiquidacionImpuestos
                {
                    gestion = t.gestion,
                    mes = t.mes,
                    ventas_gravadas = t.base_debito_fiscal,
                    debito_fiscal = CN_Contabilidad.R(t.base_debito_fiscal * CN_Contabilidad.TasaIva),
                    compras_gravadas = t.base_credito_fiscal,
                    credito_fiscal_compras = CN_Contabilidad.R(t.base_credito_fiscal * CN_Contabilidad.TasaIva),
                    devoluciones = t.devoluciones,
                    credito_fiscal_devoluciones = t.credito_fiscal_devoluciones,
                    saldo_favor_anterior = saldoFavor
                };
                l.credito_fiscal_total = l.credito_fiscal_compras + l.credito_fiscal_devoluciones;
                decimal diferencia = l.debito_fiscal - l.credito_fiscal_total - l.saldo_favor_anterior;
                l.iva_a_pagar = diferencia > 0 ? diferencia : 0m;
                l.saldo_favor_siguiente = diferencia < 0 ? -diferencia : 0m;
                l.ingresos_brutos = t.base_debito_fiscal - t.devoluciones;
                l.it_a_pagar = l.ingresos_brutos > 0 ? CN_Contabilidad.R(l.ingresos_brutos * CN_Contabilidad.TasaIt) : 0m;
                saldoFavor = l.saldo_favor_siguiente;
            }
            return l ?? new LiquidacionImpuestos { gestion = gestion, mes = mes };
        }

        private static string M(decimal v) { return v.ToString("0.00", Inv); }

        private static string Csv(string v)
        {
            v = v ?? "";
            return v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
        }
    }
}
