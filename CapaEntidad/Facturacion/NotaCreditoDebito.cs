using System;
using System.Collections.Generic;

namespace CapaEntidad.Facturacion
{
    /// <summary>
    /// Nota Crédito-Débito (documento sector 24, documento de ajuste) sobre una factura validada
    /// (tabla FACTURACION.NOTA_CREDITO_DEBITO). Los datos del comprador y del emisor se toman de la factura original.
    /// </summary>
    public class NotaCreditoDebito
    {
        public int id_nota { get; set; }
        public int id_factura { get; set; }
        public int id_tienda { get; set; }
        public long numero_nota { get; set; }
        public string cuf { get; set; }
        public string cufd { get; set; }
        public DateTime fecha_emision { get; set; }
        public int codigo_sucursal { get; set; }
        public int codigo_punto_venta { get; set; }
        public string direccion { get; set; }
        public string municipio { get; set; }
        public string telefono { get; set; }

        /// <summary>Monto total sujeto a crédito fiscal de la factura original.</summary>
        public decimal monto_total_original { get; set; }

        public decimal monto_total_devuelto { get; set; }

        /// <summary>Crédito / débito fiscal: 13 % del monto devuelto.</summary>
        public decimal monto_efectivo_credito_debito { get; set; }

        public int codigo_excepcion { get; set; }
        public string leyenda { get; set; }
        public string usuario { get; set; }
        public string motivo { get; set; }
        public bool reingreso_stock { get; set; }
        public string archivo_xml { get; set; }
        public string hash_archivo { get; set; }
        public string codigo_recepcion { get; set; }

        /// <summary>VALIDADA, OBSERVADA o ANULADA.</summary>
        public string estado { get; set; }

        public int codigo_estado { get; set; }
        public string mensajes { get; set; }
        public DateTime? fecha_anulacion { get; set; }
        public int? codigo_motivo_anulacion { get; set; }
        public string motivo_anulacion { get; set; }

        /// <summary>Factura original (emisor, comprador, número, CUF y fecha).</summary>
        public Factura oFactura { get; set; }

        public List<NotaDetalle> oDetalle { get; set; } = new List<NotaDetalle>();
    }

    /// <summary>Línea de la nota: 1 = transacción original, 2 = monto devuelto.</summary>
    public class NotaDetalle
    {
        public int id_nota_detalle { get; set; }
        public int id_factura_detalle { get; set; }
        public int codigo_detalle_transaccion { get; set; }
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
    }

    public static class DetalleTransaccionNota
    {
        public const int Original = 1;
        public const int Devuelto = 2;
    }

    /// <summary>Línea de una factura con lo que todavía se puede devolver.</summary>
    public class LineaDevolvible
    {
        public FacturaDetalle oDetalle { get; set; }
        public decimal cantidad_devuelta { get; set; }
        public decimal cantidad_disponible { get { return oDetalle.cantidad - cantidad_devuelta; } }
    }

    /// <summary>Datos que envía el usuario para emitir una nota.</summary>
    public class SolicitudEmisionNota
    {
        public int id_factura { get; set; }
        public string motivo { get; set; }
        public bool reingresar_stock { get; set; }
        public List<LineaDevolucion> lineas { get; set; } = new List<LineaDevolucion>();
    }

    public class LineaDevolucion
    {
        public int id_factura_detalle { get; set; }
        public decimal cantidad { get; set; }
    }

    /// <summary>Datos para el servicio "recepcionDocumentoAjuste".</summary>
    public class SolicitudRecepcionDocumentoAjuste : SolicitudRecepcionFactura
    {
    }
}
