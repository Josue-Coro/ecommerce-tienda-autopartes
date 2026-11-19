using System;
using System.Collections.Generic;

namespace CapaEntidad.Facturacion
{
    /// <summary>Mensaje (código + descripción) devuelto por un servicio del SIN.</summary>
    public class MensajeSiat
    {
        public int codigo { get; set; }
        public string descripcion { get; set; }

        /// <summary>En la validación de paquetes: posición (base 0) del archivo observado; null si no aplica.</summary>
        public int? numero_archivo { get; set; }

        public override string ToString()
        {
            return (numero_archivo.HasValue ? "[archivo " + numero_archivo + "] " : "") + codigo + " - " + descripcion;
        }
    }

    /// <summary>Respuesta común de los servicios del SIN.</summary>
    public class RespuestaSiat
    {
        public bool transaccion { get; set; }
        public List<MensajeSiat> mensajes { get; set; } = new List<MensajeSiat>();

        public string MensajesComoTexto()
        {
            return string.Join(" | ", mensajes);
        }
    }

    public class RespuestaCuis : RespuestaSiat
    {
        public string codigo { get; set; }
        public DateTime fecha_vigencia { get; set; }
    }

    public class RespuestaCufd : RespuestaSiat
    {
        public string codigo { get; set; }
        public string codigo_control { get; set; }
        public string direccion { get; set; }
        public DateTime fecha_vigencia { get; set; }
    }

    public class RespuestaCatalogo : RespuestaSiat
    {
        public List<CatalogoSin> registros { get; set; } = new List<CatalogoSin>();
    }

    /// <summary>Respuesta de recepción, anulación o verificación de estado de una factura.</summary>
    public class RespuestaRecepcion : RespuestaSiat
    {
        /// <summary>908 = validada, 904 = observada, 901 = pendiente, 905 = anulada.</summary>
        public int codigo_estado { get; set; }

        public string codigo_descripcion { get; set; }
        public string codigo_recepcion { get; set; }
    }

    /// <summary>Códigos de estado que usa el SIN en las respuestas de recepción.</summary>
    public static class CodigoEstadoSiat
    {
        public const int Pendiente = 901;
        public const int Rechazada = 902;
        public const int Observada = 904;
        public const int Anulada = 905;
        public const int Validada = 908;
    }

    /// <summary>Datos que se envían al servicio de recepción de facturas.</summary>
    public class SolicitudRecepcionFactura
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

        /// <summary>XML de la factura comprimido con GZip.</summary>
        public byte[] archivo { get; set; }

        /// <summary>SHA-256 (hexadecimal) del archivo comprimido.</summary>
        public string hash_archivo { get; set; }

        public DateTime fecha_envio { get; set; }
    }

    /// <summary>Datos que se envían al servicio de anulación de facturas.</summary>
    public class SolicitudAnulacionFactura
    {
        public int codigo_ambiente { get; set; }
        public int codigo_modalidad { get; set; }
        public int codigo_documento_sector { get; set; }
        public int tipo_factura_documento { get; set; }
        public string codigo_sistema { get; set; }
        public long nit { get; set; }
        public string cuis { get; set; }
        public string cufd { get; set; }
        public int codigo_sucursal { get; set; }
        public int codigo_punto_venta { get; set; }
        public string cuf { get; set; }
        public int codigo_motivo { get; set; }
    }
}
