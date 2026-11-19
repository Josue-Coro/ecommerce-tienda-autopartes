using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

using CapaDatos.Facturacion;
using CapaEntidad.Facturacion;
using CapaNegocio.Facturacion.Siat;

namespace CapaNegocio.Facturacion
{
    /// <summary>
    /// Contingencia (Anexo Técnico, "Contingencia y Eventos Significativos" e "Ingreso a Contingencia"):
    ///  1. Al perder comunicación con el SIN se abre un evento y se factura fuera de línea con el CUFD vigente
    ///     al inicio del evento (su validez se extiende hasta 72 horas).
    ///  2. Al recuperar la comunicación: nuevo CUFD → registro del evento significativo → envío de las facturas
    ///     en paquetes .tar.gz de hasta 500 → validación de cada paquete.
    ///  3. El evento debe registrarse y los paquetes enviarse dentro de las 48 horas posteriores a su fin.
    /// Los eventos 5, 6 y 7 (falla de software, de hardware o corte de energía) requieren facturas manuales
    /// de contingencia (CAFC) y su transcripción; esa parte no está implementada.
    /// </summary>
    public class CN_Contingencia
    {
        public const int EventoCorteInternet = 1;
        public const int EventoInaccesibilidadSin = 2;
        public const int MaximoEventoSinCafc = 4;
        public const int HorasValidezCufdContingencia = 72;
        public const int HorasPlazoEnvio = 48;

        private readonly CD_Contingencia _cd = new CD_Contingencia();
        private readonly CN_ConfiguracionFacturacion _cnConfig = new CN_ConfiguracionFacturacion();

        public EventoSignificativo ObtenerEventoAbierto(int idTienda)
        {
            return _cd.ListarEventos(idTienda, null, true).FirstOrDefault();
        }

        public EventoSignificativo ObtenerEvento(int idEvento)
        {
            return _cd.ListarEventos(null, idEvento, false).FirstOrDefault();
        }

        public List<EventoSignificativo> ListarEventos(int? idTienda)
        {
            return _cd.ListarEventos(idTienda, null, false);
        }

        public bool GuardarSimulacionSinConexion(bool valor, out string Mensaje)
        {
            ConfiguracionFacturacion config = _cnConfig.ObtenerConfiguracion(out Mensaje);
            if (config == null) return false;
            if (valor && !config.modo_simulado)
            {
                Mensaje = "El corte de conexión solo se puede simular en modo simulado.";
                return false;
            }
            if (!_cd.GuardarSimulacionSinConexion(valor, out Mensaje)) return false;
            Mensaje = valor
                ? "El SIN simulado dejará de responder. La próxima factura pasará automáticamente a emisión fuera de línea."
                : "El SIN simulado vuelve a responder. Ya puede finalizar la contingencia y enviar los paquetes.";
            return true;
        }

        // ------------------------------------------------------------------ Inicio

        public EventoSignificativo Iniciar(int idTienda, int codigoEvento, string descripcion, out string Mensaje)
        {
            if (codigoEvento < 1 || codigoEvento > 7)
            {
                Mensaje = "Seleccione un tipo de evento significativo.";
                return null;
            }
            if (codigoEvento > MaximoEventoSinCafc)
            {
                Mensaje = "Los eventos 5, 6 y 7 requieren emitir facturas manuales de contingencia (CAFC) y transcribirlas; "
                        + "esa modalidad todavía no está implementada.";
                return null;
            }
            if (string.IsNullOrWhiteSpace(descripcion) || descripcion.Trim().Length > 500)
            {
                Mensaje = "Describa el evento (máximo 500 caracteres).";
                return null;
            }
            return Crear(idTienda, codigoEvento, descripcion.Trim(), false, out Mensaje);
        }

        /// <summary>Abre (o devuelve) la contingencia cuando el SIN no responde durante una operación.</summary>
        public EventoSignificativo IniciarAutomatico(int idTienda, string detalle, out string Mensaje)
        {
            EventoSignificativo abierto = ObtenerEventoAbierto(idTienda);
            if (abierto != null)
            {
                Mensaje = string.Empty;
                return abierto;
            }
            string descripcion = "Inaccesibilidad al servicio web del SIN detectada automáticamente por el sistema. " + (detalle ?? "");
            return Crear(idTienda, EventoInaccesibilidadSin, descripcion.Length > 500 ? descripcion.Substring(0, 500) : descripcion, true, out Mensaje);
        }

