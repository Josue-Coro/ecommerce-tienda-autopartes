using System.Collections.Generic;
using System.Linq;

using CapaEntidad.Facturacion;

namespace CapaNegocio.Facturacion.Siat
{
    /// <summary>
    /// Catálogos que devuelve el SIN SIMULADO.
    ///
    /// Los catálogos de tipo de documento, motivos de anulación, eventos significativos, tipos de emisión,
    /// tipos de factura y documento sector siguen lo publicado en el Anexo Técnico. Los códigos de
    /// productos/servicios, actividades, unidades y leyendas son DATOS DE EJEMPLO: al conectar con el
    /// SIN real, la sincronización los reemplaza por los oficiales y hay que volver a homologar.
    /// </summary>
    public static class CatalogosSimulados
    {
        public const string ActividadVentaAutopartes = "453000";
        public const string ActividadTallerMecanico = "452000";

        public static List<CatalogoSin> Obtener(string tipo)
        {
            switch (tipo)
            {
                case TipoCatalogoSin.ActividadEconomica:
                    return Lista(tipo,
                        R(ActividadVentaAutopartes, "VENTA DE PARTES, PIEZAS Y ACCESORIOS DE VEHÍCULOS AUTOMOTORES"),
                        R(ActividadTallerMecanico, "MANTENIMIENTO Y REPARACIÓN DE VEHÍCULOS AUTOMOTORES"));

                case TipoCatalogoSin.ProductoServicio:
                    return Lista(tipo,
                        R("4531001", "FILTROS PARA MOTOR (ACEITE, AIRE, COMBUSTIBLE)", ActividadVentaAutopartes),
                        R("4531002", "BUJÍAS Y COMPONENTES DEL SISTEMA DE ENCENDIDO", ActividadVentaAutopartes),
                        R("4531003", "FRENOS Y SUS PARTES (PASTILLAS, DISCOS, ZAPATAS)", ActividadVentaAutopartes),
                        R("4531004", "PARTES DE SUSPENSIÓN Y DIRECCIÓN", ActividadVentaAutopartes),
                        R("4531005", "BATERÍAS PARA VEHÍCULOS", ActividadVentaAutopartes),
                        R("4531006", "ACEITES Y LUBRICANTES PARA MOTOR", ActividadVentaAutopartes),
                        R("4531007", "NEUMÁTICOS Y CÁMARAS DE AIRE", ActividadVentaAutopartes),
                        R("4531008", "PARTES ELÉCTRICAS E ILUMINACIÓN", ActividadVentaAutopartes),
                        R("4531009", "CORREAS, MANGUERAS Y EMPAQUETADURAS", ActividadVentaAutopartes),
                        R("4531010", "ACCESORIOS PARA VEHÍCULOS", ActividadVentaAutopartes),
                        R("4531099", "OTRAS PARTES Y PIEZAS PARA VEHÍCULOS AUTOMOTORES", ActividadVentaAutopartes),
                        R("4521001", "SERVICIO DE MANTENIMIENTO PREVENTIVO DE VEHÍCULOS", ActividadTallerMecanico),
                        R("4521002", "SERVICIO DE REPARACIÓN MECÁNICA", ActividadTallerMecanico),
                        R("4521003", "SERVICIO DE INSTALACIÓN DE PARTES Y ACCESORIOS", ActividadTallerMecanico),
                        R("4521004", "SERVICIO DE ALINEADO Y BALANCEADO", ActividadTallerMecanico));

                case TipoCatalogoSin.LeyendaFactura:
                    string[] leyendas =
                    {
                        "Ley N° 453: El proveedor deberá suministrar el servicio en las modalidades y términos ofertados o convenidos.",
                        "Ley N° 453: Tienes derecho a recibir información sobre las características y contenidos de los productos que consumes.",
                        "Ley N° 453: Los productos deben suministrarse en condiciones de inocuidad, calidad y seguridad.",
                        "Ley N° 453: El proveedor de productos debe habilitar medios e instrumentos para efectuar consultas y reclamaciones.",
                        "Ley N° 453: Tienes derecho a un trato equitativo sin discriminación en la oferta de productos.",
                        "Ley N° 453: Está prohibido importar, distribuir o comercializar productos expirados o prontos a expirar."
                    };
                    List<CatalogoSin> lista = new List<CatalogoSin>();
                    int n = 1;
                    foreach (string actividad in new[] { ActividadVentaAutopartes, ActividadTallerMecanico })
                        foreach (string l in leyendas)
                            lista.Add(new CatalogoSin { tipo = tipo, codigo = (n++).ToString(), descripcion = l, codigo_actividad = actividad });
                    return lista;

                case TipoCatalogoSin.UnidadMedida:
                    return Lista(tipo,
                        R("57", "UNIDAD (BIENES)"),
                        R("58", "UNIDAD (SERVICIOS)"),
                        R("62", "PIEZAS"),
                        R("29", "JUEGO"),
                        R("47", "LITRO"),
                        R("20", "GALÓN"),
                        R("9", "CAJA"),
                        R("33", "KIT"),
                        R("31", "METRO"));

                case TipoCatalogoSin.MetodoPago:
                    return Lista(tipo,
                        R("1", "EFECTIVO"),
                        R("2", "TARJETA"),
                        R("3", "CHEQUE"),
                        R("4", "VALES"),
                        R("5", "OTROS"),
                        R("6", "PAGO POSTERIOR (CRÉDITO)"),
                        R("7", "TRANSFERENCIA BANCARIA"),
                        R("8", "DEPÓSITO EN CUENTA"));

                case TipoCatalogoSin.TipoDocumentoIdentidad:
                    return Lista(tipo,
                        R("1", "CI - CÉDULA DE IDENTIDAD"),
                        R("2", "CEX - CÉDULA DE IDENTIDAD DE EXTRANJERO"),
                        R("3", "PAS - PASAPORTE"),
                        R("4", "OD - OTRO DOCUMENTO DE IDENTIDAD"),
                        R("5", "NIT - NÚMERO DE IDENTIFICACIÓN TRIBUTARIA"));

                case TipoCatalogoSin.TipoMoneda:
                    return Lista(tipo, R("1", "BOLIVIANO"), R("2", "DÓLAR"));

                case TipoCatalogoSin.MotivoAnulacion:
                    return Lista(tipo,
                        R("1", "FACTURA MAL EMITIDA"),
                        R("2", "NOTA DE CRÉDITO-DÉBITO MAL EMITIDA"),
                        R("3", "DATOS DE EMISIÓN INCORRECTOS"),
                        R("4", "FACTURA O NOTA DE CRÉDITO-DÉBITO DEVUELTA"));

                case TipoCatalogoSin.EventoSignificativo:
                    return Lista(tipo,
                        R("1", "CORTE DEL SERVICIO DE INTERNET"),
                        R("2", "INACCESIBILIDAD AL SERVICIO WEB DE LA ADMINISTRACIÓN TRIBUTARIA"),
                        R("3", "INGRESO A ZONAS SIN INTERNET POR DESPLIEGUE DE PUNTOS DE VENTA"),
                        R("4", "VENTA EN LUGARES SIN INTERNET"),
                        R("5", "VIRUS INFORMÁTICO O FALLA DE SOFTWARE"),
                        R("6", "CAMBIO DE INFRAESTRUCTURA DE SISTEMA O FALLA DE HARDWARE"),
                        R("7", "CORTE DE SUMINISTRO DE ENERGÍA ELÉCTRICA"));

                case TipoCatalogoSin.TipoEmision:
                    return Lista(tipo, R("1", "EN LÍNEA"), R("2", "FUERA DE LÍNEA"), R("3", "MASIVA"));

                case TipoCatalogoSin.TipoFactura:
                    return Lista(tipo,
                        R("1", "FACTURA CON DERECHO A CRÉDITO FISCAL"),
                        R("2", "FACTURA SIN DERECHO A CRÉDITO FISCAL"),
                        R("3", "DOCUMENTO DE AJUSTE"));

                case TipoCatalogoSin.DocumentoSector:
                    return Lista(tipo,
                        R("1", "FACTURA COMPRA VENTA"),
                        R("2", "RECIBO DE ALQUILER DE BIENES INMUEBLES"),
                        R("24", "NOTA DE CRÉDITO-DÉBITO"));

                case TipoCatalogoSin.MensajeServicio:
                    return MensajesSimulados.Todos
                        .Select(m => new CatalogoSin { tipo = tipo, codigo = m.Key.ToString(), descripcion = m.Value })
                        .ToList();

                default:
                    return new List<CatalogoSin>();
            }
        }

