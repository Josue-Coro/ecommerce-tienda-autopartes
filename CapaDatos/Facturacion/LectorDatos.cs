using System;
using System.Data;

namespace CapaDatos.Facturacion
{
    /// <summary>Lectura segura de columnas que pueden venir nulas desde SQL Server.</summary>
    internal static class LectorDatos
    {
        public static string Texto(IDataRecord dr, string columna)
        {
            object valor = dr[columna];
            return valor == DBNull.Value ? null : valor.ToString();
        }

        public static int Entero(IDataRecord dr, string columna, int porDefecto = 0)
        {
            object valor = dr[columna];
            return valor == DBNull.Value ? porDefecto : Convert.ToInt32(valor);
        }

        public static int? EnteroNulo(IDataRecord dr, string columna)
        {
            object valor = dr[columna];
            return valor == DBNull.Value ? (int?)null : Convert.ToInt32(valor);
        }

        public static long EnteroLargo(IDataRecord dr, string columna)
        {
            object valor = dr[columna];
            return valor == DBNull.Value ? 0 : Convert.ToInt64(valor);
        }

        public static decimal Decimal(IDataRecord dr, string columna)
        {
            object valor = dr[columna];
            return valor == DBNull.Value ? 0m : Convert.ToDecimal(valor);
        }

        public static bool Booleano(IDataRecord dr, string columna)
        {
            object valor = dr[columna];
            return valor != DBNull.Value && Convert.ToBoolean(valor);
        }

        public static DateTime Fecha(IDataRecord dr, string columna)
        {
            object valor = dr[columna];
            return valor == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(valor);
        }

        public static DateTime? FechaNula(IDataRecord dr, string columna)
        {
            object valor = dr[columna];
            return valor == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(valor);
        }

        /// <summary>Convierte null o cadena vacía en DBNull para enviar como parámetro.</summary>
        public static object Nulo(string valor)
        {
            return string.IsNullOrWhiteSpace(valor) ? (object)DBNull.Value : valor;
        }
    }
}
