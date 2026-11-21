using System;
using System.Collections.Generic;

namespace CapaEntidad.Contabilidad
{
    /// <summary>Cuenta del plan de cuentas (tabla CONTABILIDAD.CUENTA).</summary>
    public class Cuenta
    {
        public int id_cuenta { get; set; }
        public string codigo { get; set; }
        public string nombre { get; set; }
        public int nivel { get; set; }
        public int? id_padre { get; set; }
        public string codigo_padre { get; set; }

        /// <summary>ACTIVO, PASIVO, PATRIMONIO, INGRESO o EGRESO.</summary>
        public string tipo { get; set; }

        /// <summary>D = deudora, A = acreedora.</summary>
        public string naturaleza { get; set; }

        /// <summary>Solo las cuentas imputables reciben movimientos.</summary>
        public bool imputable { get; set; }

        public bool estado { get; set; }
        public bool tiene_movimientos { get; set; }
    }

    /// <summary>Cuenta que usa un tipo de asiento automático (tabla CONTABILIDAD.CUENTA_CONFIG).</summary>
    public class CuentaConfig
    {
        public string clave { get; set; }
        public string descripcion { get; set; }
        public int id_cuenta { get; set; }
        public string codigo { get; set; }
        public string nombre { get; set; }
    }

    public static class ClaveCuenta
    {
        public const string Caja = "CAJA";
        public const string Bancos = "BANCOS";
        public const string CuentasCobrar = "CUENTAS_COBRAR";
        public const string CreditoFiscal = "CREDITO_FISCAL";
        public const string Inventario = "INVENTARIO";
        public const string CuentasPagar = "CUENTAS_PAGAR";
        public const string DebitoFiscal = "DEBITO_FISCAL";
        public const string ItPorPagar = "IT_POR_PAGAR";
        public const string Ventas = "VENTAS";
        public const string Servicios = "SERVICIOS";
        public const string Devoluciones = "DEVOLUCIONES";
        public const string CostoVentas = "COSTO_VENTAS";
        public const string ItGasto = "IT_GASTO";
    }

    /// <summary>Asiento contable (tabla CONTABILIDAD.ASIENTO).</summary>
    public class Asiento
    {
        public int id_asiento { get; set; }
        public int gestion { get; set; }
        public int numero { get; set; }
        public DateTime fecha { get; set; }
        public string glosa { get; set; }

        /// <summary>AUTOMATICO o MANUAL.</summary>
        public string tipo { get; set; }

        /// <summary>FACTURA, ANULACION_FACTURA, NOTA, ANULACION_NOTA, COMPRA o null (manual).</summary>
        public string origen_tipo { get; set; }

        public int? origen_id { get; set; }

        /// <summary>VIGENTE o ANULADO.</summary>
        public string estado { get; set; }

        public string usuario { get; set; }
        public List<AsientoDetalle> oDetalle { get; set; } = new List<AsientoDetalle>();
    }

    public class AsientoDetalle
    {
        public int id_cuenta { get; set; }
        public string codigo { get; set; }
        public string nombre { get; set; }
        public decimal debe { get; set; }
        public decimal haber { get; set; }
        public string glosa { get; set; }
    }

    public static class OrigenAsiento
    {
        public const string Factura = "FACTURA";
        public const string AnulacionFactura = "ANULACION_FACTURA";
        public const string Nota = "NOTA";
        public const string AnulacionNota = "ANULACION_NOTA";
        public const string Compra = "COMPRA";
    }

    /// <summary>Movimiento del Libro Mayor con saldo acumulado.</summary>
    public class MovimientoMayor
    {
        public int id_asiento { get; set; }
        public int numero { get; set; }
        public DateTime fecha { get; set; }
        public string glosa { get; set; }
        public decimal debe { get; set; }
        public decimal haber { get; set; }
        public decimal saldo { get; set; }
    }

    public class LibroMayor
    {
        public Cuenta oCuenta { get; set; }
        public decimal saldo_anterior { get; set; }
        public List<MovimientoMayor> movimientos { get; set; } = new List<MovimientoMayor>();
        public decimal total_debe { get; set; }
        public decimal total_haber { get; set; }
        public decimal saldo_final { get; set; }
    }

    /// <summary>Fila del Balance de comprobación de sumas y saldos.</summary>
    public class SumaCuenta
    {
        public int id_cuenta { get; set; }
        public string codigo { get; set; }
        public string nombre { get; set; }
        public string tipo { get; set; }
        public string naturaleza { get; set; }
        public decimal debe { get; set; }
        public decimal haber { get; set; }
        public decimal saldo_deudor { get { return debe > haber ? debe - haber : 0m; } }
        public decimal saldo_acreedor { get { return haber > debe ? haber - debe : 0m; } }
    }

    public class PeriodoCerrado
    {
        public int gestion { get; set; }
        public int mes { get; set; }
        public string usuario { get; set; }
        public DateTime fecha_cierre { get; set; }
    }

    /// <summary>Datos de la factura del proveedor de una compra (tabla CONTABILIDAD.COMPRA_FISCAL).</summary>
    public class CompraFiscal
    {
        public int id_compra { get; set; }
        public DateTime fecha_compra { get; set; }
        public decimal monto_compra { get; set; }
        public string proveedor { get; set; }
        public string nit_proveedor_registrado { get; set; }
        public bool registrada { get; set; }
        public bool contabilizada { get; set; }

