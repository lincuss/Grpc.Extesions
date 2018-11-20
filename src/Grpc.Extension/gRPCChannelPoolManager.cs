using Consul;
using Grpc.Core;
using Grpc.Core.Logging;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Extension.LoadBalancer;

namespace Grpc.Extension
{
    /// <summary>
    /// grpc ChannelPool Manager
    /// </summary>
    /// <seealso cref="FM.ConsulInterop.ConsulInterop" />
    public class GRPCChannelPoolManager : ConsulInterop
    {
        ILogger _logger => GrpcEnvironment.Logger.ForType<GRPCChannelPoolManager>();

        /// <summary>
        /// timer for fresh service  
        /// </summary>
        Timer _freshServiceListTimer = null;

        /// <summary>
        /// fresh ServiceList Interval
        /// </summary>
        int _freshServiceListInterval = Timeout.Infinite;

        /// <summary>
        /// service register service name
        /// </summary>
        public string GrpcSrvName => RemoteServiceOption.GrpcSrvName;

        public ILoadBalancer LoadBalancer { get; private set; } = new RoundLoadBalancer();

        internal RemoteServiceOption RemoteServiceOption { get; private set; }

        private GRPCChannelPoolManager()
        {

        }

        public GRPCChannelPoolManager(RemoteServiceOption config)
        {
            RemoteServiceOption = config;
            InitGrpcChannel();
            _logger.Info($"添加client with :{config.ToString()}");
        }

        /// <summary>
        /// init grpc channel
        /// </summary>
        private void InitGrpcChannel()
        {
            if (RemoteServiceOption.ConsulIntegration)
            {
                InitConsulClient(RemoteServiceOption.ConsulAddress);

                InitUpdateServiceListTimer(this.RemoteServiceOption.FreshInterval);
                _logger.Debug($"InitUpdateServiceListTimer: {this.RemoteServiceOption.FreshInterval}ms");
            }
            else
            {
                _logger.Debug("direct connect:" + this.RemoteServiceOption.ServiceAddress);
                var addressList = this.RemoteServiceOption.ServiceAddress.Split(',');
                foreach (var address in addressList)
                {
                    if (string.IsNullOrWhiteSpace(address)) continue;

                    var hostIp = address.Split(':');
                    AddGrpcChannel(hostIp[0], int.Parse(hostIp[1]), new AgentService { ID = $"direct:{address}" });
                }
            }
        }

        /// <summary>
        /// Consul Agent Server Comparer
        /// </summary>
        /// <seealso cref="System.Collections.Generic.IEqualityComparer{Consul.AgentService}" />
        internal class AgentServerComparer : IEqualityComparer<AgentService>
        {
            public bool Equals(AgentService x, AgentService y)
            {
                return x.ID == y.ID;
            }

            public int GetHashCode(AgentService obj)
            {
                return $"{obj.ID}".GetHashCode();
            }
        }

        /// <summary>
        /// Gets the connected agent service channels.
        /// </summary>
        /// <value>
        /// The connected agent service channels.
        /// </value>
        public List<AgentServiceChannelPair> ConnectedAgentServiceChannels { get; private set; } = new List<AgentServiceChannelPair>();

        public void SetLoadBalanceStragety(ILoadBalancer loadBalancer)
        {
            LoadBalancer = loadBalancer;
        }

