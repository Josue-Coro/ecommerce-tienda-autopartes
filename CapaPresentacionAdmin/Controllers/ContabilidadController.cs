using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web.Mvc;

using CapaEntidad;
using CapaEntidad.Contabilidad;
using CapaNegocio.Contabilidad;
using CapaPresentacionAdmin.Filters;

namespace CapaPresentacionAdmin.Controllers
{
    /// <summary>Módulo de Contabilidad. Disponible para los roles ADMINISTRADOR, GERENTE y CONTADOR.</summary>
    [ValidarSesion(Permisos = "CONTABILIDAD")]
    public class ContabilidadController : Controller
    {
        private readonly CN_Contabilidad _cn = new CN_Contabilidad();
        private readonly CN_LibrosFiscales _libros = new CN_LibrosFiscales();

        private string Usuario
        {
            get
            {
                User_activo u = Session["Usuario"] as User_activo;
                return u == null ? "SISTEMA" : ((u.nombre ?? "") + " " + (u.apellidos ?? "")).Trim();
            }
        }

        // =============================================================== vistas

        public ActionResult PlanCuentas() { return View(); }
        public ActionResult LibroDiario() { return View(); }
        public ActionResult LibroMayor() { return View(); }
        public ActionResult EstadosFinancieros() { return View(); }
        public ActionResult LibrosFiscales() { return View(); }
        public ActionResult Liquidacion() { return View(); }

        // =============================================================== plan de cuentas

