using Consul;
using Grpc.Extension.Model;
using System;
using System.Threading;
using Grpc.Core.Logging;
using System.Threading.Tasks;

namespace Grpc.Extension.Registers
{
    public class ServiceRegister
    {
        ILogger _logger => Core.GrpcEnvironment.Logger.ForType<ServiceRegister>();

        public bool RegisterEnable => LocalServiceOption.Instance.ConsulIntegration;

        private Timer _timerTTL;

        /// <summary>
        /// 用于标识服务ID
        /// </summary>
        private string _id;

        public ServiceRegister()
        {
            _id = Guid.NewGuid().ToString();
        }

        /// <summary>
        /// 注册服务到consul
        /// </summary>
        public void RegisterService()
        {
            if (!RegisterEnable)
            {
                _logger.Info("当前配置不需要注册服务!");
                return;
            }

            RegisterServiceCore();

            _timerTTL = new Timer(state => DoTTLAsync().Wait(), null, Timeout.Infinite, Timeout.Infinite);
            DoTTLAsync().Wait();
        }

        private void RegisterServiceCore()
        {
            using (var client = CreateConsulClient())
            {
                var registration = new AgentServiceRegistration()
                {
                    ID = GetServiceId(),
                    Name = LocalServiceOption.Instance.ServiceName,
                    Tags = LocalServiceOption.Instance.ConsulTags?.Split(','),
                    EnableTagOverride = true,
                    Address = MetaModel.Ip,
                    Port = MetaModel.Port,
                    Check = new AgentCheckRegistration
                    {
                        ID = GetTTLCheckId(),
                        Name = "ttlcheck",
                        TTL = TimeSpan.FromSeconds(15),
                        Status = HealthStatus.Passing,
                        DeregisterCriticalServiceAfter = TimeSpan.FromMinutes(1),
                    }
                };
                client.Agent.ServiceRegister(registration).Wait();
            }

            _logger.Info("RegisterServiceCore success!");
        }

        /// <summary>
        /// 从consul反注册
        /// </summary>
        public void DeregisterService()
        {
            if (!RegisterEnable) return;

            using (var client = CreateConsulClient())
            {
                client.Agent.ServiceDeregister(GetServiceId()).Wait();
                _logger.Info("DeregisterService success!");
            }
        }

        private ConsulClient CreateConsulClient(string consulUrl = null)
        {
            return new ConsulClient(conf => conf.Address = new Uri(!string.IsNullOrWhiteSpace(consulUrl) ?
                consulUrl : LocalServiceOption.Instance.ConsulAddress));
        }

        private string GetServiceId()
        {
            return $"{LocalServiceOption.Instance.ServiceName}-{(MetaModel.Ip)}-{(MetaModel.Port)}-{_id}";
        }

        private string GetTTLCheckId()
        {
            return $"service:{GetServiceId()}";
        }

        private async Task DoTTLAsync()
        {
            _timerTTL.Change(Timeout.Infinite, Timeout.Infinite);
            Exception err = null;
            try
            {
                using (var client = CreateConsulClient())
                {
                    await client.Agent.PassTTL(GetTTLCheckId(), "timer:" + DateTime.Now);
                }
            }
            catch (Exception ex)
            {
                /*
                 * passTTL会出现如下几种情况：
                 * 1. consul服务重启中，ex会显示 connection refused by ip:port
                 *          这种情况下，不去处理，等consul服务重启之后就好了
                 * 2. consul服务重启之后，会丢失之前的service，check，会有如下的错误：
                 *          Unexpected response, status code InternalServerError: CheckID "followme.srv.sms-192.168.3.10-10086-07f21040-0be9-4a73-b0a1-71755c6d6d46:ttlcheck" does not have associated TTL
                 *          在这种情况下，需要处理，重新注册服务，check；     
                 */
                if (ex.ToString().Contains($"CheckID \"{GetTTLCheckId()}\" does not have associated TTL"))
                {
                    RegisterServiceCore();
                }
                err = ex;
            }
            finally
            {
                _timerTTL.Change(TimeSpan.FromSeconds(LocalServiceOption.Instance.TCPInterval),
                    TimeSpan.FromSeconds(LocalServiceOption.Instance.TCPInterval));
            }

            _logger.Debug($"passing TTL:{err}");
        }
    }
}
