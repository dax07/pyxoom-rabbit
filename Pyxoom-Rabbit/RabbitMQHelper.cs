using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
//using System.Web.Configuration;

namespace Pyxoom_Rabbit
{
    namespace PSW.Pyxoom.Utility
    {

        public class RabbitMQHelper
        {
            public static string ERROR_PUBLISH_MESSAGE = "Error al publicar el mensaje";
            public static string ERROR_CONSUME_MESSAGE = "Error al consumir el mensaje";
            public static string ERROR_NO_CONTROLLED_AND_PROCCESS = "Error no controlado al procesar el mensaje";

            private readonly string _hostName;
            private readonly int _port;
            private readonly string _userName;
            private readonly string _password;
            private readonly string _virtualHost;
            private readonly string _queueName;
            private string _exchange;
            private readonly Dictionary<string, object> _arguments;

            public RabbitMQHelper(string hostName, int port = 5671, string userName = "guest", string password = "guest", string virtualHost = "/", string queueName = null)
            {
                _hostName = hostName;
                _port = port;
                _userName = userName;
                _password = password;
                _virtualHost = virtualHost;
                _queueName = queueName;
                _arguments = new Dictionary<string, object>
            {
                { "x-dead-letter-exchange", "" },
                { "x-dead-letter-routing-key", _queueName + "_delayedQueue" }
            };
                this.CreateQueue(queueName);
            }

            //public RabbitMQHelper(string virtualHost = "/")
            //{
            //    _hostName = WebConfigurationManager.AppSettings["RabbitHostName"];
            //    _userName = WebConfigurationManager.AppSettings["RabbitUserName"];
            //    _password = WebConfigurationManager.AppSettings["RabbitPassword"];
            //    _virtualHost = WebConfigurationManager.AppSettings["RabbitVirtualHost"];
            //    _queueName = WebConfigurationManager.AppSettings["RabbitChannel"];
            //    _port = 5671;
            //    _arguments = new Dictionary<string, object>
            //    {
            //        { "x-dead-letter-exchange", "" },
            //        { "x-dead-letter-routing-key", _queueName + "_delayedQueue" }
            //    };
            //    this.CreateQueue(_queueName);
            //}

            public RabbitMQHelper(string queueName)
            {
                _hostName = WebConfigurationManager.AppSettings["RabbitHostName"];
                _userName = WebConfigurationManager.AppSettings["RabbitUserName"];
                _password = WebConfigurationManager.AppSettings["RabbitPassword"];
                _virtualHost = WebConfigurationManager.AppSettings["RabbitVirtualHost"];
                _port = int.TryParse(WebConfigurationManager.AppSettings["RabbitPort"], out int parsedPort) ? parsedPort : 5671;
                _queueName = queueName;

                _arguments = new Dictionary<string, object>
                {
                    { "x-dead-letter-exchange", "" },
                    { "x-dead-letter-routing-key", _queueName + "_delayedQueue" }
                };

                this.CreateQueue(_queueName);
            }

            private ConnectionFactory GetSecureConnectionFactory()
            {
                return new ConnectionFactory
                {
                    HostName = _hostName,
                    Port = _port,
                    UserName = _userName,
                    Password = _password,
                    VirtualHost = _virtualHost,
                    Ssl = new SslOption
                    {
                        Enabled = true,
                        ServerName = _hostName,
                        Version = System.Security.Authentication.SslProtocols.Tls12,
                        AcceptablePolicyErrors = System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch |
                                                 System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors
                    }
                };
            }

            public string GetQueueName() => _queueName;

            public void Publish(string message, string exchange = "")
            {
                _exchange = exchange;

                var factory = GetSecureConnectionFactory();

                try
                {
                    using (var connection = factory.CreateConnection())
                    using (var channel = connection.CreateModel())
                    {
                        var properties = channel.CreateBasicProperties();
                        properties.Headers = new Dictionary<string, object>
                    {
                        { "x-attempts", 1 },
                        { "x-datetime", DateTime.Now.ToString("O") }
                    };
                        properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                        properties.MessageId = Guid.NewGuid().ToString();

                        var body = Encoding.UTF8.GetBytes(message);

                        channel.BasicPublish(exchange: exchange,
                                             routingKey: _queueName,
                                             basicProperties: properties,
                                             body: body);

                        Console.WriteLine($"[x] Sent '{properties.MessageId}'");
                    }
                }
                catch (Exception)
                {
                    throw new Exception(ERROR_PUBLISH_MESSAGE);
                }
            }

