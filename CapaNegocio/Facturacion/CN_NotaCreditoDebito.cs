using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using CapaDatos.Facturacion;
using CapaEntidad.Facturacion;
using CapaNegocio.Facturacion.Siat;

namespace CapaNegocio.Facturacion
{
    /// <summary>Resultado de emitir una nota de crédito-débito.</summary>
    public class ResultadoNota
    {
        public bool exito { get; set; }
        public int id_nota { get; set; }
        public long numero_nota { get; set; }
        public string estado { get; set; }
        public string mensaje { get; set; }
    }

    /// <summary>
    /// Notas de Crédito-Débito (documento sector 24, tipo de documento 3 = documento de ajuste), modalidad
    /// Computarizada en Línea. Registran la devolución total o parcial de lo facturado y ajustan el crédito /
    /// débito fiscal en el 13 % del monto devuelto. El servicio del SIN solo las recibe en línea.
    /// </summary>
    public class CN_NotaCreditoDebito
    {
        public const int SectorNotaCreditoDebito = 24;
        public const int TipoDocumentoAjuste = 3;
        public const decimal TasaCreditoDebito = 0.13m;

        private readonly CD_NotaCreditoDebito _cd = new CD_NotaCreditoDebito();
        private readonly CD_Factura _cdFactura = new CD_Factura();
        private readonly CN_ConfiguracionFacturacion _cnConfig = new CN_ConfiguracionFacturacion();

        /// <summary>Líneas de la factura con la cantidad que todavía puede devolverse.</summary>
        public List<LineaDevolvible> ObtenerLineasDevolvibles(int idFactura)
        {
            return _cd.ObtenerLineasDevolvibles(idFactura);
        }

