namespace Grpc.Extension.LoadBalancer
{
    public interface ILoadBalancer
    {
        AgentServiceChannelPair SelectEndpoint(string serviceName);
    }
}