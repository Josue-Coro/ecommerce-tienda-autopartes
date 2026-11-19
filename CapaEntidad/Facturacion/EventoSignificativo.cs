using System;
using System.Collections.Generic;

namespace CapaEntidad.Facturacion
{
    /// <summary>
    /// Evento significativo (contingencia) de una tienda: mientras está ABIERTO las facturas se emiten
    /// fuera de línea con el CUFD vigente al inicio del evento (tabla FACTURACION.EVENTO_SIGNIFICATIVO).
    /// </summary>
    public class EventoSignificativo
    {
        public int id_evento { get; set; }
        public int id_tienda { get; set; }
        public string nombre_tienda { get; set; }

        /// <summary>Código del catálogo EVENTO_SIGNIFICATIVO (1 a 7).</summary>
        public int codigo_evento { get; set; }

        public string descripcion_evento { get; set; }
        public string descripcion { get; set; }
        public DateTime fecha_inicio { get; set; }
        public DateTime? fecha_fin { get; set; }
        public string cufd_evento { get; set; }
        public string codigo_control_evento { get; set; }

        /// <summary>true si el sistema abrió el evento al detectar que el SIN no respondía.</summary>
        public bool automatico { get; set; }

        /// <summary>ABIERTO, REGISTRADO, VALIDADO u OBSERVADO.</summary>
        public string estado { get; set; }

        public string codigo_recepcion_evento { get; set; }
        public string mensajes { get; set; }
        public int cantidad_facturas { get; set; }
        public int facturas_pendientes { get; set; }
    }

    public static class EstadoEvento
    {
        public const string Abierto = "ABIERTO";
        public const string Registrado = "REGISTRADO";
        public const string Validado = "VALIDADO";
        public const string Observado = "OBSERVADO";
    }

    /// <summary>Factura emitida fuera de línea que espera ser enviada en un paquete.</summary>
    public class FacturaPendienteEnvio
    {
        public int id_factura { get; set; }
        public long numero_factura { get; set; }
        public string archivo_xml { get; set; }
    }

    // ------------------------------------------------------------- Servicios del SIN

    public class SolicitudEventoSignificativo
    {
        public int codigo_ambiente { get; set; }
        public string codigo_sistema { get; set; }
        public long nit { get; set; }
        public string cuis { get; set; }

        /// <summary>CUFD vigente al momento de registrar (obtenido después de recuperar la conexión).</summary>
        public string cufd { get; set; }

        public int codigo_sucursal { get; set; }
        public int codigo_punto_venta { get; set; }
        public int codigo_evento { get; set; }
        public string descripcion { get; set; }
        public DateTime fecha_inicio_evento { get; set; }
        public DateTime fecha_fin_evento { get; set; }

        /// <summary>CUFD usado para emitir durante la contingencia.</summary>
        public string cufd_evento { get; set; }
    }

    public class RespuestaEvento : RespuestaSiat
    {
        public string codigo_recepcion { get; set; }
    }

    public class SolicitudRecepcionPaquete
    {
        public int codigo_ambiente { get; set; }
        public int codigo_modalidad { get; set; }
        public int codigo_emision { get; set; }
        public int codigo_documento_sector { get; set; }
        public int tipo_factura_documento { get; set; }
        public string codigo_sistema { get; set; }
        public long nit { get; set; }
        public string cuis { get; set; }
        public string cufd { get; set; }
        public int codigo_sucursal { get; set; }
        public int codigo_punto_venta { get; set; }

        /// <summary>Paquete TAR con los XML, comprimido con GZip (paquete.tar.gz).</summary>
        public byte[] archivo { get; set; }

        public string hash_archivo { get; set; }
        public DateTime fecha_envio { get; set; }
        public int cantidad_facturas { get; set; }

        /// <summary>Código de recepción que devolvió el registro del evento significativo.</summary>
        public string codigo_evento { get; set; }

        public string cafc { get; set; }
    }

    public class SolicitudValidacionPaquete
    {
        public int codigo_ambiente { get; set; }
        public int codigo_modalidad { get; set; }
        public int codigo_emision { get; set; }
        public int codigo_documento_sector { get; set; }
        public int tipo_factura_documento { get; set; }
        public string codigo_sistema { get; set; }
        public long nit { get; set; }
        public string cuis { get; set; }
        public string cufd { get; set; }
        public int codigo_sucursal { get; set; }
        public int codigo_punto_venta { get; set; }
        public string codigo_recepcion { get; set; }
    }
}
