using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using CapaDatos.Contabilidad;
using CapaEntidad.Contabilidad;

namespace CapaNegocio.Contabilidad
{
    /// <summary>Resultado de contabilizar los documentos de un periodo.</summary>
    public class ResultadoContabilizacion
    {
        public int asientos_generados { get; set; }
        public List<string> errores { get; set; } = new List<string>();
        public string resumen { get; set; }
    }

    /// <summary>
    /// Contabilidad: plan de cuentas, asientos automáticos a partir de facturas, notas y compras,
    /// asientos manuales, cierre de meses y reportes (diario, mayor, comprobación, resultados, balance).
    ///
    /// Asientos automáticos (precios con IVA incluido):
    ///  - Factura: Caja/Bancos/Ctas. por cobrar  a  Ventas + Servicios (87 %) y Débito fiscal IVA (13 %);
    ///             IT 3 % (gasto a IT por pagar); costo de ventas al último precio de compra (sin IVA).
    ///  - Nota C/D: Devoluciones sobre ventas (87 %) y Crédito fiscal (13 %)  a  Caja/Bancos; reverso del IT;
    ///             si reingresa al inventario, reverso del costo.
    ///  - Compra:  Inventario (87 %) y Crédito fiscal (13 %, solo con factura del proveedor)  a  Caja/Bancos/Ctas. por pagar.
    ///  - Anulaciones: asiento inverso al del documento original.
    /// </summary>
    public class CN_Contabilidad
    {
        public const decimal TasaIva = 0.13m;
        public const decimal TasaIt = 0.03m;
        public const string Manual = "MANUAL";
        public const string Automatico = "AUTOMATICO";

        private readonly CD_Contabilidad _cd = new CD_Contabilidad();

        public static decimal R(decimal v) { return Math.Round(v, 2, MidpointRounding.AwayFromZero); }

        // ------------------------------------------------------------------ plan de cuentas

        public List<Cuenta> ListarCuentas() { return _cd.ListarCuentas(); }

        public bool GuardarCuenta(Cuenta c, out string Mensaje)
        {
            Mensaje = string.Empty;
            if (c == null) Mensaje = "No se recibieron los datos de la cuenta.";
            else if (c.id_cuenta == 0 && (string.IsNullOrWhiteSpace(c.codigo) || !Regex.IsMatch(c.codigo.Trim(), @"^[1-9](\.\d{1,3})+$")))
                Mensaje = "El código debe tener el formato del plan de cuentas, por ejemplo 5.2.1.07.";
            else if (string.IsNullOrWhiteSpace(c.nombre) || c.nombre.Trim().Length > 150)
                Mensaje = "El nombre es obligatorio (máximo 150 caracteres).";
            if (!string.IsNullOrEmpty(Mensaje)) return false;

            c.codigo = (c.codigo ?? "").Trim();
            c.nombre = c.nombre.Trim();
            return _cd.GuardarCuenta(c, out Mensaje);
        }

        public List<CuentaConfig> ListarCuentaConfig() { return _cd.ListarCuentaConfig(); }

        public bool GuardarCuentaConfig(string clave, int idCuenta, out string Mensaje)
        {
            return _cd.GuardarCuentaConfig(clave, idCuenta, out Mensaje);
        }

        // ------------------------------------------------------------------ asientos manuales

        public int RegistrarAsientoManual(Asiento a, string usuario, out string Mensaje)
        {
            Mensaje = string.Empty;
            if (a == null || a.oDetalle == null) Mensaje = "No se recibieron los datos del asiento.";
            else if (a.fecha == DateTime.MinValue) Mensaje = "Indique la fecha del asiento.";
            else if (string.IsNullOrWhiteSpace(a.glosa) || a.glosa.Trim().Length > 500) Mensaje = "La glosa es obligatoria (máximo 500 caracteres).";
            else if (a.oDetalle.Count < 2) Mensaje = "El asiento necesita al menos dos líneas.";
            else if (a.oDetalle.Any(d => d.debe < 0 || d.haber < 0 || (d.debe > 0 && d.haber > 0) || d.debe + d.haber == 0))
                Mensaje = "Cada línea debe tener un importe en el debe o en el haber (no en ambos).";
            else if (a.oDetalle.Any(d => R(d.debe) != d.debe || R(d.haber) != d.haber))
                Mensaje = "Los importes admiten como máximo dos decimales.";
            else if (a.oDetalle.Sum(d => d.debe) != a.oDetalle.Sum(d => d.haber))
                Mensaje = "El asiento no cuadra: debe " + a.oDetalle.Sum(d => d.debe).ToString("N2") + " / haber " + a.oDetalle.Sum(d => d.haber).ToString("N2") + ".";
            if (!string.IsNullOrEmpty(Mensaje)) return 0;

            a.glosa = a.glosa.Trim();
            a.tipo = Manual;
            a.origen_tipo = null;
            a.origen_id = null;
            a.usuario = usuario;
            return _cd.RegistrarAsiento(a, out Mensaje);
        }

