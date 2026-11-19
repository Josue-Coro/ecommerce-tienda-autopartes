using System;

namespace CapaEntidad.Facturacion
{
    /// <summary>
    /// Relación entre una tienda del sistema y su sucursal / punto de venta en el Padrón del SIN
    /// (tabla FACTURACION.SUCURSAL), junto con el CUIS y el CUFD vigentes.
    /// </summary>
    public class SucursalFacturacion
    {
        public int id_tienda { get; set; }
        public string nombre_tienda { get; set; }
        public string direccion_tienda { get; set; }

        /// <summary>0 = Casa Matriz, 1..n = Sucursal.</summary>
        public int codigo_sucursal { get; set; }

        /// <summary>0 = no corresponde, 1..n = punto de venta.</summary>
        public int codigo_punto_venta { get; set; }

        public string municipio { get; set; }
        public string telefono { get; set; }

        /// <summary>Indica si la tienda ya tiene datos de sucursal registrados.</summary>
        public bool configurada { get; set; }

        public CodigoCuis oCuis { get; set; }
        public CodigoCufd oCufd { get; set; }
    }

    /// <summary>Código Único de Inicio de Sistemas (vigencia 365 días).</summary>
    public class CodigoCuis
    {
        public int id_cuis { get; set; }
        public int id_tienda { get; set; }
        public string codigo { get; set; }
        public DateTime fecha_solicitud { get; set; }
        public DateTime fecha_vigencia { get; set; }
        public bool vigente { get { return DateTime.Now < fecha_vigencia; } }
    }

    /// <summary>Código Único de Facturación Diaria (vigencia 24 horas).</summary>
    public class CodigoCufd
    {
        public int id_cufd { get; set; }
        public int id_tienda { get; set; }
        public string codigo { get; set; }
        public string codigo_control { get; set; }
        public string direccion { get; set; }
        public DateTime fecha_solicitud { get; set; }
        public DateTime fecha_vigencia { get; set; }
        public bool vigente { get { return DateTime.Now < fecha_vigencia; } }
    }
}
