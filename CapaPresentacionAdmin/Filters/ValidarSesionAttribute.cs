using System;
using System.Linq;
using System.Web.Mvc;

using CapaEntidad;

namespace CapaPresentacionAdmin.Filters
{
    /// <summary>
    /// Exige un usuario con sesión iniciada y, opcionalmente, uno de los roles indicados.
    /// Protege tanto las vistas como los endpoints JSON (devuelve 401/403 a las peticiones AJAX).
    /// Uso: [ValidarSesion(Roles = "ADMINISTRADOR,GERENTE")]
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class ValidarSesionAttribute : ActionFilterAttribute
    {
        /// <summary>Roles permitidos separados por coma. Vacío = cualquier usuario con sesión.</summary>
        public string Roles { get; set; }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            // Un [ValidarSesion] en la acción reemplaza al del controlador.
            if (filterContext.ActionDescriptor.IsDefined(typeof(ValidarSesionAttribute), true)
                && !filterContext.ActionDescriptor.GetCustomAttributes(typeof(ValidarSesionAttribute), true).Contains(this))
                return;

            User_activo usuario = filterContext.HttpContext.Session["Usuario"] as User_activo;
            bool esAjax = filterContext.HttpContext.Request.IsAjaxRequest()
                          || (filterContext.HttpContext.Request.ContentType ?? "").Contains("application/json");

            if (usuario == null || usuario.id_user_activo == 0)
            {
                filterContext.Result = esAjax
                    ? Denegar(filterContext, 401, "Su sesión expiró. Inicie sesión nuevamente.")
                    : new RedirectResult(new UrlHelper(filterContext.RequestContext).Action("Index", "Acceso"));
                return;
            }

            if (!string.IsNullOrWhiteSpace(Roles))
            {
                string rol = usuario.oRol != null ? usuario.oRol.nombre : null;
                bool permitido = Roles.Split(',').Select(r => r.Trim())
                    .Any(r => string.Equals(r, rol, StringComparison.OrdinalIgnoreCase));
                if (!permitido)
                {
                    filterContext.Result = esAjax
                        ? Denegar(filterContext, 403, "No tiene permisos para realizar esta acción.")
                        : new RedirectResult(new UrlHelper(filterContext.RequestContext).Action("AccesoDenegado", "Home"));
                }
            }
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
}
