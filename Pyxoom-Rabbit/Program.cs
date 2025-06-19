using Serilog;
using PSW.Pyxoom.Analytix.Models;
using PSW.Pyxoom.Analytix.Models.RabbitMQ;
using System.Text.Json;
using System.Collections.Generic;
using System;
using System.Configuration;

namespace PSW.Pyxoom.Analytix.Queue
{
    internal class Program
    {
        private const string events = "events";
        private const string normaEvents = "normaEvents";
        static void Main(string[] args)
        {
            var logFile = ConfigurationManager.AppSettings["serilog:write-to:File.path"];
            var templateFile = ConfigurationManager.AppSettings["serilog:write-to:File.outputTemplate"];

            var logger = new LoggerConfiguration()
                            .Enrich.FromLogContext()
                            .MinimumLevel.Information()
                            .WriteTo.Console(outputTemplate: templateFile)
                            .WriteTo.File(logFile, rollingInterval: RollingInterval.Day)
                            .CreateLogger();
            //.ReadFrom.AppSettings().CreateLogger();
            logger.Information("Starting Analytix Worker Services");
            Log.Logger = logger;
            var rabbitHelper = new RabbitMQHelper();
            //var service2 = new ModelAnalytix();
            //service2.GetEventComments("90");
            ProcessResult ConsumeFunction(string body, string messageId = "")
            {
                var pr = new ProcessResult { };
                Log.Logger.Information($"Mensaje recibido: {messageId}");
                var jsonData = JsonSerializer.Deserialize<Dictionary<string, object>>(body);
                var method = jsonData["method"].ToString();
                if (method == events)
                {
                    var service = new ModelAnalytix();
                    var res = service.SaveQuestionnaireAnswers(jsonData["_event"].ToString(), jsonData["_Dimmensions"].ToString(), jsonData["_xmlRecommendations"].ToString(), jsonData["_xmlResult"].ToString(), jsonData["AccessKey"].ToString());
                    if (!res.IsSuccess)
                    {
                        Log.Logger.Error($"Mensaje con error: {messageId} {messageId}");
                    }
                    pr = res;
                }
                if (method == normaEvents)
                {
                    var jsonDataObject = JsonSerializer.Deserialize<Dictionary<string, object>>(body);
                    var service = new InteractiveModel();
                    var eventId = int.Parse(jsonData["eventId"].ToString());
                    var data = JsonSerializer.Deserialize<List<AnswerQuestion>>(jsonData["data"].ToString());
                    var res = service.SaveQuestionnaire(jsonData["webAccess"].ToString(), eventId, jsonData["accessKey"].ToString(), jsonData["dimensions"].ToString(), data);
                    if (!res.IsSuccess)
                    {
                        Log.Logger.Error($"Mensaje con error: {messageId} {body}");
                    }
                    pr.IsSuccess = res.IsSuccess;
                    pr.Msg = res.Msg;
                }
                return pr;
            }

            rabbitHelper.Consume(ConsumeFunction, (string body, string messageId, string _queueName, string errorMessage) => {
                var service = new ErrorService();
                service.InsertError(new EF.ErrorQueue
                {
                    Error = errorMessage,
                    FechaHora = DateTime.Now,
                    Mensaje = body,
                    MessageId = messageId,
                    TipoError = ErrorService.ERROR_CONSUME_MESSAGE,
                    Queue = _queueName
                });
            });
            Environment.Exit(0);
        }
    }
}