using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Services.Description;
using CapaEntidad;
using CapaNegocio;
using OfficeOpenXml; 
using OfficeOpenXml.Style; 
using System.Drawing; 
using System.IO;

using CapaPresentacionAdmin.ViewModels;
using CapaPresentacionAdmin.Filters;

namespace CapaPresentacionAdmin.Controllers
{
    //[Authorize]
    [ValidarSesion]
    public class HomeController : Controller
    {
        #region Dashboard
        //Dashboard
        public ActionResult Index()
        {
            return View();
        }
        //DashboardResumen
        [HttpGet]
        public JsonResult VistaDashBoard()
        {
            CN_Reporte objCnReporte = new CN_Reporte();

            DashboardResumen oResumen = objCnReporte.ObtenerDatosDashboardResumen();

            return Json(new { resultado = oResumen }, JsonRequestBehavior.AllowGet);
        }



        //HistorialVentas para el DASHBOARD
        [HttpGet]
        public JsonResult ObtenerHistorialVentas(string fechaInicio, string fechaFin, string idVenta)
        {
            List<ReporteVentaDetalle> lista = new List<ReporteVentaDetalle>();
            CN_Reporte objCnReporte = new CN_Reporte();

            lista = objCnReporte.ObtenerHistorialVentas(fechaInicio, fechaFin, idVenta);

            return Json(new { data = lista }, JsonRequestBehavior.AllowGet);
        }

        #endregion



        #region USUARIO
        //USUARIO
        //public ActionResult Usuarios()
        //{
        //    //validar que id_user_activo no sea null osea que el usuario haya iniciado sesion para poder entrar aqui
        //    if (Session["Usuario"] == null)
        //    {
        //        return RedirectToAction("Index", "Acceso");
        //    }

        //    else
        //    {
        //        //validar que el usuario tenga un rol 
        //        User_activo oUserActivo = (User_activo)Session["Usuario"];
        //        if (oUserActivo.id_user_activo == 0)
        //        {
        //            return RedirectToAction("Index", "Acceso");
        //        }
        //    }
        //    //validar que el usuario tenga un rol asignado asignado como ADMINISTRADOR
        //    return View();
        //}
        [ValidarSesion(Permisos = "ADMIN_USUARIOS")]
        public ActionResult Usuarios()
        {


            return View();
        }

        [ValidarSesion(Permisos = "ADMIN_USUARIOS")]
        [HttpGet]
        public JsonResult ListarUsuarios()
        {
            List<Usuario> olista = new List<Usuario>();
            olista = new CN_Usuarios().Listar();
            // Nunca se envía el hash de la contraseña al navegador.
            var resultado = olista.Select(u => new
            {
                u.id_usuario, u.nombres, u.apellidos, u.correo, u.telefono, u.estado, u.id_rol, u.rol, u.id_tienda, u.tienda
            }).ToList();
            return Json(new { data = resultado }, JsonRequestBehavior.AllowGet);

        }
        [ValidarSesion(Permisos = "ADMIN_USUARIOS")]
        [HttpPost]
        public JsonResult GuardarUsuario(Usuario objeto)
        {
            object resultado;
            string Mensaje = string.Empty;

            // Solo un administrador puede crear administradores o modificar a uno.
            if (!PuedeGestionarUsuario(objeto.id_usuario, objeto.id_rol, out Mensaje))
                return Json(new { resultado = 0, mensaje = Mensaje }, JsonRequestBehavior.AllowGet);

            if(objeto.id_usuario == 0)
            {
                resultado = new CN_Usuarios().Registrar(objeto, out Mensaje);
            }
            else
            {
                resultado = new CN_Usuarios().Editar(objeto, out Mensaje);
            }
            CN_Autorizacion.Invalidar();   // un cambio de rol o de estado se aplica de inmediato
            return Json(new { resultado = resultado, mensaje = Mensaje }, JsonRequestBehavior.AllowGet);

        }