        public bool AnularAsiento(int idAsiento, out string Mensaje) { return _cd.AnularAsiento(idAsiento, out Mensaje); }

        // ------------------------------------------------------------------ asientos automáticos

        /// <summary>
        /// Genera los asientos de los documentos del rango que todavía no los tienen. Es idempotente:
        /// volver a ejecutarlo no duplica asientos.
        /// </summary>
        public ResultadoContabilizacion Contabilizar(DateTime fechaInicio, DateTime fechaFin, string usuario)
        {
            ResultadoContabilizacion res = new ResultadoContabilizacion();
            Dictionary<string, int> cuentas = _cd.ListarCuentaConfig().ToDictionary(c => c.clave, c => c.id_cuenta);
            DocumentosPendientes docs = _cd.DocumentosPendientes(fechaInicio, fechaFin);
            string msg;

            // 1) Facturas (también las ya anuladas: primero se registra la venta y luego su reverso).
            foreach (FacturaAContabilizar f in docs.facturas)
                Registrar(res, AsientoFactura(f, cuentas, usuario), "Factura N° " + f.numero);

            // 2) Notas de crédito-débito
            foreach (NotaAContabilizar n in docs.notas)
                Registrar(res, AsientoNota(n, cuentas, usuario), "Nota N° " + n.numero);

            // 3) Anulaciones: asiento inverso al original (el original ya debe existir).
            foreach (AnulacionAContabilizar a in docs.anulaciones_factura)
                Registrar(res, Reverso(OrigenAsiento.Factura, OrigenAsiento.AnulacionFactura, a, "Anulación de la factura N° " + a.numero, usuario, out msg),
                          "Anulación de la factura N° " + a.numero, msg);
            foreach (AnulacionAContabilizar a in docs.anulaciones_nota)
                Registrar(res, Reverso(OrigenAsiento.Nota, OrigenAsiento.AnulacionNota, a, "Anulación de la nota N° " + a.numero, usuario, out msg),
                          "Anulación de la nota N° " + a.numero, msg);

            // 4) Compras
            foreach (CompraAContabilizar c in docs.compras)
                Registrar(res, AsientoCompra(c, cuentas, usuario), "Compra #" + c.id);

            int total = docs.facturas.Count + docs.notas.Count + docs.anulaciones_factura.Count + docs.anulaciones_nota.Count + docs.compras.Count;
            res.resumen = total == 0
                ? "No hay documentos pendientes de contabilizar en el periodo."
                : res.asientos_generados + " asiento(s) generado(s) de " + total + " documento(s) pendiente(s)."
                  + (res.errores.Count > 0 ? " " + res.errores.Count + " no se pudieron contabilizar." : "");
            return res;
        }

        private void Registrar(ResultadoContabilizacion res, Asiento a, string documento, string motivo = null)
        {
            if (a == null)
            {
                res.errores.Add(documento + ": " + (motivo ?? "no se pudo armar el asiento."));
                return;
            }
            string msg;
            if (_cd.RegistrarAsiento(a, out msg) > 0)
                res.asientos_generados++;
            else
                res.errores.Add(documento + ": " + msg);
        }

        /// <summary>Cuenta de cobro/pago según el método de pago del SIN.</summary>
        public static string ClavePago(int? codigoMetodoPago, bool esCompra)
        {
            switch (codigoMetodoPago ?? 1)
            {
                case 2:   // tarjeta
                case 3:   // cheque
                case 7:   // transferencia bancaria
                case 8:   // depósito en cuenta
                    return ClaveCuenta.Bancos;
                case 6:   // pago posterior (crédito)
                    return esCompra ? ClaveCuenta.CuentasPagar : ClaveCuenta.CuentasCobrar;
                default:
                    return ClaveCuenta.Caja;
            }
        }

