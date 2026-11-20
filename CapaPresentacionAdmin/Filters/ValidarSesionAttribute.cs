using System;
using System.Linq;
using System.Web.Mvc;

using CapaEntidad;
using CapaNegocio;

namespace CapaPresentacionAdmin.Filters
{
    /// <summary>
    /// Exige un usuario con sesión iniciada y, opcionalmente, alguno de los permisos indicados.
    /// Protege tanto las vistas como los endpoints JSON (devuelve 401/403 a las peticiones AJAX).
    ///  Uso: [ValidarSesion(Permisos = "VENTAS,REPORTES")]  → basta con tener uno de ellos.
    /// El rol ADMINISTRADOR tiene todos los permisos. En cada petición se refresca el rol del usuario desde la base
    /// (con caché), así los cambios de rol o de permisos se aplican sin volver a iniciar sesión, y un usuario
    /// desactivado pierde la sesión.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class ValidarSesionAttribute : ActionFilterAttribute
    {
        /// <summary>Códigos de permiso separados por coma (ver PermisoSistema). Vacío = cualquier usuario con sesión.</summary>
        public string Permisos { get; set; }

        /// <summary>Nombres de rol separados por coma (compatibilidad; preferir Permisos).</summary>
        public string Roles { get; set; }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            // Un [ValidarSesion] en la acción reemplaza al del controlador.
            if (filterContext.ActionDescriptor.IsDefined(typeof(ValidarSesionAttribute), true)
                && !filterContext.ActionDescriptor.GetCustomAttributes(typeof(ValidarSesionAttribute), true).Contains(this))
                return;

            var http = filterContext.HttpContext;
            User_activo usuario = http.Session["Usuario"] as User_activo;
            bool esAjax = http.Request.IsAjaxRequest() || (http.Request.ContentType ?? "").Contains("application/json");

            if (usuario != null && !CN_Autorizacion.RefrescarUsuario(usuario))
            {
                // El usuario fue desactivado o eliminado: se cierra su sesión.
                http.Session.Clear();
                usuario = null;
            }

            if (usuario == null || usuario.id_user_activo == 0)
            {
                filterContext.Result = esAjax
                    ? Denegar(filterContext, 401, "Su sesión expiró. Inicie sesión nuevamente.")
                    : new RedirectResult(new UrlHelper(filterContext.RequestContext).Action("Index", "Acceso"));
                return;
            }

            http.Session["NombreRol"] = usuario.oRol != null ? usuario.oRol.nombre : null;

            bool permitido = true;
            if (!string.IsNullOrWhiteSpace(Permisos))
                permitido = CN_Autorizacion.TieneAlguno(usuario, Lista(Permisos));
            if (permitido && !string.IsNullOrWhiteSpace(Roles))
                permitido = CN_Autorizacion.EsSuperusuario(usuario)
                            || Lista(Roles).Any(r => string.Equals(r, usuario.oRol != null ? usuario.oRol.nombre : null, StringComparison.OrdinalIgnoreCase));

            if (!permitido)
            {
                filterContext.Result = esAjax
                    ? Denegar(filterContext, 403, "No tiene permisos para realizar esta acción.")
                    : new RedirectResult(new UrlHelper(filterContext.RequestContext).Action("AccesoDenegado", "Home"));
            }
        }

        private static string[] Lista(string valor)
        {
            return valor.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
        }

        private static ActionResult Denegar(ActionExecutingContext ctx, int codigo, string mensaje)
        {
            ctx.HttpContext.Response.StatusCode = codigo;
            ctx.HttpContext.Response.TrySkipIisCustomErrors = true;
            // Sin esto, el módulo de Forms Authentication convierte el 401 en una redirección al login.
            ctx.HttpContext.Response.SuppressFormsAuthenticationRedirect = true;
            return new JsonResult
            {
                Data = new { resultado = false, mensaje = mensaje },
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }
    }

    /// <summary>Consulta de permisos desde las vistas: @if (Permiso.Tiene("VENTAS")) { ... }</summary>
    public static class Permiso
    {
        public static bool Tiene(params string[] permisos)
        {
            var ctx = System.Web.HttpContext.Current;
            User_activo usuario = ctx != null && ctx.Session != null ? ctx.Session["Usuario"] as User_activo : null;
            return CN_Autorizacion.TieneAlguno(usuario, permisos);
        }
    }
}
