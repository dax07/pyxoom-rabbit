using Microsoft.Extensions.Configuration;
using Pyxoom_Rabbit;
using Pyxoom_Rabbit.Database;
using Serilog;
using System.Text.Json;

namespace PSW.Pyxoom.Analytix.Queue
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            var logFile = config["serilog:write-to:File.path"]!;
            var templateFile = config["serilog:write-to:File.outputTemplate"]!;

            //var logger = new LoggerConfiguration()
            //                .Enrich.FromLogContext()
            //                .MinimumLevel.Information()
            //                .WriteTo.Console(outputTemplate: templateFile)
            //                .WriteTo.File(logFile, rollingInterval: RollingInterval.Day)
            //                .CreateLogger();
            ////.ReadFrom.AppSettings().CreateLogger();
            //logger.Information("Starting Analytix Worker Services");
            //Log.Logger = logger;
            var rabbitHelper = new RabbitMQHelper(config);
            //var service2 = new ModelAnalytix();
            //service2.GetEventComments("90");
            RabbitMQHelper.ProcessResult ConsumeFunction(string body, string messageId = "")
            {
                var pr = new RabbitMQHelper.ProcessResult { };
                Log.Logger.Information($"Mensaje recibido: {messageId}");
                var jsonData = JsonSerializer.Deserialize<Dictionary<string, object>>(body);
                var method = jsonData["method"].ToString();

                return pr;
            }

            rabbitHelper.Consume(ConsumeFunction, (string body, string messageId, string _queueName, string errorMessage) =>
            {
                //var service = new ErrorService();
                //service.InsertError(new EF.ErrorQueue
                //{
                //    Error = errorMessage,
                //    FechaHora = DateTime.Now,
                //    Mensaje = body,
                //    MessageId = messageId,
                //    TipoError = ErrorService.ERROR_CONSUME_MESSAGE,
                //    Queue = _queueName
                //});
            });

            Environment.Exit(0);

            var dbManager = new DbManager(config);

            // Ejecutar SQL
            dbManager.SqlService.EjecutarConsulta();
        }
    }
}