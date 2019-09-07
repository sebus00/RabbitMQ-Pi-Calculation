using System;
using RabbitMQ.Client;

namespace RabbitMQ
{
    public class RabbitMQService
    {
        private string _hostName = "localhost";
        private string _userName = "guest";
        private string _password = "guest";
        public string queueName = "PiQueue";
        public string stopQueueName = "stopQueue";
        public ushort processCount = 4;

        public IConnection GetRabbitMqConnection()
        {
            ConnectionFactory connectionFactory = new ConnectionFactory();
            connectionFactory.HostName = _hostName;
            connectionFactory.UserName = _userName;
            connectionFactory.Password = _password;
            return connectionFactory.CreateConnection();
        }
    }

    public struct PiTask
    {
        public string name;
        public int id;
        public int precision;
        public string result;
    }
}