        private static CatalogoSin R(string codigo, string descripcion, string actividad = null)
        {
            return new CatalogoSin { codigo = codigo, descripcion = descripcion, codigo_actividad = actividad };
        }

        private static List<CatalogoSin> Lista(string tipo, params CatalogoSin[] registros)
        {
            foreach (CatalogoSin r in registros)
                r.tipo = tipo;
            return registros.ToList();
        }
    }

    /// <summary>Mensajes que usa el SIN simulado en sus respuestas.</summary>
    public static class MensajesSimulados
    {
        public const int CuisOtorgado = 980;
        public const int CufdOtorgado = 981;
        public const int NitActivo = 986;
        public const int NitInexistente = 994;
        public const int Comunicacion = 926;
        public const int HashNoCoincide = 1001;
        public const int XmlNoCumpleXsd = 1002;
        public const int NitEmisorNoCoincide = 1003;
        public const int CufdNoCoincide = 1004;
        public const int CufInvalido = 1005;
        public const int ProductoNoExiste = 1006;
        public const int SubtotalIncorrecto = 1007;
        public const int MontoTotalIncorrecto = 1008;
        public const int NitCompradorInvalido = 1009;
        public const int FechaFueraDeRango = 1010;
        public const int MotivoAnulacionInvalido = 1011;
        public const int PlazoAnulacionVencido = 1012;
        public const int MetodoPagoInvalido = 1013;
        public const int TarjetaSinEnmascarar = 1014;
        public const int EventoRegistrado = 1015;
        public const int EventoInvalido = 1016;
        public const int EventoFueraDePlazo = 1017;
        public const int EventoNoExiste = 1018;
        public const int CantidadNoCoincide = 1019;
        public const int PaqueteExcedeLimite = 1020;
        public const int FacturaFueraDelEvento = 1021;
        public const int PaqueteNoExiste = 1022;
        public const int NotaSinDetalle = 1023;
        public const int MontoDevueltoIncorrecto = 1024;
        public const int CreditoDebitoIncorrecto = 1025;
        public const int DevueltoSuperaOriginal = 1026;
        public const int NotaSoloEnLinea = 1027;