        private EventoSignificativo Crear(int idTienda, int codigoEvento, string descripcion, bool automatico, out string Mensaje)
        {
            SucursalFacturacion suc = _cnConfig.ObtenerSucursal(idTienda, out Mensaje);
            if (suc == null) return null;
            if (!suc.configurada || suc.oCuis == null || !suc.oCuis.vigente)
            {
                Mensaje = "La tienda necesita datos de sucursal y un CUIS vigente para emitir fuera de línea.";
                return null;
            }
            // Fuera de línea se emite con el último CUFD obtenido; en contingencia su validez se amplía hasta 72 horas.
            if (suc.oCufd == null || suc.oCufd.fecha_solicitud < DateTime.Now.AddHours(-HorasValidezCufdContingencia))
            {
                Mensaje = "No hay un CUFD obtenido en las últimas " + HorasValidezCufdContingencia + " horas; no es posible emitir fuera de línea.";
                return null;
            }

            EventoSignificativo e = new EventoSignificativo
            {
                id_tienda = idTienda,
                codigo_evento = codigoEvento,
                descripcion = descripcion,
                fecha_inicio = Truncar(DateTime.Now),
                cufd_evento = suc.oCufd.codigo,
                codigo_control_evento = suc.oCufd.codigo_control,
                automatico = automatico,
                estado = EstadoEvento.Abierto
            };

            int id = _cd.IniciarEvento(e, out Mensaje);
            if (id == 0) return null;
            if (id < 0) return ObtenerEvento(-id);   // otra petición la abrió al mismo tiempo

            Mensaje = "Contingencia iniciada en " + suc.nombre_tienda + ". Las facturas se emitirán fuera de línea.";
            return ObtenerEvento(id);
        }

        // ------------------------------------------------------------------ Cierre y envío