        /// <summary>
        /// Initializes the update service list timer.
        /// </summary>
        /// <param name="consulSerivceName">Name of the consul serivce.</param>
        /// <param name="freshServiceListInterval">The fresh service list interval.</param>
        private void InitUpdateServiceListTimer(int freshServiceListInterval)
        {
            _freshServiceListInterval = freshServiceListInterval;

            _freshServiceListTimer = new Timer(async obj =>
            {
                await this.DownLoadServiceListAsync();
                _logger.Debug($"{this._freshServiceListInterval}后，继续timer");
            });

            _freshServiceListTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        internal async Task DownLoadServiceListAsync()
        {
            try
            {
                _freshServiceListTimer.Change(Timeout.Infinite, Timeout.Infinite);
                _logger.Debug($"start DownLoadServiceList.");

                //当前正在使用的servicelist
                var currentUsageServiceChannels =
                    ConnectedAgentServiceChannels.ConvertAll(p => p.AgentService);

                var newestService = new List<AgentService>();
                var passOnlyService = await ConsulClient.Health.Service(this.RemoteServiceOption.ServiceName, "", true);

                passOnlyService.Response.ToList().ForEach(p =>
                {
                    newestService.Add(p.Service);
                });

                if (newestService.Count == 0)
                {
                    _logger.Info($"找不到服务  {this.RemoteServiceOption.ServiceName};warning!!!");
                    return;
                }

                //检查consul服务是否有变化；
                var newServices = newestService.Except(currentUsageServiceChannels, new AgentServerComparer());
                var abandonServices = currentUsageServiceChannels.Except(newestService, new AgentServerComparer());
                if (newServices.Count() == 0 && abandonServices.Count() == 0)
                {
                    _logger.Debug($"[{RemoteServiceOption.ServiceName}]服务没有变化");
                    return;
                }

                //update consul服务
                //移除已经失效的channel
                abandonServices.ToList().ForEach(p =>
                {
                    var abandonPair = ConnectedAgentServiceChannels.First(pair => pair.AgentService == p);
                    ConnectedAgentServiceChannels.Remove(abandonPair);
                    abandonPair.Channel.ShutdownAsync();

                    _logger.Info($"移除失效的service: {abandonPair.AgentService.Service}:{abandonPair.AgentService.ID}  {abandonPair.AgentService.Address}:{abandonPair.AgentService.Port}, ");
                });

                //添加新的channel
                newServices.ToList().ForEach(p => AddGrpcChannel(p.Address, p.Port, p));
            }
            catch (Exception ex)
            {
                _logger.Error(ex.ToString());
            }
            finally
            {
                this._freshServiceListTimer.Change(this._freshServiceListInterval, Timeout.Infinite);
                _logger.Debug($"set time for {RemoteServiceOption.ServiceName}, {this._freshServiceListInterval} 后继续downloadServiceList");
            }
        }

        internal void CheckPoolState()
        {
            // download service list
            if (ConnectedAgentServiceChannels.Count == 0)
            {
                DownLoadServiceListAsync().Wait(); //sync
            }

            if (ConnectedAgentServiceChannels.Count == 0)
            {
                throw new Exception($"[no-available-grpc-service->{RemoteServiceOption}]");
            }
        }

        /// <summary>
        /// 判断channel状态,如果channel状态不正常则移除
        /// </summary>
        /// <param name="choosePair"></param>
        /// <returns></returns>
        internal bool CheckAndProcessChannelStatus(AgentServiceChannelPair choosePair)
        {
            //当channel相关的service shutdown之后,该状态一直会处于connecting的状态
            //如果此时采用的是random port,问题比较严重
            //所以,一旦服务检测到挂了之后,就直接清除该connection
            if (choosePair.Channel.State == ChannelState.Shutdown ||
                choosePair.Channel.State == ChannelState.TransientFailure ||
                choosePair.Channel.State == ChannelState.Connecting)
            {
                ConnectedAgentServiceChannels.Remove(choosePair);
                _logger.Error(
                    $"当前Channel异常,状态：{choosePair.Channel.State}  ServiceId:{choosePair.AgentService.ID} ,已经被移除");

                return false;
            }

            return true;
        }

        private void AddGrpcChannel(string address, int port, AgentService agentService)
        {
            var newChannle = new Channel(address, port, ChannelCredentials.Insecure, new List<ChannelOption>());

            var newPair = new AgentServiceChannelPair
            {
                AgentService = agentService,
                Channel = newChannle
            };

            ConnectedAgentServiceChannels.Add(newPair);
            _logger.Info($"添加新的service: {newPair.AgentService?.Service}:{newPair.AgentService?.ID}  {newPair.AgentService?.Address}:{newPair.AgentService?.Port}, ");
        }

        /// <summary>
        /// fetch one channel
        /// </summary>
        /// <exception cref="Exception"></exception>
        public Channel FetchOneChannel => FetchOneAgentServiceChannelPair.Channel;

        /// <summary>
        /// Gets the fetch one agent service channel pair.
        /// </summary>
        /// <value>
        /// The fetch one agent service channel pair.
        /// </value>
        /// <exception cref="Exception"></exception>
        public AgentServiceChannelPair FetchOneAgentServiceChannelPair => LoadBalancer.SelectEndpoint(this.GrpcSrvName);

        public static Lazy<List<GRPCChannelPoolManager>> Instances = new Lazy<List<GRPCChannelPoolManager>>(() => new List<GRPCChannelPoolManager>(), true);
    }

    public class AgentServiceChannelPair
    {
        /// <summary>
        /// Gets or sets the agent service.
        /// </summary>
        /// <value>
        /// The agent service.
        /// </value>
        public AgentService AgentService { get; set; }

        /// <summary>
        /// Gets or sets the channel.
        /// </summary>
        /// <value>
        /// The channel.
        /// </value>
        public Channel Channel { get; set; }

        /// <summary>
        /// Gets or sets the proxy.
        /// </summary>
        /// <value>
        /// The proxy.
        /// </value>
        public Object Proxy { get; set; }
    }
}
