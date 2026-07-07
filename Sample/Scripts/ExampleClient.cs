using UnityEngine;
using System.Threading.Tasks;
using LANServiceDiscovery.Runtime;

namespace LANServiceDiscovery.Sample
{
    /// <summary>
    /// 客户端示例：发现 IP 后打印日志，超时时自动重试。
    /// </summary>
    public class ExampleClient : UdpDiscoveryClientBase
    {
        [Header("UDP 广播端口")]
        public int broadcastPort = 8888;

        [Header("最大重试次数（0 = 无限）")]
        public int maxRetries;

        [Header("重试间隔（秒）")]
        public float retryInterval = 1f;

        private int _retryCount;

        public ExampleClient() : base(8888, 3f) { }

        public void Start()
        {
            Task.Run(StartDiscoveryAsync);
        }

        protected override void OnHostDiscovered(string ip)
        {
            _retryCount = 0;
            Debug.Log($"发现服务端: {ip}");
        }

        /// <summary>
        /// 超时时由基类在 while 循环内 await，返回 true 继续循环，false 退出。
        /// </summary>
        protected override async Task<bool> OnDiscoveryTimeout()
        {
            _retryCount++;

            if (maxRetries > 0 && _retryCount > maxRetries)
            {
                Debug.LogError($"重试 {maxRetries} 次后仍未发现服务端，已停止");
                return false;
            }

            Debug.LogWarning($"未收到服务端回复，即将重试... ({_retryCount}/{(maxRetries > 0 ? maxRetries.ToString() : "∞")})");
            await Task.Delay((int)(retryInterval * 1000));
            return true;
        }

        protected override void OnDiscoveryError(System.Exception ex)
        {
            Debug.LogError($"异常: {ex.Message}");
        }

        public void Destroy()
        {
            Dispose();
        }
    }
}
