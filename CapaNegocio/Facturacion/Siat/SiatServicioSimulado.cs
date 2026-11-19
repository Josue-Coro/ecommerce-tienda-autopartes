using System;
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
    /// No guarda estado: valida cada factura con la información que trae (XML, hash, CUF), aplicando
    /// las mismas reglas que describe el Anexo Técnico. Las respuestas NO tienen validez tributaria.
    /// </summary>
    public class SiatServicioSimulado : ISiatServicio
    {
        /// <summary>Longitud del código de control que acompaña al CUFD (se concatena al final del CUF).</summary>
        public const int LongitudCodigoControl = 15;

        private static readonly RandomNumberGenerator _aleatorio = RandomNumberGenerator.Create();

        public bool EsSimulado { get { return true; } }

        public RespuestaSiat VerificarComunicacion()
        {
            RespuestaSiat r = new RespuestaSiat { transaccion = true };
            r.mensajes.Add(MensajesSimulados.Crear(MensajesSimulados.Comunicacion));
            return r;
        }

        public RespuestaCuis SolicitarCuis(ConfiguracionFacturacion config, int codigoSucursal, int codigoPuntoVenta)
        {
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
            bool valido = NitEsValido(nitParaVerificar);
            RespuestaSiat r = new RespuestaSiat { transaccion = valido };
            r.mensajes.Add(MensajesSimulados.Crear(valido ? MensajesSimulados.NitActivo : MensajesSimulados.NitInexistente));
            return r;
        }

        public RespuestaCatalogo SincronizarCatalogo(ConfiguracionFacturacion config, string cuis, int codigoSucursal, int codigoPuntoVenta, string tipoCatalogo)
        {
            RespuestaCatalogo r = new RespuestaCatalogo { transaccion = true };
            r.registros = CatalogosSimulados.Obtener(tipoCatalogo);
            return r;
        }

        public RespuestaRecepcion RecepcionFactura(SolicitudRecepcionFactura s)
        {
            List<MensajeSiat> errores = new List<MensajeSiat>();

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

            // 2) Estructura: validación contra el XSD oficial.
            List<string> erroresXsd = GeneradorXmlFactura.Validar(xml);
            if (erroresXsd.Count > 0)
                return Observada(erroresXsd.Take(5).Select(e => MensajesSimulados.Crear(MensajesSimulados.XmlNoCumpleXsd, e)).ToArray());

            XElement raiz = XDocument.Parse(xml).Root;
            XElement cab = raiz.Element("cabecera");

            // 3) Coherencia con la solicitud.
            long nitEmisor = long.Parse(Valor(cab, "nitEmisor"), CultureInfo.InvariantCulture);
            if (nitEmisor != s.nit)
                errores.Add(MensajesSimulados.Crear(MensajesSimulados.NitEmisorNoCoincide));
            if (Valor(cab, "cufd") != s.cufd)
                errores.Add(MensajesSimulados.Crear(MensajesSimulados.CufdNoCoincide));

            // 4) El CUF debe corresponder a los datos de la factura (se decodifica y se compara campo a campo).
            DateTime fechaEmision = DateTime.Parse(Valor(cab, "fechaEmision"), CultureInfo.InvariantCulture);
            string pv = Valor(cab, "codigoPuntoVenta");
            string cadenaEsperada = AlgoritmosSiat.ArmarCadenaCuf(
                nitEmisor, fechaEmision,
                int.Parse(Valor(cab, "codigoSucursal"), CultureInfo.InvariantCulture),
                s.codigo_modalidad, s.codigo_emision, s.tipo_factura_documento,
                int.Parse(Valor(cab, "codigoDocumentoSector"), CultureInfo.InvariantCulture),
                long.Parse(Valor(cab, "numeroFactura"), CultureInfo.InvariantCulture),
                string.IsNullOrEmpty(pv) ? 0 : int.Parse(pv, CultureInfo.InvariantCulture));
            if (!CufCorresponde(Valor(cab, "cuf"), cadenaEsperada))
                errores.Add(MensajesSimulados.Crear(MensajesSimulados.CufInvalido));

            // 5) Fecha de emisión razonable para una emisión en línea.
            if (s.codigo_emision == 1 && (fechaEmision > DateTime.Now.AddMinutes(5) || fechaEmision < DateTime.Now.AddHours(-24)))
                errores.Add(MensajesSimulados.Crear(MensajesSimulados.FechaFueraDeRango));

            // 6) Método de pago y tarjeta.
            string metodo = Valor(cab, "codigoMetodoPago");
            if (!CatalogosSimulados.Obtener(TipoCatalogoSin.MetodoPago).Any(c => c.codigo == metodo))
                errores.Add(MensajesSimulados.Crear(MensajesSimulados.MetodoPagoInvalido, metodo));
            string tarjeta = Valor(cab, "numeroTarjeta");
            if (!string.IsNullOrEmpty(tarjeta) && tarjeta.Length > 8 && tarjeta.Substring(4, tarjeta.Length - 8).Trim('0').Length > 0)
                errores.Add(MensajesSimulados.Crear(MensajesSimulados.TarjetaSinEnmascarar));

            // 7) NIT del comprador (salvo que se envíe el código de excepción).
            if (Valor(cab, "codigoTipoDocumentoIdentidad") == "5" && Valor(cab, "codigoExcepcion") != "1"
                && !NitEsValido(Valor(cab, "numeroDocumento")))
                errores.Add(MensajesSimulados.Crear(MensajesSimulados.NitCompradorInvalido));

            // 8) Detalle: productos del catálogo y aritmética.
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

            if (errores.Count > 0)
                return Observada(errores.ToArray());

            RespuestaRecepcion ok = new RespuestaRecepcion
            {
                transaccion = true,
                codigo_estado = CodigoEstadoSiat.Validada,
                codigo_descripcion = "VALIDADA",
                codigo_recepcion = Guid.NewGuid().ToString()
            };
            return ok;
        }

        public RespuestaRecepcion AnulacionFactura(SolicitudAnulacionFactura s)
        {
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
