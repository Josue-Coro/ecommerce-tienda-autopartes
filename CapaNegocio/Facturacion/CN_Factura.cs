using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;

using CapaDatos.Facturacion;
using CapaEntidad.Facturacion;
using CapaNegocio.Facturacion.Siat;

namespace CapaNegocio.Facturacion
{
    /// <summary>Resultado de emitir una factura.</summary>
    public class ResultadoEmision
    {
        public bool exito { get; set; }
        public int id_factura { get; set; }
        public long numero_factura { get; set; }
        public string estado { get; set; }
        public string mensaje { get; set; }

        /// <summary>true cuando el NIT no es válido y el usuario puede reintentar con código de excepción.</summary>
        public bool requiere_excepcion_nit { get; set; }
    }

    /// <summary>
    /// Emisión, anulación y consulta de facturas Compra Venta en modalidad Computarizada en Línea.
    /// </summary>
    public class CN_Factura
    {
        public const int ModalidadComputarizada = 2;
        public const int EmisionEnLinea = 1;
        public const int FacturaConCreditoFiscal = 1;
        public const int SectorCompraVenta = 1;
        public const int MonedaBoliviano = 1;
        public const int UnidadBienesPorDefecto = 57;
        public const int UnidadServicios = 58;
        public const int MetodoPagoTarjeta = 2;
        public const int TipoDocumentoCI = 1;
        public const int TipoDocumentoNIT = 5;

        /// <summary>Códigos especiales: 99001 consulados/embajadas, 99002 control tributario, 99003 ventas menores del día.</summary>
        public static readonly string[] DocumentosEspeciales = { "99001", "99002", "99003" };

        public const string LeyendaPrincipal =
            "ESTA FACTURA CONTRIBUYE AL DESARROLLO DEL PAÍS, EL USO ILÍCITO SERÁ SANCIONADO PENALMENTE DE ACUERDO A LEY";
        public const string LeyendaEnLinea =
            "“Este documento es la Representación Gráfica de un Documento Fiscal Digital emitido en una modalidad de facturación en línea”";
        public const string LeyendaFueraDeLinea =
            "“Este documento es la Representación Gráfica de un Documento Fiscal Digital emitido fuera de línea, verifique su envío con su proveedor o en la página web www.impuestos.gob.bo”";

        private readonly CD_Factura _cdFactura = new CD_Factura();
        private readonly CN_ConfiguracionFacturacion _cnConfig = new CN_ConfiguracionFacturacion();

        // ------------------------------------------------------------------ Emisión

