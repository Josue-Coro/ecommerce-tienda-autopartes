using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

using CapaEntidad.Facturacion;

namespace CapaNegocio.Facturacion.Siat
{
    /// <summary>
    /// Simula los servicios web del SIN para poder demostrar y probar el sistema sin un NIT autorizado.
    ///
    /// Valida cada factura con la información que trae (XML, hash, CUF), aplicando las mismas reglas que
    /// describe el Anexo Técnico. Solo guarda en memoria los eventos significativos y los paquetes
    /// recibidos (como hace el SIN entre la recepción y la validación). Las respuestas NO tienen validez tributaria.
    /// </summary>
    public class SiatServicioSimulado : ISiatServicio
    {
        /// <summary>Longitud del código de control que acompaña al CUFD (se concatena al final del CUF).</summary>
        public const int LongitudCodigoControl = 15;

        /// <summary>Máximo de facturas por paquete de contingencia.</summary>
        public const int MaximoFacturasPaquete = 500;

        private static readonly RandomNumberGenerator _aleatorio = RandomNumberGenerator.Create();

        private class EventoRegistrado
        {
            public string CufdEvento;
            public DateTime Inicio;
            public DateTime Fin;
        }

        private static readonly ConcurrentDictionary<string, EventoRegistrado> _eventos = new ConcurrentDictionary<string, EventoRegistrado>();
        private static readonly ConcurrentDictionary<string, RespuestaRecepcion> _paquetes = new ConcurrentDictionary<string, RespuestaRecepcion>();

        private readonly bool _sinConexion;

        /// <param name="sinConexion">true simula que el SIN no responde (para demostrar la contingencia).</param>
        public SiatServicioSimulado(bool sinConexion = false)
        {
            _sinConexion = sinConexion;
        }

        public bool EsSimulado { get { return true; } }

        private void Conectar()
        {
            if (_sinConexion)
                throw new SiatSinConexionException("El SIN simulado no responde (corte de conexión simulado).");
        }

        public RespuestaSiat VerificarComunicacion()
        {
            Conectar();
            RespuestaSiat r = new RespuestaSiat { transaccion = true };
            r.mensajes.Add(MensajesSimulados.Crear(MensajesSimulados.Comunicacion));
            return r;
        }

        public RespuestaCuis SolicitarCuis(ConfiguracionFacturacion config, int codigoSucursal, int codigoPuntoVenta)
        {
            Conectar();
            RespuestaCuis r = new RespuestaCuis
            {
                transaccion = true,
                codigo = Hex(4),                                  // 8 caracteres, como los CUIS reales
                fecha_vigencia = DateTime.Now.AddDays(365)
            };
            r.mensajes.Add(MensajesSimulados.Crear(MensajesSimulados.CuisOtorgado));
            return r;
        }

        public RespuestaCufd SolicitarCufd(ConfiguracionFacturacion config, string cuis, int codigoSucursal, int codigoPuntoVenta, string direccion)
        {
            Conectar();
            if (string.IsNullOrWhiteSpace(cuis))
                return Error<RespuestaCufd>(CodigoEstadoSiat.Rechazada, "Se requiere un CUIS vigente para solicitar el CUFD.");

            RespuestaCufd r = new RespuestaCufd
            {
                transaccion = true,
                codigo = Convert.ToBase64String(Bytes(30)).Replace("/", "A").Replace("=", ""),
                codigo_control = Hex(8).Substring(0, LongitudCodigoControl),
                direccion = direccion,
                fecha_vigencia = DateTime.Now.AddHours(24)
            };
            r.mensajes.Add(MensajesSimulados.Crear(MensajesSimulados.CufdOtorgado));
            return r;
        }

        public RespuestaSiat VerificarNit(ConfiguracionFacturacion config, string cuis, int codigoSucursal, string nitParaVerificar)
        {
            Conectar();
            bool valido = NitEsValido(nitParaVerificar);
            RespuestaSiat r = new RespuestaSiat { transaccion = valido };
            r.mensajes.Add(MensajesSimulados.Crear(valido ? MensajesSimulados.NitActivo : MensajesSimulados.NitInexistente));
            return r;
        }

        public RespuestaCatalogo SincronizarCatalogo(ConfiguracionFacturacion config, string cuis, int codigoSucursal, int codigoPuntoVenta, string tipoCatalogo)
        {
            Conectar();
            RespuestaCatalogo r = new RespuestaCatalogo { transaccion = true };
            r.registros = CatalogosSimulados.Obtener(tipoCatalogo);
            return r;
        }

