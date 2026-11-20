using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Schema;

using CapaEntidad.Facturacion;

namespace CapaNegocio.Facturacion
{
    /// <summary>
    /// Genera el XML de la Factura Compra Venta (modalidad Computarizada en Línea) respetando el orden
    /// de los elementos del XSD oficial del SIN y lo valida contra ese XSD.
    /// </summary>
    public static class GeneradorXmlFactura
    {
        public const string ElementoRaizComputarizada = "facturaComputarizadaCompraVenta";
        public const string ElementoRaizNotaComputarizada = "notaFiscalComputarizadaCreditoDebito";
        private static readonly string[] RecursosXsd =
        {
            "CapaNegocio.Facturacion.Esquemas.facturaComputarizadaCompraVenta.xsd",
            "CapaNegocio.Facturacion.Esquemas.notaComputarizadaCreditoDebito.xsd"
        };
        private const string NsXsi = "http://www.w3.org/2001/XMLSchema-instance";

        private static XmlSchemaSet _esquemas;
        private static readonly object _bloqueo = new object();

        public static string Generar(Factura f)
        {
            XmlWriterSettings settings = new XmlWriterSettings()
            {
                Encoding = new UTF8Encoding(false),
                Indent = false,
                OmitXmlDeclaration = false
            };

            using (MemoryStream ms = new MemoryStream())
            {
                using (XmlWriter w = XmlWriter.Create(ms, settings))
                {
                    w.WriteStartDocument(true);
                    w.WriteStartElement(ElementoRaizComputarizada);
                    w.WriteAttributeString("xmlns", "xsi", null, NsXsi);
                    w.WriteAttributeString("xsi", "noNamespaceSchemaLocation", NsXsi, ElementoRaizComputarizada + ".xsd");

                    // ---------------- CABECERA (orden del XSD) ----------------
                    w.WriteStartElement("cabecera");
                    Elemento(w, "nitEmisor", f.nit_emisor.ToString(CultureInfo.InvariantCulture));
                    Elemento(w, "razonSocialEmisor", f.razon_social_emisor);
                    Elemento(w, "municipio", f.municipio);
                    ElementoNulable(w, "telefono", f.telefono);
                    Elemento(w, "numeroFactura", f.numero_factura.ToString(CultureInfo.InvariantCulture));
                    Elemento(w, "cuf", f.cuf);
                    Elemento(w, "cufd", f.cufd);
                    Elemento(w, "codigoSucursal", f.codigo_sucursal.ToString(CultureInfo.InvariantCulture));
                    Elemento(w, "direccion", f.direccion);
                    // El punto de venta 0 significa "no corresponde": se envía nulo.
                    ElementoNulable(w, "codigoPuntoVenta", f.codigo_punto_venta > 0 ? f.codigo_punto_venta.ToString(CultureInfo.InvariantCulture) : null);
                    Elemento(w, "fechaEmision", FormatoFecha(f.fecha_emision));
                    ElementoNulable(w, "nombreRazonSocial", f.nombre_razon_social);
                    Elemento(w, "codigoTipoDocumentoIdentidad", f.codigo_tipo_documento.ToString(CultureInfo.InvariantCulture));
                    Elemento(w, "numeroDocumento", f.numero_documento);
                    ElementoNulable(w, "complemento", f.complemento);
                    Elemento(w, "codigoCliente", f.codigo_cliente);
                    Elemento(w, "codigoMetodoPago", f.codigo_metodo_pago.ToString(CultureInfo.InvariantCulture));
                    ElementoNulable(w, "numeroTarjeta", f.numero_tarjeta);
                    Elemento(w, "montoTotal", Monto(f.monto_total));
                    Elemento(w, "montoTotalSujetoIva", Monto(f.monto_total_sujeto_iva));
                    Elemento(w, "codigoMoneda", f.codigo_moneda.ToString(CultureInfo.InvariantCulture));
                    Elemento(w, "tipoCambio", Monto(f.tipo_cambio));
                    Elemento(w, "montoTotalMoneda", Monto(f.monto_total_moneda));
                    ElementoNulable(w, "montoGiftCard", null);
                    ElementoNulable(w, "descuentoAdicional", f.descuento_adicional > 0 ? Monto(f.descuento_adicional) : null);
                    ElementoNulable(w, "codigoExcepcion", f.codigo_excepcion > 0 ? f.codigo_excepcion.ToString(CultureInfo.InvariantCulture) : null);
                    ElementoNulable(w, "cafc", null);
                    Elemento(w, "leyenda", f.leyenda);
                    Elemento(w, "usuario", f.usuario);
                    Elemento(w, "codigoDocumentoSector", f.codigo_documento_sector.ToString(CultureInfo.InvariantCulture));
                    w.WriteEndElement();

                    // ---------------- DETALLE ----------------
                    foreach (FacturaDetalle d in f.oDetalle)
                    {
                        w.WriteStartElement("detalle");
                        Elemento(w, "actividadEconomica", d.actividad_economica);
                        Elemento(w, "codigoProductoSin", d.codigo_producto_sin.ToString(CultureInfo.InvariantCulture));
                        Elemento(w, "codigoProducto", d.codigo_producto);
                        Elemento(w, "descripcion", d.descripcion);
                        Elemento(w, "cantidad", Monto(d.cantidad));
                        Elemento(w, "unidadMedida", d.unidad_medida.ToString(CultureInfo.InvariantCulture));
                        Elemento(w, "precioUnitario", Monto(d.precio_unitario));
                        ElementoNulable(w, "montoDescuento", d.monto_descuento > 0 ? Monto(d.monto_descuento) : null);
                        Elemento(w, "subTotal", Monto(d.sub_total));
                        ElementoNulable(w, "numeroSerie", d.numero_serie);
                        ElementoNulable(w, "numeroImei", null);
                        w.WriteEndElement();
                    }

                    w.WriteEndElement();
                    w.WriteEndDocument();
                }
                return new UTF8Encoding(false).GetString(ms.ToArray());
            }
        }