        public ResultadoEmision Emitir(SolicitudEmisionFactura sol, int idTienda, string usuario)
        {
            string mensaje;
            ResultadoEmision res = new ResultadoEmision();

            // 1) Datos del comprador
            if (!ValidarComprador(sol, out mensaje))
                return Fallo(res, mensaje);

            // 2) Configuración, sucursal, CUIS y CUFD
            ConfiguracionFacturacion config = _cnConfig.ObtenerConfiguracion(out mensaje);
            if (config == null)
                return Fallo(res, mensaje);
            if (config.codigo_modalidad != ModalidadComputarizada)
                return Fallo(res, "Solo está implementada la modalidad Computarizada en Línea.");

            SucursalFacturacion suc = _cnConfig.ObtenerSucursalOperativa(config, idTienda, out mensaje);
            if (suc == null)
                return Fallo(res, mensaje);

            // 3) Venta y homologaciones
            VentaParaFacturar venta = _cdFactura.ObtenerVentaParaFacturar(sol.id_venta, out mensaje);
            if (venta == null)
                return Fallo(res, mensaje);
            if (!string.IsNullOrEmpty(venta.estado_factura_vigente))
                return Fallo(res, "La venta #" + venta.id_venta + " ya tiene una factura " + venta.estado_factura_vigente + ".");
            if (venta.oLineas.Count == 0)
                return Fallo(res, "La venta no tiene detalle.");
            if (venta.oLineas.Count > 500)
                return Fallo(res, "Una factura admite como máximo 500 líneas de detalle.");

            List<CatalogoSin> productosSin = _cnConfig.ListarCatalogo(TipoCatalogoSin.ProductoServicio);
            if (productosSin.Count == 0)
                return Fallo(res, "Los catálogos del SIN no están sincronizados. Sincronícelos en Facturación > Configuración.");

            List<string> sinHomologar = venta.oLineas
                .Where(l => l.codigo_producto_sin == null)
                .Select(l => (l.tipo_item == "SERVICIO" ? "servicio " : "producto ") + "\"" + l.descripcion + "\"")
                .Distinct()
                .ToList();
            if (sinHomologar.Count > 0)
                return Fallo(res, "Falta homologar con el catálogo del SIN: " + string.Join(", ", sinHomologar)
                                + ". Hágalo en Facturación > Homologación.");
            if (venta.codigo_metodo_pago == null)
                return Fallo(res, "El tipo de pago \"" + venta.tipo_pago + "\" no está homologado con un método de pago del SIN.");

            string tarjetaEnmascarada = null;
            if (venta.codigo_metodo_pago == MetodoPagoTarjeta)
            {
                tarjetaEnmascarada = AlgoritmosSiat.EnmascararTarjeta(sol.numero_tarjeta);
                if (tarjetaEnmascarada == null)
                    return Fallo(res, "Para pagos con tarjeta ingrese el número de la tarjeta (se guardará enmascarado).");
            }

            // 4) Verificación del NIT del comprador
            int codigoExcepcion = 0;
            string numeroDocumento = sol.numero_documento.Trim();
            bool esEspecial = DocumentosEspeciales.Contains(numeroDocumento);
            ISiatServicio siat = SiatServicioFactory.Crear(config);
            if (sol.codigo_tipo_documento == TipoDocumentoNIT)
            {
                if (esEspecial || sol.autorizar_nit_invalido)
                {
                    codigoExcepcion = 1;
                }
                else
                {
                    RespuestaSiat rn = siat.VerificarNit(config, suc.oCuis.codigo, suc.codigo_sucursal, numeroDocumento);
                    if (!rn.transaccion)
                    {
                        res.requiere_excepcion_nit = true;
                        return Fallo(res, "El SIN indica que el NIT " + numeroDocumento + " no es válido (" + rn.MensajesComoTexto()
                                        + "). Verifique el número o confirme la emisión con código de excepción.");
                    }
                }
            }

            // 5) Detalle de la factura
            Dictionary<string, string> actividadPorProducto = productosSin
                .GroupBy(p => p.codigo)
                .ToDictionary(g => g.Key, g => g.First().codigo_actividad);

            Factura f = new Factura
            {
                id_venta = venta.id_venta,
                id_tienda = idTienda,
                nit_emisor = config.nit,
                razon_social_emisor = config.razon_social,
                municipio = suc.municipio,
                telefono = suc.telefono,
                direccion = string.IsNullOrWhiteSpace(suc.oCufd.direccion) ? suc.direccion_tienda : suc.oCufd.direccion,
                codigo_sucursal = suc.codigo_sucursal,
                codigo_punto_venta = suc.codigo_punto_venta,
                cufd = suc.oCufd.codigo,
                codigo_modalidad = ModalidadComputarizada,
                tipo_emision = EmisionEnLinea,
                tipo_factura = FacturaConCreditoFiscal,
                codigo_documento_sector = SectorCompraVenta,
                nombre_razon_social = sol.razon_social.Trim().ToUpperInvariant(),
                codigo_tipo_documento = sol.codigo_tipo_documento,
                numero_documento = numeroDocumento,
                complemento = string.IsNullOrWhiteSpace(sol.complemento) ? null : sol.complemento.Trim().ToUpperInvariant(),
                codigo_cliente = venta.id_cliente.ToString(CultureInfo.InvariantCulture),
                correo_cliente = string.IsNullOrWhiteSpace(sol.correo) ? null : sol.correo.Trim(),
                codigo_metodo_pago = venta.codigo_metodo_pago.Value,
                numero_tarjeta = tarjetaEnmascarada,
                descuento_adicional = 0m,
                codigo_moneda = MonedaBoliviano,
                tipo_cambio = 1m,
                codigo_excepcion = codigoExcepcion,
                usuario = usuario
            };

            foreach (LineaVentaParaFacturar l in venta.oLineas)
            {
                bool esServicio = l.tipo_item == "SERVICIO";
                string codigoSin = l.codigo_producto_sin.Value.ToString(CultureInfo.InvariantCulture);
                string actividad;
                if (!actividadPorProducto.TryGetValue(codigoSin, out actividad) || string.IsNullOrEmpty(actividad))
                    actividad = config.actividad_economica;

                decimal precio = AlgoritmosSiat.Redondear(l.precio);
                decimal cantidad = AlgoritmosSiat.Redondear(l.cantidad);
                f.oDetalle.Add(new FacturaDetalle
                {
                    actividad_economica = actividad,
                    codigo_producto_sin = l.codigo_producto_sin.Value,
                    codigo_producto = (esServicio ? "S-" + l.id_servicio : "P-" + l.id_producto),
                    descripcion = Recortar(l.descripcion, 500),
                    cantidad = cantidad,
                    unidad_medida = esServicio ? UnidadServicios : (l.codigo_unidad_medida ?? UnidadBienesPorDefecto),
                    precio_unitario = precio,
                    monto_descuento = 0m,
                    sub_total = AlgoritmosSiat.Redondear(cantidad * precio)
                });
            }

            f.monto_total = AlgoritmosSiat.Redondear(f.oDetalle.Sum(d => d.sub_total) - f.descuento_adicional);
            f.monto_total_sujeto_iva = f.monto_total;
            f.monto_total_moneda = f.monto_total;
            if (f.monto_total <= 0)
                return Fallo(res, "No se emiten facturas por monto cero.");
            if (Math.Abs(f.monto_total - AlgoritmosSiat.Redondear(venta.monto_total)) > 0.01m)
                return Fallo(res, "El total de la venta (Bs " + venta.monto_total.ToString("0.00", CultureInfo.InvariantCulture)
                                + ") no coincide con la suma de su detalle (Bs " + f.monto_total.ToString("0.00", CultureInfo.InvariantCulture)
                                + "). Revise la venta antes de facturarla.");

            // La leyenda de la Ley 453 debe cambiar aleatoriamente en cada factura.
            f.leyenda = ElegirLeyenda(f.oDetalle[0].actividad_economica);
            if (f.leyenda == null)
                return Fallo(res, "No hay leyendas sincronizadas. Sincronice los catálogos del SIN.");

            // 6) Número, fecha, CUF y XML
            f.numero_factura = _cdFactura.ObtenerSiguienteNumero(idTienda, out mensaje);
            if (f.numero_factura <= 0)
                return Fallo(res, mensaje);

            DateTime ahora = DateTime.Now;
            f.fecha_emision = new DateTime(ahora.Year, ahora.Month, ahora.Day, ahora.Hour, ahora.Minute, ahora.Second, ahora.Millisecond);
            f.cuf = AlgoritmosSiat.GenerarCuf(f.nit_emisor, f.fecha_emision, f.codigo_sucursal, f.codigo_modalidad,
                                              f.tipo_emision, f.tipo_factura, f.codigo_documento_sector,
                                              f.numero_factura, f.codigo_punto_venta, suc.oCufd.codigo_control);

            f.archivo_xml = GeneradorXmlFactura.Generar(f);
            List<string> erroresXsd = GeneradorXmlFactura.Validar(f.archivo_xml);
            if (erroresXsd.Count > 0)
                return Fallo(res, "El XML generado no cumple el XSD del SIN: " + string.Join(" | ", erroresXsd.Take(3)));

            byte[] comprimido = AlgoritmosSiat.ComprimirGzip(f.archivo_xml);
            f.hash_archivo = AlgoritmosSiat.Sha256(comprimido);

            // 7) Envío al SIN
            RespuestaRecepcion rr;
            try
            {
                rr = siat.RecepcionFactura(new SolicitudRecepcionFactura
                {
                    codigo_ambiente = config.codigo_ambiente,
                    codigo_modalidad = f.codigo_modalidad,
                    codigo_emision = f.tipo_emision,
                    codigo_documento_sector = f.codigo_documento_sector,
                    tipo_factura_documento = f.tipo_factura,
                    codigo_sistema = config.codigo_sistema,
                    nit = config.nit,
                    cuis = suc.oCuis.codigo,
                    cufd = f.cufd,
                    codigo_sucursal = f.codigo_sucursal,
                    codigo_punto_venta = f.codigo_punto_venta,
                    archivo = comprimido,
                    hash_archivo = f.hash_archivo,
                    fecha_envio = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                // La emisión fuera de línea (contingencia) se implementará en la siguiente etapa.
                return Fallo(res, "No se pudo comunicar con el SIN: " + ex.Message);
            }

            f.codigo_estado = rr.codigo_estado;
            f.codigo_recepcion = rr.codigo_recepcion;
            f.mensajes = rr.mensajes.Count > 0 ? rr.MensajesComoTexto() : null;
            if (rr.codigo_estado == CodigoEstadoSiat.Validada)
                f.estado = EstadoFactura.Validada;
            else if (rr.codigo_estado == CodigoEstadoSiat.Pendiente)
                f.estado = EstadoFactura.Pendiente;
            else
                f.estado = EstadoFactura.Observada;

            // 8) Registro
            int idFactura = _cdFactura.Registrar(f, out mensaje);
            if (idFactura <= 0)
                return Fallo(res, mensaje);

            if (sol.guardar_datos_cliente)
            {
                string msgCliente;
                _cdFactura.GuardarDatosFiscalesCliente(new DatosFiscalesCliente
                {
                    id_cliente = venta.id_cliente,
                    codigo_tipo_documento = f.codigo_tipo_documento,
                    numero_documento = f.numero_documento,
                    complemento = f.complemento,
                    razon_social = f.nombre_razon_social,
                    correo = f.correo_cliente
                }, out msgCliente);
            }

            res.id_factura = idFactura;
            res.numero_factura = f.numero_factura;
            res.estado = f.estado;
            res.exito = f.estado != EstadoFactura.Observada;
            res.mensaje = f.estado == EstadoFactura.Validada
                ? "Factura N° " + f.numero_factura + " emitida y VALIDADA por el SIN" + (siat.EsSimulado ? " (simulado)." : ".")
                : "Factura N° " + f.numero_factura + " " + f.estado + ": " + f.mensajes;
            return res;
        }

        private static bool ValidarComprador(SolicitudEmisionFactura sol, out string Mensaje)
        {
            Mensaje = string.Empty;
            if (sol == null || sol.id_venta <= 0)
                Mensaje = "Seleccione una venta.";
            else if (sol.codigo_tipo_documento < 1 || sol.codigo_tipo_documento > 5)
                Mensaje = "Seleccione el tipo de documento del comprador.";
            else if (string.IsNullOrWhiteSpace(sol.numero_documento) || sol.numero_documento.Trim().Length > 20)
                Mensaje = "Ingrese el número de documento del comprador (máximo 20 caracteres).";
            else if ((sol.codigo_tipo_documento == TipoDocumentoCI || sol.codigo_tipo_documento == TipoDocumentoNIT)
                     && !sol.numero_documento.Trim().All(char.IsDigit))
                Mensaje = "El número de CI o NIT debe ser numérico.";
            else if (DocumentosEspeciales.Contains(sol.numero_documento.Trim()) && sol.codigo_tipo_documento != TipoDocumentoNIT)
                Mensaje = "Los códigos especiales 99001, 99002 y 99003 se envían con tipo de documento NIT.";
            else if (!string.IsNullOrWhiteSpace(sol.complemento) && sol.complemento.Trim().Length > 5)
                Mensaje = "El complemento admite como máximo 5 caracteres.";
            else if (string.IsNullOrWhiteSpace(sol.razon_social) || sol.razon_social.Trim().Length > 500)
                Mensaje = "Ingrese el nombre o razón social del comprador.";
            else if (!string.IsNullOrWhiteSpace(sol.correo) && (sol.correo.Trim().Length > 100 || !sol.correo.Contains("@")))
                Mensaje = "El correo del comprador no es válido.";

            return string.IsNullOrEmpty(Mensaje);
        }

        private string ElegirLeyenda(string actividad)
        {
            List<CatalogoSin> leyendas = _cnConfig.ListarCatalogo(TipoCatalogoSin.LeyendaFactura);
            List<CatalogoSin> deActividad = leyendas.Where(l => l.codigo_actividad == actividad).ToList();
            List<CatalogoSin> candidatas = deActividad.Count > 0 ? deActividad : leyendas;
            if (candidatas.Count == 0)
                return null;

            byte[] b = new byte[4];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                rng.GetBytes(b);
            int indice = (int)(BitConverter.ToUInt32(b, 0) % (uint)candidatas.Count);
            return Recortar(candidatas[indice].descripcion, 200);
        }

        // ------------------------------------------------------------------ Anulación

        /// <summary>Plazo de anulación: hasta el día 9 (inclusive) del mes siguiente al de la emisión.</summary>
        public static DateTime FechaLimiteAnulacion(DateTime fechaEmision)
        {
            DateTime primeroMesSiguiente = new DateTime(fechaEmision.Year, fechaEmision.Month, 1).AddMonths(1);
            return primeroMesSiguiente.AddDays(9).AddTicks(-1);
        }

        public bool Anular(int idFactura, int codigoMotivo, out string Mensaje)
        {
            Factura f = _cdFactura.Obtener(idFactura, out Mensaje);
            if (f == null) return false;

            if (f.estado != EstadoFactura.Validada)
            {
                Mensaje = "Solo se pueden anular facturas en estado VALIDADA (estado actual: " + f.estado + ").";
                return false;
            }
            if (!_cnConfig.ListarCatalogo(TipoCatalogoSin.MotivoAnulacion).Any(m => m.codigo == codigoMotivo.ToString(CultureInfo.InvariantCulture)))
            {
                Mensaje = "Seleccione un motivo de anulación válido.";
                return false;
            }
            if (DateTime.Now > FechaLimiteAnulacion(f.fecha_emision))
            {
                Mensaje = "El plazo para anular esta factura venció el " + FechaLimiteAnulacion(f.fecha_emision).ToString("dd/MM/yyyy") + ".";
                return false;
            }

            ConfiguracionFacturacion config = _cnConfig.ObtenerConfiguracion(out Mensaje);
            if (config == null) return false;

            // La anulación puede hacerse desde la sucursal de origen u otra habilitada; se usa la de origen.
            SucursalFacturacion suc = _cnConfig.ObtenerSucursalOperativa(config, f.id_tienda, out Mensaje);
            if (suc == null) return false;

            RespuestaRecepcion r = SiatServicioFactory.Crear(config).AnulacionFactura(new SolicitudAnulacionFactura
            {
                codigo_ambiente = config.codigo_ambiente,
                codigo_modalidad = f.codigo_modalidad,
                codigo_documento_sector = f.codigo_documento_sector,
                tipo_factura_documento = f.tipo_factura,
                codigo_sistema = config.codigo_sistema,
                nit = config.nit,
                cuis = suc.oCuis.codigo,
                cufd = suc.oCufd.codigo,
                codigo_sucursal = suc.codigo_sucursal,
                codigo_punto_venta = suc.codigo_punto_venta,
                cuf = f.cuf,
                codigo_motivo = codigoMotivo
            });

            if (r.codigo_estado != CodigoEstadoSiat.Anulada)
            {
                Mensaje = "El SIN rechazó la anulación: " + r.MensajesComoTexto();
                return false;
            }

            if (!_cdFactura.RegistrarAnulacion(idFactura, codigoMotivo, r.codigo_estado, r.MensajesComoTexto(), out Mensaje))
                return false;

            Mensaje = "Factura N° " + f.numero_factura + " anulada.";

            // El anexo exige notificar la anulación al comprador.
            if (!string.IsNullOrWhiteSpace(f.correo_cliente))
            {
                string motivo = _cnConfig.ListarCatalogo(TipoCatalogoSin.MotivoAnulacion)
                    .First(m => m.codigo == codigoMotivo.ToString(CultureInfo.InvariantCulture)).descripcion;
                string cuerpo = "<p>Estimado(a) " + System.Net.WebUtility.HtmlEncode(f.nombre_razon_social) + ":</p>"
                    + "<p>Le informamos que la siguiente factura fue <b>anulada</b>:</p><ul>"
                    + "<li>Código de autorización (CUF): " + f.cuf + "</li>"
                    + "<li>Número de factura: " + f.numero_factura + "</li>"
                    + "<li>Motivo: " + System.Net.WebUtility.HtmlEncode(motivo) + "</li></ul>"
                    + "<p>" + System.Net.WebUtility.HtmlEncode(f.razon_social_emisor) + "</p>";
                bool enviado = CN_Recursos.EnviarCorreo(f.correo_cliente, "Anulación de factura N° " + f.numero_factura, cuerpo);
                Mensaje += enviado ? " Se notificó al comprador por correo." : " No se pudo enviar el correo de notificación al comprador.";
            }
            return true;
        }

        // ------------------------------------------------------------------ Consultas

        public List<Factura> Listar(DateTime? fechaInicio, DateTime? fechaFin, string estado)
        {
            return _cdFactura.Listar(fechaInicio, fechaFin, estado);
        }

        public Factura Obtener(int idFactura, out string Mensaje)
        {
            return _cdFactura.Obtener(idFactura, out Mensaje);
        }

        public List<VentaPendienteFactura> ListarVentasPendientes(DateTime? fechaInicio, DateTime? fechaFin)
        {
            return _cdFactura.ListarVentasPendientes(fechaInicio, fechaFin);
        }

        public DatosFiscalesCliente ObtenerDatosFiscalesCliente(int idCliente)
        {
            return _cdFactura.ObtenerDatosFiscalesCliente(idCliente);
        }

        /// <summary>Datos de la venta para mostrarlos antes de emitir (monto, método de pago, cliente).</summary>
        public VentaParaFacturar ObtenerVentaParaFacturar(int idVenta, out string Mensaje)
        {
            return _cdFactura.ObtenerVentaParaFacturar(idVenta, out Mensaje);
        }

        /// <summary>
        /// URL del QR de verificación de la factura en el portal del SIN. La ruta de piloto está publicada en el
        /// anexo; la de producción la comunica el SIN al finalizar la autorización (confirmarla en ese momento).
        /// </summary>
        public static string UrlQr(Factura f, int codigoAmbiente, int tamano = 1)
        {
            string host = codigoAmbiente == 1 ? "https://siat.impuestos.gob.bo" : "https://pilotosiat.impuestos.gob.bo";
            return host + "/consulta/QR?nit=" + f.nit_emisor.ToString(CultureInfo.InvariantCulture)
                 + "&cuf=" + f.cuf + "&numero=" + f.numero_factura.ToString(CultureInfo.InvariantCulture)
                 + "&t=" + tamano.ToString(CultureInfo.InvariantCulture);
        }

        private static ResultadoEmision Fallo(ResultadoEmision res, string mensaje)
        {
            res.exito = false;
            res.mensaje = mensaje;
            return res;
        }

        private static string Recortar(string texto, int maximo)
        {
            if (string.IsNullOrEmpty(texto)) return texto;
            texto = texto.Trim();
            return texto.Length <= maximo ? texto : texto.Substring(0, maximo);
        }
    }
}
