namespace CapaEntidad.Facturacion
{
    /// <summary>
    /// Datos del comprador que exige la factura (tabla FACTURACION.CLIENTE_FISCAL).
    /// Se guardan aparte para no modificar la tabla COMERCIAL.CLIENTE.
    /// </summary>
    public class DatosFiscalesCliente
    {
        public int id_cliente { get; set; }

        /// <summary>1 = CI, 2 = CEX, 3 = PAS, 4 = OD, 5 = NIT.</summary>
        public int codigo_tipo_documento { get; set; }

        public string numero_documento { get; set; }
        public string complemento { get; set; }
        public string razon_social { get; set; }
        public string correo { get; set; }
    }

    /// <summary>Datos que el usuario envía al emitir la factura de una venta.</summary>
    public class SolicitudEmisionFactura
    {
        public int id_venta { get; set; }
        public int codigo_tipo_documento { get; set; }
        public string numero_documento { get; set; }
        public string complemento { get; set; }
        public string razon_social { get; set; }
        public string correo { get; set; }

        /// <summary>Solo para pago con tarjeta: se guarda enmascarado (primeros y últimos 4 dígitos).</summary>
        public string numero_tarjeta { get; set; }

        /// <summary>Autoriza al SIN registrar la factura aunque el NIT no sea válido (codigoExcepcion = 1).</summary>
        public bool autorizar_nit_invalido { get; set; }

        /// <summary>Si es true, actualiza los datos fiscales guardados del cliente.</summary>
        public bool guardar_datos_cliente { get; set; }
    }
}
