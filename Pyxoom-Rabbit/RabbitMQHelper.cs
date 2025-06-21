using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace Pyxoom_Rabbit
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
        private string _exchange = "";
        private readonly Dictionary<string, object> _arguments;

        public RabbitMQHelper()
        {
            var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

            _hostName = config["RabbitMQ:HostName"]!;
            _port = int.TryParse(config["RabbitMQ:Port"], out var port) ? port : 5671;
            _userName = config["RabbitMQ:UserName"]!;
            _password = config["RabbitMQ:Password"]!;
            _virtualHost = config["RabbitMQ:VirtualHost"]!;
            _queueName = config["RabbitMQ:QueueName"]!;

            _arguments = new()
            {
                { "x-dead-letter-exchange", "" },
                { "x-dead-letter-routing-key", _queueName + "_delayedQueue" }
            };
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
                    AcceptablePolicyErrors =
                        System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch |
                        System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors
                }
            };
        }

        //public async Task PublishAsync(string message, string exchange = "")
        //{
        //    _exchange = exchange;
        //    var factory = GetSecureConnectionFactory();

        //    try
        //    {
        //        using IConnection connection = await factory.CreateConnection();
        //        using var channel = await connection.CreateModel();                 

        //        var properties = channel.CreateBasicProperties();
        //        properties.Headers = new Dictionary<string, object>
        //        {
        //            { "x-attempts", 1 },
        //            { "x-datetime", DateTime.Now.ToString("O") }
        //        };
        //        properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        //        properties.MessageId = Guid.NewGuid().ToString();

        //        var body = Encoding.UTF8.GetBytes(message);

        //        channel.BasicPublish(exchange, _queueName, properties, body);
        //        Console.WriteLine($"[x] Sent '{properties.MessageId}'");
        //    }
        //    catch
        //    {
        //        throw new Exception(ERROR_PUBLISH_MESSAGE);
        //    }
        //}

        //public async Task CreateQueueAsync(string queueName)
        //{
        //    var factory = GetSecureConnectionFactory();
        //    using var connection = await factory.CreateConnection();
        //    using var channel = connection.CreateModel();

        //    channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: _arguments);
        //    channel.QueueDeclare(queue: queueName + "_delayedQueue", durable: true, exclusive: false, autoDelete: false);

        //    Console.WriteLine($"[x] Queue '{queueName}' created successfully.");
        //}

        //public void Consume(Func<string, string, ProcessResult> onConsumeFunction, Action<string, string, string, string> rejectFn)
        //{
        //    var factory = GetSecureConnectionFactory();
        //    var connection = factory.CreateConnection(); // Esto sigue siendo válido si lo haces sin `await`
        //    var channel = connection.CreateModel();

        //    channel.QueueDeclare(_queueName, true, false, false, _arguments);

        //    var consumer = new EventingBasicConsumer(channel);
        //    consumer.Received += (model, ea) =>
        //    {
        //        var props = ea.BasicProperties;
        //        var headers = props.Headers ?? new Dictionary<string, object>();
        //        var messageId = props.MessageId ?? Guid.NewGuid().ToString();

        //        var body = ea.Body.ToArray();
        //        var message = Encoding.UTF8.GetString(body);
        //        Console.WriteLine($"[x] Received '{messageId}'");

        //        var attemptCount = headers.TryGetValue("x-attempts", out var val) ? Convert.ToInt32(val) : 1;

        //        if (attemptCount >= 3)
        //        {
        //            channel.BasicReject(ea.DeliveryTag, false);
        //            return;
        //        }

        //        try
        //        {
        //            var result = onConsumeFunction(message, messageId);
        //            attemptCount++;

        //            if (!result.IsSuccess)
        //            {
        //                props.Headers["x-attempts"] = attemptCount;

        //                channel.BasicPublish(_exchange ?? "", _queueName, props, body);
        //                if (attemptCount == 3)
        //                    rejectFn?.Invoke(message, messageId, _queueName, result.Msg);
        //            }

        //            channel.BasicAck(ea.DeliveryTag, false);
        //        }
        //        catch
        //        {
        //            channel.BasicReject(ea.DeliveryTag, false);
        //            throw new Exception(ERROR_NO_CONTROLLED_AND_PROCCESS);
        //        }
        //    };

        //    channel.BasicConsume(_queueName, false, consumer);
        //    Console.WriteLine("[*] Waiting for messages. Press [enter] to exit.");
        //    Console.ReadLine();
        //}

        public class ProcessResult
        {
            public ProcessResult(bool isSuccess, string msg, string? data = null)
            {
                IsSuccess = isSuccess;
                Msg = msg;
                Data = data;
            }

            public bool IsSuccess { get; set; }
            public string Msg { get; set; }
            public string? Data { get; set; }
        }
    }
}