        public static readonly Dictionary<int, string> Todos = new Dictionary<int, string>
        {
            { CodigoEstadoSiat.Pendiente, "PENDIENTE" },
            { CodigoEstadoSiat.Rechazada, "RECHAZADA" },
            { CodigoEstadoSiat.Observada, "OBSERVADA" },
            { CodigoEstadoSiat.Anulada, "ANULACIÓN CONFIRMADA" },
            { CodigoEstadoSiat.Validada, "VALIDADA" },
            { Comunicacion, "COMUNICACIÓN EXITOSA" },
            { CuisOtorgado, "CUIS OTORGADO" },
            { CufdOtorgado, "CUFD OTORGADO" },
            { NitActivo, "NIT ACTIVO" },
            { NitInexistente, "NIT INEXISTENTE" },
            { HashNoCoincide, "EL HASH DEL ARCHIVO NO COINCIDE CON EL ARCHIVO ENVIADO" },
            { XmlNoCumpleXsd, "EL XML NO CUMPLE CON EL ESQUEMA XSD DEL DOCUMENTO SECTOR" },
            { NitEmisorNoCoincide, "EL NIT DEL EMISOR NO COINCIDE CON EL DE LA SOLICITUD" },
            { CufdNoCoincide, "EL CUFD DEL XML NO COINCIDE CON EL DE LA SOLICITUD" },
            { CufInvalido, "EL CUF NO CORRESPONDE A LOS DATOS DE LA FACTURA" },
            { ProductoNoExiste, "EL CÓDIGO DE PRODUCTO SIN NO EXISTE EN EL CATÁLOGO" },
            { SubtotalIncorrecto, "EL SUBTOTAL NO ES IGUAL A CANTIDAD x PRECIO UNITARIO - DESCUENTO" },
            { MontoTotalIncorrecto, "EL MONTO TOTAL NO COINCIDE CON LA SUMA DE LOS SUBTOTALES MENOS EL DESCUENTO ADICIONAL" },
            { NitCompradorInvalido, "EL NIT DEL COMPRADOR NO ES VÁLIDO; ENVÍE CÓDIGO DE EXCEPCIÓN 1 PARA REGISTRARLO" },
            { FechaFueraDeRango, "LA FECHA DE EMISIÓN ESTÁ FUERA DEL RANGO PERMITIDO" },
            { MotivoAnulacionInvalido, "EL MOTIVO DE ANULACIÓN NO EXISTE EN EL CATÁLOGO" },
            { PlazoAnulacionVencido, "EL PLAZO DE ANULACIÓN VENCIÓ (DÍA 9 DEL MES SIGUIENTE A LA EMISIÓN)" },
            { MetodoPagoInvalido, "EL MÉTODO DE PAGO NO EXISTE EN EL CATÁLOGO" },
            { TarjetaSinEnmascarar, "EL NÚMERO DE TARJETA DEBE ENVIARSE ENMASCARADO" },
            { EventoRegistrado, "EVENTO SIGNIFICATIVO REGISTRADO" },
            { EventoInvalido, "LOS DATOS DEL EVENTO SIGNIFICATIVO NO SON VÁLIDOS" },
            { EventoFueraDePlazo, "EL EVENTO DEBE REGISTRARSE DENTRO DE LAS 48 HORAS POSTERIORES A SU FINALIZACIÓN" },
            { EventoNoExiste, "EL CÓDIGO DE EVENTO NO EXISTE" },
            { CantidadNoCoincide, "LA CANTIDAD DE FACTURAS NO COINCIDE CON EL CONTENIDO DEL PAQUETE" },
            { PaqueteExcedeLimite, "EL PAQUETE EXCEDE EL MÁXIMO DE 500 FACTURAS" },
            { FacturaFueraDelEvento, "LA FACTURA NO CORRESPONDE AL EVENTO SIGNIFICATIVO (CUFD O FECHA FUERA DEL PERIODO)" },
            { PaqueteNoExiste, "EL CÓDIGO DE RECEPCIÓN DEL PAQUETE NO EXISTE" },
            { NotaSinDetalle, "LA NOTA DEBE INCLUIR DETALLE ORIGINAL (1) Y DETALLE DEVUELTO (2)" },
            { MontoDevueltoIncorrecto, "EL MONTO TOTAL DEVUELTO NO COINCIDE CON LA SUMA DEL DETALLE DEVUELTO" },
            { CreditoDebitoIncorrecto, "EL MONTO EFECTIVO CRÉDITO-DÉBITO DEBE SER EL 13% DEL MONTO DEVUELTO" },
            { DevueltoSuperaOriginal, "EL MONTO DEVUELTO SUPERA EL MONTO DE LA FACTURA ORIGINAL" },
            { NotaSoloEnLinea, "LOS DOCUMENTOS DE AJUSTE SOLO SE RECIBEN EN LÍNEA (CÓDIGO DE EMISIÓN 1)" }
        };

        public static MensajeSiat Crear(int codigo, string detalle = null)
        {
            string descripcion = Todos.ContainsKey(codigo) ? Todos[codigo] : "MENSAJE " + codigo;
            if (!string.IsNullOrEmpty(detalle))
                descripcion += ": " + detalle;
            return new MensajeSiat { codigo = codigo, descripcion = descripcion };
        }
    }
}
