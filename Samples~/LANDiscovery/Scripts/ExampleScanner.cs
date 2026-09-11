using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;
using VoyageForge.NetLink.Discovery;
using VoyageForge.NetLink.Runtime;

namespace VoyageForge.NetLink.Samples.LANDiscovery
{
    /// <summary>
    /// 遍历 / 退化查找「客户端」示例。
    /// <para>演示如何继承 <see cref="UdpDiscoveryScannerBase"/>，并通过覆盖钩子精细控制：
    /// 并发度（<see cref="GetScanConcurrency"/>）、每批响应超时（<see cref="GetFallbackPerHostTimeoutMs"/>）、
    /// 退化网段（<see cref="GetFallbackNetwork"/>）。扫描到的设备通过 <c>Codec.On&lt;DiscoveryReply&gt;</c> 收集。</para>
    /// </summary>
    public class ExampleScanner : UdpDiscoveryScannerBase
    {
        [Header("目标端口")] public int port = 8888;
        [Header("广播最大重试")] public int maxRetries = 3;
        [Header("广播重试间隔(秒)")] public float retryInterval = 1f;
        [Header("并发度(每批主机数)")] public int concurrency = 64;
        [Header("每批响应超时(ms)")] public int perHostTimeoutMs = 100;
        [Header("退化网段")] public string fallbackNetwork = "192.168.2.0";
        [Header("退化掩码")] public string fallbackMask = "255.255.255.0";

        /// <summary>已发现的设备列表（Remote 地址）。跨线程访问，内部加锁。</summary>
        public IReadOnlyList<IPEndPoint> Devices
        {
            get { lock (_devices) return _devices.ToArray(); }
        }

        /// <summary>收集到的设备（后台接收线程写入，需加锁）。</summary>
        private readonly List<IPEndPoint> _devices = new List<IPEndPoint>();

        /// <summary>发现回复订阅句柄，Destroy 时退订。</summary>
        private System.IDisposable _subscription;

        public ExampleScanner() : base(8888)
        {
            // 收到 DiscoveryReply 时记录来源地址（即服务端地址）
            _subscription = Codec.On<DiscoveryReply>(msg =>
            {
                lock (_devices) _devices.Add(msg.Remote);
                Debug.Log($"<color=green>发现设备: {msg.Remote.Address}</color>");
            });
        }

        // ==================== 覆盖钩子：精细控制遍历 / 退化行为 ====================

        /// <summary>并发度：每批同时探测的主机数（设为 1 即串行遍历）。</summary>
        protected override int GetScanConcurrency() => concurrency;

        /// <summary>退化遍历时，每批响应等待超时（毫秒）。</summary>
        protected override int GetFallbackPerHostTimeoutMs() => perHostTimeoutMs;

        /// <summary>退化遍历的网段（默认取本机网段，这里改为 Inspector 可配置）。</summary>
        protected override (IPAddress Network, IPAddress SubnetMask) GetFallbackNetwork()
            => (IPAddress.Parse(fallbackNetwork), IPAddress.Parse(fallbackMask));

        // ==================== 启动 ====================

        /// <summary>启动扫描：先广播，广播未发现则自动退化到「并发遍历网段」。</summary>
        public async Task StartScanAsync()
        {
            _devices.Clear();
            Start();

            // DiscoverWithFallbackAsync：广播 → 退化为并发遍历
            bool found = await DiscoverWithFallbackAsync(
                new DiscoveryRequest(), maxRetries, retryInterval,
                () => { lock (_devices) return _devices.Count > 0; },
                Cts.Token);

            if (Cts.Token.IsCancellationRequested) return;

            if (found) Debug.Log($"发现完成，共 {Devices.Count} 台设备");
            else Debug.LogWarning("未发现任何设备");
        }

        /// <summary>停止并退订。</summary>
        public void Destroy()
        {
            _subscription?.Dispose();
            _subscription = null;
            Stop();
        }
    }
}
