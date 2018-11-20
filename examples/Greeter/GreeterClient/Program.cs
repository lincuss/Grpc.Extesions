using Grpc.Core;
using Grpc.Extension;
using Grpc.Extension.Interceptors;
using Helloworld;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;


namespace GreeterClient
{
    class Program
    {
        public static void Main(string[] args)
        {
            //使用配制文件
            var configPath = Path.Combine(AppContext.BaseDirectory, "config");
            var configBuilder = new ConfigurationBuilder();
            var conf = configBuilder.SetBasePath(configPath).AddJsonFile("appsettings.json", false, true).Build();

            var innerLogger = new Grpc.Core.Logging.LogLevelFilterLogger(new Grpc.Core.Logging.ConsoleLogger(), Grpc.Core.Logging.LogLevel.Debug);
            GrpcEnvironment.SetLogger(innerLogger);

            //使用依赖注入
            var services = new ServiceCollection()
                 .AddGrpcMiddleware4Client()
                //.AddSingleton<ClientInterceptor>(new ClientCallTimeout(10))//注入客户端中间件
                .AddGrpcClient<Greeter.GreeterClient>(conf.GetSection("services:remotes:GreeterServer").Get<RemoteServiceOption>());//注入grpc client
            var provider = services.BuildServiceProvider();

            //从容器获取client
            var client = provider.GetService<Greeter.GreeterClient>();
            var user = "you";

        call:
            for (int i = 0; i < 10; i++)
            {
                var reply = client.SayHello(new HelloRequest { Name = user + i.ToString() });
                Console.WriteLine($"Greeting{i.ToString()}: {reply.Message}");
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            goto call;
        }
    }
}
