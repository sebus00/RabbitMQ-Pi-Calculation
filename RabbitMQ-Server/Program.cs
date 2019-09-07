using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ;
using Newtonsoft.Json;
using System.Threading;
using System.Collections.Generic;

namespace RabbitMQServer
{
    class Program
    {
        static void Main(string[] args)
        {
            RabbitMQService rabbitMQService = new RabbitMQService();
            IConnection connection = rabbitMQService.GetRabbitMqConnection();
            IModel channel = connection.CreateModel();
            channel.QueueDeclare(rabbitMQService.queueName, false, false, false, null);
            channel.BasicQos(0, rabbitMQService.processCount, false);
            EventingBasicConsumer eventingTaskConsumer = new EventingBasicConsumer(channel);

            bool cancelAll = false;
            Dictionary<string, CancellationTokenSource> tokenSourcesList = new Dictionary<string, CancellationTokenSource>();
            List<String> tasksToCancel = new List<String>();

            eventingTaskConsumer.Received += (sender, basicDeliveryEventArgs) =>
            {          
                tokenSourcesList.Add(basicDeliveryEventArgs.BasicProperties.CorrelationId, new CancellationTokenSource());
                Task.Run(async () =>
                {
                    bool cancelled = cancelAll || tasksToCancel.Contains(basicDeliveryEventArgs.BasicProperties.CorrelationId);
                    string taskId = await DoWork(channel, basicDeliveryEventArgs, cancelled, tokenSourcesList[basicDeliveryEventArgs.BasicProperties.CorrelationId]);
                    tokenSourcesList[taskId].Dispose();
                    tokenSourcesList.Remove(taskId);
                    tasksToCancel.Remove(taskId);
                }, tokenSourcesList[basicDeliveryEventArgs.BasicProperties.CorrelationId].Token);
            };

            channel.BasicConsume(rabbitMQService.queueName, false, eventingTaskConsumer);

            channel.QueueDeclare(rabbitMQService.stopQueueName, false, false, false, null);
            channel.BasicQos(0, 1, false);
            EventingBasicConsumer eventingStopConsumer = new EventingBasicConsumer(channel);
            eventingStopConsumer.Received += (sender, basicDeliveryEventArgs) =>
            {
                string response = Encoding.UTF8.GetString(basicDeliveryEventArgs.Body);
                Console.WriteLine("[Received] " + response);
                if (response == "stop")
                {
                    cancelAll = true;
                    Dictionary<string, CancellationTokenSource>.KeyCollection keys = tokenSourcesList.Keys;
                    foreach(string key in keys)
                    {
                        tokenSourcesList[key].Cancel();
                    }
                }
                else if (response.Contains("stop"))
                {
                    string[] splitString = response.Split(' ');
                    string taskToCancel;
                    if (splitString.Length > 1)
                    {
                        taskToCancel = splitString[1];
                        if (tokenSourcesList.ContainsKey(taskToCancel))
                            tokenSourcesList[taskToCancel].Cancel();
                        else
                            tasksToCancel.Add(taskToCancel);
                    }
                }
                
            };
            channel.BasicConsume(rabbitMQService.stopQueueName, true, eventingStopConsumer);

        }

        public static async Task<String> DoWork(IModel channel, BasicDeliverEventArgs basicDeliveryEventArgs, bool cancelled,  CancellationTokenSource cancellationTokenSource = default)
        {
            PiTask response = new PiTask();
            try
            {
                response = JsonConvert.DeserializeObject<PiTask>(Encoding.UTF8.GetString(basicDeliveryEventArgs.Body));                
                Console.WriteLine("[Received] " + response.name);
                if (cancelled)
                {
                    response.result = "-1";
                }
                else
                    response.result = await CalculatePiPrecision(response.precision, cancellationTokenSource.Token);
            }
            catch (TaskCanceledException e)
            {
                response.result = "-1";
            }
            catch (OperationCanceledException e)
            {
                response.result = "-1";
            }
            finally
            {
                IBasicProperties replyBasicProperties = channel.CreateBasicProperties();
                replyBasicProperties.CorrelationId = basicDeliveryEventArgs.BasicProperties.CorrelationId;
                byte[] responseBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response));
                channel.BasicPublish("", basicDeliveryEventArgs.BasicProperties.ReplyTo, replyBasicProperties, responseBytes);
                channel.BasicAck(basicDeliveryEventArgs.DeliveryTag, false);               
            }
            return basicDeliveryEventArgs.BasicProperties.CorrelationId;
        }

        private static async Task<string> CalculatePiPrecision(int precision, CancellationToken cancellationToken = default)
        {
            LongNumber x = new LongNumber(precision);
            LongNumber y = new LongNumber(precision);
            await x.ArcTan(16, 5, cancellationToken);
            await y.ArcTan(4, 239, cancellationToken);
            x.Subtract(y);
            return x.Print();
        }
    }
}