        /// <summary>
        /// Evita la escalada de privilegios: quien administra usuarios sin ser administrador no puede asignar el rol
        /// ADMINISTRADOR ni modificar, eliminar o cambiar la contraseña de un administrador.
        /// </summary>
        private bool PuedeGestionarUsuario(int idUsuario, int idRolNuevo, out string Mensaje)
        {
            Mensaje = string.Empty;
            if (CN_Autorizacion.EsSuperusuario(Session["Usuario"] as User_activo))
                return true;

            bool destinoEsAdmin = false;
            if (idUsuario > 0)
            {
                Usuario actual = new CN_Usuarios().Listar().FirstOrDefault(u => u.id_usuario == idUsuario);
                destinoEsAdmin = actual != null && CN_Autorizacion.EsNombreReservado(actual.rol);
            }
            if (idRolNuevo > 0)
            {
                Roles rolNuevo = new CN_Roles().ObtenerPorId(idRolNuevo);
                destinoEsAdmin |= rolNuevo != null && CN_Autorizacion.EsNombreReservado(rolNuevo.nombre);
            }
            if (destinoEsAdmin)
            {
                Mensaje = "Solo un administrador puede gestionar cuentas de administrador.";
                return false;
            }
            return true;
        }

        [ValidarSesion(Permisos = "ADMIN_USUARIOS")]
        [HttpPost]
        public JsonResult EliminarUsuario(int id)
        {
            bool respuesta = false;
            string Mensaje = string.Empty;

            if (!PuedeGestionarUsuario(id, 0, out Mensaje))
                return Json(new { resultado = false, mensaje = Mensaje }, JsonRequestBehavior.AllowGet);
            User_activo yo = Session["Usuario"] as User_activo;
            if (yo != null && yo.id_user_activo == id)
                return Json(new { resultado = false, mensaje = "No puede eliminar su propia cuenta." }, JsonRequestBehavior.AllowGet);

            respuesta = new CN_Usuarios().Eliminar(id, out Mensaje);
            CN_Autorizacion.Invalidar();

            return Json(new { resultado = respuesta, mensaje = Mensaje }, JsonRequestBehavior.AllowGet);
        }

        //Actualizar contraseña
        [ValidarSesion(Permisos = "ADMIN_USUARIOS")]
        [HttpPost]
        public JsonResult ActualizarContraseña(Usuario objeto)
        {
            bool resultado = false;
            string Mensaje = string.Empty;
            if (!PuedeGestionarUsuario(objeto.id_usuario, 0, out Mensaje))
                return Json(new { resultado = false, mensaje = Mensaje }, JsonRequestBehavior.AllowGet);
            resultado = new CN_Usuarios().ActualizarContraseñaUsuario(objeto.id_usuario, objeto.correo, objeto.contraseña, out Mensaje);
            return Json(new { resultado = resultado, mensaje = Mensaje }, JsonRequestBehavior.AllowGet);
        }
        #endregion



        #region ROL
        //ROLES
        [ValidarSesion(Permisos = "ADMIN_ROLES")]
        public ActionResult Roles()
        {
            return View();
        }

        [ValidarSesion(Permisos = "ADMIN_ROLES,ADMIN_USUARIOS")]
        [HttpGet]
        public JsonResult ListarRoles()
        {
            List<Roles> olista = new List<Roles>();
            olista = new CN_Roles().Listar();
            return Json(new { data = olista }, JsonRequestBehavior.AllowGet);

        }

        [ValidarSesion(Permisos = "ADMIN_ROLES")]
        [HttpPost]
        public JsonResult GuardarRol(Roles objeto)
        {
            object resultado;
            string Mensaje = string.Empty;

            if (objeto.id_rol == 0)
            {
                resultado = new CN_Roles().Registrar(objeto, out Mensaje);
            }
            else
            {
                resultado = new CN_Roles().Editar(objeto, out Mensaje);
            }
            return Json(new { resultado = resultado, mensaje = Mensaje }, JsonRequestBehavior.AllowGet);

        }

        [ValidarSesion(Permisos = "ADMIN_ROLES")]
        [HttpPost]
        public JsonResult EliminarRol(int id)
        {
            bool respuesta = false;
            string Mensaje = string.Empty;
            respuesta = new CN_Roles().Eliminar(id, out Mensaje);
            return Json(new { resultado = respuesta, mensaje = Mensaje }, JsonRequestBehavior.AllowGet);
        }
        #endregion