        /// <summary>
        /// Genera el XML de la Nota Crédito-Débito (documento sector 24, modalidad Computarizada en Línea)
        /// respetando el orden del XSD oficial. Los datos del comprador se toman de la factura original.
        /// </summary>
        public static string GenerarNota(NotaCreditoDebito n)
        {
            Factura f = n.oFactura;
            XmlWriterSettings settings = new XmlWriterSettings() { Encoding = new UTF8Encoding(false), Indent = false };

            using (MemoryStream ms = new MemoryStream())
            {
                using (XmlWriter w = XmlWriter.Create(ms, settings))
                {
                    w.WriteStartDocument(true);
                    w.WriteStartElement(ElementoRaizNotaComputarizada);
                    w.WriteAttributeString("xmlns", "xsi", null, NsXsi);
                    w.WriteAttributeString("xsi", "noNamespaceSchemaLocation", NsXsi, "notaComputarizadaCreditoDebito.xsd");

                    w.WriteStartElement("cabecera");
                    Elemento(w, "nitEmisor", f.nit_emisor.ToString(CultureInfo.InvariantCulture));
                    Elemento(w, "razonSocialEmisor", f.razon_social_emisor);
                    Elemento(w, "municipio", n.municipio);
                    ElementoNulable(w, "telefono", n.telefono);
                    Elemento(w, "numeroNotaCreditoDebito", n.numero_nota.ToString(CultureInfo.InvariantCulture));
                    Elemento(w, "cuf", n.cuf);
                    Elemento(w, "cufd", n.cufd);
                    Elemento(w, "codigoSucursal", n.codigo_sucursal.ToString(CultureInfo.InvariantCulture));
                    Elemento(w, "direccion", n.direccion);
                    ElementoNulable(w, "codigoPuntoVenta", n.codigo_punto_venta > 0 ? n.codigo_punto_venta.ToString(CultureInfo.InvariantCulture) : null);
                    Elemento(w, "fechaEmision", FormatoFecha(n.fecha_emision));
                    ElementoNulable(w, "nombreRazonSocial", f.nombre_razon_social);
                    Elemento(w, "codigoTipoDocumentoIdentidad", f.codigo_tipo_documento.ToString(CultureInfo.InvariantCulture));
                    Elemento(w, "numeroDocumento", f.numero_documento);
                    ElementoNulable(w, "complemento", f.complemento);
                    Elemento(w, "codigoCliente", f.codigo_cliente);
                    Elemento(w, "numeroFactura", f.numero_factura.ToString(CultureInfo.InvariantCulture));
                    Elemento(w, "numeroAutorizacionCuf", f.cuf);
                    Elemento(w, "fechaEmisionFactura", FormatoFecha(f.fecha_emision));
                    Elemento(w, "montoTotalOriginal", Monto(n.monto_total_original));
                    Elemento(w, "montoTotalDevuelto", Monto(n.monto_total_devuelto));
                    ElementoNulable(w, "montoDescuentoCreditoDebito", null);
                    Elemento(w, "montoEfectivoCreditoDebito", Monto(n.monto_efectivo_credito_debito));
                    ElementoNulable(w, "codigoExcepcion", n.codigo_excepcion > 0 ? n.codigo_excepcion.ToString(CultureInfo.InvariantCulture) : null);
                    Elemento(w, "leyenda", n.leyenda);
                    Elemento(w, "usuario", n.usuario);
                    Elemento(w, "codigoDocumentoSector", "24");
                    w.WriteEndElement();

                    foreach (NotaDetalle d in n.oDetalle)
                    {
                        w.WriteStartElement("detalle");
                        Elemento(w, "actividadEconomica", d.actividad_economica);
                        Elemento(w, "codigoProductoSin", d.codigo_producto_sin.ToString(CultureInfo.InvariantCulture));
                        Elemento(w, "codigoProducto", d.codigo_producto);
                        Elemento(w, "descripcion", d.descripcion);
                        Elemento(w, "cantidad", Monto(d.cantidad));
                        Elemento(w, "unidadMedida", d.unidad_medida.ToString(CultureInfo.InvariantCulture));
                        Elemento(w, "precioUnitario", Monto(d.precio_unitario));
                        ElementoNulable(w, "montoDescuento", d.monto_descuento > 0 ? Monto(d.monto_descuento) : null);
                        Elemento(w, "subTotal", Monto(d.sub_total));
                        Elemento(w, "codigoDetalleTransaccion", d.codigo_detalle_transaccion.ToString(CultureInfo.InvariantCulture));
                        w.WriteEndElement();
                    }

                    w.WriteEndElement();
                    w.WriteEndDocument();
                }
                return new UTF8Encoding(false).GetString(ms.ToArray());
            }
        }

