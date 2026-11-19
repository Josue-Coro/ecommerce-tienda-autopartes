using System;
using System.Collections.Generic;
using System.Linq;

using CapaDatos.Facturacion;
using CapaEntidad.Facturacion;
using CapaNegocio.Facturacion.Siat;

namespace CapaNegocio.Facturacion
{
    /// <summary>
    /// Reglas de negocio de la configuración de facturación: datos del emisor, sucursales,
    /// códigos CUIS / CUFD, sincronización de catálogos y homologación.
    /// </summary>
    public class CN_ConfiguracionFacturacion
    {
        private readonly CD_ConfiguracionFacturacion _cdConfig = new CD_ConfiguracionFacturacion();
        private readonly CD_CatalogoSin _cdCatalogo = new CD_CatalogoSin();

        // ------------------------------------------------------------------ Emisor

        public ConfiguracionFacturacion ObtenerConfiguracion(out string Mensaje)
        {
            return _cdConfig.ObtenerConfiguracion(out Mensaje);
        }

        public bool GuardarConfiguracion(ConfiguracionFacturacion obj, out string Mensaje)
        {
            Mensaje = string.Empty;
            if (obj == null)
                Mensaje = "No se recibieron los datos de la configuración.";
            else if (obj.nit <= 0 || obj.nit > 9999999999999)
                Mensaje = "El NIT del emisor debe ser numérico (máximo 13 dígitos).";
            else if (string.IsNullOrWhiteSpace(obj.razon_social) || obj.razon_social.Trim().Length > 200)
                Mensaje = "La razón social es obligatoria (máximo 200 caracteres).";
            else if (string.IsNullOrWhiteSpace(obj.codigo_sistema))
                Mensaje = "El código de sistema es obligatorio.";
            else if (obj.codigo_ambiente != 1 && obj.codigo_ambiente != 2)
                Mensaje = "El ambiente debe ser 1 (Producción) o 2 (Pruebas y Piloto).";
            else if (obj.codigo_modalidad != 2)
                Mensaje = "Por ahora solo está implementada la modalidad Computarizada en Línea (2).";
            else if (string.IsNullOrWhiteSpace(obj.actividad_economica) || obj.actividad_economica.Trim().Length > 10)
                Mensaje = "La actividad económica es obligatoria (máximo 10 caracteres).";

            if (!string.IsNullOrEmpty(Mensaje))
                return false;

            obj.razon_social = obj.razon_social.Trim().ToUpperInvariant();
            obj.codigo_sistema = obj.codigo_sistema.Trim();
            obj.actividad_economica = obj.actividad_economica.Trim();
            return _cdConfig.GuardarConfiguracion(obj, out Mensaje);
        }

        // ------------------------------------------------------------------ Sucursales

        public List<SucursalFacturacion> ListarSucursales(out string Mensaje)
        {
            return _cdConfig.ListarSucursales(out Mensaje);
        }

        public bool GuardarSucursal(SucursalFacturacion obj, out string Mensaje)
        {
            Mensaje = string.Empty;
            if (obj == null || obj.id_tienda <= 0)
                Mensaje = "Seleccione una tienda.";
            else if (obj.codigo_sucursal < 0 || obj.codigo_sucursal > 9999)
                Mensaje = "El código de sucursal debe estar entre 0 (Casa Matriz) y 9999.";
            else if (obj.codigo_punto_venta < 0 || obj.codigo_punto_venta > 9999)
                Mensaje = "El código de punto de venta debe estar entre 0 y 9999.";
            else if (string.IsNullOrWhiteSpace(obj.municipio) || obj.municipio.Trim().Length > 25)
                Mensaje = "El municipio es obligatorio (máximo 25 caracteres).";
            else if (!string.IsNullOrEmpty(obj.telefono) && obj.telefono.Trim().Length > 25)
                Mensaje = "El teléfono admite como máximo 25 caracteres.";

            if (!string.IsNullOrEmpty(Mensaje))
                return false;

            obj.municipio = obj.municipio.Trim();
            obj.telefono = string.IsNullOrWhiteSpace(obj.telefono) ? null : obj.telefono.Trim();
            return _cdConfig.GuardarSucursal(obj, out Mensaje);
        }

        public SucursalFacturacion ObtenerSucursal(int idTienda, out string Mensaje)
        {
            SucursalFacturacion s = ListarSucursales(out Mensaje).FirstOrDefault(x => x.id_tienda == idTienda);
            if (s == null && string.IsNullOrEmpty(Mensaje))
                Mensaje = "La tienda no existe o está inactiva.";
            return s;
        }