        public ResultadoNota Emitir(SolicitudEmisionNota sol, int idTienda, string usuario)
        {
            string mensaje;
            ResultadoNota res = new ResultadoNota();

            if (sol == null || sol.id_factura <= 0)
                return Fallo(res, "Seleccione la factura.");
            if (string.IsNullOrWhiteSpace(sol.motivo) || sol.motivo.Trim().Length > 500)
                return Fallo(res, "Indique el motivo de la devolución (máximo 500 caracteres).");
            List<LineaDevolucion> pedidas = (sol.lineas ?? new List<LineaDevolucion>()).Where(l => l.cantidad > 0).ToList();
            if (pedidas.Count == 0)
                return Fallo(res, "Indique la cantidad devuelta de al menos un ítem.");
            if (pedidas.GroupBy(l => l.id_factura_detalle).Any(g => g.Count() > 1))
                return Fallo(res, "Un ítem de la factura aparece más de una vez.");

            Factura f = _cdFactura.Obtener(sol.id_factura, out mensaje);
            if (f == null)
                return Fallo(res, mensaje);
            if (f.estado != EstadoFactura.Validada)
                return Fallo(res, "Solo se emiten notas sobre facturas VALIDADAS (estado actual: " + f.estado + ").");

            ConfiguracionFacturacion config = _cnConfig.ObtenerConfiguracion(out mensaje);
            if (config == null)
                return Fallo(res, mensaje);
            if (new CN_Contingencia().ObtenerEventoAbierto(idTienda) != null)
                return Fallo(res, "La tienda está en contingencia. Las notas de crédito-débito solo se emiten en línea: finalice la contingencia primero.");

            ISiatServicio siat = SiatServicioFactory.Crear(config);
            SucursalFacturacion suc;
            try
            {
                suc = _cnConfig.ObtenerSucursalOperativa(config, idTienda, out mensaje);
                if (suc == null)
                    return Fallo(res, mensaje);
                siat.VerificarComunicacion();
            }
            catch (SiatSinConexionException)
            {
                return Fallo(res, "El SIN no responde. Las notas de crédito-débito no pueden emitirse fuera de línea; intente más tarde.");
            }

            // Cantidades: no se puede devolver más de lo facturado menos lo ya devuelto en otras notas vigentes.
            List<LineaDevolvible> lineas = _cd.ObtenerLineasDevolvibles(f.id_factura);
            foreach (LineaDevolucion p in pedidas)
            {
                LineaDevolvible l = lineas.FirstOrDefault(x => x.oDetalle.id_factura_detalle == p.id_factura_detalle);
                if (l == null)
                    return Fallo(res, "Uno de los ítems no pertenece a la factura N° " + f.numero_factura + ".");
                if (AlgoritmosSiat.Redondear(p.cantidad) != p.cantidad)
                    return Fallo(res, "Las cantidades admiten como máximo dos decimales.");
                if (sol.reingresar_stock && l.oDetalle.codigo_producto.StartsWith("P-") && p.cantidad != Math.Floor(p.cantidad))
                    return Fallo(res, "\"" + l.oDetalle.descripcion + "\": para reingresar al inventario la cantidad devuelta debe ser entera.");
                if (p.cantidad > l.cantidad_disponible)
                    return Fallo(res, "\"" + l.oDetalle.descripcion + "\": se puede devolver como máximo " + l.cantidad_disponible.ToString("0.##", CultureInfo.InvariantCulture)
                                    + " (facturado " + l.oDetalle.cantidad.ToString("0.##", CultureInfo.InvariantCulture) + ", ya devuelto "
                                    + l.cantidad_devuelta.ToString("0.##", CultureInfo.InvariantCulture) + ").");
            }

            NotaCreditoDebito n = new NotaCreditoDebito
            {
                id_factura = f.id_factura,
                id_tienda = idTienda,
                oFactura = f,
                cufd = suc.oCufd.codigo,
                codigo_sucursal = suc.codigo_sucursal,
                codigo_punto_venta = suc.codigo_punto_venta,
                direccion = string.IsNullOrWhiteSpace(suc.oCufd.direccion) ? suc.direccion_tienda : suc.oCufd.direccion,
                municipio = suc.municipio,
                telefono = suc.telefono,
                monto_total_original = f.monto_total_sujeto_iva,
                codigo_excepcion = f.codigo_excepcion,
                usuario = usuario,
                motivo = sol.motivo.Trim(),
                reingreso_stock = sol.reingresar_stock
            };

            // Detalle: la transacción original completa (1) y lo devuelto (2), como en el ejemplo del SIN.
            foreach (LineaDevolvible l in lineas)
                n.oDetalle.Add(CopiarLinea(l.oDetalle, DetalleTransaccionNota.Original, l.oDetalle.cantidad, l.oDetalle.monto_descuento, l.oDetalle.sub_total));
            foreach (LineaDevolucion p in pedidas)
            {
                FacturaDetalle d = lineas.First(x => x.oDetalle.id_factura_detalle == p.id_factura_detalle).oDetalle;
                // Si la línea tuvo descuento, se prorratea según la cantidad devuelta.
                decimal descuento = d.cantidad == 0 ? 0m : AlgoritmosSiat.Redondear(d.monto_descuento * p.cantidad / d.cantidad);
                n.oDetalle.Add(CopiarLinea(d, DetalleTransaccionNota.Devuelto, p.cantidad, descuento,
                                           AlgoritmosSiat.Redondear(p.cantidad * d.precio_unitario - descuento)));
            }

            n.monto_total_devuelto = AlgoritmosSiat.Redondear(n.oDetalle.Where(d => d.codigo_detalle_transaccion == DetalleTransaccionNota.Devuelto).Sum(d => d.sub_total));
            n.monto_efectivo_credito_debito = AlgoritmosSiat.Redondear(n.monto_total_devuelto * TasaCreditoDebito);
            if (n.monto_total_devuelto <= 0)
                return Fallo(res, "El monto devuelto debe ser mayor a cero.");
            if (n.monto_total_devuelto > n.monto_total_original)
                return Fallo(res, "El monto devuelto supera el de la factura original.");
            if (n.oDetalle.Count > 500)
                return Fallo(res, "La nota admite como máximo 500 líneas de detalle.");

            n.leyenda = new CN_Factura().ElegirLeyenda(n.oDetalle[0].actividad_economica);
            if (n.leyenda == null)
                return Fallo(res, "No hay leyendas sincronizadas. Sincronice los catálogos del SIN.");

            n.numero_nota = _cd.ObtenerSiguienteNumero(idTienda, out mensaje);
            if (n.numero_nota <= 0)
                return Fallo(res, mensaje);

            DateTime ahora = DateTime.Now;
            n.fecha_emision = new DateTime(ahora.Year, ahora.Month, ahora.Day, ahora.Hour, ahora.Minute, ahora.Second, ahora.Millisecond);
            n.cuf = AlgoritmosSiat.GenerarCuf(config.nit, n.fecha_emision, n.codigo_sucursal, config.codigo_modalidad,
                                              CN_Factura.EmisionEnLinea, TipoDocumentoAjuste, SectorNotaCreditoDebito,
                                              n.numero_nota, n.codigo_punto_venta, suc.oCufd.codigo_control);

            n.archivo_xml = GeneradorXmlFactura.GenerarNota(n);
            List<string> erroresXsd = GeneradorXmlFactura.Validar(n.archivo_xml);
            if (erroresXsd.Count > 0)
                return Fallo(res, "El XML de la nota no cumple el XSD del SIN: " + string.Join(" | ", erroresXsd.Take(3)));

            byte[] comprimido = AlgoritmosSiat.ComprimirGzip(n.archivo_xml);
            n.hash_archivo = AlgoritmosSiat.Sha256(comprimido);

            RespuestaRecepcion rr;
            try
            {
                rr = siat.RecepcionDocumentoAjuste(new SolicitudRecepcionDocumentoAjuste
                {
                    codigo_ambiente = config.codigo_ambiente,
                    codigo_modalidad = config.codigo_modalidad,
                    codigo_emision = CN_Factura.EmisionEnLinea,
                    codigo_documento_sector = SectorNotaCreditoDebito,
                    tipo_factura_documento = TipoDocumentoAjuste,
                    codigo_sistema = config.codigo_sistema,
                    nit = config.nit,
                    cuis = suc.oCuis.codigo,
                    cufd = n.cufd,
                    codigo_sucursal = n.codigo_sucursal,
                    codigo_punto_venta = n.codigo_punto_venta,
                    archivo = comprimido,
                    hash_archivo = n.hash_archivo,
                    fecha_envio = DateTime.Now
                });
            }
            catch (SiatSinConexionException)
            {
                return Fallo(res, "El SIN dejó de responder al enviar la nota. No se registró; intente nuevamente más tarde.");
            }

            n.codigo_estado = rr.codigo_estado;
            n.codigo_recepcion = rr.codigo_recepcion;
            n.mensajes = rr.mensajes.Count > 0 ? rr.MensajesComoTexto() : null;
            n.estado = rr.codigo_estado == CodigoEstadoSiat.Validada ? EstadoFactura.Validada : EstadoFactura.Observada;

            int idNota = _cd.Registrar(n, out mensaje);
            if (idNota <= 0)
                return Fallo(res, mensaje);

            res.id_nota = idNota;
            res.numero_nota = n.numero_nota;
            res.estado = n.estado;
            res.exito = n.estado == EstadoFactura.Validada;
            res.mensaje = res.exito
                ? "Nota de crédito-débito N° " + n.numero_nota + " VALIDADA" + (siat.EsSimulado ? " (simulado)" : "") + ": devolución de Bs "
                  + n.monto_total_devuelto.ToString("0.00", CultureInfo.InvariantCulture) + ", crédito fiscal Bs "
                  + n.monto_efectivo_credito_debito.ToString("0.00", CultureInfo.InvariantCulture)
                  + (n.reingreso_stock ? ". Los productos devueltos volvieron al inventario." : ".")
                : "Nota N° " + n.numero_nota + " OBSERVADA por el SIN: " + n.mensajes;
            return res;
        }