        public static Asiento AsientoFactura(FacturaAContabilizar f, Dictionary<string, int> cuentas, string usuario)
        {
            decimal total = f.monto_total;
            decimal debito = R(total * TasaIva);
            decimal servicios = total - f.monto_productos;
            decimal netoProductos = servicios == 0 ? total - debito : R(f.monto_productos * (1 - TasaIva));
            decimal netoServicios = total - debito - netoProductos;
            decimal it = R(total * TasaIt);
            decimal costo = R(f.costo_productos);

            Asiento a = Nuevo(f.fecha, "Venta según factura N° " + f.numero + " - " + f.tercero, OrigenAsiento.Factura, f.id, usuario);
            Linea(a, cuentas, ClavePago(f.codigo_metodo_pago, false), total, 0, "Cobro de la factura N° " + f.numero);
            Linea(a, cuentas, ClaveCuenta.Ventas, 0, netoProductos, "Venta de mercaderías");
            Linea(a, cuentas, ClaveCuenta.Servicios, 0, netoServicios, "Ingresos por servicios");
            Linea(a, cuentas, ClaveCuenta.DebitoFiscal, 0, debito, "IVA débito fiscal 13%");
            Linea(a, cuentas, ClaveCuenta.ItGasto, it, 0, "IT 3% sobre la venta");
            Linea(a, cuentas, ClaveCuenta.ItPorPagar, 0, it, "IT 3% por pagar");
            Linea(a, cuentas, ClaveCuenta.CostoVentas, costo, 0, "Costo de las mercaderías vendidas");
            Linea(a, cuentas, ClaveCuenta.Inventario, 0, costo, "Salida de inventario");
            return a;
        }

        public static Asiento AsientoNota(NotaAContabilizar n, Dictionary<string, int> cuentas, string usuario)
        {
            decimal devuelto = n.monto_total_devuelto;
            decimal credito = n.monto_efectivo_credito_debito;
            decimal it = R(devuelto * TasaIt);
            decimal costo = n.reingreso_stock ? R(n.costo_productos) : 0m;

            Asiento a = Nuevo(n.fecha, "Devolución según nota crédito-débito N° " + n.numero + " (factura N° " + n.numero_factura + ") - " + n.tercero,
                              OrigenAsiento.Nota, n.id, usuario);
            Linea(a, cuentas, ClaveCuenta.Devoluciones, devuelto - credito, 0, "Devolución sobre ventas");
            Linea(a, cuentas, ClaveCuenta.CreditoFiscal, credito, 0, "IVA crédito fiscal por devolución 13%");
            Linea(a, cuentas, ClavePago(n.codigo_metodo_pago, false), 0, devuelto, "Reembolso al cliente");
            Linea(a, cuentas, ClaveCuenta.ItPorPagar, it, 0, "Reverso del IT 3%");
            Linea(a, cuentas, ClaveCuenta.ItGasto, 0, it, "Reverso del IT 3%");
            Linea(a, cuentas, ClaveCuenta.Inventario, costo, 0, "Reingreso de mercaderías al inventario");
            Linea(a, cuentas, ClaveCuenta.CostoVentas, 0, costo, "Reverso del costo de ventas");
            return a;
        }

        public static Asiento AsientoCompra(CompraAContabilizar c, Dictionary<string, int> cuentas, string usuario)
        {
            bool conCredito = c.con_factura && c.tipo_compra == 1;
            decimal credito = conCredito ? R(c.base_credito_fiscal * TasaIva) : 0m;

            Asiento a = Nuevo(c.fecha, "Compra #" + c.id + " a " + c.tercero + (c.con_factura ? " según factura N° " + c.numero_factura : " (sin factura registrada)"),
                              OrigenAsiento.Compra, c.id, usuario);
            Linea(a, cuentas, ClaveCuenta.Inventario, c.monto_total - credito, 0, "Ingreso de mercaderías");
            Linea(a, cuentas, ClaveCuenta.CreditoFiscal, credito, 0, "IVA crédito fiscal 13%");
            Linea(a, cuentas, ClavePago(c.codigo_metodo_pago, true), 0, c.monto_total, "Pago de la compra");
            return a;
        }

        private Asiento Reverso(string origenOriginal, string origenNuevo, AnulacionAContabilizar an, string glosa, string usuario, out string Mensaje)
        {
            Mensaje = null;
            Asiento original = _cd.ObtenerAsientoPorOrigen(origenOriginal, an.id);
            if (original == null)
            {
                Mensaje = "el documento original no está contabilizado (contabilice primero el periodo en que se emitió).";
                return null;
            }
            Asiento a = Nuevo(an.fecha, glosa + " (reverso del asiento N° " + original.numero + "/" + original.gestion + ")", origenNuevo, an.id, usuario);
            foreach (AsientoDetalle d in original.oDetalle)
                a.oDetalle.Add(new AsientoDetalle { id_cuenta = d.id_cuenta, debe = d.haber, haber = d.debe, glosa = "Reverso: " + d.glosa });
            return a;
        }

        private static Asiento Nuevo(DateTime fecha, string glosa, string origenTipo, int origenId, string usuario)
        {
            return new Asiento
            {
                fecha = fecha.Date,
                glosa = glosa.Length > 500 ? glosa.Substring(0, 500) : glosa,
                tipo = Automatico,
                origen_tipo = origenTipo,
                origen_id = origenId,
                usuario = usuario
            };
        }

