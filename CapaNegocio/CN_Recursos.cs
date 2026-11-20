using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using System.Net.Mail;
using System.Net;
using System.IO;
using System.Configuration;

namespace CapaNegocio
{
    public class CN_Recursos
    {

        public static string GenerarClave()
        {
            string clave = Guid.NewGuid().ToString("N").Substring(0,6);
            return clave;
        }

        //ecriptacion de texto en SHA256

        public static string ConvertirSha256(string texto)
        {
            StringBuilder Sb = new StringBuilder();

            using (SHA256 hash = SHA256Managed.Create())
            {
                Encoding enc = Encoding.UTF8;
                byte[] result = hash.ComputeHash(enc.GetBytes(texto));

                foreach (byte b in result)
                    Sb.Append(b.ToString("x2"));
                
            }
            return Sb.ToString();
        }

        /// <summary>
        /// Envía un correo HTML. Las credenciales se leen de la configuración (Secretos.config):
        /// CorreoRemitente, CorreoClave, CorreoServidor y CorreoPuerto. Si no están configuradas, no envía y devuelve false.
        /// </summary>
        public static bool EnviarCorreo(string correo, string asunto, string mensaje)
        {
            string remitente = ConfigurationManager.AppSettings["CorreoRemitente"];
            string clave = ConfigurationManager.AppSettings["CorreoClave"];
            string servidor = ConfigurationManager.AppSettings["CorreoServidor"] ?? "smtp.gmail.com";
            int puerto;
            if (!int.TryParse(ConfigurationManager.AppSettings["CorreoPuerto"], out puerto))
                puerto = 587;

            if (string.IsNullOrWhiteSpace(remitente) || string.IsNullOrWhiteSpace(clave) || string.IsNullOrWhiteSpace(correo))
                return false;

            try
            {
                using (MailMessage mail = new MailMessage())
                using (SmtpClient smtp = new SmtpClient(servidor, puerto) { Credentials = new NetworkCredential(remitente, clave), EnableSsl = true })
                {
                    mail.To.Add(correo);
                    mail.From = new MailAddress(remitente);
                    mail.Subject = asunto;
                    mail.Body = mensaje;
                    mail.IsBodyHtml = true;
                    smtp.Send(mail);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string ConvertirBase64(string ruta, out bool conversion)
        {
            string textoBase64 = string.Empty;
            conversion = false;
            try
            {
                byte[] bytes = File.ReadAllBytes(ruta);
                textoBase64 = Convert.ToBase64String(bytes);
                conversion = true; // Solo marcar como true si la lectura fue exitosa
            }
            catch (Exception ex)
            {
                conversion = false;
                Console.WriteLine($"Error al convertir a Base64 para la ruta: {ruta} - Error: {ex.Message}");
                // También podrías loguear el error en un archivo o sistema de logging
            }
            return textoBase64;
        }
        //public static string ConvertirBase64(string ruta, out bool conversion)
        //{
        //    string textoBase64 = string.Empty;
        //    conversion = false;
        //    try
        //    {
        //        byte[] bytes = File.ReadAllBytes(ruta);
        //        textoBase64 = Convert.ToBase64String(bytes);

        //    }
        //    catch
        //    {
        //        conversion = false;

        //    }
        //    return textoBase64;
        //}



    }
}
