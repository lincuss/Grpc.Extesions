using Grpc.Core;
using System;
using System.Collections;
using System.Reflection;
using Grpc.Extension.Common;
using Grpc.Extension.Model;

namespace Grpc.Extension.BaseService
{
    public static class GrpcServiceExtension
    {
        /// <summary>
        /// 生成Grpc方法（CodeFirst方式，用于生成BaseService）
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <typeparam name="TResponse"></typeparam>
        /// <param name="srv"></param>
        /// <param name="methodName"></param>
        /// <param name="package"></param>
        /// <param name="srvName"></param>
        /// <param name="mType"></param>
        /// <returns></returns>
        public static Method<TRequest, TResponse> BuildMethod<TRequest, TResponse>(this IGrpcService srv,
            string methodName, string package = null, string srvName = null, MethodType mType = MethodType.Unary)
        {
            var serviceName = srvName ??
                              GrpcExtensionsOptions.Instance.GlobalService ??
                              srv.GetType().Name;
            var pkg = package ?? GrpcExtensionsOptions.Instance.GlobalPackage;
            if (!string.IsNullOrWhiteSpace(pkg))
            {
                serviceName = $"{pkg}.{serviceName}";
            }
            var request = Marshallers.Create<TRequest>((arg) => ProtobufExtensions.Serialize<TRequest>(arg), data => ProtobufExtensions.Deserialize<TRequest>(data));
            var response = Marshallers.Create<TResponse>((arg) => ProtobufExtensions.Serialize<TResponse>(arg), data => ProtobufExtensions.Deserialize<TResponse>(data));
            return new Method<TRequest, TResponse>(mType, serviceName, methodName, request, response);
        }

        /// <summary>
        /// 生成Grpc元数据信息
        /// </summary>
        /// <param name="builder"></param>
        public static void BuildMeta(IDictionary callHandlers)
        {
            var bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            //获取Grpc元数据信息
            foreach (DictionaryEntry callHandler in callHandlers)
            {
                //实现过程
                /*
                 * grpc注入handler:
                 *      public static grpc::ServerServiceDefinition BindService(GreeterBase serviceImpl)
                        {
                                return grpc::ServerServiceDefinition.CreateBuilder()
                                .AddMethod(__Method_SayHello, serviceImpl.SayHello).Build();
                        }
                   
                  AddMethod内部会调用:callHandlers.Add(method.FullName, ServerCalls.[UnaryxxxCall](method, handler));
                  这个时候, 所有的Sayhello之类的delegate 会变成IServerCallHandler
                  {
                    UnaryServerCallHandler
                    ServerStreamingServerCallHandler
                    ......
                  }
                  此时传递进来的callHandlers数据结构
                 * callHandlers:
                 *      map[key, IServerCallHandler]
                 *      key: /url
                 *      value:  x-delegate
                 *  IServerCallHandler都会有2个属性:
                                readonly Method<TRequest, TResponse> method;
                                readonly [ServerStreamingServerMethod]<TRequest, TResponse> handler; -->这个XXXServerMethod就是一个delegate
                 *  
                 * 
                 */
                var hFiled = callHandler.Value.GetFieldValue<Delegate>("handler", bindingFlags);
                var handler = hFiled.Item1;
                var types = hFiled.Item2.DeclaringType.GenericTypeArguments;
                MetaModel.Methods.Add((new MetaMethodModel
                {
                    FullName = callHandler.Key.ToString(),
                    RequestType = types[0],
                    ResponseType = types[1],
                    Handler = handler
                }));
            }
        }
    }
}
