using Microsoft.Data.SqlClient;
using System.Data;

namespace Pyxoom_Rabbit.Database
{
    public class SqlDbService
    {
        private readonly string _connectionString;

        public SqlDbService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public void EjecutarConsulta()
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            var command = new SqlCommand("SELECT * FROM Facturas", connection);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                Console.WriteLine(reader[0]); // ejemplo
            }
        }

        public void EjecutarSP_LeerVariables(string folio)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("SP_TRAER_TABLERO_POR_FOLIO", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@folio", folio);

            try
            {
                conn.Open();
                using var reader = cmd.ExecuteReader();

                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        int? id = DbUtils.GetNullableInt(reader, "Id");
                        string correo = DbUtils.GetNullableString(reader, "Correo");
                        string cliente = DbUtils.GetNullableString(reader, "Cliente");
                        string estatus = DbUtils.GetNullableString(reader, "Estatus");

                        Console.WriteLine($"ID: {id}, Correo: {correo}, Cliente: {cliente}, Estatus: {estatus}");
                    }
                }
                else
                {
                    Console.WriteLine("No se encontraron resultados.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al ejecutar SP: " + ex.Message);
            }
        }

    }

}
