using CapaEntidad.Facturacion;

namespace CapaNegocio.Facturacion.Siat
{
    /// <summary>Elige la implementación del SIN según la configuración (simulado o real).</summary>
    public static class SiatServicioFactory
    {
        public static ISiatServicio Crear(ConfiguracionFacturacion config)
        {
            if (config == null || config.modo_simulado)
                return new SiatServicioSimulado(config != null && config.simular_sin_conexion);

            return new SiatServicioNoDisponible();
        }
    }

    /// <summary>
    /// Marcador para el modo real: la integración SOAP con los servicios del SIN todavía no está
    /// implementada. Responde con un mensaje claro en lugar de fallar.
    /// </summary>
    public class SiatServicioNoDisponible : ISiatServicio
    {
        private const string Aviso = "La conexión con los servicios reales del SIN aún no está implementada. Active el modo simulado en la configuración de facturación.";

        public bool EsSimulado { get { return false; } }

        public RespuestaSiat VerificarComunicacion() { return Fallo<RespuestaSiat>(); }
        public RespuestaCuis SolicitarCuis(ConfiguracionFacturacion config, int codigoSucursal, int codigoPuntoVenta) { return Fallo<RespuestaCuis>(); }
        public RespuestaCufd SolicitarCufd(ConfiguracionFacturacion config, string cuis, int codigoSucursal, int codigoPuntoVenta, string direccion) { return Fallo<RespuestaCufd>(); }
        public RespuestaSiat VerificarNit(ConfiguracionFacturacion config, string cuis, int codigoSucursal, string nitParaVerificar) { return Fallo<RespuestaSiat>(); }
        public RespuestaCatalogo SincronizarCatalogo(ConfiguracionFacturacion config, string cuis, int codigoSucursal, int codigoPuntoVenta, string tipoCatalogo) { return Fallo<RespuestaCatalogo>(); }
        public RespuestaRecepcion RecepcionFactura(SolicitudRecepcionFactura solicitud) { return Fallo<RespuestaRecepcion>(); }
        public RespuestaRecepcion AnulacionFactura(SolicitudAnulacionFactura solicitud) { return Fallo<RespuestaRecepcion>(); }
        public RespuestaEvento RegistroEventoSignificativo(SolicitudEventoSignificativo solicitud) { return Fallo<RespuestaEvento>(); }
        public RespuestaRecepcion RecepcionPaqueteFactura(SolicitudRecepcionPaquete solicitud) { return Fallo<RespuestaRecepcion>(); }
        public RespuestaRecepcion ValidacionRecepcionPaqueteFactura(SolicitudValidacionPaquete solicitud) { return Fallo<RespuestaRecepcion>(); }

        private static T Fallo<T>() where T : RespuestaSiat, new()
        {
            T r = new T { transaccion = false };
            r.mensajes.Add(new MensajeSiat { codigo = 0, descripcion = Aviso });
            return r;
        }
    }
}
