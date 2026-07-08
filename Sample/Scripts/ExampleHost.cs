using UnityEngine;
using System.Net;
using LANServiceDiscovery.Runtime;

namespace LANServiceDiscovery.Sample
{
    public class ExampleHost : UdpDiscoveryHostBase
    {
        [Header("UDP 监听端口")]
        public int listenPort = 8888;

        public ExampleHost() : base(8888) { }

        public void Start() => StartSync();

        protected override byte[] OnDiscoveryRequest(IPEndPoint clientEndpoint)
        {
            string localIP = GetLocalIPAddress();
            Debug.Log($"收到来自 {clientEndpoint} 的发现请求，回复 IP: {localIP}");

            return Writer
                .WriteString(localIP)
                .Encode(DiscoveryOpcode.DiscoveryReply);
        }

        // 扩展示例：处理自定义命令码
        // protected override async Task OnDataReceived()
        // {
        //     if (Reader.Cmd == (byte)MyOpcode.CustomRequest)
        //     {
        //         string name = Reader.ReadString();
        //         var reply = Writer.WriteString("ok").Encode(MyOpcode.CustomReply);
        //         await ReplyAsync(reply, RemoteEndPoint);
        //         return;
        //     }
        //     await base.OnDataReceived();
        // }

        protected override void OnListenStarted(int port)
        {
            Debug.Log($"<color=green>UDP 监听已启动: 0.0.0.0:{port}</color>");
        }

        protected override void OnRawDataReceived(IPEndPoint remote, int byteCount)
        {
            Debug.Log($"收到原始 UDP: {remote} → {byteCount} 字节");
        }

        protected override void OnListenError(System.Exception ex)
        {
            Debug.LogError($"UDP 监听异常: {ex.Message}");
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

        public void Destroy() => Stop();
    }
}