        public long nit_proveedor { get; set; }
        public string razon_social { get; set; }
        public string codigo_autorizacion { get; set; }
        public long numero_factura { get; set; }
        public DateTime? fecha_factura { get; set; }
        public decimal importe_total { get; set; }
        public decimal importe_no_sujeto { get; set; }
        public decimal descuentos { get; set; }

        /// <summary>1 = destino a actividades gravadas (con crédito fiscal); 2 a 5 según el RCV.</summary>
        public int tipo_compra { get; set; }

        public string codigo_control { get; set; }
        public decimal base_credito_fiscal { get { return importe_total - importe_no_sujeto - descuentos; } }
    }

    // ---------------------------------------------------------------- documentos a contabilizar

    public class FacturaAContabilizar
    {
        public int id { get; set; }
        public DateTime fecha { get; set; }
        public long numero { get; set; }
        public string tercero { get; set; }
        public decimal monto_total { get; set; }
        public int codigo_metodo_pago { get; set; }
        public decimal monto_productos { get; set; }
        public decimal costo_productos { get; set; }
    }

    public class AnulacionAContabilizar
    {
        public int id { get; set; }
        public DateTime fecha { get; set; }
        public long numero { get; set; }
    }

    public class NotaAContabilizar
    {
        public int id { get; set; }
        public DateTime fecha { get; set; }
        public long numero { get; set; }
        public long numero_factura { get; set; }
        public string tercero { get; set; }
        public decimal monto_total_devuelto { get; set; }
        public decimal monto_efectivo_credito_debito { get; set; }
        public int codigo_metodo_pago { get; set; }
        public bool reingreso_stock { get; set; }
        public decimal monto_productos { get; set; }
        public decimal costo_productos { get; set; }
    }

    public class CompraAContabilizar
    {
        public int id { get; set; }
        public DateTime fecha { get; set; }
        public string tercero { get; set; }
        public decimal monto_total { get; set; }
        public int? codigo_metodo_pago { get; set; }
        public bool con_factura { get; set; }
        public long? numero_factura { get; set; }
        public int tipo_compra { get; set; }
        public decimal base_credito_fiscal { get; set; }
    }

    public class DocumentosPendientes
    {
        public List<FacturaAContabilizar> facturas { get; set; } = new List<FacturaAContabilizar>();
        public List<AnulacionAContabilizar> anulaciones_factura { get; set; } = new List<AnulacionAContabilizar>();
        public List<NotaAContabilizar> notas { get; set; } = new List<NotaAContabilizar>();
        public List<AnulacionAContabilizar> anulaciones_nota { get; set; } = new List<AnulacionAContabilizar>();
        public List<CompraAContabilizar> compras { get; set; } = new List<CompraAContabilizar>();
    }

    // ---------------------------------------------------------------- libros fiscales e impuestos

    /// <summary>Fila del Libro de Ventas (estructura "Ventas Estándar" del RCV).</summary>
    public class FilaLibroVentas
    {
        public int numero { get; set; }
        public DateTime fecha_factura { get; set; }
        public long numero_factura { get; set; }
        public string codigo_autorizacion { get; set; }
        public string nit_ci_cliente { get; set; }
        public string complemento { get; set; }
        public string razon_social { get; set; }
        public decimal importe_total { get; set; }
        public decimal subtotal { get; set; }
        public decimal descuentos { get; set; }
        public decimal base_debito_fiscal { get; set; }
        public decimal debito_fiscal { get; set; }

        /// <summary>V = válida, A = anulada, C = emitida en contingencia.</summary>
        public string estado { get; set; }
    }

    /// <summary>Fila del Libro de Compras (estructura del Registro de Compras del RCV).</summary>
    public class FilaLibroCompras
    {
        public int numero { get; set; }
        public long nit_proveedor { get; set; }
        public string razon_social { get; set; }
        public string codigo_autorizacion { get; set; }
        public long numero_factura { get; set; }
        public DateTime fecha_factura { get; set; }
        public decimal importe_total { get; set; }
        public decimal importe_no_sujeto { get; set; }
        public decimal subtotal { get; set; }
        public decimal descuentos { get; set; }
        public decimal base_credito_fiscal { get; set; }
        public decimal credito_fiscal { get; set; }
        public int tipo_compra { get; set; }
        public string codigo_control { get; set; }
    }

    /// <summary>Totales impositivos de un mes (base para la liquidación).</summary>
    public class TotalesMes
    {
        public int gestion { get; set; }
        public int mes { get; set; }
        public decimal base_debito_fiscal { get; set; }
        public decimal devoluciones { get; set; }
        public decimal credito_fiscal_devoluciones { get; set; }
        public decimal base_credito_fiscal { get; set; }
    }

    /// <summary>Liquidación mensual de IVA e IT.</summary>
    public class LiquidacionImpuestos
    {
        public int gestion { get; set; }
        public int mes { get; set; }

        // IVA
        public decimal ventas_gravadas { get; set; }
        public decimal debito_fiscal { get; set; }
        public decimal compras_gravadas { get; set; }
        public decimal credito_fiscal_compras { get; set; }
        public decimal devoluciones { get; set; }
        public decimal credito_fiscal_devoluciones { get; set; }
        public decimal credito_fiscal_total { get; set; }
        public decimal saldo_favor_anterior { get; set; }
        public decimal iva_a_pagar { get; set; }
        public decimal saldo_favor_siguiente { get; set; }

        // IT
        public decimal ingresos_brutos { get; set; }
        public decimal it_a_pagar { get; set; }
    }
}
