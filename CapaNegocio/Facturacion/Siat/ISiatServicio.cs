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

        /// <summary>Servicio "registroEventoSignificativo" (informa la contingencia ya superada).</summary>
        RespuestaEvento RegistroEventoSignificativo(SolicitudEventoSignificativo solicitud);

        /// <summary>Servicio "recepcionPaqueteFactura" (paquete .tar.gz de hasta 500 facturas fuera de línea).</summary>
        RespuestaRecepcion RecepcionPaqueteFactura(SolicitudRecepcionPaquete solicitud);

        /// <summary>Servicio "validacionRecepcionPaqueteFactura".</summary>
        RespuestaRecepcion ValidacionRecepcionPaqueteFactura(SolicitudValidacionPaquete solicitud);

        /// <summary>Servicio "recepcionDocumentoAjuste" (notas de crédito-débito; solo en línea).</summary>
        RespuestaRecepcion RecepcionDocumentoAjuste(SolicitudRecepcionDocumentoAjuste solicitud);

        /// <summary>Servicio "anulacionDocumentoAjuste".</summary>
        RespuestaRecepcion AnulacionDocumentoAjuste(SolicitudAnulacionFactura solicitud);
    }

    /// <summary>
    /// El SIN no respondió (time out, error 500, sin internet...). Quien la recibe debe pasar a
    /// emitir fuera de línea, como indica el anexo en "Ingreso a Contingencia".
    /// </summary>
    public class SiatSinConexionException : System.Exception
    {
        public SiatSinConexionException(string mensaje) : base(mensaje) { }
        public SiatSinConexionException(string mensaje, System.Exception interna) : base(mensaje, interna) { }
    }
}