        // ------------------------------------------------------------------ CUIS / CUFD

        public bool SolicitarCuis(int idTienda, out string Mensaje)
        {
            ConfiguracionFacturacion config = ObtenerConfiguracion(out Mensaje);
            if (config == null) return false;

            SucursalFacturacion suc = ObtenerSucursal(idTienda, out Mensaje);
            if (suc == null) return false;
            if (!suc.configurada)
            {
                Mensaje = "Primero registre los datos de sucursal de la tienda.";
                return false;
            }

            if (suc.oCuis != null && suc.oCuis.vigente && suc.oCuis.fecha_vigencia > DateTime.Now.AddDays(5))
            {
                // El anexo permite renovar el CUIS recién desde el quinto día anterior a su vencimiento.
                Mensaje = "La tienda ya tiene un CUIS vigente hasta el " + suc.oCuis.fecha_vigencia.ToString("dd/MM/yyyy")
                        + ". Solo puede renovarse desde 5 días antes de su vencimiento.";
                return false;
            }

            RespuestaCuis r;
            try
            {
                r = SiatServicioFactory.Crear(config).SolicitarCuis(config, suc.codigo_sucursal, suc.codigo_punto_venta);
            }
            catch (SiatSinConexionException ex)
            {
                Mensaje = "El SIN no responde: " + ex.Message;
                return false;
            }
            if (!r.transaccion)
            {
                Mensaje = "El SIN no otorgó el CUIS: " + r.MensajesComoTexto();
                return false;
            }

            if (!_cdConfig.RegistrarCuis(idTienda, r.codigo, r.fecha_vigencia, out Mensaje))
                return false;

            Mensaje = "CUIS " + r.codigo + " obtenido, vigente hasta el " + r.fecha_vigencia.ToString("dd/MM/yyyy") + ". Ahora solicite el CUFD.";
            return true;
        }

        public bool SolicitarCufd(int idTienda, out string Mensaje)
        {
            ConfiguracionFacturacion config = ObtenerConfiguracion(out Mensaje);
            if (config == null) return false;

            SucursalFacturacion suc = ObtenerSucursal(idTienda, out Mensaje);
            if (suc == null) return false;
            try
            {
                return RenovarCufd(config, suc, out Mensaje) != null;
            }
            catch (SiatSinConexionException ex)
            {
                Mensaje = "El SIN no responde: " + ex.Message;
                return false;
            }
        }

        /// <summary>Solicita un CUFD nuevo para la sucursal y lo registra. Puede lanzar SiatSinConexionException.</summary>
        public CodigoCufd RenovarCufd(ConfiguracionFacturacion config, SucursalFacturacion suc, out string Mensaje)
        {
            Mensaje = string.Empty;
            if (suc.oCuis == null || !suc.oCuis.vigente)
            {
                Mensaje = "La tienda " + suc.nombre_tienda + " no tiene un CUIS vigente. Solicítelo en Facturación > Configuración.";
                return null;
            }

            RespuestaCufd r = SiatServicioFactory.Crear(config)
                .SolicitarCufd(config, suc.oCuis.codigo, suc.codigo_sucursal, suc.codigo_punto_venta, suc.direccion_tienda);
            if (!r.transaccion)
            {
                Mensaje = "El SIN no otorgó el CUFD: " + r.MensajesComoTexto();
                return null;
            }

            string direccion = string.IsNullOrWhiteSpace(r.direccion) ? suc.direccion_tienda : r.direccion;
            if (!_cdConfig.RegistrarCufd(suc.id_tienda, r.codigo, r.codigo_control, direccion, r.fecha_vigencia, out Mensaje))
                return null;

            Mensaje = "CUFD obtenido, vigente hasta el " + r.fecha_vigencia.ToString("dd/MM/yyyy HH:mm") + ".";
            return new CodigoCufd
            {
                id_tienda = suc.id_tienda,
                codigo = r.codigo,
                codigo_control = r.codigo_control,
                direccion = direccion,
                fecha_solicitud = DateTime.Now,
                fecha_vigencia = r.fecha_vigencia
            };
        }

