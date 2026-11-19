using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CapaNegocio.Facturacion
{
    /// <summary>
    /// Contenedor TAR (formato ustar) mínimo. El SIN pide enviar los paquetes de facturas como
    /// "paquete.tar.gz": los XML dentro de un TAR, comprimido con GZip. .NET Framework 4.7.2 no
    /// incluye soporte para TAR, por eso se implementa aquí.
    /// </summary>
    public static class ArchivoTar
    {
        private const int Bloque = 512;

        public static byte[] Crear(IList<KeyValuePair<string, byte[]>> archivos)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                long fecha = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                foreach (KeyValuePair<string, byte[]> a in archivos)
                {
                    byte[] cabecera = new byte[Bloque];
                    Escribir(cabecera, 0, 100, a.Key);                        // nombre
                    Escribir(cabecera, 100, 8, "0000644");                     // permisos
                    Escribir(cabecera, 108, 8, "0000000");                     // uid
                    Escribir(cabecera, 116, 8, "0000000");                     // gid
                    Escribir(cabecera, 124, 12, Octal(a.Value.Length, 11));    // tamaño
                    Escribir(cabecera, 136, 12, Octal(fecha, 11));             // fecha de modificación
                    cabecera[156] = (byte)'0';                                 // archivo normal
                    Escribir(cabecera, 257, 6, "ustar");
                    Escribir(cabecera, 263, 2, "00");

                    // Suma de verificación: se calcula con el campo lleno de espacios.
                    for (int i = 148; i < 156; i++) cabecera[i] = (byte)' ';
                    long suma = 0;
                    foreach (byte b in cabecera) suma += b;
                    Escribir(cabecera, 148, 8, Octal(suma, 6));
                    cabecera[154] = 0;
                    cabecera[155] = (byte)' ';

                    ms.Write(cabecera, 0, Bloque);
                    ms.Write(a.Value, 0, a.Value.Length);
                    int relleno = (Bloque - a.Value.Length % Bloque) % Bloque;
                    ms.Write(new byte[relleno], 0, relleno);
                }
                ms.Write(new byte[Bloque * 2], 0, Bloque * 2);   // fin del archivo: dos bloques vacíos
                return ms.ToArray();
            }
        }

        public static List<KeyValuePair<string, byte[]>> Leer(byte[] tar)
        {
            List<KeyValuePair<string, byte[]>> archivos = new List<KeyValuePair<string, byte[]>>();
            int pos = 0;
            while (pos + Bloque <= tar.Length)
            {
                bool vacio = true;
                for (int i = pos; i < pos + Bloque; i++) if (tar[i] != 0) { vacio = false; break; }
                if (vacio) break;

                string nombre = Texto(tar, pos, 100);
                long tamano = Convert.ToInt64(Texto(tar, pos + 124, 12).Trim(), 8);
                char tipo = (char)tar[pos + 156];
                pos += Bloque;

                if (pos + tamano > tar.Length)
                    throw new InvalidDataException("El archivo TAR está truncado.");

                if (tipo == '0' || tipo == '\0')
                {
                    byte[] datos = new byte[tamano];
                    Buffer.BlockCopy(tar, pos, datos, 0, (int)tamano);
                    archivos.Add(new KeyValuePair<string, byte[]>(nombre, datos));
                }
                pos += (int)((tamano + Bloque - 1) / Bloque * Bloque);
            }
            return archivos;
        }

        private static void Escribir(byte[] destino, int inicio, int longitud, string valor)
        {
            byte[] b = Encoding.ASCII.GetBytes(valor);
            Buffer.BlockCopy(b, 0, destino, inicio, Math.Min(b.Length, longitud));
        }

        private static string Octal(long valor, int digitos)
        {
            return Convert.ToString(valor, 8).PadLeft(digitos, '0');
        }

        private static string Texto(byte[] origen, int inicio, int longitud)
        {
            int fin = inicio;
            while (fin < inicio + longitud && origen[fin] != 0) fin++;
            return Encoding.ASCII.GetString(origen, inicio, fin - inicio).Trim(' ');
        }

        public static string NombreXml(long numeroFactura)
        {
            return "factura_" + numeroFactura.ToString(CultureInfo.InvariantCulture) + ".xml";
        }
    }
}
