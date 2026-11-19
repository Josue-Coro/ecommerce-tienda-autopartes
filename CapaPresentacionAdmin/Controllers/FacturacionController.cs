using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web.Mvc;

using CapaEntidad;
using CapaEntidad.Facturacion;
using CapaNegocio.Facturacion;
using CapaPresentacionAdmin.Filters;
using CapaPresentacionAdmin.ViewModels;

namespace CapaPresentacionAdmin.Controllers
{
    /// <summary>
    /// Módulo de Facturación (modalidad Computarizada en Línea, documento sector Compra Venta).
    /// Todas las acciones exigen sesión; la configuración y la homologación, además, rol ADMINISTRADOR o GERENTE.
    /// </summary>
    [ValidarSesion(Roles = RolesFacturar)]
    public class FacturacionController : Controller
    {
        private const string RolesFacturar = "ADMINISTRADOR,GERENTE,VENDEDOR";
        private const string RolesConfigurar = "ADMINISTRADOR,GERENTE";

        private readonly CN_ConfiguracionFacturacion _cnConfig = new CN_ConfiguracionFacturacion();
        private readonly CN_Factura _cnFactura = new CN_Factura();
        private readonly CN_Contingencia _cnContingencia = new CN_Contingencia();

        private User_activo UsuarioActual
        {
            get { return Session["Usuario"] as User_activo; }
        }

        // =============================================================== CONFIGURACIÓN

        [ValidarSesion(Roles = RolesConfigurar)]
        public ActionResult Configuracion()
        {
            return View();
        }

