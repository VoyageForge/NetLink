using UnityEngine;
using VoyageForge.NetLink.Runtime;

namespace VoyageForge.NetLink.Samples.LANDiscovery
{
    /// <summary>服务端示例：收到 DiscoveryRequest 后回复空 DiscoveryReply</summary>
    public class ExampleHost : UdpDiscoveryHostBase
    {
        [Header("UDP 监听端口")] public int listenPort = 8888;

        public ExampleHost() : base(8888) { }

        public void Start()
        {
            Codec.On<DiscoveryRequest>(async msg =>
            {
                Debug.Log($"收到来自 {msg.Remote.Address} 的发现请求");

                byte[] frame = Codec.Encode(new DiscoveryReply());
                await ReplyAsync(frame, msg.Remote);
            });

            StartSync();
        }

        protected override void OnListenStarted(int port)
            => Debug.Log($"<color=green>UDP 监听: 0.0.0.0:{port}</color>");

        protected override void OnListenError(System.Exception ex)
            => Debug.LogError($"异常: {ex.Message}");

        public void Destroy() => Stop();
    }
}
