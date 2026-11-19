namespace CapaEntidad.Facturacion
{
    /// <summary>
    /// Relación entre un registro propio (producto, servicio, unidad de medida o tipo de pago)
    /// y el código equivalente del catálogo del SIN.
    /// </summary>
    public class Homologacion
    {
        /// <summary>PRODUCTO, SERVICIO, UNIDAD_MEDIDA o TIPO_PAGO.</summary>
        public string tipo { get; set; }

        /// <summary>Id del registro propio (id_producto, id_servicio, ...).</summary>
        public int id_registro { get; set; }

        /// <summary>Nombre o descripción del registro propio.</summary>
        public string descripcion { get; set; }

        /// <summary>Código del SIN asignado; null si aún no se homologó.</summary>
        public string codigo_sin { get; set; }

        /// <summary>Descripción del código del SIN asignado.</summary>
        public string descripcion_sin { get; set; }
    }

    public static class TipoHomologacion
    {
        public const string Producto = "PRODUCTO";
        public const string Servicio = "SERVICIO";
        public const string UnidadMedida = "UNIDAD_MEDIDA";
        public const string TipoPago = "TIPO_PAGO";
    }
}
