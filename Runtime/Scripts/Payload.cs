using System.Text;
using Newtonsoft.Json;

namespace VoyageForge.NetLink.Runtime
{
    /// <summary>负载抽象基类</summary>
    public abstract class Payload
    {
        /// <summary>命令码</summary>
        public byte Cmd { get; set; }

        /// <summary>序列化为字节</summary>
        public abstract byte[] Serialize();

        /// <summary>从字节反序列化</summary>
        public abstract void Deserialize(byte[] data);
    }

   
}