        [HttpGet]
        [ValidarSesion(Roles = RolesConfigurar)]
        public JsonResult ObtenerConfiguracion()
        {
            string mensaje;
            ConfiguracionFacturacion config = _cnConfig.ObtenerConfiguracion(out mensaje);
            if (config != null)
                config.token_delegado = string.IsNullOrEmpty(config.token_delegado) ? null : "********"; // nunca se devuelve el token

            return Json(new { resultado = config != null, data = config, mensaje = mensaje }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidarSesion(Roles = RolesConfigurar)]
        public JsonResult GuardarConfiguracion(ConfiguracionFacturacion objeto)
        {
            string mensaje;
            // El token solo se actualiza si se escribió uno nuevo.
            if (objeto != null && (string.IsNullOrWhiteSpace(objeto.token_delegado) || objeto.token_delegado == "********"))
                objeto.token_delegado = null;

            bool ok = _cnConfig.GuardarConfiguracion(objeto, out mensaje);
            return Json(new { resultado = ok, mensaje = mensaje });
        }

        [HttpGet]
        [ValidarSesion(Roles = RolesConfigurar)]
        public JsonResult ListarSucursales()
        {
            string mensaje;
            var lista = _cnConfig.ListarSucursales(out mensaje).Select(s => new
            {
                s.id_tienda,
                s.nombre_tienda,
                s.direccion_tienda,
                s.configurada,
                s.codigo_sucursal,
                s.codigo_punto_venta,
                s.municipio,
                s.telefono,
                cuis = s.oCuis == null ? null : new { s.oCuis.codigo, vigencia = s.oCuis.fecha_vigencia.ToString("dd/MM/yyyy"), s.oCuis.vigente },
                cufd = s.oCufd == null ? null : new { codigo = Abreviar(s.oCufd.codigo), vigencia = s.oCufd.fecha_vigencia.ToString("dd/MM/yyyy HH:mm"), s.oCufd.vigente }
            }).ToList();
            return Json(new { data = lista, mensaje = mensaje }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidarSesion(Roles = RolesConfigurar)]
        public JsonResult GuardarSucursal(SucursalFacturacion objeto)
        {
            string mensaje;
            bool ok = _cnConfig.GuardarSucursal(objeto, out mensaje);
            return Json(new { resultado = ok, mensaje = mensaje });
        }

        [HttpPost]
        [ValidarSesion(Roles = RolesConfigurar)]
        public JsonResult SolicitarCuis(int idTienda)
        {
            string mensaje;
            bool ok = _cnConfig.SolicitarCuis(idTienda, out mensaje);
            return Json(new { resultado = ok, mensaje = mensaje });
        }

        [HttpPost]
        [ValidarSesion(Roles = RolesConfigurar)]
        public JsonResult SolicitarCufd(int idTienda)
        {
            string mensaje;
            bool ok = _cnConfig.SolicitarCufd(idTienda, out mensaje);
            return Json(new { resultado = ok, mensaje = mensaje });
        }

        [HttpPost]
        [ValidarSesion(Roles = RolesConfigurar)]
        public JsonResult SincronizarCatalogos()
        {
            string mensaje;
            bool ok = _cnConfig.SincronizarCatalogos(out mensaje);
            return Json(new { resultado = ok, mensaje = mensaje });
        }

        [HttpGet]
        [ValidarSesion(Roles = RolesConfigurar)]
        public JsonResult ResumenCatalogos()
        {
            var lista = _cnConfig.ResumenCatalogos().Select(r => new
            {
                r.tipo,
                r.cantidad,
                ultima_sincronizacion = r.ultima_sincronizacion.HasValue ? r.ultima_sincronizacion.Value.ToString("dd/MM/yyyy HH:mm") : null
            }).ToList();
            return Json(new { data = lista }, JsonRequestBehavior.AllowGet);
        }

        /// <summary>Catálogos de consulta (tipos de documento, motivos de anulación...). Disponible para quien factura.</summary>
        [HttpGet]
        public JsonResult ListarCatalogo(string tipo)
        {
            if (!TipoCatalogoSin.Todos.Contains(tipo))
                return Json(new { data = new object[0] }, JsonRequestBehavior.AllowGet);

            var lista = _cnConfig.ListarCatalogo(tipo).Select(c => new { c.codigo, c.descripcion, c.codigo_actividad }).ToList();
            return Json(new { data = lista }, JsonRequestBehavior.AllowGet);
        }

        // =============================================================== HOMOLOGACIÓN

        [ValidarSesion(Roles = RolesConfigurar)]
        public ActionResult Homologacion()
        {
            return View();
        }

        [HttpGet]
        [ValidarSesion(Roles = RolesConfigurar)]
        public JsonResult ListarHomologacion(string tipo)
        {
            return Json(new { data = _cnConfig.ListarHomologacion(tipo) }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidarSesion(Roles = RolesConfigurar)]
        public JsonResult GuardarHomologacion(string tipo, int idRegistro, string codigoSin)
        {
            string mensaje;
            bool ok = _cnConfig.GuardarHomologacion(tipo, idRegistro, codigoSin, out mensaje);
            return Json(new { resultado = ok, mensaje = mensaje });
        }

        // =============================================================== FACTURAS

        public ActionResult Facturas()
        {
            return View();
        }

        [HttpGet]
        public JsonResult ListarFacturas(string fechaInicio, string fechaFin, string estado)
        {
            var lista = _cnFactura.Listar(ParsearFecha(fechaInicio), ParsearFecha(fechaFin), estado).Select(f => new
            {
                f.id_factura,
                f.id_venta,
                f.numero_factura,
                f.cuf,
                fecha_emision = f.fecha_emision.ToString("dd/MM/yyyy HH:mm"),
                f.nombre_razon_social,
                documento = f.numero_documento + (string.IsNullOrEmpty(f.complemento) ? "" : "-" + f.complemento),
                f.monto_total,
                f.estado,
                f.codigo_estado,
                f.mensajes,
                f.tipo_emision,
                f.id_evento,
                f.motivo_anulacion,
                fecha_anulacion = f.fecha_anulacion.HasValue ? f.fecha_anulacion.Value.ToString("dd/MM/yyyy HH:mm") : null,
                fecha_limite_anulacion = CN_Factura.FechaLimiteAnulacion(f.fecha_emision).ToString("dd/MM/yyyy"),
                anulable = f.estado == EstadoFactura.Validada && DateTime.Now <= CN_Factura.FechaLimiteAnulacion(f.fecha_emision)
            }).ToList();
            return Json(new { data = lista }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult ListarVentasPendientes(string fechaInicio, string fechaFin)
        {
            var lista = _cnFactura.ListarVentasPendientes(ParsearFecha(fechaInicio), ParsearFecha(fechaFin)).Select(v => new
            {
                v.id_venta,
                fecha = v.fecha.ToString("dd/MM/yyyy"),
                v.monto_total,
                v.id_cliente,
                v.cliente,
                v.tipo_pago
            }).ToList();
            return Json(new { data = lista }, JsonRequestBehavior.AllowGet);
        }

        /// <summary>Datos para precargar el formulario de emisión de una venta.</summary>
        [HttpGet]
        public JsonResult DatosEmision(int idVenta)
        {
            string mensaje;
            VentaParaFacturar venta = _cnFactura.ObtenerVentaParaFacturar(idVenta, out mensaje);
            if (venta == null)
                return Json(new { resultado = false, mensaje = mensaje }, JsonRequestBehavior.AllowGet);

            DatosFiscalesCliente cliente = _cnFactura.ObtenerDatosFiscalesCliente(venta.id_cliente);
            return Json(new
            {
                resultado = true,
                venta = new
                {
                    venta.id_venta,
                    fecha = venta.fecha.ToString("dd/MM/yyyy"),
                    venta.monto_total,
                    venta.tipo_pago,
                    venta.codigo_metodo_pago,
                    es_tarjeta = venta.codigo_metodo_pago == CN_Factura.MetodoPagoTarjeta,
                    venta.estado_factura_vigente,
                    lineas = venta.oLineas.Count
                },
                cliente = cliente
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult EmitirFactura(SolicitudEmisionFactura objeto)
        {
            User_activo usuario = UsuarioActual;
            ResultadoEmision r = _cnFactura.Emitir(objeto, usuario.id_tienda_user, NombreUsuarioFactura(usuario));
            return Json(new
            {
                resultado = r.exito,
                r.id_factura,
                r.numero_factura,
                r.estado,
                mensaje = r.mensaje,
                r.requiere_excepcion_nit
            });
        }

        [HttpPost]
        [ValidarSesion(Roles = RolesConfigurar)]
        public JsonResult AnularFactura(int idFactura, int codigoMotivo)
        {
            string mensaje;
            bool ok = _cnFactura.Anular(idFactura, codigoMotivo, out mensaje);
            return Json(new { resultado = ok, mensaje = mensaje });
        }

        /// <summary>Representación gráfica imprimible (formato "rollo" de 80 mm o "carta").</summary>
        [HttpGet]
        public ActionResult Representacion(int id, string formato = "carta")
        {
            string mensaje;
            Factura f = _cnFactura.Obtener(id, out mensaje);
            if (f == null)
                return HttpNotFound(mensaje);

            ConfiguracionFacturacion config = _cnConfig.ObtenerConfiguracion(out mensaje);
            int ambiente = config != null ? config.codigo_ambiente : 2;
            bool esRollo = formato == "rollo";

            RepresentacionFacturaViewModel vm = new RepresentacionFacturaViewModel
            {
                oFactura = f,
                formato = esRollo ? "rollo" : "carta",
                url_qr = CN_Factura.UrlQr(f, ambiente, esRollo ? 1 : 2),
                monto_literal = NumeroALetras.Convertir(f.monto_total),
                descripcion_metodo_pago = DescripcionCatalogo(TipoCatalogoSin.MetodoPago, f.codigo_metodo_pago),
                descripcion_tipo_documento = DescripcionCatalogo(TipoCatalogoSin.TipoDocumentoIdentidad, f.codigo_tipo_documento),
                leyenda_principal = CN_Factura.LeyendaPrincipal,
                leyenda_modalidad = f.tipo_emision == CN_Factura.EmisionEnLinea ? CN_Factura.LeyendaEnLinea : CN_Factura.LeyendaFueraDeLinea,
                es_simulado = config == null || config.modo_simulado
            };
            return View(vm);
        }

        /// <summary>Descarga el XML de la factura tal como se envió al SIN.</summary>
        [HttpGet]
        public ActionResult DescargarXml(int id)
        {
            string mensaje;
            Factura f = _cnFactura.Obtener(id, out mensaje);
            if (f == null)
                return HttpNotFound(mensaje);

            byte[] contenido = new UTF8Encoding(false).GetBytes(f.archivo_xml);
            return File(contenido, "application/xml", "factura_" + f.nit_emisor + "_" + f.numero_factura + ".xml");
        }

        // =============================================================== CONTINGENCIA

        [ValidarSesion(Roles = RolesConfigurar)]
        public ActionResult Contingencia()
        {
            return View();
        }

        /// <summary>Estado de cada tienda configurada (en línea / fuera de línea) y de la simulación de corte.</summary>
        [HttpGet]
        [ValidarSesion(Roles = RolesConfigurar)]
        public JsonResult EstadoContingencia()
        {
            string mensaje;
            ConfiguracionFacturacion config = _cnConfig.ObtenerConfiguracion(out mensaje);
            var tiendas = _cnConfig.ListarSucursales(out mensaje).Where(s => s.configurada).Select(s =>
            {
                EventoSignificativo e = _cnContingencia.ObtenerEventoAbierto(s.id_tienda);
                return new
                {
                    s.id_tienda,
                    s.nombre_tienda,
                    s.codigo_sucursal,
                    s.codigo_punto_venta,
                    fuera_de_linea = e != null,
                    evento = e == null ? null : new
                    {
                        e.id_evento,
                        e.codigo_evento,
                        e.descripcion_evento,
                        e.automatico,
                        desde = e.fecha_inicio.ToString("dd/MM/yyyy HH:mm"),
                        e.cantidad_facturas
                    }
                };
            }).ToList();

            return Json(new
            {
                modo_simulado = config != null && config.modo_simulado,
                simular_sin_conexion = config != null && config.simular_sin_conexion,
                tiendas = tiendas
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [ValidarSesion(Roles = RolesConfigurar)]
        public JsonResult ListarEventos()
        {
            var lista = _cnContingencia.ListarEventos(null).Select(e => new
            {
                e.id_evento,
                e.nombre_tienda,
                e.codigo_evento,
                e.descripcion_evento,
                e.descripcion,
                e.automatico,
                fecha_inicio = e.fecha_inicio.ToString("dd/MM/yyyy HH:mm"),
                fecha_fin = e.fecha_fin.HasValue ? e.fecha_fin.Value.ToString("dd/MM/yyyy HH:mm") : null,
                limite_envio = CN_Contingencia.FechaLimiteEnvio(e).HasValue ? CN_Contingencia.FechaLimiteEnvio(e).Value.ToString("dd/MM/yyyy HH:mm") : null,
                e.estado,
                e.codigo_recepcion_evento,
                e.mensajes,
                e.cantidad_facturas,
                e.facturas_pendientes
            }).ToList();
            return Json(new { data = lista }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidarSesion(Roles = RolesConfigurar)]
        public JsonResult IniciarContingencia(int idTienda, int codigoEvento, string descripcion)
        {
            string mensaje;
            EventoSignificativo e = _cnContingencia.Iniciar(idTienda, codigoEvento, descripcion, out mensaje);
            return Json(new { resultado = e != null, mensaje = mensaje });
        }

        [HttpPost]
        [ValidarSesion(Roles = RolesConfigurar)]
        public JsonResult FinalizarContingencia(int idEvento)
        {
            string mensaje;
            bool ok = _cnContingencia.FinalizarYEnviar(idEvento, out mensaje);
            return Json(new { resultado = ok, mensaje = mensaje });
        }

        [HttpPost]
        [ValidarSesion(Roles = RolesConfigurar)]
        public JsonResult SimularSinConexion(bool valor)
        {
            string mensaje;
            bool ok = _cnContingencia.GuardarSimulacionSinConexion(valor, out mensaje);
            return Json(new { resultado = ok, mensaje = mensaje });
        }

        // =============================================================== utilidades

        private string DescripcionCatalogo(string tipo, int codigo)
        {
            string c = codigo.ToString(CultureInfo.InvariantCulture);
            CatalogoSin item = _cnConfig.ListarCatalogo(tipo).FirstOrDefault(x => x.codigo == c);
            return item != null ? item.descripcion : c;
        }

        /// <summary>Usuario descriptivo para el XML (ej.: "JPEREZ").</summary>
        private static string NombreUsuarioFactura(User_activo u)
        {
            string nombre = (u.nombre ?? "").Trim();
            string apellido = (u.apellidos ?? "").Trim().Split(' ').FirstOrDefault() ?? "";
            string valor = (nombre.Length > 0 ? nombre.Substring(0, 1) : "") + apellido;
            if (string.IsNullOrWhiteSpace(valor))
                valor = (u.correo ?? "USUARIO").Split('@')[0];
            valor = valor.ToUpperInvariant();
            return valor.Length > 100 ? valor.Substring(0, 100) : valor;
        }

        private static DateTime? ParsearFecha(string valor)
        {
            DateTime fecha;
            if (!string.IsNullOrWhiteSpace(valor)
                && DateTime.TryParseExact(valor, new[] { "yyyy-MM-dd", "dd/MM/yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha))
                return fecha;
            return null;
        }

        private static string Abreviar(string codigo)
        {
            if (string.IsNullOrEmpty(codigo) || codigo.Length <= 16) return codigo;
            return codigo.Substring(0, 12) + "…";
        }
    }
}
