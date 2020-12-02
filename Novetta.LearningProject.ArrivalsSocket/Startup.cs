using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using Novetta.LearningProject.ArrivalsSocket.RabbitMQ.Consumers;
using System.Net.WebSockets;

namespace Novetta.LearningProject.ArrivalsSocket
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<AConsumer, Arrivals>();
            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            var webSocketOptions = new WebSocketOptions()
            {
                KeepAliveInterval = TimeSpan.FromSeconds(120),
                ReceiveBufferSize = 4 * 1024
            };

            app.UseWebSockets(webSocketOptions);

            #region
            app.Use(async (context, next) =>
            {
                Console.WriteLine("context");

                if (context.Request.Path == "/arrivals")
                {
                    Console.WriteLine("arrivals");

                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();

                        Console.WriteLine("websocket");

                        try
                        {
                            var handler = app.ApplicationServices.GetRequiredService<Novetta.LearningProject.ArrivalsSocket.RabbitMQ.Consumers.AConsumer>();
                            await handler.PushAsync(context, webSocket);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                    }
                }
                else
                {
                    await next();
                }
            });
            #endregion
            app.UseFileServer();
        }
    }
}