            public void Consume(Func<string, string, ProcessResult> onConsumeFunction, Action<string, string, string, string> rejectFn)
            {
                var factory = GetSecureConnectionFactory();

                var connection = factory.CreateConnection();
                var channel = connection.CreateModel();

                channel.QueueDeclare(queue: _queueName,
                                     durable: true,
                                     exclusive: false,
                                     autoDelete: false,
                                     arguments: _arguments);

                var consumer = new EventingBasicConsumer(channel);
                consumer.Received += (model, ea) =>
                {
                    var properties = ea.BasicProperties;
                    var headers = properties.Headers ?? new Dictionary<string, object>();
                    string messageId = properties.MessageId;

                    var body = ea.Body;
                    var message = Encoding.UTF8.GetString(body.ToArray());
                    Console.WriteLine($"[x] Received '{messageId}'");

                    var attemptCount = Convert.ToInt32(properties.Headers?["x-attempts"]);
                    if (attemptCount >= 3)
                    {
                        channel.BasicReject(deliveryTag: ea.DeliveryTag, requeue: false);
                        return;
                    }

                    ProcessResult pr = new ProcessResult(false, string.Empty);
                    try
                    {
                        pr = onConsumeFunction?.Invoke(message, messageId);
                    }
                    catch (Exception)
                    {
                        channel.BasicReject(deliveryTag: ea.DeliveryTag, requeue: false);
                        throw new Exception(ERROR_NO_CONTROLLED_AND_PROCCESS);
                    }

                    Console.WriteLine($"[x] x-attempts '{attemptCount}'");

                    attemptCount++;
                    if (pr.IsSuccess != true)
                    {
                        properties.Headers["x-attempts"] = attemptCount;

                        channel.BasicPublish(exchange: _exchange ?? string.Empty,
                                             routingKey: _queueName,
                                             basicProperties: properties,
                                             body: body);

                        if (attemptCount == 3)
                        {
                            rejectFn?.Invoke(message, messageId, _queueName, pr.Msg);
                        }
                    }

                    channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                };

                channel.BasicConsume(queue: _queueName,
                                     autoAck: false,
                                     consumer: consumer);

                Console.WriteLine("[*] Waiting for messages. Press [enter] to exit.");
                Console.ReadLine();
            }

            public void CreateQueue(string queueName = null)
            {
                var factory = GetSecureConnectionFactory();

                using (var connection = factory.CreateConnection())
                using (var channel = connection.CreateModel())
                {
                    channel.QueueDeclare(queue: _queueName,
                                         durable: true,
                                         exclusive: false,
                                         autoDelete: false,
                                         arguments: _arguments);

                    channel.QueueDeclare(queue: _queueName + "_delayedQueue",
                                         durable: true,
                                         exclusive: false,
                                         autoDelete: false);

                    Console.WriteLine($"[x] Queue '{queueName}' created successfully.");
                }
            }

            public class ErrorQueue
            {
                public int Id { get; set; }
                public string Queue { get; set; }
                public string MessageId { get; set; }
                public Nullable<System.DateTime> FechaHora { get; set; }
                public string TipoError { get; set; }
                public string Mensaje { get; set; }
                public string Error { get; set; }
            }

            public class ProcessResult
            {
                public ProcessResult(bool isSuccess, string msg)
                {
                    this.IsSuccess = isSuccess;
                    this.Msg = msg;
                }
                public ProcessResult(bool isSuccess, string msg, string data)
                {
                    this.IsSuccess = isSuccess;
                    this.Msg = msg;
                    this.Data = data;
                }
                public ProcessResult() { }

                public bool IsSuccess { get; set; }
                public string Msg { get; set; }
                public string Data { get; set; }
            }
        }
    }

}
