using System;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ;
using Newtonsoft.Json;
using System.Threading;

namespace RabbitMQClient
{
    class Program
    {
        public static int maxPrecision = 100000;
        static void Main(string[] args)
        {
            RabbitMQService rabbitMQService = new RabbitMQService();
            IConnection connection = rabbitMQService.GetRabbitMqConnection();
            IModel channel = connection.CreateModel();
            channel.QueueDeclare(rabbitMQService.queueName, false, false, false, null);
            string responseQueue = channel.QueueDeclare().QueueName;

            EventingBasicConsumer basicResponseConsumer = new EventingBasicConsumer(channel);
            basicResponseConsumer.Received += (sender, basicDeliveryEventArgs) =>
            {
                IBasicProperties props = basicDeliveryEventArgs.BasicProperties;
                PiTask response = new PiTask();
                if (props != null)
                {
                    response = JsonConvert.DeserializeObject<PiTask>(Encoding.UTF8.GetString(basicDeliveryEventArgs.Body));
                }
                channel.BasicAck(basicDeliveryEventArgs.DeliveryTag, false);
                Console.WriteLine("[Received] " + "Task No. " + response.id + " Precision: " + response.precision +  " Result: " + response.result);
            };
            channel.BasicConsume(responseQueue, false, basicResponseConsumer);

            string[] tasks = new string[100];
            Random rnd = new Random();
            for (int i = 0; i < 100; i++)
            {
                PiTask message = new PiTask() { id = i, name = "Calculate Pi Precision Task No. " + i.ToString(), precision = rnd.Next(1, maxPrecision) };
                string correlationId = Guid.NewGuid().ToString();
                tasks[i] = correlationId;
                IBasicProperties basicProperties = channel.CreateBasicProperties();
                basicProperties.ReplyTo = responseQueue;
                basicProperties.CorrelationId = correlationId;
                byte[] messageBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message));
                channel.BasicPublish("", rabbitMQService.queueName, basicProperties, messageBytes);
                Console.WriteLine("[Sent] " + "Task No. " + message.id + " Precision: " + message.precision);
            }

            channel.QueueDeclare(rabbitMQService.stopQueueName, false, false, false, null);
            IBasicProperties basicPropertiess = channel.CreateBasicProperties();
            bool running = true;
            Console.WriteLine("Write \"exit\" to stop program");
            Console.WriteLine("Write \"stop\" to stop all tasks");
            Console.WriteLine("Write \"stop\" [id] to stop task with directed ID");
            string command;
            int result;
            bool parse;
            string[] splitString;
            while (running)
            {
                command = Console.ReadLine().ToLower();
                if (command == "exit") running = false;
                else if (command == "stop")
                    channel.BasicPublish("", rabbitMQService.stopQueueName, basicPropertiess, Encoding.UTF8.GetBytes("stop"));
                else
                {
                    splitString = command.Split(' ');
                    if (splitString.Length > 1)
                    {
                        parse = Int32.TryParse(splitString[1], out result);
                        if (parse && result < 100)
                            channel.BasicPublish("", rabbitMQService.stopQueueName, basicPropertiess, Encoding.UTF8.GetBytes("stop " + tasks[result]));
                    }
                }
            }
            Console.ReadKey();

            channel.Close();
            connection.Close();
        }
    }
}