        /// <summary>
        /// Cierra la contingencia: verifica comunicación, obtiene un CUFD nuevo, registra el evento y envía
        /// las facturas pendientes en paquetes. También sirve para reintentar un envío que quedó incompleto.
        /// </summary>
        public bool FinalizarYEnviar(int idEvento, out string Mensaje)
        {
            EventoSignificativo e = ObtenerEvento(idEvento);
            if (e == null)
            {
                Mensaje = "No se encontró el evento #" + idEvento + ".";
                return false;
            }
            if (e.estado == EstadoEvento.Validado)
            {
                Mensaje = "El evento ya fue registrado y todas sus facturas validadas.";
                return false;
            }

            ConfiguracionFacturacion config = _cnConfig.ObtenerConfiguracion(out Mensaje);
            if (config == null) return false;
            ISiatServicio siat = SiatServicioFactory.Crear(config);

            try
            {
                siat.VerificarComunicacion();

                // Primero un CUFD nuevo, para no enviar paquetes con uno a punto de vencer.
                SucursalFacturacion suc = _cnConfig.ObtenerSucursal(e.id_tienda, out Mensaje);
                if (suc == null) return false;
                CodigoCufd cufdNuevo = _cnConfig.RenovarCufd(config, suc, out Mensaje);
                if (cufdNuevo == null) return false;

                // Registro del evento significativo (una sola vez).
                if (e.estado == EstadoEvento.Abierto)
                {
                    DateTime fin = Truncar(DateTime.Now);
                    RespuestaEvento re = siat.RegistroEventoSignificativo(new SolicitudEventoSignificativo
                    {
                        codigo_ambiente = config.codigo_ambiente,
                        codigo_sistema = config.codigo_sistema,
                        nit = config.nit,
                        cuis = suc.oCuis.codigo,
                        cufd = cufdNuevo.codigo,
                        codigo_sucursal = suc.codigo_sucursal,
                        codigo_punto_venta = suc.codigo_punto_venta,
                        codigo_evento = e.codigo_evento,
                        descripcion = e.descripcion,
                        fecha_inicio_evento = e.fecha_inicio,
                        fecha_fin_evento = fin,
                        cufd_evento = e.cufd_evento
                    });

                    if (!re.transaccion)
                    {
                        // Queda abierto para corregir y reintentar; las facturas siguen pendientes.
                        _cd.ActualizarEvento(e.id_evento, EstadoEvento.Abierto, null, null, re.MensajesComoTexto(), out Mensaje);
                        Mensaje = "El SIN rechazó el registro del evento: " + re.MensajesComoTexto();
                        return false;
                    }

                    if (!_cd.ActualizarEvento(e.id_evento, EstadoEvento.Registrado, fin, re.codigo_recepcion, null, out Mensaje))
                        return false;
                    e.estado = EstadoEvento.Registrado;
                    e.fecha_fin = fin;
                    e.codigo_recepcion_evento = re.codigo_recepcion;
                }

                // Envío de paquetes.
                List<FacturaPendienteEnvio> pendientes = _cd.ListarFacturasPendientes(e.id_evento);
                int validadas = 0, observadas = 0, paquetes = 0;
                List<string> problemas = new List<string>();

                for (int inicio = 0; inicio < pendientes.Count; inicio += SiatServicioSimulado.MaximoFacturasPaquete)
                {
                    List<FacturaPendienteEnvio> lote = pendientes.Skip(inicio).Take(SiatServicioSimulado.MaximoFacturasPaquete).ToList();
                    paquetes++;
                    int v, o;
                    string problema = EnviarPaquete(config, siat, suc, cufdNuevo, e, lote, out v, out o);
                    validadas += v;
                    observadas += o;
                    if (problema != null) problemas.Add("Paquete " + paquetes + ": " + problema);
                }

                int restantes = _cd.ListarFacturasPendientes(e.id_evento).Count;
                string estadoFinal = restantes == 0 && observadas == 0 && problemas.Count == 0 ? EstadoEvento.Validado
                                   : restantes == 0 ? EstadoEvento.Observado : EstadoEvento.Registrado;
                string resumen = paquetes + " paquete(s) enviados: " + validadas + " factura(s) validada(s), " + observadas + " observada(s)"
                               + (restantes > 0 ? ", " + restantes + " pendiente(s) de reenvío" : "") + ".";
                if (problemas.Count > 0) resumen += " " + string.Join(" | ", problemas);
                _cd.ActualizarEvento(e.id_evento, estadoFinal, null, null, resumen, out Mensaje);

                Mensaje = "Evento #" + e.id_evento + " registrado en el SIN (código " + e.codigo_recepcion_evento + "). "
                        + (pendientes.Count == 0 ? "No había facturas pendientes de envío." : resumen);
                return restantes == 0;
            }
            catch (SiatSinConexionException)
            {
                Mensaje = "El SIN todavía no responde. La tienda sigue facturando fuera de línea; intente nuevamente más tarde "
                        + "(recuerde el plazo de " + HorasPlazoEnvio + " horas desde que se recupera la conexión).";
                return false;
            }
        }

