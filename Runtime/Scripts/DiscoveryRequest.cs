using System.Text;
using Newtonsoft.Json;

namespace LANServiceDiscovery.Runtime
{
    /// <summary>
    /// 发现请求
    /// </summary>
    public class DiscoveryRequest : Payload
    {
        public DiscoveryRequest() => Cmd = (byte)DiscoveryOpcode.DiscoveryRequest;
    }

    /// <summary>
    /// 发现回复
    /// </summary>
    public class DiscoveryReply : Payload
    {
        public string[] Ips;

        public DiscoveryReply() => Cmd = (byte)DiscoveryOpcode.DiscoveryReply;

        public DiscoveryReply(params string[] ips)
        {
            Cmd = (byte)DiscoveryOpcode.DiscoveryReply;
            Ips = ips;
        }

        public override byte[] Serialize() =>
            Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(Ips ?? new string[0]));

        public override void Deserialize(byte[] data) =>
            Ips = JsonConvert.DeserializeObject<string[]>(Encoding.UTF8.GetString(data));
    }
}
