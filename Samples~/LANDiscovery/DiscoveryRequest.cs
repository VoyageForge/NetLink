using System;
using VoyageForge.NetLink.Runtime;

namespace VoyageForge.NetLink.Samples.LANDiscovery
{
    /// <summary>发现请求（空负载，无序列化数据）</summary>
    public class DiscoveryRequest : Payload
    {
        public DiscoveryRequest() => Cmd = (byte)DiscoveryOpcode.DiscoveryRequest;

        public override byte[] Serialize() => Array.Empty<byte>();

        public override void Deserialize(byte[] data) { }
    }

    /// <summary>发现回复（JSON 序列化）</summary>
    public class DiscoveryReply : JsonPayload
    {
        public string[] Ips;

        public DiscoveryReply() => Cmd = (byte)DiscoveryOpcode.DiscoveryReply;

        public DiscoveryReply(params string[] ips)
        {
            Cmd = (byte)DiscoveryOpcode.DiscoveryReply;
            Ips = ips;
        }
    }
}
