using System;

namespace CapaEntidad.Facturacion
{
    /// <summary>
    /// Tipos de catálogo (paramétricas) que se descargan del SIN con la sincronización diaria.
    /// </summary>
    public static class TipoCatalogoSin
    {
        public const string ActividadEconomica = "ACTIVIDAD_ECONOMICA";
        public const string ProductoServicio = "PRODUCTO_SERVICIO";
        public const string LeyendaFactura = "LEYENDA_FACTURA";
        public const string UnidadMedida = "UNIDAD_MEDIDA";
        public const string MetodoPago = "METODO_PAGO";
        public const string TipoDocumentoIdentidad = "TIPO_DOCUMENTO_IDENTIDAD";
        public const string TipoMoneda = "TIPO_MONEDA";
        public const string MotivoAnulacion = "MOTIVO_ANULACION";
        public const string EventoSignificativo = "EVENTO_SIGNIFICATIVO";
        public const string MensajeServicio = "MENSAJE_SERVICIO";
        public const string TipoEmision = "TIPO_EMISION";
        public const string TipoFactura = "TIPO_FACTURA";
        public const string DocumentoSector = "DOCUMENTO_SECTOR";

        public static readonly string[] Todos =
        {
            ActividadEconomica, ProductoServicio, LeyendaFactura, UnidadMedida, MetodoPago,
            TipoDocumentoIdentidad, TipoMoneda, MotivoAnulacion, EventoSignificativo,
            MensajeServicio, TipoEmision, TipoFactura, DocumentoSector
        };
    }

    /// <summary>Un registro de una paramétrica del SIN (tabla FACTURACION.CATALOGO).</summary>
    public class CatalogoSin
    {
        public string tipo { get; set; }
        public string codigo { get; set; }
        public string descripcion { get; set; }

        /// <summary>Actividad económica asociada (aplica a productos/servicios y leyendas).</summary>
        public string codigo_actividad { get; set; }

        public DateTime? fecha_sincronizacion { get; set; }
    }

    /// <summary>Cantidad de registros por catálogo, para mostrar el estado de la sincronización.</summary>
    public class ResumenCatalogo
    {
        public string tipo { get; set; }
        public int cantidad { get; set; }
        public DateTime? ultima_sincronizacion { get; set; }
    }
}