        public RespuestaRecepcion RecepcionFactura(SolicitudRecepcionFactura s)
        {
            Conectar();

            // 1) Integridad del archivo: el hash debe corresponder al archivo comprimido.
            if (s.archivo == null || s.archivo.Length == 0 || !string.Equals(AlgoritmosSiat.Sha256(s.archivo), s.hash_archivo, StringComparison.OrdinalIgnoreCase))
                return Observada(MensajesSimulados.Crear(MensajesSimulados.HashNoCoincide));

            string xml;
            try
            {
                xml = AlgoritmosSiat.DescomprimirGzip(s.archivo);
            }
            catch (Exception ex)
            {
                return Observada(MensajesSimulados.Crear(MensajesSimulados.XmlNoCumpleXsd, "el archivo no es un GZip válido (" + ex.Message + ")"));
            }

            List<MensajeSiat> errores = ValidarFactura(xml, s.nit, s.cufd, s.codigo_modalidad, s.codigo_emision, s.tipo_factura_documento, null);
            if (errores.Count > 0)
                return Observada(errores.ToArray());

            return new RespuestaRecepcion
            {
                transaccion = true,
                codigo_estado = CodigoEstadoSiat.Validada,
                codigo_descripcion = "VALIDADA",
                codigo_recepcion = Guid.NewGuid().ToString()
            };
        }

