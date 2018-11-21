using System;
using System.IO;
using Grpc.Extension;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GreeterServer
{
    class Program
    {
        public static void Main(string[] args)
        {
            using (var host = BuildHost(args))
            {
                host.Run();
            }
        }

        public static IHost BuildHost(string[] args)
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "config");
            var host = new HostBuilder()
                .ConfigureHostConfiguration(conf =>
                {
                    conf.SetBasePath(configPath);
                    conf.AddJsonFile("hostsettings.json", optional: true);
                })
                .ConfigureAppConfiguration((ctx, conf) =>
                {
                    conf.SetBasePath(configPath);
                    conf.AddJsonFile("appsettings.json", false, true);
                })
                .ConfigureServices((ctx, services) =>
                {
                    services.AddGrpcMiddleware4Srv().BuildInterl4Grpc();
                    services.AddHostedService<GrpcHostServiceV2>();
                });
            return host.Build();
        }
    }
}