        [HttpGet]
        public JsonResult ListarCuentas()
        {
            return Json(new { data = _cn.ListarCuentas() }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult GuardarCuenta(Cuenta objeto)
        {
            string mensaje;
            bool ok = _cn.GuardarCuenta(objeto, out mensaje);
            return Json(new { resultado = ok, mensaje = mensaje });
        }

        [HttpGet]
        public JsonResult ListarCuentaConfig()
        {
            return Json(new { data = _cn.ListarCuentaConfig() }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult GuardarCuentaConfig(string clave, int idCuenta)
        {
            string mensaje;
            bool ok = _cn.GuardarCuentaConfig(clave, idCuenta, out mensaje);
            return Json(new { resultado = ok, mensaje = mensaje });
        }

        // =============================================================== libro diario y asientos

        [HttpGet]
        public JsonResult ListarAsientos(string fechaInicio, string fechaFin)
        {
            DateTime ini, fin;
            Rango(fechaInicio, fechaFin, out ini, out fin);
            var lista = _cn.LibroDiario(ini, fin).Select(a => new
            {
                a.id_asiento,
                a.numero,
                a.gestion,
                fecha = a.fecha.ToString("dd/MM/yyyy"),
                a.glosa,
                a.tipo,
                a.origen_tipo,
                a.origen_id,
                a.estado,
                a.usuario,
                total = a.oDetalle.Sum(d => d.debe),
                detalle = a.oDetalle.Select(d => new { d.codigo, d.nombre, d.debe, d.haber, d.glosa })
            }).ToList();
            return Json(new
            {
                data = lista,
                total_debe = lista.Where(a => a.estado == "VIGENTE").Sum(a => a.total),
                periodos_cerrados = _cn.ListarPeriodosCerrados().Select(p => new { p.gestion, p.mes, p.usuario, fecha = p.fecha_cierre.ToString("dd/MM/yyyy HH:mm") })
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult Contabilizar(string fechaInicio, string fechaFin)
        {
            DateTime ini, fin;
            Rango(fechaInicio, fechaFin, out ini, out fin);
            ResultadoContabilizacion r = _cn.Contabilizar(ini, fin, Usuario);
            return Json(new { resultado = r.errores.Count == 0, mensaje = r.resumen, errores = r.errores, r.asientos_generados });
        }

        [HttpPost]
        public JsonResult RegistrarAsiento(Asiento objeto)
        {
            string mensaje;
            int id = _cn.RegistrarAsientoManual(objeto, Usuario, out mensaje);
            return Json(new { resultado = id > 0, mensaje = mensaje });
        }

        [HttpPost]
        public JsonResult AnularAsiento(int idAsiento)
        {
            string mensaje;
            bool ok = _cn.AnularAsiento(idAsiento, out mensaje);
            return Json(new { resultado = ok, mensaje = mensaje });
        }

        [ValidarSesion(Permisos = "CONTABILIDAD_CIERRE")]
        [HttpPost]
        public JsonResult CambiarCierrePeriodo(int gestion, int mes, bool cerrar)
        {
            string mensaje;
            bool ok = _cn.CambiarCierrePeriodo(gestion, mes, cerrar, Usuario, out mensaje);
            return Json(new { resultado = ok, mensaje = mensaje });
        }

        // =============================================================== mayor y estados financieros

        [HttpGet]
        public JsonResult ObtenerLibroMayor(int idCuenta, string fechaInicio, string fechaFin)
        {
            DateTime ini, fin;
            Rango(fechaInicio, fechaFin, out ini, out fin);
            string mensaje;
            LibroMayor l = _cn.LibroMayor(idCuenta, ini, fin, out mensaje);
            if (l == null)
                return Json(new { resultado = false, mensaje = mensaje }, JsonRequestBehavior.AllowGet);

            return Json(new
            {
                resultado = true,
                cuenta = l.oCuenta.codigo + " " + l.oCuenta.nombre,
                naturaleza = l.oCuenta.naturaleza == "D" ? "deudora" : "acreedora",
                l.saldo_anterior,
                l.total_debe,
                l.total_haber,
                l.saldo_final,
                movimientos = l.movimientos.Select(m => new { m.numero, fecha = m.fecha.ToString("dd/MM/yyyy"), m.glosa, m.debe, m.haber, m.saldo })
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult ObtenerEstadosFinancieros(string fechaInicio, string fechaFin)
        {
            DateTime ini, fin;
            Rango(fechaInicio, fechaFin, out ini, out fin);

            List<SumaCuenta> comprobacion = _cn.BalanceComprobacion(ini, fin);
            List<SumaCuenta> ingresos, egresos;
            decimal totalIngresos, totalEgresos;
            _cn.EstadoResultados(ini, fin, out ingresos, out egresos, out totalIngresos, out totalEgresos);
            List<SumaCuenta> activos, pasivos, patrimonio;
            decimal resultadoAcumulado;
            _cn.BalanceGeneral(fin, out activos, out pasivos, out patrimonio, out resultadoAcumulado);

            Func<SumaCuenta, bool, object> fila = (s, deudora) => new { s.codigo, s.nombre, saldo = deudora ? s.debe - s.haber : s.haber - s.debe };
            return Json(new
            {
                comprobacion = comprobacion.Select(s => new { s.codigo, s.nombre, s.debe, s.haber, s.saldo_deudor, s.saldo_acreedor }),
                totales_comprobacion = new
                {
                    debe = comprobacion.Sum(s => s.debe),
                    haber = comprobacion.Sum(s => s.haber),
                    deudor = comprobacion.Sum(s => s.saldo_deudor),
                    acreedor = comprobacion.Sum(s => s.saldo_acreedor)
                },
                resultados = new
                {
                    ingresos = ingresos.Select(s => fila(s, false)),
                    egresos = egresos.Select(s => fila(s, true)),
                    total_ingresos = totalIngresos,
                    total_egresos = totalEgresos,
                    utilidad = totalIngresos - totalEgresos
                },
                balance = new
                {
                    fecha_corte = fin.ToString("dd/MM/yyyy"),
                    activos = activos.Select(s => fila(s, true)),
                    pasivos = pasivos.Select(s => fila(s, false)),
                    patrimonio = patrimonio.Select(s => fila(s, false)),
                    resultado_acumulado = resultadoAcumulado,
                    total_activo = activos.Sum(s => s.debe - s.haber),
                    total_pasivo = pasivos.Sum(s => s.haber - s.debe),
                    total_patrimonio = patrimonio.Sum(s => s.haber - s.debe) + resultadoAcumulado
                }
            }, JsonRequestBehavior.AllowGet);
        }

        // =============================================================== libros fiscales

        [HttpGet]
        public JsonResult ListarComprasFiscales(int gestion, int mes)
        {
            DateTime ini = new DateTime(gestion, mes, 1);
            var lista = _libros.ListarCompras(ini, ini.AddMonths(1).AddDays(-1)).Select(c => new
            {
                c.id_compra,
                fecha_compra = c.fecha_compra.ToString("dd/MM/yyyy"),
                c.monto_compra,
                c.proveedor,
                c.nit_proveedor_registrado,
                c.registrada,
                c.contabilizada,
                nit_proveedor = c.registrada ? c.nit_proveedor.ToString(CultureInfo.InvariantCulture) : null,
                c.razon_social,
                c.codigo_autorizacion,
                numero_factura = c.registrada ? c.numero_factura.ToString(CultureInfo.InvariantCulture) : null,
                fecha_factura = c.fecha_factura.HasValue ? c.fecha_factura.Value.ToString("yyyy-MM-dd") : null,
                c.importe_total,
                c.importe_no_sujeto,
                c.descuentos,
                c.tipo_compra,
                c.codigo_control
            }).ToList();
            return Json(new { data = lista }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult GuardarCompraFiscal(CompraFiscal objeto)
        {
            string mensaje;
            bool ok = _libros.GuardarCompraFiscal(objeto, out mensaje);
            return Json(new { resultado = ok, mensaje = mensaje });
        }

        [HttpGet]
        public JsonResult ObtenerLibrosFiscales(int gestion, int mes)
        {
            var ventas = _libros.LibroVentas(gestion, mes);
            var compras = _libros.LibroCompras(gestion, mes);
            return Json(new
            {
                ventas = ventas.Select(f => new
                {
                    f.numero, fecha = f.fecha_factura.ToString("dd/MM/yyyy"), f.numero_factura, f.codigo_autorizacion, f.nit_ci_cliente,
                    f.complemento, f.razon_social, f.importe_total, f.descuentos, f.base_debito_fiscal, f.debito_fiscal, f.estado
                }),
                total_ventas = new { importe = ventas.Sum(f => f.importe_total), base_df = ventas.Sum(f => f.base_debito_fiscal), df = ventas.Sum(f => f.debito_fiscal) },
                compras = compras.Select(f => new
                {
                    f.numero, f.nit_proveedor, f.razon_social, f.codigo_autorizacion, f.numero_factura, fecha = f.fecha_factura.ToString("dd/MM/yyyy"),
                    f.importe_total, f.importe_no_sujeto, f.descuentos, f.base_credito_fiscal, f.credito_fiscal, f.tipo_compra
                }),
                total_compras = new { importe = compras.Sum(f => f.importe_total), base_cf = compras.Sum(f => f.base_credito_fiscal), cf = compras.Sum(f => f.credito_fiscal) }
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult DescargarLibroVentas(int gestion, int mes)
        {
            return Csv(_libros.LibroVentasCsv(gestion, mes), "libro_ventas_" + gestion + mes.ToString("00") + ".csv");
        }

        [HttpGet]
        public ActionResult DescargarLibroCompras(int gestion, int mes)
        {
            return Csv(_libros.LibroComprasCsv(gestion, mes), "libro_compras_" + gestion + mes.ToString("00") + ".csv");
        }

        [HttpGet]
        public JsonResult ObtenerLiquidacion(int gestion, int mes)
        {
            return Json(new { data = _libros.Liquidacion(gestion, mes), periodo = CN_Contabilidad.NombreMes(mes) + " " + gestion },
                        JsonRequestBehavior.AllowGet);
        }

        // =============================================================== utilidades

        private ActionResult Csv(string contenido, string nombre)
        {
            // Con BOM para que Excel reconozca los acentos.
            byte[] bytes = new UTF8Encoding(true).GetPreamble().Concat(new UTF8Encoding(false).GetBytes(contenido)).ToArray();
            return File(bytes, "text/csv", nombre);
        }

        private static void Rango(string fechaInicio, string fechaFin, out DateTime ini, out DateTime fin)
        {
            DateTime hoy = DateTime.Today;
            if (!DateTime.TryParseExact(fechaInicio ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out ini))
                ini = new DateTime(hoy.Year, hoy.Month, 1);
            if (!DateTime.TryParseExact(fechaFin ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out fin))
                fin = hoy;
            if (fin < ini) { DateTime t = ini; ini = fin; fin = t; }
        }
    }
}
