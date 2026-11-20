using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;

using CapaEntidad;
using CapaNegocio;
using CapaPresentacionAdmin.Filters;

namespace CapaPresentacionAdmin.Controllers
{
    public class AccesoController : Controller
    {
        // GET: Acceso
        
        
        //Login
        public ActionResult Index()
        {
            return View();
        }
        
        [HttpPost]
        [PermitirPostSinAjax]
        [ValidateAntiForgeryToken]
        public ActionResult Index(string correo, string contraseña)
        {

            correo = (correo ?? "").Trim();
            string claveBloqueo = "correo:" + correo;
            string claveOrigen = "ip:" + Request.UserHostAddress;
            int minutos;
            if (CN_Seguridad.EstaBloqueado(claveBloqueo, out minutos) || CN_Seguridad.EstaBloqueado(claveOrigen, out minutos))
            {
                ViewBag.Error = "Demasiados intentos fallidos. Intente nuevamente en " + minutos + " minuto(s).";
                return View();
            }

            // La contraseña se verifica en la capa de negocio (PBKDF2; los hashes antiguos se migran al iniciar sesión).
            User_activo oUserActivo = new CN_Usuarios().ValidarUsuario(correo, contraseña);

            
            if (oUserActivo == null)
            {
                CN_Seguridad.RegistrarFallo(claveBloqueo);
                CN_Seguridad.RegistrarFallo(claveOrigen);
                ViewBag.Error = "Correo o contraseña incorrectos, o el usuario se encuentra inactivo. Por favor, verifique sus datos e intente nuevamente.";
                return View();
            }
            else
            {
                
                //validar que id_user_activo no sea null
                if (oUserActivo.id_user_activo == 0 && oUserActivo.id_tienda_user == 0)
                {
                    ViewBag.Error = "El usuario no tiene permisos para acceder al sistema.";
                    return View();
                }
                else
                {
                    CN_Seguridad.RegistrarExito(claveBloqueo);
                    CN_Seguridad.RegistrarExito(claveOrigen);
                    Session["Usuario"] = oUserActivo;
                    //Session["Usuario"] = oUsuario; guarda el objeto en la session para poder usarlo en otras vistas
                    //juntarlo con el apellido del usuario
                    Session["NombreUsuario"] = oUserActivo.nombre + " " + oUserActivo.apellidos;
                    Session["Email"] = oUserActivo.correo;
                    Session["NombreRol"] = oUserActivo.oRol != null ? oUserActivo.oRol.nombre : "Rol Desconocido";


                    //FormsAuthentication.SetAuthCookie(oUserActivo.correo, false);
                    return RedirectToAction("Index", "Home");
                }

            }
        }
        
        public ActionResult CerrarSesion()
        {
            //cerrar  session
            Session["Usuario"] = null;
            Session.Abandon();
            Session.Clear();
            FormsAuthentication.SignOut();
            //vista de login
            return RedirectToAction("Index", "Acceso");
            //return View();
        }

        [ValidarSesion]
        public ActionResult CambiarContraseña()
        {
            return View();
        }

        [HttpPost]
        [ValidarSesion]
        [PermitirPostSinAjax]
        [ValidateAntiForgeryToken]
        public ActionResult CambiarContraseña(string correo, string contraseñaActual, string nuevaContraseña, string confirmarContraseña)
        {
            string mensaje = string.Empty;

            // Solo se puede cambiar la contraseña de la cuenta con la sesión iniciada (se ignora el correo enviado).
            correo = ((User_activo)Session["Usuario"]).correo;

            //validaciones
            if (string.IsNullOrWhiteSpace(correo) ||
                string.IsNullOrWhiteSpace(contraseñaActual) ||
                string.IsNullOrWhiteSpace(nuevaContraseña) ||
                string.IsNullOrWhiteSpace(confirmarContraseña))
            {
                ViewBag.Mensaje = "Todos los campos son obligatorios.";
                return View();
            }

            if (nuevaContraseña != confirmarContraseña)
            {
                ViewBag.Mensaje = "La nueva contraseña y su confirmación no coinciden.";
                return View();
            }

            bool resultado = new CN_Usuarios().CambiarContraseña(correo, contraseñaActual, nuevaContraseña, out mensaje);

            if (resultado)
            {
                ViewBag.MensajeExito = "Contraseña actualizada correctamente. Revise su correo.";
                return RedirectToAction("Index", "Acceso");
            }
            else
            {
                ViewBag.Mensaje = mensaje;  // mensaje puede venir de la capa de negocio
            }

            return View();
        }







        
    }
}