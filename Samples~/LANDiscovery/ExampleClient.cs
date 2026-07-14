using UnityEngine;
using System.Threading.Tasks;
using VoyageForge.NetLink.Runtime;

namespace VoyageForge.NetLink.Samples.LANDiscovery
{
    /// <summary>客户端示例：注册 DiscoveryReply 处理器，定时发送发现请求</summary>
    public class ExampleClient : UdpDiscoveryClientBase
    {
        [Header("广播端口")] public int broadcastPort = 8888;
        [Header("最大重试")] public int maxRetries = 5;
        [Header("重试间隔")] public float retryInterval = 2f;

        private int _retryCount;
        private bool _discovered;

        public ExampleClient() : base(8888) { }

        public async Task StartDiscoveryAsync()
        {
            _discovered = false;
            _retryCount = 0;

            // 注册处理器：收到 DiscoveryReply 时回调
            Codec.On<DiscoveryReply>(msg =>
            {
                _discovered = true;
                Debug.Log($"<color=green>发现服务端: {RemoteEndPoint.Address}</color>");
            });

            Start();

            while (!_discovered && (maxRetries == 0 || _retryCount < maxRetries))
            {
                _retryCount++;
                Debug.Log($"广播... ({_retryCount}/{(maxRetries > 0 ? maxRetries.ToString() : "∞")})");
                await SendAsync(new DiscoveryRequest());
                await Task.Delay((int)(retryInterval * 1000));
            }

            if (!_discovered)
            {
                Debug.LogError("放弃");
            }
            else
            {
                Debug.Log("发现完成");
            }
        }

        protected override void OnStarted() => Debug.Log("客户端已启动");
        protected override void OnError(System.Exception ex) => Debug.LogError($"异常: {ex.Message}");
        public void Destroy() => Stop();
    }
}
