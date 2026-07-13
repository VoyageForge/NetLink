using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Net;
using LANServiceDiscovery.Runtime;

namespace LANServiceDiscovery.Sample
{
    /// <summary>服务端示例：注册 DiscoveryRequest 处理器，回复本机 IP 列表</summary>
    public class ExampleHost : UdpDiscoveryHostBase
    {
        [Header("UDP 监听端口")] public int listenPort = 8888;

        public ExampleHost() : base(8888) { }

        public void Start()
        {
            Codec.On<DiscoveryRequest>(async msg =>
            {
                var ips = GetLocalIPAddress();
                Debug.Log($"收到发现请求，回复 {ips.Count} 个 IP");

                byte[] frame = Codec.Encode(new DiscoveryReply(ips.ToArray()));
                await ReplyAsync(frame, RemoteEndPoint);
            });

            StartSync();
        }

        protected override void OnListenStarted(int port)
            => Debug.Log($"<color=green>UDP 监听: 0.0.0.0:{port}</color>");

        protected override void OnListenError(System.Exception ex)
            => Debug.LogError($"异常: {ex.Message}");

        private List<string> GetLocalIPAddress()
        {
            return (from ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList
                    where ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                    select ip.ToString()).ToList();
        }

        public void Destroy() => Stop();
    }
}
