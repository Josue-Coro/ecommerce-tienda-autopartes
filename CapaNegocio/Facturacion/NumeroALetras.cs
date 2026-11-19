using System;
using System.Globalization;

namespace CapaNegocio.Facturacion
{
    /// <summary>Convierte montos a literal para la representación gráfica (ej.: "CIENTO VEINTE 50/100 BOLIVIANOS").</summary>
    public static class NumeroALetras
    {
        private static readonly string[] Unidades =
        {
            "", "UNO", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE", "DIEZ",
            "ONCE", "DOCE", "TRECE", "CATORCE", "QUINCE", "DIECISÉIS", "DIECISIETE", "DIECIOCHO", "DIECINUEVE", "VEINTE",
            "VEINTIUNO", "VEINTIDÓS", "VEINTITRÉS", "VEINTICUATRO", "VEINTICINCO", "VEINTISÉIS", "VEINTISIETE",
            "VEINTIOCHO", "VEINTINUEVE"
        };

        private static readonly string[] Decenas =
            { "", "", "", "TREINTA", "CUARENTA", "CINCUENTA", "SESENTA", "SETENTA", "OCHENTA", "NOVENTA" };

        private static readonly string[] Centenas =
            { "", "CIENTO", "DOSCIENTOS", "TRESCIENTOS", "CUATROCIENTOS", "QUINIENTOS", "SEISCIENTOS",
              "SETECIENTOS", "OCHOCIENTOS", "NOVECIENTOS" };

        public static string Convertir(decimal monto, string moneda = "BOLIVIANOS")
        {
            decimal redondeado = Math.Round(Math.Abs(monto), 2, MidpointRounding.AwayFromZero);
            long entero = (long)Math.Floor(redondeado);
            int centavos = (int)((redondeado - entero) * 100);

            string letras = entero == 0 ? "CERO" : EnLetras(entero);
            return letras + " " + centavos.ToString("00", CultureInfo.InvariantCulture) + "/100 " + moneda;
        }

        private static string EnLetras(long numero)
        {
            if (numero >= 1000000000000L)
                return numero.ToString(CultureInfo.InvariantCulture);

            if (numero >= 1000000)
            {
                long millones = numero / 1000000;
                long resto = numero % 1000000;
                string texto = millones == 1 ? "UN MILLÓN" : Apocopar(EnLetras(millones)) + " MILLONES";
                return resto == 0 ? texto : texto + " " + EnLetras(resto);
            }

            if (numero >= 1000)
            {
                long miles = numero / 1000;
                long resto = numero % 1000;
                string texto = miles == 1 ? "MIL" : Apocopar(EnLetras(miles)) + " MIL";
                return resto == 0 ? texto : texto + " " + EnLetras(resto);
            }

            if (numero == 100)
                return "CIEN";

            if (numero > 100)
            {
                long resto = numero % 100;
                string texto = Centenas[numero / 100];
                return resto == 0 ? texto : texto + " " + EnLetras(resto);
            }

            if (numero < 30)
                return Unidades[numero];

            long unidad = numero % 10;
            return unidad == 0 ? Decenas[numero / 10] : Decenas[numero / 10] + " Y " + Unidades[unidad];
        }

        /// <summary>"UNO" pasa a "UN" delante de MIL / MILLONES (ej.: "VEINTIÚN MIL").</summary>
        private static string Apocopar(string texto)
        {
            if (texto.EndsWith("VEINTIUNO"))
                return texto.Substring(0, texto.Length - "VEINTIUNO".Length) + "VEINTIÚN";
            if (texto.EndsWith("UNO"))
                return texto.Substring(0, texto.Length - 1);
            return texto;
        }
    }
}
