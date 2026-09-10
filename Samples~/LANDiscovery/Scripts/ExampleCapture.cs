using System.Net;
using System.Threading.Tasks;
using UnityEngine;
using VoyageForge.NetLink.Discovery;
using VoyageForge.NetLink.Runtime;

namespace VoyageForge.NetLink.Samples.LANDiscovery
{
    /// <summary>
    /// 专用「抓包」示例：跳过广播，一启动就**直接遍历真实局域网网段**，
    /// 按并发度分批向整个网段发送 UDP 探测包（DiscoveryRequest），产生可被另一台机器抓包观察的流量。
    ///
    /// <para><b>抓包提示</b>：在另一台机器用 Wireshark 过滤 <c>udp.port == 8888</c>，
    /// 即可看到本机发出的成批探测包（目标为网段内每个主机 IP 的 8888 端口）。</para>
    ///
    /// <para>与 <see cref="ExampleScanner"/> 的区别：ExampleScanner 是「广播 → 退化遍历」，
    /// 广播阶段只有 1 个广播包；本类直接遍历整个网段，能产生大量（254 个）探测包，更适合抓包。</para>
    /// </summary>
    public class ExampleCapture : UdpDiscoveryScannerBase
    {
        [Header("遍历网段(留空=自动探测本机网段)")] public string network = "";
        [Header("子网掩码")] public string subnetMask = "255.255.255.0";
        [Header("并发度(每批主机数)")] public int concurrency = 64;
        [Header("每批响应超时(ms)")] public int perHostTimeoutMs = 200;
        [Header("遍历轮数")] public int rounds = 1;

        /// <summary>固定抓包端口为 8888。</summary>
        public ExampleCapture() : base(8888) { }

        // ==================== 精细控制钩子 ====================

        /// <summary>并发度：每批同时探测的主机数。</summary>
        protected override int GetScanConcurrency() => concurrency;

        /// <summary>每批响应等待超时（毫秒）。</summary>
        protected override int GetFallbackPerHostTimeoutMs() => perHostTimeoutMs;

        // ==================== 抓包流程 ====================

        /// <summary>
        /// 开始抓包遍历：解析遍历网段，连续 <paramref name="rounds"/> 轮遍历整个网段，
        /// 每轮按并发度分批发送 DiscoveryRequest。
        /// </summary>
        public async Task StartCaptureAsync()
        {
            Start();

            var (net, mask) = ResolveNetwork();

            for (int i = 0; i < rounds && !Cts.Token.IsCancellationRequested; i++)
            {
                Debug.Log($"<color=cyan>[抓包] 第 {i + 1}/{rounds} 轮遍历: {net}/{mask} → 端口 8888</color>");
                await ScanNetworkAsync(net, mask, new DiscoveryRequest(), perHostTimeoutMs, Cts.Token);
            }

            Debug.Log("<color=cyan>[抓包] 遍历完成</color>");
        }

        /// <summary>停止。</summary>
        public void Destroy() => Stop();

        /// <summary>解析遍历网段：留空则用基类默认（自动探测本机默认路由网段）。</summary>
        private (IPAddress Network, IPAddress SubnetMask) ResolveNetwork()
        {
            if (!string.IsNullOrWhiteSpace(network))
                return (IPAddress.Parse(network), IPAddress.Parse(subnetMask));

            var (net, mask) = GetFallbackNetwork();
            return (net ?? IPAddress.Parse("192.168.2.0"), mask ?? IPAddress.Parse("255.255.255.0"));
        }
    }
}
