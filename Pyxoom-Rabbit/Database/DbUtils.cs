using System.Data;

namespace Pyxoom_Rabbit.Database
{
    public static class DbUtils
    {
        public static int? GetNullableInt(IDataReader reader, string columnName)
        {
            return reader[columnName] == DBNull.Value ? (int?)null : Convert.ToInt32(reader[columnName]);
        }

        public static string GetNullableString(IDataReader reader, string columnName)
        {
            return reader[columnName] == DBNull.Value ? null : reader[columnName].ToString();
        }

        public static DateTime? GetNullableDateTime(IDataReader reader, string columnName)
        {
            return reader[columnName] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader[columnName]);
        }

        public static bool? GetNullableBool(IDataReader reader, string columnName)
        {
            return reader[columnName] == DBNull.Value ? (bool?)null : Convert.ToBoolean(reader[columnName]);
        }

        public static decimal? GetNullableDecimal(IDataReader reader, string columnName)
        {
            return reader[columnName] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(reader[columnName]);
        }
    }
}