        /// <summary>
        /// Reglas que se aplican a cada factura, tanto en el envío individual como dentro de un paquete.
        /// <paramref name="evento"/> se indica para facturas fuera de línea: su CUFD y su fecha deben corresponder al evento.
        /// </summary>
        private static List<MensajeSiat> ValidarFactura(string xml, long nitSolicitud, string cufdEsperado, int codigoModalidad,
                                                         int codigoEmision, int tipoFactura, EventoRegistrado evento)
        {
            List<MensajeSiat> errores = new List<MensajeSiat>();

            // Estructura: validación contra el XSD oficial.
            List<string> erroresXsd = GeneradorXmlFactura.Validar(xml);
            if (erroresXsd.Count > 0)
            {
                errores.AddRange(erroresXsd.Take(5).Select(e => MensajesSimulados.Crear(MensajesSimulados.XmlNoCumpleXsd, e)));
                return errores;
            }

            XElement raiz = XDocument.Parse(xml).Root;
            XElement cab = raiz.Element("cabecera");

            // Coherencia con la solicitud.
            long nitEmisor = long.Parse(Valor(cab, "nitEmisor"), CultureInfo.InvariantCulture);
            if (nitEmisor != nitSolicitud)
                errores.Add(MensajesSimulados.Crear(MensajesSimulados.NitEmisorNoCoincide));
            if (Valor(cab, "cufd") != cufdEsperado)
                errores.Add(evento != null
                    ? MensajesSimulados.Crear(MensajesSimulados.FacturaFueraDelEvento, "el CUFD no es el del evento")
                    : MensajesSimulados.Crear(MensajesSimulados.CufdNoCoincide));

            // El CUF debe corresponder a los datos de la factura (se decodifica y se compara campo a campo).
            DateTime fechaEmision = DateTime.Parse(Valor(cab, "fechaEmision"), CultureInfo.InvariantCulture);
            string pv = Valor(cab, "codigoPuntoVenta");
            string cadenaEsperada = AlgoritmosSiat.ArmarCadenaCuf(
                nitEmisor, fechaEmision,
                int.Parse(Valor(cab, "codigoSucursal"), CultureInfo.InvariantCulture),
                codigoModalidad, codigoEmision, tipoFactura,
                int.Parse(Valor(cab, "codigoDocumentoSector"), CultureInfo.InvariantCulture),
                long.Parse(Valor(cab, "numeroFactura"), CultureInfo.InvariantCulture),
                string.IsNullOrEmpty(pv) ? 0 : int.Parse(pv, CultureInfo.InvariantCulture));
            if (!CufCorresponde(Valor(cab, "cuf"), cadenaEsperada))
                errores.Add(MensajesSimulados.Crear(MensajesSimulados.CufInvalido));

            // Fecha de emisión: en línea, cercana a la recepción; fuera de línea, dentro del periodo del evento.
            if (evento != null)
            {
                if (fechaEmision < evento.Inicio.AddMinutes(-1) || fechaEmision > evento.Fin.AddMinutes(1))
                    errores.Add(MensajesSimulados.Crear(MensajesSimulados.FacturaFueraDelEvento, "fecha " + Valor(cab, "fechaEmision")));
            }
            else if (codigoEmision == 1 && (fechaEmision > DateTime.Now.AddMinutes(5) || fechaEmision < DateTime.Now.AddHours(-24)))
            {
                errores.Add(MensajesSimulados.Crear(MensajesSimulados.FechaFueraDeRango));
            }

            // Método de pago y tarjeta.
            string metodo = Valor(cab, "codigoMetodoPago");
            if (!CatalogosSimulados.Obtener(TipoCatalogoSin.MetodoPago).Any(c => c.codigo == metodo))
                errores.Add(MensajesSimulados.Crear(MensajesSimulados.MetodoPagoInvalido, metodo));
            string tarjeta = Valor(cab, "numeroTarjeta");
            if (!string.IsNullOrEmpty(tarjeta) && tarjeta.Length > 8 && tarjeta.Substring(4, tarjeta.Length - 8).Trim('0').Length > 0)
                errores.Add(MensajesSimulados.Crear(MensajesSimulados.TarjetaSinEnmascarar));

            // NIT del comprador (salvo que se envíe el código de excepción).
            if (Valor(cab, "codigoTipoDocumentoIdentidad") == "5" && Valor(cab, "codigoExcepcion") != "1"
                && !NitEsValido(Valor(cab, "numeroDocumento")))
                errores.Add(MensajesSimulados.Crear(MensajesSimulados.NitCompradorInvalido));

            // Detalle: productos del catálogo y aritmética.
            HashSet<string> productos = new HashSet<string>(CatalogosSimulados.Obtener(TipoCatalogoSin.ProductoServicio).Select(c => c.codigo));
            decimal sumaSubtotales = 0m;
            int linea = 0;
            foreach (XElement d in raiz.Elements("detalle"))
            {
                linea++;
                if (!productos.Contains(Valor(d, "codigoProductoSin")))
                    errores.Add(MensajesSimulados.Crear(MensajesSimulados.ProductoNoExiste, "línea " + linea + ", código " + Valor(d, "codigoProductoSin")));

                decimal cantidad = Decimal(d, "cantidad");
                decimal precio = Decimal(d, "precioUnitario");
                decimal descuento = Decimal(d, "montoDescuento");
                decimal subtotal = Decimal(d, "subTotal");
                if (AlgoritmosSiat.Redondear(cantidad * precio - descuento) != subtotal)
                    errores.Add(MensajesSimulados.Crear(MensajesSimulados.SubtotalIncorrecto, "línea " + linea));
                sumaSubtotales += subtotal;
            }

            decimal montoTotal = Decimal(cab, "montoTotal");
            if (AlgoritmosSiat.Redondear(sumaSubtotales - Decimal(cab, "descuentoAdicional") - Decimal(cab, "montoGiftCard")) != montoTotal)
                errores.Add(MensajesSimulados.Crear(MensajesSimulados.MontoTotalIncorrecto));

            return errores;
        }

        // ------------------------------------------------------------------ contingencia

        public RespuestaEvento RegistroEventoSignificativo(SolicitudEventoSignificativo s)
        {
            Conectar();

            if (!CatalogosSimulados.Obtener(TipoCatalogoSin.EventoSignificativo).Any(c => c.codigo == s.codigo_evento.ToString(CultureInfo.InvariantCulture))
                || string.IsNullOrWhiteSpace(s.descripcion) || string.IsNullOrWhiteSpace(s.cufd_evento) || string.IsNullOrWhiteSpace(s.cufd)
                || s.fecha_inicio_evento >= s.fecha_fin_evento || s.fecha_fin_evento > DateTime.Now.AddMinutes(1))
                return ErrorEvento(MensajesSimulados.Crear(MensajesSimulados.EventoInvalido));

            if (DateTime.Now > s.fecha_fin_evento.AddHours(48))
                return ErrorEvento(MensajesSimulados.Crear(MensajesSimulados.EventoFueraDePlazo));

            string codigo = Hex(5);
            _eventos[codigo] = new EventoRegistrado { CufdEvento = s.cufd_evento, Inicio = s.fecha_inicio_evento, Fin = s.fecha_fin_evento };

            RespuestaEvento r = new RespuestaEvento { transaccion = true, codigo_recepcion = codigo };
            r.mensajes.Add(MensajesSimulados.Crear(MensajesSimulados.EventoRegistrado));
            return r;
        }

