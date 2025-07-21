using Microsoft.Extensions.Configuration;

namespace Pyxoom_Rabbit.Database
{
    public class DbManager
    {
        public SqlDbService SqlService { get; }
        public MongoDbService MongoService { get; }

        public PyxoomDbService PyxoomService { get; }
        public PyxoomDbService PlataformaService { get; }

        public DbManager(IConfiguration config)
        {
            var sqlConn = config.GetConnectionString("SqlServer");
            var mongoConn = config.GetConnectionString("MongoDb");
            var mongoDbName = config.GetSection("MongoDb:Database").Value;
            var pyxoomConn = config.GetConnectionString("Pyxoom42");
            var plataformaConn = config.GetConnectionString("Plataforma42");

            SqlService = new SqlDbService(sqlConn);
            MongoService = new MongoDbService(mongoConn, mongoDbName);
            PyxoomService = new PyxoomDbService(pyxoomConn);
            PlataformaService = new PyxoomDbService(plataformaConn);
        }
    }

}
