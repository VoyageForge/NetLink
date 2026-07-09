using UnityEngine;
using System.Threading.Tasks;
using LANServiceDiscovery.Runtime;

namespace LANServiceDiscovery.Sample
{
    /// <summary>
    /// 客户端示例：继承 <see cref="UdpDiscoveryClientBase"/>，广播发现请求并自动重试。
    /// <para>
    /// <b>使用方式：</b>将此脚本挂载到场景中的任意 GameObject 上，设置参数后启动。
    /// 客户端会持续广播直到收到服务端回复或达到最大重试次数。
    /// </para>
    /// <para>
    /// <b>可配置参数：</b>
    /// - <c>maxRetries</c>：最大重试次数（0 = 无限重试）
    /// - <c>retryInterval</c>：每次重试间隔（秒）
    /// </para>
    /// <para>
    /// <b>扩展方式：</b>重写 <see cref="UdpDiscoveryClientBase.OnDataReceived"/> 处理自定义命令码，
    /// 通过 <see cref="UdpDiscoveryClientBase.Reader"/> 读取回复数据。
    /// </para>
    /// </summary>
    public class ExampleClient : UdpDiscoveryClientBase
    {
        [Header("UDP 广播端口")]
        [Tooltip("广播目标端口，需与服务端监听端口一致")]
        public int broadcastPort = 8888;

        [Header("重试设置")]
        [Tooltip("最大重试次数，0 表示无限重试")]
        public int maxRetries;

        [Tooltip("每次重试的等待间隔（秒）")]
        public float retryInterval = 1f;

        /// <summary>当前已重试次数</summary>
        private int _retryCount;

        /// <summary>构造时传入端口 8888、超时 3 秒</summary>
        public ExampleClient() : base(8888, 3f) { }

        /// <summary>MonoBehaviour 启动时自动开始发现（后台线程执行）</summary>
        public void Start() => Task.Run(StartDiscoveryAsync);

        /// <summary>
        /// 发现服务端 IP 时调用（由基类 <see cref="UdpDiscoveryClientBase.OnDataReceived"/> 触发）。
        /// </summary>
        /// <param name="ip">发现的 IP 地址</param>
        protected override void OnHostDiscovered(string ip)
        {
            _retryCount = 0; // 重置重试计数
            Debug.Log($"<color=green>发现服务端: {ip}</color>");
        }

        /// <summary>
        /// 发现超时时调用（由基类重试循环触发）。
        /// <para>
        /// 记录重试次数 → 检查是否达到上限 → 等待一段间隔 → 返回 true 告诉基类继续重试。
        /// <see cref="CancellationToken"/> 确保程序关闭时 <see cref="Task.Delay"/> 能立即退出。
        /// </para>
        /// </summary>
        /// <param name="token">取消令牌</param>
        /// <returns>true 继续重试，false 停止</returns>
        protected override async Task<bool> OnDiscoveryTimeout(System.Threading.CancellationToken token)
        {
            _retryCount++;

            // 达到最大重试次数则放弃
            if (maxRetries > 0 && _retryCount > maxRetries)
            {
                Debug.LogError($"<color=red>已重试 {maxRetries} 次，仍未发现服务端，放弃</color>");
                return false;
            }

            string countInfo = maxRetries > 0 ? $"{_retryCount}/{maxRetries}" : $"{_retryCount}/∞";
            Debug.LogWarning($"未收到服务端回复，{retryInterval}s 后重试... ({countInfo})");

            // 传入 token，程序关闭时立即取消等待
            await Task.Delay((int)(retryInterval * 1000), token);
            return true;
        }

        /// <summary>
        /// 发现过程中发生异常时的回调。
        /// </summary>
        protected override void OnDiscoveryError(System.Exception ex)
        {
            Debug.LogError($"发现过程异常: {ex.Message}");
        }

        /// <summary>GameObject 销毁时停止发现</summary>
        public void Destroy() => Dispose();
    }
}