        public RespuestaRecepcion RecepcionPaqueteFactura(SolicitudRecepcionPaquete s)
        {
            Conectar();

            EventoRegistrado evento;
            if (string.IsNullOrEmpty(s.codigo_evento) || !_eventos.TryGetValue(s.codigo_evento, out evento))
                return Rechazo(MensajesSimulados.Crear(MensajesSimulados.EventoNoExiste));
            if (s.codigo_emision != 2)
                return Rechazo(MensajesSimulados.Crear(MensajesSimulados.EventoInvalido, "los paquetes de contingencia se envían con código de emisión 2"));
            if (s.archivo == null || !string.Equals(AlgoritmosSiat.Sha256(s.archivo), s.hash_archivo, StringComparison.OrdinalIgnoreCase))
                return Rechazo(MensajesSimulados.Crear(MensajesSimulados.HashNoCoincide));

            List<KeyValuePair<string, byte[]>> archivos;
            try
            {
                archivos = ArchivoTar.Leer(AlgoritmosSiat.Descomprimir(s.archivo));
            }
            catch (Exception ex)
            {
                return Rechazo(MensajesSimulados.Crear(MensajesSimulados.XmlNoCumpleXsd, "el archivo no es un .tar.gz válido (" + ex.Message + ")"));
            }

            if (archivos.Count > MaximoFacturasPaquete)
                return Rechazo(MensajesSimulados.Crear(MensajesSimulados.PaqueteExcedeLimite));
            if (archivos.Count != s.cantidad_facturas)
                return Rechazo(MensajesSimulados.Crear(MensajesSimulados.CantidadNoCoincide, archivos.Count + " archivos, " + s.cantidad_facturas + " declarados"));

            // El SIN responde "pendiente" y valida el contenido de forma diferida.
            RespuestaRecepcion validacion = new RespuestaRecepcion { transaccion = true, codigo_recepcion = Guid.NewGuid().ToString() };
            for (int i = 0; i < archivos.Count; i++)
            {
                string xml = new UTF8Encoding(false).GetString(archivos[i].Value);
                foreach (MensajeSiat m in ValidarFactura(xml, s.nit, evento.CufdEvento, s.codigo_modalidad, 2, s.tipo_factura_documento, evento))
                {
                    m.numero_archivo = i;
                    validacion.mensajes.Add(m);
                }
            }
            validacion.codigo_estado = validacion.mensajes.Count == 0 ? CodigoEstadoSiat.Validada : CodigoEstadoSiat.Observada;
            validacion.codigo_descripcion = validacion.mensajes.Count == 0 ? "VALIDADA" : "OBSERVADA";
            _paquetes[validacion.codigo_recepcion] = validacion;

            return new RespuestaRecepcion
            {
                transaccion = true,
                codigo_estado = CodigoEstadoSiat.Pendiente,
                codigo_descripcion = "PENDIENTE",
                codigo_recepcion = validacion.codigo_recepcion
            };
        }

        public RespuestaRecepcion ValidacionRecepcionPaqueteFactura(SolicitudValidacionPaquete s)
        {
            Conectar();

            RespuestaRecepcion r;
            if (string.IsNullOrEmpty(s.codigo_recepcion) || !_paquetes.TryGetValue(s.codigo_recepcion, out r))
                return Rechazo(MensajesSimulados.Crear(MensajesSimulados.PaqueteNoExiste));
            return r;
        }

        private static RespuestaEvento ErrorEvento(MensajeSiat mensaje)
        {
            RespuestaEvento r = new RespuestaEvento { transaccion = false };
            r.mensajes.Add(mensaje);
            return r;
        }

