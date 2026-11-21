using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CapaNegocio
{
    /// <summary>
    /// Utilidades de seguridad: hash de contraseñas con PBKDF2 (sal aleatoria por usuario), verificación de los
    /// hashes SHA-256 antiguos (para migrarlos al iniciar sesión), política de contraseñas y bloqueo temporal
    /// por intentos fallidos de inicio de sesión.
    /// </summary>
    public static class CN_Seguridad
    {
        private const string Prefijo = "PBKDF2";
        private const int Iteraciones = 100000;
        private const int BytesSal = 16;
        private const int BytesHash = 32;

        public const int LongitudMinima = 8;
        public const int IntentosMaximos = 5;
        public static readonly TimeSpan TiempoBloqueo = TimeSpan.FromMinutes(15);

        // ------------------------------------------------------------------ contraseñas

        /// <summary>Formato: PBKDF2$iteraciones$salBase64$hashBase64 (cabe en la columna VARCHAR(150) existente).</summary>
        public static string HashContraseña(string contraseña)
        {
            byte[] sal = new byte[BytesSal];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                rng.GetBytes(sal);
            byte[] hash = Derivar(contraseña, sal, Iteraciones);
            return Prefijo + "$" + Iteraciones.ToString(CultureInfo.InvariantCulture) + "$" + Convert.ToBase64String(sal) + "$" + Convert.ToBase64String(hash);
        }

        /// <summary>
        /// Verifica la contraseña contra el hash guardado. Acepta el formato nuevo (PBKDF2) y el antiguo (SHA-256 en
        /// hexadecimal, sin sal); en ese caso <paramref name="requiereMigracion"/> indica que conviene guardar el hash nuevo.
        /// </summary>
        public static bool Verificar(string contraseña, string hashGuardado, out bool requiereMigracion)
        {
            requiereMigracion = false;
            if (string.IsNullOrEmpty(contraseña) || string.IsNullOrEmpty(hashGuardado))
                return false;

            if (hashGuardado.StartsWith(Prefijo + "$", StringComparison.Ordinal))
            {
                string[] partes = hashGuardado.Split('$');
                int iteraciones;
                if (partes.Length != 4 || !int.TryParse(partes[1], NumberStyles.None, CultureInfo.InvariantCulture, out iteraciones))
                    return false;
                byte[] sal, esperado;
                try
                {
                    sal = Convert.FromBase64String(partes[2]);
                    esperado = Convert.FromBase64String(partes[3]);
                }
                catch (FormatException)
                {
                    return false;
                }
                return IgualesTiempoConstante(Derivar(contraseña, sal, iteraciones, esperado.Length), esperado);
            }

            // Formato antiguo: SHA-256 en hexadecimal (64 caracteres).
            if (hashGuardado.Length == 64)
            {
                bool ok = IgualesTiempoConstante(Encoding.ASCII.GetBytes(CN_Recursos.ConvertirSha256(contraseña)),
                                                 Encoding.ASCII.GetBytes(hashGuardado.ToLowerInvariant()));
                requiereMigracion = ok;
                return ok;
            }
            return false;
        }

        /// <summary>Política mínima: 8 caracteres, al menos una letra y un número.</summary>
        public static bool CumplePolitica(string contraseña, out string Mensaje)
        {
            Mensaje = string.Empty;
            if (string.IsNullOrEmpty(contraseña) || contraseña.Length < LongitudMinima)
                Mensaje = "La contraseña debe tener al menos " + LongitudMinima + " caracteres.";
            else if (!contraseña.Any(char.IsLetter) || !contraseña.Any(char.IsDigit))
                Mensaje = "La contraseña debe combinar letras y números.";
            else if (contraseña.Length > 100)
                Mensaje = "La contraseña admite como máximo 100 caracteres.";
            return string.IsNullOrEmpty(Mensaje);
        }

        /// <summary>Contraseña temporal aleatoria (12 caracteres) que cumple la política.</summary>
        public static string GenerarContraseñaTemporal()
        {
            const string letras = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ";
            const string digitos = "23456789";
            byte[] b = new byte[12];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                rng.GetBytes(b);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < b.Length; i++)
            {
                string fuente = i % 4 == 3 ? digitos : letras;
                sb.Append(fuente[b[i] % fuente.Length]);
            }
            return sb.ToString();
        }

        private static byte[] Derivar(string contraseña, byte[] sal, int iteraciones, int bytes = BytesHash)
        {
            using (Rfc2898DeriveBytes kdf = new Rfc2898DeriveBytes(contraseña, sal, iteraciones, HashAlgorithmName.SHA256))
                return kdf.GetBytes(bytes);
        }

        private static bool IgualesTiempoConstante(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int diferencia = 0;
            for (int i = 0; i < a.Length; i++)
                diferencia |= a[i] ^ b[i];
            return diferencia == 0;
        }

        // ------------------------------------------------------------------ bloqueo por intentos fallidos

        private class Intentos
        {
            public int Fallidos;
            public DateTime? BloqueadoHasta;
        }

        private static readonly ConcurrentDictionary<string, Intentos> _intentos = new ConcurrentDictionary<string, Intentos>(StringComparer.OrdinalIgnoreCase);

        /// <summary>true si la cuenta (o el origen) está bloqueada temporalmente; indica los minutos restantes.</summary>
        public static bool EstaBloqueado(string clave, out int minutosRestantes)
        {
            minutosRestantes = 0;
            Intentos i;
            if (clave == null || !_intentos.TryGetValue(clave, out i) || i.BloqueadoHasta == null)
                return false;
            if (i.BloqueadoHasta.Value <= DateTime.Now)
            {
                Intentos eliminado;
                _intentos.TryRemove(clave, out eliminado);
                return false;
            }
            minutosRestantes = (int)Math.Ceiling((i.BloqueadoHasta.Value - DateTime.Now).TotalMinutes);
            return true;
        }

        public static void RegistrarFallo(string clave)
        {
            if (clave == null) return;
            Intentos i = _intentos.GetOrAdd(clave, k => new Intentos());
            lock (i)
            {
                i.Fallidos++;
                if (i.Fallidos >= IntentosMaximos)
                    i.BloqueadoHasta = DateTime.Now.Add(TiempoBloqueo);
            }
        }

        public static void RegistrarExito(string clave)
        {
            Intentos eliminado;
            if (clave != null) _intentos.TryRemove(clave, out eliminado);
        }
    }
}
