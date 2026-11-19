using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace CapaNegocio.Facturacion
{
    /// <summary>
    /// Algoritmos publicados en el Anexo Técnico del SIN (sección "Algoritmos Utilizados"):
    /// Módulo 11, Base 16, generación del CUF, compresión GZip, SHA-256 y redondeo HALF-UP.
    /// </summary>
    public static class AlgoritmosSiat
    {
        /// <summary>
        /// Dígito autoverificador Módulo 11 (versión del SIN: numDig = 1, limMult = 9, x10 = false).
        /// </summary>
        public static string Modulo11(string cadena)
        {
            int suma = 0;
            int multiplicador = 2;
            for (int i = cadena.Length - 1; i >= 0; i--)
            {
                suma += multiplicador * (cadena[i] - '0');
                if (++multiplicador > 9)
                    multiplicador = 2;
            }

            int digito = suma % 11;
            if (digito == 10) return "1";
            if (digito == 11) return "0";
            return digito.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Convierte una cadena numérica (base 10) a hexadecimal en mayúsculas.</summary>
        public static string Base16(string cadenaNumerica)
        {
            BigInteger valor = BigInteger.Parse(cadenaNumerica, CultureInfo.InvariantCulture);
            // BigInteger antepone un "0" cuando el primer dígito hexadecimal es >= 8 (bit de signo);
            // el SIN espera la representación sin ese cero.
            string hex = valor.ToString("X");
            return hex.Length > 1 ? hex.TrimStart('0') : hex;
        }

        /// <summary>Convierte una cadena hexadecimal a su representación en base 10.</summary>
        public static string Base10(string cadenaHex)
        {
            BigInteger valor = BigInteger.Parse("0" + cadenaHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return valor.ToString(CultureInfo.InvariantCulture);
        }

        public static string CompletarCeros(long valor, int longitud)
        {
            return valor.ToString(CultureInfo.InvariantCulture).PadLeft(longitud, '0');
        }

        /// <summary>
        /// Genera el Código Único de Factura (CUF).
        /// Cadena de 53 dígitos + Módulo 11 → Base 16 → concatenar el código de control del CUFD.
        /// </summary>
        public static string GenerarCuf(long nit, DateTime fechaEmision, int codigoSucursal, int codigoModalidad,
                                        int tipoEmision, int tipoFactura, int codigoDocumentoSector,
                                        long numeroFactura, int codigoPuntoVenta, string codigoControlCufd)
        {
            string cadena = ArmarCadenaCuf(nit, fechaEmision, codigoSucursal, codigoModalidad, tipoEmision,
                                           tipoFactura, codigoDocumentoSector, numeroFactura, codigoPuntoVenta);
            string conVerificador = cadena + Modulo11(cadena);
            return Base16(conVerificador) + codigoControlCufd;
        }

        /// <summary>Arma la cadena de 53 dígitos previa al dígito verificador.</summary>
        public static string ArmarCadenaCuf(long nit, DateTime fechaEmision, int codigoSucursal, int codigoModalidad,
                                            int tipoEmision, int tipoFactura, int codigoDocumentoSector,
                                            long numeroFactura, int codigoPuntoVenta)
        {
            return CompletarCeros(nit, 13)
                 + fechaEmision.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture)
                 + CompletarCeros(codigoSucursal, 4)
                 + CompletarCeros(codigoModalidad, 1)
                 + CompletarCeros(tipoEmision, 1)
                 + CompletarCeros(tipoFactura, 1)
                 + CompletarCeros(codigoDocumentoSector, 2)
                 + CompletarCeros(numeroFactura, 10)
                 + CompletarCeros(codigoPuntoVenta, 4);
        }

        public static byte[] ComprimirGzip(string texto)
        {
            byte[] datos = new UTF8Encoding(false).GetBytes(texto);
            using (MemoryStream salida = new MemoryStream())
            {
                using (GZipStream gzip = new GZipStream(salida, CompressionMode.Compress))
                {
                    gzip.Write(datos, 0, datos.Length);
                }
                return salida.ToArray();
            }
        }

        public static string DescomprimirGzip(byte[] archivo)
        {
            using (MemoryStream entrada = new MemoryStream(archivo))
            using (GZipStream gzip = new GZipStream(entrada, CompressionMode.Decompress))
            using (StreamReader lector = new StreamReader(gzip, Encoding.UTF8))
            {
                return lector.ReadToEnd();
            }
        }

        /// <summary>SHA-256 en hexadecimal (minúsculas) del arreglo de bytes.</summary>
        public static string Sha256(byte[] datos)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(datos);
                StringBuilder sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                    sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        /// <summary>Redondeo a dos decimales HALF-UP, como lo exige el SIN.</summary>
        public static decimal Redondear(decimal valor)
        {
            return Math.Round(valor, 2, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Enmascara un número de tarjeta: primeros y últimos 4 dígitos visibles y ceros al medio
        /// (ej.: 4797000000007896). Devuelve null si el número no es válido.
        /// </summary>
        public static string EnmascararTarjeta(string numeroTarjeta)
        {
            if (string.IsNullOrWhiteSpace(numeroTarjeta))
                return null;

            StringBuilder digitos = new StringBuilder();
            foreach (char c in numeroTarjeta)
                if (char.IsDigit(c)) digitos.Append(c);

            string solo = digitos.ToString();
            if (solo.Length < 8 || solo.Length > 16)
                return null;

            return solo.Substring(0, 4) + new string('0', solo.Length - 8) + solo.Substring(solo.Length - 4);
        }
    }
}
