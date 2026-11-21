using System.Web;
using System.Web.Mvc;
using CapaPresentacionAdmin.Filters;

namespace CapaPresentacionAdmin
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
            // Protección CSRF: las acciones POST deben llegar por AJAX (salvo las marcadas con [PermitirPostSinAjax]).
            filters.Add(new ExigirAjaxEnPostAttribute());
        }
    }
}
