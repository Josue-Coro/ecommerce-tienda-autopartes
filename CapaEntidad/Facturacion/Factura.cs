using System;
using System.Collections.Generic;

namespace CapaEntidad.Facturacion
{
    /// <summary>
    /// Factura Compra Venta (documento sector 1) emitida para una venta (tabla FACTURACION.FACTURA).
    /// Los campos replican la cabecera del XML definido por el SIN.
    /// </summary>
    public class Factura
    {
        public int id_factura { get; set; }
        public int id_venta { get; set; }
        public int id_tienda { get; set; }

        // ---- Emisor ----
        public long nit_emisor { get; set; }
        public string razon_social_emisor { get; set; }
        public string municipio { get; set; }
        public string telefono { get; set; }
        public string direccion { get; set; }
        public int codigo_sucursal { get; set; }
        public int codigo_punto_venta { get; set; }

        // ---- Identificación de la factura ----
        public long numero_factura { get; set; }
        public string cuf { get; set; }
        public string cufd { get; set; }
        public DateTime fecha_emision { get; set; }
        public int codigo_modalidad { get; set; }

        /// <summary>1 = En línea, 2 = Fuera de línea, 3 = Masiva.</summary>
        public int tipo_emision { get; set; }

        /// <summary>1 = Con derecho a crédito fiscal.</summary>
        public int tipo_factura { get; set; }

        /// <summary>1 = Factura Compra Venta.</summary>
        public int codigo_documento_sector { get; set; }

        // ---- Comprador ----
        public string nombre_razon_social { get; set; }
        public int codigo_tipo_documento { get; set; }
        public string numero_documento { get; set; }
        public string complemento { get; set; }
        public string codigo_cliente { get; set; }
        public string correo_cliente { get; set; }

        // ---- Montos ----
        public int codigo_metodo_pago { get; set; }
        public string numero_tarjeta { get; set; }
        public decimal monto_total { get; set; }
        public decimal monto_total_sujeto_iva { get; set; }
        public decimal descuento_adicional { get; set; }
        public int codigo_moneda { get; set; }
        public decimal tipo_cambio { get; set; }
        public decimal monto_total_moneda { get; set; }
        public int codigo_excepcion { get; set; }

        public string leyenda { get; set; }
        public string usuario { get; set; }

        // ---- Resultado del envío al SIN ----
        public string archivo_xml { get; set; }
        public string hash_archivo { get; set; }
        public string codigo_recepcion { get; set; }

        /// <summary>VALIDADA, OBSERVADA, PENDIENTE o ANULADA.</summary>
        public string estado { get; set; }

        /// <summary>Código de estado devuelto por el SIN (908, 904, 901, 905...).</summary>
        public int codigo_estado { get; set; }

        public string mensajes { get; set; }

        // ---- Anulación ----
        public DateTime? fecha_anulacion { get; set; }
        public int? codigo_motivo_anulacion { get; set; }
        public string motivo_anulacion { get; set; }

        public List<FacturaDetalle> oDetalle { get; set; } = new List<FacturaDetalle>();
    }

    /// <summary>Línea de la factura (tabla FACTURACION.FACTURA_DETALLE).</summary>
    public class FacturaDetalle
    {
        public int id_factura_detalle { get; set; }
        public string actividad_economica { get; set; }
        public int codigo_producto_sin { get; set; }
        public string codigo_producto { get; set; }
        public string descripcion { get; set; }
        public decimal cantidad { get; set; }
        public int unidad_medida { get; set; }
        public string descripcion_unidad_medida { get; set; }
        public decimal precio_unitario { get; set; }
        public decimal monto_descuento { get; set; }
        public decimal sub_total { get; set; }
        public string numero_serie { get; set; }
    }

    public static class EstadoFactura
    {
        public const string Validada = "VALIDADA";
        public const string Observada = "OBSERVADA";
        public const string Pendiente = "PENDIENTE";
        public const string Anulada = "ANULADA";
    }

    /// <summary>Datos de una venta con los códigos homologados, listos para armar la factura.</summary>
    public class VentaParaFacturar
    {
        public int id_venta { get; set; }
        public DateTime fecha { get; set; }
        public decimal monto_total { get; set; }
        public int id_cliente { get; set; }
        public int id_tipo_pago { get; set; }
        public string tipo_pago { get; set; }

        /// <summary>Método de pago del SIN homologado al tipo de pago; null si falta homologar.</summary>
        public int? codigo_metodo_pago { get; set; }

        /// <summary>Estado de la factura vigente de la venta (VALIDADA o PENDIENTE); null si no tiene.</summary>
        public string estado_factura_vigente { get; set; }

        public List<LineaVentaParaFacturar> oLineas { get; set; } = new List<LineaVentaParaFacturar>();
    }

    public class LineaVentaParaFacturar
    {
        public string tipo_item { get; set; }
        public int? id_producto { get; set; }
        public int? id_servicio { get; set; }
        public string descripcion { get; set; }
        public decimal cantidad { get; set; }
        public decimal precio { get; set; }
        public decimal sub_total { get; set; }
        public int? codigo_producto_sin { get; set; }
        public int? codigo_unidad_medida { get; set; }
    }

    /// <summary>Venta registrada que todavía no tiene una factura válida.</summary>
    public class VentaPendienteFactura
    {
        public int id_venta { get; set; }
        public DateTime fecha { get; set; }
        public decimal monto_total { get; set; }
        public int id_cliente { get; set; }
        public string cliente { get; set; }
        public string tipo_pago { get; set; }
    }
}
