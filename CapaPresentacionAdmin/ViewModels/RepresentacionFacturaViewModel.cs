using CapaEntidad.Facturacion;

namespace CapaPresentacionAdmin.ViewModels
{
    /// <summary>Datos para la representación gráfica (impresión) de una factura.</summary>
    public class RepresentacionFacturaViewModel
    {
        public Factura oFactura { get; set; }
        public string url_qr { get; set; }
        public string monto_literal { get; set; }
        public string descripcion_metodo_pago { get; set; }
        public string descripcion_tipo_documento { get; set; }
        public string leyenda_principal { get; set; }
        public string leyenda_modalidad { get; set; }
        public bool es_simulado { get; set; }

        /// <summary>"rollo" (80 mm) o "carta" (media hoja / carta).</summary>
        public string formato { get; set; }
    }
}