        /// <summary>
        /// Devuelve la sucursal lista para emitir: con datos registrados, CUIS vigente y CUFD vigente.
        /// Si el CUFD venció, solicita uno nuevo automáticamente (el CUFD dura 24 horas).
        /// </summary>
        public SucursalFacturacion ObtenerSucursalOperativa(ConfiguracionFacturacion config, int idTienda, out string Mensaje)
        {
            SucursalFacturacion suc = ObtenerSucursal(idTienda, out Mensaje);
            if (suc == null) return null;

            if (!suc.configurada)
            {
                Mensaje = "La tienda " + suc.nombre_tienda + " no tiene datos de sucursal para facturar. Regístrelos en Facturación > Configuración.";
                return null;
            }
            if (suc.oCuis == null || !suc.oCuis.vigente)
            {
                Mensaje = "La tienda " + suc.nombre_tienda + " no tiene un CUIS vigente. Solicítelo en Facturación > Configuración.";
                return null;
            }
            if (suc.oCufd == null || !suc.oCufd.vigente)
            {
                CodigoCufd nuevo = RenovarCufd(config, suc, out Mensaje);
                if (nuevo == null) return null;
                suc.oCufd = nuevo;
            }

            Mensaje = string.Empty;
            return suc;
        }

        // ------------------------------------------------------------------ Catálogos

        /// <summary>Descarga todos los catálogos del SIN y reemplaza los locales.</summary>
        public bool SincronizarCatalogos(out string Mensaje)
        {
            ConfiguracionFacturacion config = ObtenerConfiguracion(out Mensaje);
            if (config == null) return false;

            // La sincronización requiere un CUIS vigente de cualquier sucursal (esquema centralizado).
            SucursalFacturacion suc = ListarSucursales(out Mensaje).FirstOrDefault(s => s.oCuis != null && s.oCuis.vigente);
            if (suc == null)
            {
                Mensaje = "Para sincronizar se necesita al menos una tienda con CUIS vigente.";
                return false;
            }

            ISiatServicio siat = SiatServicioFactory.Crear(config);
            List<string> errores = new List<string>();
            int total = 0;

            foreach (string tipo in TipoCatalogoSin.Todos)
            {
                RespuestaCatalogo r;
                try
                {
                    r = siat.SincronizarCatalogo(config, suc.oCuis.codigo, suc.codigo_sucursal, suc.codigo_punto_venta, tipo);
                }
                catch (SiatSinConexionException ex)
                {
                    Mensaje = "El SIN no responde; los catálogos locales se mantienen. " + ex.Message;
                    return false;
                }
                if (!r.transaccion)
                {
                    errores.Add(tipo + ": " + r.MensajesComoTexto());
                    continue;
                }

                string msg;
                if (_cdCatalogo.ReemplazarCatalogo(tipo, r.registros, out msg))
                    total += r.registros.Count;
                else
                    errores.Add(msg);
            }

            if (errores.Count > 0)
            {
                Mensaje = "Sincronización incompleta: " + string.Join(" | ", errores);
                return false;
            }

            Mensaje = TipoCatalogoSin.Todos.Length + " catálogos sincronizados (" + total + " registros)"
                    + (siat.EsSimulado ? " desde el SIN simulado." : ".");
            return true;
        }

        public List<CatalogoSin> ListarCatalogo(string tipo)
        {
            return _cdCatalogo.Listar(tipo);
        }

        public List<ResumenCatalogo> ResumenCatalogos()
        {
            List<ResumenCatalogo> existentes = _cdCatalogo.Resumen();
            // Incluye también los catálogos todavía no sincronizados (cantidad 0).
            return TipoCatalogoSin.Todos
                .Select(t => existentes.FirstOrDefault(e => e.tipo == t) ?? new ResumenCatalogo { tipo = t, cantidad = 0 })
                .ToList();
        }

        // ------------------------------------------------------------------ Homologación

        public List<Homologacion> ListarHomologacion(string tipo)
        {
            return _cdCatalogo.ListarHomologacion(tipo);
        }

        public bool GuardarHomologacion(string tipo, int idRegistro, string codigoSin, out string Mensaje)
        {
            Mensaje = string.Empty;
            if (tipo != TipoHomologacion.Producto && tipo != TipoHomologacion.Servicio
                && tipo != TipoHomologacion.UnidadMedida && tipo != TipoHomologacion.TipoPago)
            {
                Mensaje = "Tipo de homologación no válido.";
                return false;
            }
            if (idRegistro <= 0)
            {
                Mensaje = "Registro no válido.";
                return false;
            }

            int? codigo = null;
            if (!string.IsNullOrWhiteSpace(codigoSin))
            {
                int valor;
                if (!int.TryParse(codigoSin, out valor) || valor <= 0)
                {
                    Mensaje = "El código del SIN debe ser numérico.";
                    return false;
                }
                codigo = valor;
            }

            return _cdCatalogo.GuardarHomologacion(tipo, idRegistro, codigo, out Mensaje);
        }
    }
}