        private static void Linea(Asiento a, Dictionary<string, int> cuentas, string clave, decimal debe, decimal haber, string glosa)
        {
            if (debe <= 0 && haber <= 0) return;   // las líneas en cero no se registran
            a.oDetalle.Add(new AsientoDetalle { id_cuenta = cuentas[clave], debe = debe, haber = haber, glosa = glosa });
        }

        // ------------------------------------------------------------------ reportes

        public List<Asiento> LibroDiario(DateTime fechaInicio, DateTime fechaFin)
        {
            return _cd.ListarAsientos(fechaInicio, fechaFin, null);
        }

        public LibroMayor LibroMayor(int idCuenta, DateTime fechaInicio, DateTime fechaFin, out string Mensaje)
        {
            Mensaje = string.Empty;
            Cuenta c = _cd.ListarCuentas().FirstOrDefault(x => x.id_cuenta == idCuenta);
            if (c == null)
            {
                Mensaje = "Seleccione una cuenta.";
                return null;
            }

            decimal debeAnt, haberAnt;
            LibroMayor libro = _cd.LibroMayor(idCuenta, fechaInicio, fechaFin, out debeAnt, out haberAnt);
            libro.oCuenta = c;
            bool deudora = c.naturaleza == "D";
            libro.saldo_anterior = deudora ? debeAnt - haberAnt : haberAnt - debeAnt;

            decimal saldo = libro.saldo_anterior;
            foreach (MovimientoMayor m in libro.movimientos)
            {
                saldo += deudora ? m.debe - m.haber : m.haber - m.debe;
                m.saldo = saldo;
            }
            libro.total_debe = libro.movimientos.Sum(m => m.debe);
            libro.total_haber = libro.movimientos.Sum(m => m.haber);
            libro.saldo_final = saldo;
            return libro;
        }

        public List<SumaCuenta> BalanceComprobacion(DateTime fechaInicio, DateTime fechaFin)
        {
            return _cd.SumasPorCuenta(fechaInicio, fechaFin);
        }

        /// <summary>Saldos de ingresos y egresos del periodo y la utilidad (o pérdida).</summary>
        public void EstadoResultados(DateTime fechaInicio, DateTime fechaFin, out List<SumaCuenta> ingresos, out List<SumaCuenta> egresos,
                                     out decimal totalIngresos, out decimal totalEgresos)
        {
            List<SumaCuenta> sumas = _cd.SumasPorCuenta(fechaInicio, fechaFin);
            ingresos = sumas.Where(s => s.tipo == "INGRESO").ToList();
            egresos = sumas.Where(s => s.tipo == "EGRESO").ToList();
            totalIngresos = ingresos.Sum(s => s.haber - s.debe);
            totalEgresos = egresos.Sum(s => s.debe - s.haber);
        }

        /// <summary>
        /// Saldos acumulados de activo, pasivo y patrimonio a la fecha. El resultado acumulado (ingresos − egresos)
        /// se presenta dentro del patrimonio, de modo que activo = pasivo + patrimonio.
        /// </summary>
        public void BalanceGeneral(DateTime fechaCorte, out List<SumaCuenta> activos, out List<SumaCuenta> pasivos, out List<SumaCuenta> patrimonio,
                                   out decimal resultado)
        {
            List<SumaCuenta> sumas = _cd.SumasPorCuenta(new DateTime(1900, 1, 1), fechaCorte);
            activos = sumas.Where(s => s.tipo == "ACTIVO").ToList();
            pasivos = sumas.Where(s => s.tipo == "PASIVO").ToList();
            patrimonio = sumas.Where(s => s.tipo == "PATRIMONIO").ToList();
            resultado = sumas.Where(s => s.tipo == "INGRESO").Sum(s => s.haber - s.debe)
                      - sumas.Where(s => s.tipo == "EGRESO").Sum(s => s.debe - s.haber);
        }

        // ------------------------------------------------------------------ periodos

        public List<PeriodoCerrado> ListarPeriodosCerrados() { return _cd.ListarPeriodosCerrados(); }

        public bool CambiarCierrePeriodo(int gestion, int mes, bool cerrar, string usuario, out string Mensaje)
        {
            Mensaje = string.Empty;
            if (gestion < 2000 || gestion > 2100 || mes < 1 || mes > 12)
            {
                Mensaje = "Periodo no válido.";
                return false;
            }
            _cd.CambiarCierrePeriodo(gestion, mes, cerrar, usuario);
            string periodo = mes.ToString("00") + "/" + gestion;
            Mensaje = cerrar ? "El mes " + periodo + " quedó cerrado: ya no admite asientos ni cambios."
                             : "El mes " + periodo + " quedó abierto nuevamente.";
            return true;
        }

        public static string NombreMes(int mes)
        {
            return CultureInfo.GetCultureInfo("es-BO").DateTimeFormat.GetMonthName(mes).ToUpperInvariant();
        }
    }
}
