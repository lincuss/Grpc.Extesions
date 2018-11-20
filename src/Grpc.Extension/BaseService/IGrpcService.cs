using Grpc.Core;

namespace Grpc.Extension.BaseService
{
    public interface IGrpcService
    {
        /// <summary>
        /// 注册服务方法
        /// </summary>
        void RegisterMethod(ServerServiceDefinition.Builder builder);
    }
}
