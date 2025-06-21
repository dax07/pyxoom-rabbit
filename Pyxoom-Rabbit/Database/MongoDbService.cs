using MongoDB.Driver;

namespace Pyxoom_Rabbit.Database
{

    public class MongoDbService
    {
        private readonly IMongoDatabase _database;

        public MongoDbService(string connectionString, string databaseName)
        {
            var client = new MongoClient(connectionString);
            _database = client.GetDatabase(databaseName);
        }

        public void MostrarColeccion(string nombreColeccion)
        {
            var collection = _database.GetCollection<dynamic>(nombreColeccion);
            var documentos = collection.Find(FilterDefinition<dynamic>.Empty).ToList();
            foreach (var doc in documentos)
            {
                Console.WriteLine(doc);
            }
        }
    }

}
