using CapaEntidad.Facturacion;

namespace CapaNegocio.Facturacion.Siat
{
    /// <summary>
    /// Servicios web del SIN que usa el Sistema de Facturación.
    /// Hoy existe la implementación simulada; la conexión real (SOAP con Token Delegado)
    /// se agrega implementando esta misma interfaz, sin cambiar el resto del sistema.
    /// </summary>
    public interface ISiatServicio
    {
        /// <summary>true si las respuestas son simuladas (no tienen validez tributaria).</summary>
        bool EsSimulado { get; }

        /// <summary>Servicio "verificarComunicacion".</summary>
        RespuestaSiat VerificarComunicacion();

        /// <summary>Servicio "cuis" (Código Único de Inicio de Sistemas).</summary>
        RespuestaCuis SolicitarCuis(ConfiguracionFacturacion config, int codigoSucursal, int codigoPuntoVenta);

        /// <summary>Servicio "cufd" (Código Único de Facturación Diaria).</summary>
        RespuestaCufd SolicitarCufd(ConfiguracionFacturacion config, string cuis, int codigoSucursal, int codigoPuntoVenta, string direccion);

        /// <summary>Servicio "verificarNit".</summary>
        RespuestaSiat VerificarNit(ConfiguracionFacturacion config, string cuis, int codigoSucursal, string nitParaVerificar);

        /// <summary>Servicios de sincronización (un catálogo por llamada).</summary>
        RespuestaCatalogo SincronizarCatalogo(ConfiguracionFacturacion config, string cuis, int codigoSucursal, int codigoPuntoVenta, string tipoCatalogo);

        /// <summary>Servicio "recepcionFactura" (emisión individual en línea).</summary>
        RespuestaRecepcion RecepcionFactura(SolicitudRecepcionFactura solicitud);

        /// <summary>Servicio "anulacionFactura".</summary>
        RespuestaRecepcion AnulacionFactura(SolicitudAnulacionFactura solicitud);
    }
}