        public bool Anular(int idNota, int codigoMotivo, out string Mensaje)
        {
            NotaCreditoDebito n = Obtener(idNota, out Mensaje);
            if (n == null) return false;

            if (n.estado != EstadoFactura.Validada)
            {
                Mensaje = "Solo se pueden anular notas en estado VALIDADA (estado actual: " + n.estado + ").";
                return false;
            }
            if (!_cnConfig.ListarCatalogo(TipoCatalogoSin.MotivoAnulacion).Any(m => m.codigo == codigoMotivo.ToString(CultureInfo.InvariantCulture)))
            {
                Mensaje = "Seleccione un motivo de anulación válido.";
                return false;
            }
            if (DateTime.Now > CN_Factura.FechaLimiteAnulacion(n.fecha_emision))
            {
                Mensaje = "El plazo para anular esta nota venció el " + CN_Factura.FechaLimiteAnulacion(n.fecha_emision).ToString("dd/MM/yyyy") + ".";
                return false;
            }

            ConfiguracionFacturacion config = _cnConfig.ObtenerConfiguracion(out Mensaje);
            if (config == null) return false;

            RespuestaRecepcion r;
            try
            {
                SucursalFacturacion suc = _cnConfig.ObtenerSucursalOperativa(config, n.id_tienda, out Mensaje);
                if (suc == null) return false;

                r = SiatServicioFactory.Crear(config).AnulacionDocumentoAjuste(new SolicitudAnulacionFactura
                {
                    codigo_ambiente = config.codigo_ambiente,
                    codigo_modalidad = config.codigo_modalidad,
                    codigo_documento_sector = SectorNotaCreditoDebito,
                    tipo_factura_documento = TipoDocumentoAjuste,
                    codigo_sistema = config.codigo_sistema,
                    nit = config.nit,
                    cuis = suc.oCuis.codigo,
                    cufd = suc.oCufd.codigo,
                    codigo_sucursal = suc.codigo_sucursal,
                    codigo_punto_venta = suc.codigo_punto_venta,
                    cuf = n.cuf,
                    codigo_motivo = codigoMotivo
                });
            }
            catch (SiatSinConexionException)
            {
                Mensaje = "El SIN no responde; no es posible anular en este momento. Intente nuevamente más tarde.";
                return false;
            }

            if (r.codigo_estado != CodigoEstadoSiat.Anulada)
            {
                Mensaje = "El SIN rechazó la anulación: " + r.MensajesComoTexto();
                return false;
            }
            if (!_cd.RegistrarAnulacion(idNota, codigoMotivo, r.codigo_estado, out Mensaje))
                return false;

            Mensaje = "Nota N° " + n.numero_nota + " anulada." + (n.reingreso_stock ? " Se revirtió el reingreso al inventario." : "");
            return true;
        }

