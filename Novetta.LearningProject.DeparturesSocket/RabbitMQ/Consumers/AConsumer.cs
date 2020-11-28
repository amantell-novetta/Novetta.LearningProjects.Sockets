using Microsoft.AspNetCore.Http;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Novetta.LearningProject.DeparturesSocket.RabbitMQ.Consumers
{
    public abstract class AConsumer
    {
        protected IConnection InitConnection()
        {
            var factory = new ConnectionFactory()
            {
                HostName = "127.0.0.1",
                UserName = "guest",
                Password = "guest",
                Port = 5672,
                VirtualHost = "/",
            };

            var conn = factory.CreateConnection();
            return conn;
        }

        protected abstract IModel InitChannel(IConnection conn);

        protected abstract EventingBasicConsumer InitConsumer(IModel channel);

        public abstract Task PushAsync(HttpContext context, WebSocket webSocket);
    }
}
