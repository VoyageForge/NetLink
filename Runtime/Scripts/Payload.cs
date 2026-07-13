namespace LANServiceDiscovery.Runtime
{
    /// <summary>负载基类。Cmd + Serialize/Deserialize 配对。</summary>
    public abstract class Payload
    {
        /// <summary>命令码</summary>
        public byte Cmd { get; set; }

        /// <summary>序列化为字节</summary>
        public virtual byte[] Serialize() => new byte[] { Cmd };

        /// <summary>从字节反序列化</summary>
        public virtual void Deserialize(byte[] data) { }
    }
}