        /// <summary>Arma el .tar.gz, lo envía, lo valida y actualiza el estado de cada factura. Devuelve un problema o null.</summary>
        private string EnviarPaquete(ConfiguracionFacturacion config, ISiatServicio siat, SucursalFacturacion suc, CodigoCufd cufd,
                                     EventoSignificativo e, List<FacturaPendienteEnvio> lote, out int validadas, out int observadas)
        {
            validadas = 0;
            observadas = 0;

            List<KeyValuePair<string, byte[]>> archivos = lote
                .Select(f => new KeyValuePair<string, byte[]>(ArchivoTar.NombreXml(f.numero_factura), new UTF8Encoding(false).GetBytes(f.archivo_xml)))
                .ToList();
            byte[] paquete = AlgoritmosSiat.Comprimir(ArchivoTar.Crear(archivos));
            string hash = AlgoritmosSiat.Sha256(paquete);

            RespuestaRecepcion rp = siat.RecepcionPaqueteFactura(new SolicitudRecepcionPaquete
            {
                codigo_ambiente = config.codigo_ambiente,
                codigo_modalidad = config.codigo_modalidad,
                codigo_emision = CN_Factura.EmisionFueraDeLinea,
                codigo_documento_sector = CN_Factura.SectorCompraVenta,
                tipo_factura_documento = CN_Factura.FacturaConCreditoFiscal,
                codigo_sistema = config.codigo_sistema,
                nit = config.nit,
                cuis = suc.oCuis.codigo,
                cufd = cufd.codigo,
                codigo_sucursal = suc.codigo_sucursal,
                codigo_punto_venta = suc.codigo_punto_venta,
                archivo = paquete,
                hash_archivo = hash,
                fecha_envio = DateTime.Now,
                cantidad_facturas = lote.Count,
                codigo_evento = e.codigo_recepcion_evento
            });

            if (!rp.transaccion || rp.codigo_estado != CodigoEstadoSiat.Pendiente)
            {
                _cd.RegistrarPaquete(e.id_evento, lote.Count, hash, rp.codigo_recepcion, rp.codigo_estado, "RECHAZADO", rp.MensajesComoTexto());
                return "rechazado en la recepción (" + rp.MensajesComoTexto() + ")";
            }

            RespuestaRecepcion rv = siat.ValidacionRecepcionPaqueteFactura(new SolicitudValidacionPaquete
            {
                codigo_ambiente = config.codigo_ambiente,
                codigo_modalidad = config.codigo_modalidad,
                codigo_emision = CN_Factura.EmisionFueraDeLinea,
                codigo_documento_sector = CN_Factura.SectorCompraVenta,
                tipo_factura_documento = CN_Factura.FacturaConCreditoFiscal,
                codigo_sistema = config.codigo_sistema,
                nit = config.nit,
                cuis = suc.oCuis.codigo,
                cufd = cufd.codigo,
                codigo_sucursal = suc.codigo_sucursal,
                codigo_punto_venta = suc.codigo_punto_venta,
                codigo_recepcion = rp.codigo_recepcion
            });

            if (rv.codigo_estado == CodigoEstadoSiat.Pendiente)
            {
                _cd.RegistrarPaquete(e.id_evento, lote.Count, hash, rp.codigo_recepcion, rv.codigo_estado, "PENDIENTE", rv.MensajesComoTexto());
                return "el SIN todavía está validando el paquete; reintente más tarde";
            }
            if (rv.codigo_estado != CodigoEstadoSiat.Validada && rv.codigo_estado != CodigoEstadoSiat.Observada)
            {
                _cd.RegistrarPaquete(e.id_evento, lote.Count, hash, rp.codigo_recepcion, rv.codigo_estado, "RECHAZADO", rv.MensajesComoTexto());
                return "rechazado en la validación (" + rv.MensajesComoTexto() + ")";
            }

            // Las observaciones vienen por número de archivo: solo esas facturas quedan observadas.
            for (int i = 0; i < lote.Count; i++)
            {
                List<MensajeSiat> propias = rv.mensajes.Where(m => m.numero_archivo == i).ToList();
                if (propias.Count == 0)
                {
                    _cd.ActualizarEstadoFactura(lote[i].id_factura, EstadoFactura.Validada, CodigoEstadoSiat.Validada, rp.codigo_recepcion, null);
                    validadas++;
                }
                else
                {
                    _cd.ActualizarEstadoFactura(lote[i].id_factura, EstadoFactura.Observada, CodigoEstadoSiat.Observada, rp.codigo_recepcion,
                                                string.Join(" | ", propias.Select(m => m.codigo + " - " + m.descripcion)));
                    observadas++;
                }
            }

            _cd.RegistrarPaquete(e.id_evento, lote.Count, hash, rp.codigo_recepcion, rv.codigo_estado,
                                 rv.codigo_estado == CodigoEstadoSiat.Validada ? "VALIDADO" : "OBSERVADO", rv.MensajesComoTexto());
            return null;
        }

        /// <summary>Fecha límite para registrar el evento y enviar sus paquetes.</summary>
        public static DateTime? FechaLimiteEnvio(EventoSignificativo e)
        {
            return e.fecha_fin.HasValue ? e.fecha_fin.Value.AddHours(HorasPlazoEnvio) : (DateTime?)null;
        }

        private static DateTime Truncar(DateTime d)
        {
            return new DateTime(d.Year, d.Month, d.Day, d.Hour, d.Minute, d.Second, d.Millisecond);
        }
    }
}
