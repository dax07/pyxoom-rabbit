using Microsoft.Extensions.Configuration;

namespace Pyxoom_Rabbit.Database
{
    public class DbManager
    {
        public SqlDbService SqlService { get; }
        public MongoDbService MongoService { get; }

        public DbManager(IConfiguration config)
        {
            var sqlConn = config.GetConnectionString("SqlServer");
            var mongoConn = config.GetConnectionString("MongoDb");
            var mongoDbName = config.GetSection("MongoDb:Database").Value;

            SqlService = new SqlDbService(sqlConn);
            MongoService = new MongoDbService(mongoConn, mongoDbName);
        }
    }

}