        #region PERMISOS
        //PERMISOS
        [ValidarSesion(Permisos = "ADMIN_ROLES")]
        public ActionResult Permisos()
        {


            return View();
        }
        [ValidarSesion(Permisos = "ADMIN_ROLES")]
        [HttpGet]
        public JsonResult ListarPermisos()
        {
            List<Permisos> olista = new List<Permisos>();
            olista = new CN_Permisos().Listar(); 
            return Json(new { data = olista }, JsonRequestBehavior.AllowGet);
        }

        [ValidarSesion(Permisos = "ADMIN_ROLES")]
        [HttpPost]
        public JsonResult GuardarPermiso(Permisos objeto)
        {
            object resultado; 
            string Mensaje = string.Empty; 

            if (objeto.id_permiso == 0)
            {
                resultado = new CN_Permisos().Registrar(objeto, out Mensaje);
            }
            else 
            {
                resultado = new CN_Permisos().Editar(objeto, out Mensaje);
            }

            return Json(new { resultado = resultado, mensaje = Mensaje }, JsonRequestBehavior.AllowGet);
        }

        [ValidarSesion(Permisos = "ADMIN_ROLES")]
        [HttpPost]
        public JsonResult EliminarPermiso(int id)
        {
            bool respuesta = false; 
            string Mensaje = string.Empty;

            respuesta = new CN_Permisos().Eliminar(id, out Mensaje); 

            return Json(new { resultado = respuesta, mensaje = Mensaje }, JsonRequestBehavior.AllowGet);
        }

        #endregion



        [ValidarSesion(Permisos = "ADMIN_ROLES")]
        public ActionResult RolesPorPermisos()
        {
            return View(); 
        }

        [ValidarSesion(Permisos = "ADMIN_ROLES")]
        [HttpGet]
        public JsonResult ListarRolesPermisos()
        {
            List<Roles> olista = new List<Roles>();
            olista = new CN_Roles().Listar(); 

            return Json(new { data = olista }, JsonRequestBehavior.AllowGet);
        }


        [ValidarSesion(Permisos = "ADMIN_ROLES")]
        [HttpGet]
        public JsonResult ObtenerPermisosPorRol(int idRol)
        {
            CN_Permisos cnPermisos = new CN_Permisos();
            CN_RolesPermisos cnRolesPermisos = new CN_RolesPermisos();

            List<Permisos> todosPermisos = cnPermisos.Listar();

            List<int> idsPermisosAsignados = cnRolesPermisos.ObtenerPermisosPorRol(idRol);

            List<PermisoAsignacionViewModel> listaPermisosAsignacion = new List<PermisoAsignacionViewModel>();

            foreach (var permiso in todosPermisos)
            {
                listaPermisosAsignacion.Add(new PermisoAsignacionViewModel
                {
                    IdPermiso = permiso.id_permiso, 
                    Nombre = permiso.nombre,         
                    Descripcion = permiso.descripcion, 
                    Asignado = idsPermisosAsignados.Contains(permiso.id_permiso) 
                });
            }

            return Json(new { listaPermisos = listaPermisosAsignacion }, JsonRequestBehavior.AllowGet);
        }

        // Método AJAX para guardar las asignaciones de permisos a un rol
        [ValidarSesion(Permisos = "ADMIN_ROLES")]
        [HttpPost]
        public JsonResult GuardarPermisosRol(int idRol, List<int> idsPermisos)
        {
            string mensaje = string.Empty;
            bool resultado = false;

            CN_RolesPermisos cnRolesPermisos = new CN_RolesPermisos();

            resultado = cnRolesPermisos.AsignarPermisosARol(idRol, idsPermisos, out mensaje);

            return Json(new { resultado = resultado, mensaje = mensaje }, JsonRequestBehavior.AllowGet);
        }

        #region PrivacyPolicy
        //PrivacyPolicy
        public ActionResult PrivacyPolicy()
        {
            return View();
        }
        #endregion



        #region TermsAndConditions
        // TermsAndConditions
        public ActionResult TermsAndConditions()
        {
            return View();
        }
        #endregion

        #region Acceso Denegado
        public ActionResult AccesoDenegado()
        {
            return View();
        }
        #endregion
    }
}