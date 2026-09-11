using UnityEngine;
using System.Threading.Tasks;
using VoyageForge.NetLink.Runtime;
using VoyageForge.NetLink.Discovery;

namespace VoyageForge.NetLink.Samples.LANDiscovery
{
    /// <summary>客户端示例：注册 DiscoveryReply 处理器，广播发现失败后自动退化到遍历网段（复用 Runtime 的 DiscoverWithFallbackAsync）</summary>
    public class ExampleClient : UdpDiscoveryClientBase
    {
        [Header("广播端口")] public int broadcastPort = 8888;
        [Header("最大重试")] public int maxRetries = 5;
        [Header("重试间隔")] public float retryInterval = 2f;

        private bool _discovered;

        /// <summary>发现回复订阅句柄，Destroy 时退订。</summary>
        private System.IDisposable _subscription;

        public ExampleClient() : base(8888)
        {
            // 注册处理器：收到 DiscoveryReply 时回调
            _subscription = Codec.On<DiscoveryReply>(msg =>
            {
                _discovered = true;
                Debug.Log($"<color=green>发现服务端: {msg.Remote.Address}</color>");
            });
        }

        public async Task StartDiscoveryAsync()
        {
            _discovered = false;
            Start();

            // 广播发现；搜不到时自动退化到遍历网段（FallbackEnabled/GetFallbackNetwork 可覆盖定制）
            bool found = await DiscoverWithFallbackAsync(
                new DiscoveryRequest(), maxRetries, retryInterval, () => _discovered, Cts.Token);

            if (Cts.Token.IsCancellationRequested) return;   // 停止播放 / Stop()：静默退出

            if (!found) Debug.LogError("放弃");
            else Debug.Log("发现完成");
        }

        protected override void OnStarted() => Debug.Log("客户端已启动");
        protected override void OnError(System.Exception ex) => Debug.LogError($"异常: {ex.Message}");

        public void Destroy()
        {
            _subscription?.Dispose();
            _subscription = null;
            Stop();
        }
    }
}
