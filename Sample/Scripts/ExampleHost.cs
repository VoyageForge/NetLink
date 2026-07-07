using UnityEngine;
using System.Net;
using System.Text;
using LANServiceDiscovery.Runtime;

namespace LANServiceDiscovery.Sample
{
    /// <summary>
    /// 服务端示例：继承 UdpDiscoveryHostBase，回复本机 IP
    /// </summary>
    public class ExampleHost : UdpDiscoveryHostBase
    {
        [Header("UDP 监听端口")] public int listenPort = 8888;

        public ExampleHost() : base(8888)
        {
        }

       public void Start()
        {
            StartSync();
        }

        /// <summary>
        /// 收到发现请求时，回复本机局域网 IPv4 地址
        /// </summary>
        protected override byte[] OnDiscoveryRequest(IPEndPoint clientEndpoint)
        {
            string localIP = GetLocalIPAddress();
            Debug.Log($"收到来自 {clientEndpoint} 的发现请求，回复 IP: {localIP}");
            // 回复命令码 0x02，数据为 IP 字符串
            return PacketCodec.Encode(0x02, Encoding.UTF8.GetBytes(localIP));
        }

        protected override void OnListenStarted(int port)
        {
            Debug.Log($"<color=green>UDP 监听已启动: 0.0.0.0:{port}</color>");
        }

        protected override void OnRawDataReceived(IPEndPoint remote, int byteCount)
        {
            Debug.Log($"收到原始 UDP 数据: {remote} → {byteCount} 字节");
            
            
        }

        private string GetLocalIPAddress()
        {
            foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    return ip.ToString();
            }

            return "127.0.0.1";
        }

        protected override void OnListenError(System.Exception ex)
        {
            Debug.LogError($"UDP 监听异常: {ex.Message}");
        }

        public void Destroy()
        {
            Stop();
        }
    }
}