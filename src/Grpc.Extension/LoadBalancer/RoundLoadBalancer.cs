using Grpc.Core;
using Grpc.Core.Logging;
using System;
using System.Linq;
using System.Threading;

namespace Grpc.Extension.LoadBalancer
{
    public class RoundLoadBalancer : ILoadBalancer
    {
        private ILogger _logger = Core.GrpcEnvironment.Logger.ForType<RoundLoadBalancer>();

        /// <summary>
        /// round point
        /// </summary>
        private int _roundProxyIndex = 0;

        /// <summary>
        /// for lock
        /// </summary>
        private readonly object _fetchLock = new object();

        public AgentServiceChannelPair SelectEndpoint(string serviceName)
        {
            var entryed = false;
            var pool = GRPCChannelPoolManager.Instances.Value.First(p => p.GrpcSrvName == serviceName);
            try
            {
                /*
                 * 移除lock的原因在于lock没有timeout机制
                 * 如果突然大量的请求来了之后,
                 * 会全部卡在这个方法之中就会导致大量的阻塞(并且阻塞之后没有任何意义)
                 * 出现阻塞的地方只有可能是正在更新,正在更新的过程之中,所有的调用都应该全部失败..
                 */
                entryed = Monitor.TryEnter(_fetchLock, 100);
                if (!entryed)
                {
                    //timeout
                    throw new Exception("Fetch timeout, 服务暂不可用");
                }

            fetch:

                pool.CheckPoolState();

                //reset to first
                if (_roundProxyIndex == pool.ConnectedAgentServiceChannels.Count)
                {
                    Interlocked.Exchange(ref _roundProxyIndex, 0);
                }

                var choosePair = pool.ConnectedAgentServiceChannels[_roundProxyIndex];

                if (!pool.CheckAndProcessChannelStatus(choosePair))
                {
                    Interlocked.Exchange(ref _roundProxyIndex, 0);
                    goto fetch;
                }

                Interlocked.Increment(ref _roundProxyIndex);

                _logger.Debug($"使用proxy:{choosePair.AgentService.ID} ");
                return choosePair;
            }
            finally
            {
                if (entryed) Monitor.Exit(_fetchLock);
            }
        }
    }
}