        public RespuestaRecepcion AnulacionFactura(SolicitudAnulacionFactura s)
        {
            Conectar();
            if (!CatalogosSimulados.Obtener(TipoCatalogoSin.MotivoAnulacion).Any(c => c.codigo == s.codigo_motivo.ToString(CultureInfo.InvariantCulture)))
                return Rechazo(MensajesSimulados.Crear(MensajesSimulados.MotivoAnulacionInvalido));

            DateTime? fechaEmision = FechaDesdeCuf(s.cuf);
            if (fechaEmision == null)
                return Rechazo(MensajesSimulados.Crear(MensajesSimulados.CufInvalido));

            if (DateTime.Now > CN_Factura.FechaLimiteAnulacion(fechaEmision.Value))
                return Rechazo(MensajesSimulados.Crear(MensajesSimulados.PlazoAnulacionVencido));

            RespuestaRecepcion r = new RespuestaRecepcion
            {
                transaccion = true,
                codigo_estado = CodigoEstadoSiat.Anulada,
                codigo_descripcion = "ANULACION CONFIRMADA",
                codigo_recepcion = Guid.NewGuid().ToString()
            };
            r.mensajes.Add(MensajesSimulados.Crear(CodigoEstadoSiat.Anulada));
            return r;
        }

        // ------------------------------------------------------------------ utilidades

        /// <summary>NIT válido en el simulador: numérico, 7 a 13 dígitos y sin cero inicial.</summary>
        public static bool NitEsValido(string nit)
        {
            return !string.IsNullOrEmpty(nit) && nit.Length >= 7 && nit.Length <= 13
                && nit.All(char.IsDigit) && nit[0] != '0';
        }

        /// <summary>Obtiene la fecha de emisión codificada dentro del CUF.</summary>
        public static DateTime? FechaDesdeCuf(string cuf)
        {
            string cadena = DecodificarCuf(cuf);
            if (cadena == null)
                return null;

            DateTime fecha;
            if (DateTime.TryParseExact(cadena.Substring(13, 17), "yyyyMMddHHmmssfff", CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha))
                return fecha;
            return null;
        }

        /// <summary>Quita el código de control, pasa de Base 16 a Base 10 y devuelve los 54 dígitos; null si no es válido.</summary>
        private static string DecodificarCuf(string cuf)
        {
            if (string.IsNullOrEmpty(cuf) || cuf.Length <= LongitudCodigoControl)
                return null;
            try
            {
                string hex = cuf.Substring(0, cuf.Length - LongitudCodigoControl);
                string cadena = AlgoritmosSiat.Base10(hex).PadLeft(54, '0');
                return cadena.Length == 54 ? cadena : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool CufCorresponde(string cuf, string cadenaEsperada)
        {
            string cadena = DecodificarCuf(cuf);
            return cadena != null
                && cadena.Substring(0, 53) == cadenaEsperada
                && cadena.Substring(53, 1) == AlgoritmosSiat.Modulo11(cadenaEsperada);
        }

        private static string Valor(XElement padre, string nombre)
        {
            XElement e = padre.Element(nombre);
            if (e == null) return null;
            XAttribute nil = e.Attribute(XName.Get("nil", "http://www.w3.org/2001/XMLSchema-instance"));
            if (nil != null && nil.Value == "true") return null;
            return e.Value.Trim();
        }

        private static decimal Decimal(XElement padre, string nombre)
        {
            string v = Valor(padre, nombre);
            return string.IsNullOrEmpty(v) ? 0m : decimal.Parse(v, CultureInfo.InvariantCulture);
        }

        private static RespuestaRecepcion Observada(params MensajeSiat[] mensajes)
        {
            RespuestaRecepcion r = new RespuestaRecepcion
            {
                transaccion = false,
                codigo_estado = CodigoEstadoSiat.Observada,
                codigo_descripcion = "OBSERVADA",
                codigo_recepcion = Guid.NewGuid().ToString()
            };
            r.mensajes.AddRange(mensajes);
            return r;
        }

        private static RespuestaRecepcion Rechazo(MensajeSiat mensaje)
        {
            RespuestaRecepcion r = new RespuestaRecepcion
            {
                transaccion = false,
                codigo_estado = CodigoEstadoSiat.Rechazada,
                codigo_descripcion = "RECHAZADA"
            };
            r.mensajes.Add(mensaje);
            return r;
        }

        private static T Error<T>(int codigo, string detalle) where T : RespuestaSiat, new()
        {
            T r = new T { transaccion = false };
            r.mensajes.Add(MensajesSimulados.Crear(codigo, detalle));
            return r;
        }

        private static byte[] Bytes(int cantidad)
        {
            byte[] b = new byte[cantidad];
            _aleatorio.GetBytes(b);
            return b;
        }

        private static string Hex(int cantidadBytes)
        {
            StringBuilder sb = new StringBuilder();
            foreach (byte b in Bytes(cantidadBytes))
                sb.Append(b.ToString("X2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }
    }
}
