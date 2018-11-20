using Microsoft.Extensions.DependencyInjection;
using Grpc.Core;
using Grpc.Extension.Consul;
using System.Reflection;
using Grpc.Extension.Common;
using Grpc.Extension.Interceptors;
using Grpc.Extension.Registers;

namespace Grpc.Extension
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 添加GrpcClient,生成元数据
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="services"></param>
        /// <param name="consulUrl"></param>
        /// <param name="consulServiceName"></param>
        /// <returns></returns>
        public static IServiceCollection AddGrpcClient<T>(this IServiceCollection services,
            RemoteServiceOption config) where T : class
        {
            services.AddSingleton<T>();
            var bindFlags = BindingFlags.Static | BindingFlags.NonPublic;
            config.GrpcSrvName = typeof(T).DeclaringType.GetFieldValue<string>("__ServiceName", bindFlags);

            GRPCChannelPoolManager.Instances.Value.Add(new GRPCChannelPoolManager(config));
            return services;
        }

        public static IServiceCollection AddGrpcMiddleware4Srv(this IServiceCollection services)
        {
            //添加服务端中间件
            services.AddSingleton<ServiceRegister>();
            services.AddSingleton<ServerInterceptor, MonitorInterceptor>();
            services.AddSingleton<ServerInterceptor, ThrottleInterceptor>();
            return services;
        }

        public static IServiceCollection AddGrpcMiddleware4Client(this IServiceCollection services)
        {
            //添加客户端中间件的CallInvoker
            services.AddSingleton<AutoChannelCallInvoker>();
            services.AddSingleton<CallInvoker, ClientMiddlewareCallInvoker>();
            return services;
        }

        public static void BuildInterl4Grpc(this IServiceCollection services)
        {
            GrpcServicesExtensions.ServiceProvider = services.BuildServiceProvider();
        }
    }
}