        /// <summary>Valida el XML (factura o nota) contra el XSD oficial. Devuelve la lista de errores (vacía si es válido).</summary>
        public static List<string> Validar(string xml)
        {
            List<string> errores = new List<string>();

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.ValidationType = ValidationType.Schema;
            settings.Schemas = ObtenerEsquemas();
            settings.ValidationEventHandler += (s, e) =>
                errores.Add((e.Severity == XmlSeverityType.Error ? "Error" : "Advertencia")
                            + " (línea " + e.Exception.LineNumber + "): " + e.Message);

            try
            {
                using (StringReader sr = new StringReader(xml))
                using (XmlReader reader = XmlReader.Create(sr, settings))
                {
                    while (reader.Read()) { }
                }
            }
            catch (XmlException ex)
            {
                errores.Add("XML mal formado: " + ex.Message);
            }
            return errores;
        }

        public static string FormatoFecha(DateTime fecha)
        {
            return fecha.ToString("yyyy-MM-dd'T'HH:mm:ss.fff", CultureInfo.InvariantCulture);
        }

        private static string Monto(decimal valor)
        {
            return AlgoritmosSiat.Redondear(valor).ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static void Elemento(XmlWriter w, string nombre, string valor)
        {
            w.WriteElementString(nombre, valor ?? string.Empty);
        }

        private static void ElementoNulable(XmlWriter w, string nombre, string valor)
        {
            w.WriteStartElement(nombre);
            if (string.IsNullOrWhiteSpace(valor))
                w.WriteAttributeString("xsi", "nil", NsXsi, "true");
            else
                w.WriteString(valor);
            w.WriteEndElement();
        }

        private static XmlSchemaSet ObtenerEsquemas()
        {
            if (_esquemas != null)
                return _esquemas;

            lock (_bloqueo)
            {
                if (_esquemas == null)
                {
                    Assembly asm = typeof(GeneradorXmlFactura).Assembly;
                    XmlSchemaSet set = new XmlSchemaSet();
                    foreach (string recurso in RecursosXsd)
                    {
                        using (Stream s = asm.GetManifestResourceStream(recurso))
                        {
                            if (s == null)
                                throw new InvalidOperationException("No se encontró el XSD incrustado: " + recurso);
                            using (XmlReader r = XmlReader.Create(s))
                                set.Add(null, r);
                        }
                    }
                    set.Compile();
                    _esquemas = set;
                }
            }
            return _esquemas;
        }
    }
}
