using System;
using System.Web.Mvc;

namespace CapaPresentacionAdmin.Filters
{
    /// <summary>
    /// Protección contra CSRF para las acciones POST que se llaman por AJAX: exige la cabecera
    /// "X-Requested-With: XMLHttpRequest" (jQuery la agrega en peticiones al mismo sitio). Un sitio externo no
    /// puede agregar esa cabecera a una petición hacia este dominio sin autorización CORS del servidor.
    /// Los formularios normales (inicio de sesión y cambio de contraseña) usan [ValidateAntiForgeryToken]
    /// y se marcan con [PermitirPostSinAjax].
    /// </summary>
    public class ExigirAjaxEnPostAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var request = filterContext.HttpContext.Request;
            if (!string.Equals(request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
                return;
            if (filterContext.ActionDescriptor.IsDefined(typeof(PermitirPostSinAjaxAttribute), true)
                || filterContext.ActionDescriptor.ControllerDescriptor.IsDefined(typeof(PermitirPostSinAjaxAttribute), true))
                return;
            if (request.IsAjaxRequest())
                return;

            filterContext.HttpContext.Response.StatusCode = 403;
            filterContext.HttpContext.Response.TrySkipIisCustomErrors = true;
            filterContext.Result = new JsonResult
            {
                Data = new { resultado = false, mensaje = "Petición rechazada por seguridad." },
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }
    }

    /// <summary>Excluye una acción o controlador del filtro <see cref="ExigirAjaxEnPostAttribute"/> (usar con token antifalsificación).</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class PermitirPostSinAjaxAttribute : Attribute
    {
    }
}
