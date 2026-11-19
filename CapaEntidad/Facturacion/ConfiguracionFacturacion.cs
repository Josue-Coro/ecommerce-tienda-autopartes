using System;

namespace CapaEntidad.Facturacion
{
    /// <summary>
    /// Datos del emisor y parámetros generales del Sistema de Facturación (tabla FACTURACION.CONFIGURACION, una sola fila).
    /// </summary>
    public class ConfiguracionFacturacion
    {
        public long nit { get; set; }
        public string razon_social { get; set; }
        public string codigo_sistema { get; set; }

        /// <summary>1 = Producción, 2 = Pruebas y Piloto.</summary>
        public int codigo_ambiente { get; set; }

        /// <summary>1 = Electrónica en Línea, 2 = Computarizada en Línea.</summary>
        public int codigo_modalidad { get; set; }

        /// <summary>Actividad económica (CAEB) registrada en el Padrón para el NIT.</summary>
        public string actividad_economica { get; set; }

        /// <summary>Cuando es true, las llamadas al SIN se atienden con el servicio simulado.</summary>
        public bool modo_simulado { get; set; }

        /// <summary>Token Delegado obtenido en el portal SIAT (solo se usa contra el SIN real).</summary>
        public string token_delegado { get; set; }

        public DateTime? fecha_actualizacion { get; set; }
    }
}
