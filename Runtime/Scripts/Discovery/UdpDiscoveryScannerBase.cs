using VoyageForge.NetLink.Runtime;

namespace VoyageForge.NetLink.Discovery
{
    /// <summary>
    /// 局域网「遍历查找」客户端基类。
    /// <para>遍历能力（<see cref="UdpDiscoveryClientBase.EnumerateHosts"/> / ScanHostsAsync / ScanNetworkAsync）
    /// 已上移到 <see cref="UdpDiscoveryClientBase"/>，广播与遍历共用同一 socket / 编解码，并支持退化模式。</para>
    /// <para>保留本类作为语义清晰的「遍历」入口；也可以直接继承 <see cref="UdpDiscoveryClientBase"/>。</para>
    /// </summary>
    public abstract class UdpDiscoveryScannerBase : UdpDiscoveryClientBase
    {
        protected UdpDiscoveryScannerBase(int port) : base(port) { }
    }
}
