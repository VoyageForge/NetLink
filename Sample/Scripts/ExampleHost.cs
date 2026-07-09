using UnityEngine;
using System.Net;
using LANServiceDiscovery.Runtime;

namespace LANServiceDiscovery.Sample
{
    /// <summary>
    /// 服务端示例：继承 <see cref="UdpDiscoveryHostBase"/>，默认处理 DiscoveryRequest 并回复本机 IP。
    /// <para>
    /// <b>使用方式：</b>将此脚本挂载到场景中的任意 GameObject 上，设置监听端口后启动。
    /// 客户端广播 DiscoveryRequest 后，服务端自动回复本机局域网 IPv4 地址。
    /// </para>
    /// <para>
    /// <b>扩展方式：</b>重写 <see cref="UdpDiscoveryHostBase.OnDataReceived"/> 处理自定义命令码，
    /// 通过 <see cref="UdpDiscoveryHostBase.Reader"/> 读取数据、<see cref="UdpDiscoveryHostBase.Writer"/> 构建回复。
    /// </para>
    /// </summary>
    public class ExampleHost : UdpDiscoveryHostBase
    {
        [Header("UDP 监听端口")]
        [Tooltip("服务端监听的 UDP 端口，客户端需广播到同一端口")]
        public int listenPort = 8888;

        /// <summary>构造时传入默认端口 8888</summary>
        public ExampleHost() : base(8888) { }

        /// <summary>MonoBehaviour 启动时自动开始监听</summary>
        public void Start() => StartSync();

        /// <summary>
        /// 收到客户端发现请求时调用（由基类 <see cref="UdpDiscoveryHostBase.OnDataReceived"/> 触发）。
        /// 返回包含本机局域网 IP 的回复帧。
        /// </summary>
        /// <param name="clientEndpoint">客户端网络地址</param>
        /// <returns>用 <see cref="PacketWriter"/> 构建的完整回复帧，包含 IP 字符串</returns>
        protected override byte[] OnDiscoveryRequest(IPEndPoint clientEndpoint)
        {
            string localIP = GetLocalIPAddress();
            Debug.Log($"收到来自 {clientEndpoint.Address}:{clientEndpoint.Port} 的发现请求，回复 IP: {localIP}");

            // 用 Writer 构建回复：写入 IP 字符串 → 编码为 DiscoveryReply 帧
            return Writer
                .WriteString(localIP)
                .Encode(DiscoveryOpcode.DiscoveryReply);
        }

        /// <summary>
        /// 监听成功启动时的回调。
        /// </summary>
        protected override void OnListenStarted(int port)
        {
            Debug.Log($"<color=green>UDP 发现服务已启动 → 0.0.0.0:{port}</color>");
        }

        /// <summary>
        /// 收到原始 UDP 数据时的回调（解码之前），用于网络连通性诊断。
        /// </summary>
        protected override void OnRawDataReceived(IPEndPoint remote, int byteCount)
        {
            Debug.Log($"收到原始 UDP 数据报: {remote.Address}:{remote.Port} → {byteCount} 字节");
        }

        /// <summary>
        /// 监听循环异常时的回调。
        /// </summary>
        protected override void OnListenError(System.Exception ex)
        {
            Debug.LogError($"UDP 监听异常: {ex.Message}");
        }

        /// <summary>
        /// 获取本机第一个可用的局域网 IPv4 地址。
        /// 遍历所有网络接口，返回第一个 InterNetwork 地址。
        /// </summary>
        private string GetLocalIPAddress()
        {
            foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    return ip.ToString();
            }
            return "127.0.0.1";
        }

        /// <summary>GameObject 销毁时停止监听</summary>
        public void Destroy() => Stop();
    }
}
