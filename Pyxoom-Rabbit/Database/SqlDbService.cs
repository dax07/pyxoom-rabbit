using Microsoft.Data.SqlClient;

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
    }

}
