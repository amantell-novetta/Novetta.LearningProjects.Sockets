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
using System.Collections.Generic;

namespace Novetta.LearningProject.DeparturesSocket.RabbitMQ.Consumers
{
    public class Departures : AConsumer, IDisposable
    {

        private readonly IConnection _conn;
        private readonly IModel _channel;
        private readonly EventingBasicConsumer _consumer;
        private readonly ConcurrentDictionary<string, WebSocket> _sockets;
        private string _queueName;

        public Departures()
        {
            _sockets = new ConcurrentDictionary<string, WebSocket>();
            _conn = InitConnection();
            _channel = InitChannel(_conn);
            _consumer = InitConsumer(_channel);
            _channel.BasicConsume(_queueName, false, _consumer);
        }

        protected override IModel InitChannel(IConnection conn)
        {
            var channel = conn.CreateModel();
            channel.ExchangeDeclare(exchange: "flights", type: "direct");
            _queueName = channel.QueueDeclare().QueueName;
            channel.QueueBind(queue: _queueName,
                              exchange: "flights",
                              routingKey: "departures");
            channel.BasicQos(0, 1, false);
            return channel;
        }

        protected override EventingBasicConsumer InitConsumer(IModel channel)
        {
            var consumer = new EventingBasicConsumer(channel);

            consumer.Shutdown += OnConsumerShutdown;
            consumer.Registered += OnConsumerRegistered;
            consumer.Unregistered += OnConsumerUnregistered;
            consumer.ConsumerCancelled += OnConsumerConsumerCancelled;

            consumer.Received += (ch, ea) =>
            {
                var content = Encoding.UTF8.GetString(ea.Body.ToArray());
                Console.WriteLine($"received content = {content}");

                var consumer = new EventingBasicConsumer(channel);
                consumer.Received += async (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var routingKey = ea.RoutingKey;
                    //Console.WriteLine(" [x] Received '{0}':'{1}'....",
                    //                  routingKey, message.Substring(0, 10));

                    byte[] output = Encoding.UTF8.GetBytes(message);

                    var key = _sockets.Keys.ToList()[0];

                    if (_sockets.TryGetValue(key, out var ws) && ws.State == WebSocketState.Open)
                    {
                        Console.WriteLine("sent");
                        await ws.SendAsync(new ArraySegment<byte>(output, 0, output.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    else
                    {
                        Console.WriteLine("Not found a worker");
                    }
                };
                channel.BasicConsume(queue: _queueName,
                                     autoAck: true,
                                     consumer: consumer);

                //Console.WriteLine(" Press [enter] to exit.");
                //Console.ReadLine();

            };

            return consumer;
        }

        public async override Task PushAsync(HttpContext context, WebSocket webSocket)
        {
            var buffer = new byte[1024 * 4];
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            string clientId = Encoding.UTF8.GetString(buffer, 0, result.Count);

            // record the client id and it's websocket instance
            if (_sockets.TryGetValue(clientId, out var wsi))
            {
                if (wsi.State == WebSocketState.Open)
                {
                    Console.WriteLine($"abort the before clientId named {clientId}");
                    await wsi.CloseAsync(WebSocketCloseStatus.InternalServerError, "A new client with same id was connected!", CancellationToken.None);
                }

                _sockets.AddOrUpdate(clientId, webSocket, (x, y) => webSocket);
            }
            else
            {
                Console.WriteLine($"add or update {clientId}");
                _sockets.AddOrUpdate(clientId, webSocket, (x, y) => webSocket);
            }

            while (!result.CloseStatus.HasValue)
            {
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }

            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
            Console.WriteLine("close=" + clientId);

            _sockets.TryRemove(clientId, out _);
        }

        private void OnConsumerConsumerCancelled(object sender, ConsumerEventArgs e)
        {
        }

        private void OnConsumerUnregistered(object sender, ConsumerEventArgs e)
        {
            Console.WriteLine("OnConsumerUnregistered");
        }

        private void OnConsumerRegistered(object sender, ConsumerEventArgs e)
        {
            Console.WriteLine("OnConsumerRegistered");
        }

        private void OnConsumerShutdown(object sender, ShutdownEventArgs e)
        {
            Console.WriteLine("OnConsumerShutdown");
        }

        private void RabbitMQ_ConnectionShutdown(object sender, ShutdownEventArgs e) { }

        public void Dispose()
        {
            Console.WriteLine("Dispose");
            _sockets?.Clear();
            _channel?.Dispose();
            _conn?.Dispose();
        }
    }
}
