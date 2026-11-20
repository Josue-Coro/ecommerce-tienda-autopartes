using System.Collections.Generic;
using System.Linq;

namespace CapaEntidad
{
    /// <summary>Permiso que el sistema verifica (se guarda en SEGURIDAD.PERMISOS con el código como nombre).</summary>
    public class DefinicionPermiso
    {
        public string codigo { get; set; }
        public string descripcion { get; set; }
        public string modulo { get; set; }

        /// <summary>Roles que reciben este permiso al instalarlo (solo si existen con ese nombre).</summary>
        public string[] roles_por_defecto { get; set; }
    }

    /// <summary>
    /// Catálogo de permisos que controlan el acceso a cada pantalla y acción. El rol ADMINISTRADOR los tiene
    /// todos siempre (para no poder quedarse sin acceso); el resto de los roles tiene solo los asignados en
    /// Administración > Registro de permisos.
    /// </summary>
    public static class PermisoSistema
    {
        public const string RolSuperusuario = "ADMINISTRADOR";

        public const string AdminUsuarios = "ADMIN_USUARIOS";
        public const string AdminRoles = "ADMIN_ROLES";
        public const string Mantenedor = "MANTENEDOR";
        public const string Ventas = "VENTAS";
        public const string Compras = "COMPRAS";
        public const string Clientes = "CLIENTES";
        public const string Proveedores = "PROVEEDORES";
        public const string Stock = "STOCK";
        public const string Reportes = "REPORTES";
        public const string FacturasEmitir = "FACTURAS_EMITIR";
        public const string FacturasAnular = "FACTURAS_ANULAR";
        public const string FacturacionConfig = "FACTURACION_CONFIG";
        public const string Contabilidad = "CONTABILIDAD";
        public const string ContabilidadCierre = "CONTABILIDAD_CIERRE";

        private static readonly string[] Admin = { "ADMINISTRADOR" };
        private static readonly string[] Gerencia = { "ADMINISTRADOR", "GERENTE" };
        private static readonly string[] Operacion = { "ADMINISTRADOR", "GERENTE", "VENDEDOR" };

        public static readonly List<DefinicionPermiso> Catalogo = new List<DefinicionPermiso>
        {
            P(AdminUsuarios, "Administración", "Administrar usuarios (crear, editar, eliminar y restablecer contraseñas)", Admin),
            P(AdminRoles, "Administración", "Administrar roles y asignar permisos", Admin),
            P(Mantenedor, "Mantenimiento", "Mantener catálogos: categorías, marcas, productos, servicios, unidades, tipos de pago y tiendas", Gerencia),
            P(Ventas, "Operaciones", "Registrar ventas", Operacion),
            P(Compras, "Operaciones", "Registrar compras", Operacion),
            P(Clientes, "Operaciones", "Administrar clientes", Operacion),
            P(Proveedores, "Operaciones", "Administrar proveedores", Operacion),
            P(Stock, "Operaciones", "Consultar y ajustar el stock", Operacion),
            P(Reportes, "Reportes", "Ver y exportar los reportes de ventas y compras", Gerencia),
            P(FacturasEmitir, "Facturación", "Emitir, consultar e imprimir facturas", Operacion),
            P(FacturasAnular, "Facturación", "Anular facturas y emitir o anular notas crédito-débito", Gerencia),
            P(FacturacionConfig, "Facturación", "Configurar facturación, homologación SIN y contingencia", Gerencia),
            P(Contabilidad, "Contabilidad", "Contabilidad: plan de cuentas, asientos, libros, estados financieros e impuestos", new[] { "ADMINISTRADOR", "GERENTE", "CONTADOR" }),
            P(ContabilidadCierre, "Contabilidad", "Cerrar y reabrir meses contables", Gerencia),
        };

        public static bool EsDelSistema(string nombre)
        {
            return nombre != null && Catalogo.Any(p => p.codigo == nombre.Trim().ToUpperInvariant());
        }

        private static DefinicionPermiso P(string codigo, string modulo, string descripcion, string[] roles)
        {
            return new DefinicionPermiso { codigo = codigo, modulo = modulo, descripcion = descripcion, roles_por_defecto = roles };
        }
    }
}