        public List<NotaCreditoDebito> Listar(DateTime? fechaInicio, DateTime? fechaFin, int? idFactura)
        {
            return _cd.Listar(fechaInicio, fechaFin, idFactura);
        }

        /// <summary>Nota con su detalle y la factura original.</summary>
        public NotaCreditoDebito Obtener(int idNota, out string Mensaje)
        {
            NotaCreditoDebito n = _cd.Obtener(idNota, out Mensaje);
            if (n == null) return null;
            n.oFactura = _cdFactura.Obtener(n.id_factura, out Mensaje);
            return n.oFactura == null ? null : n;
        }

        private static NotaDetalle CopiarLinea(FacturaDetalle d, int codigoTransaccion, decimal cantidad, decimal descuento, decimal subtotal)
        {
            return new NotaDetalle
            {
                id_factura_detalle = d.id_factura_detalle,
                codigo_detalle_transaccion = codigoTransaccion,
                actividad_economica = d.actividad_economica,
                codigo_producto_sin = d.codigo_producto_sin,
                codigo_producto = d.codigo_producto,
                descripcion = d.descripcion,
                cantidad = cantidad,
                unidad_medida = d.unidad_medida,
                precio_unitario = d.precio_unitario,
                monto_descuento = descuento,
                sub_total = subtotal
            };
        }

        private static ResultadoNota Fallo(ResultadoNota res, string mensaje)
        {
            res.exito = false;
            res.mensaje = mensaje;
            return res;
        }
    }
}
