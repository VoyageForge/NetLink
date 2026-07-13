namespace LANServiceDiscovery.Runtime
{
    /// <summary>内置操作码枚举（与 <see cref="DiscoveryRequest"/> / <see cref="DiscoveryReply"/> 配套）</summary>
    public enum DiscoveryOpcode : byte
    {
        /// <summary>发现请求（0x01）</summary>
        DiscoveryRequest = 0x01,
        /// <summary>发现回复（0x02）</summary>
        DiscoveryReply = 0x02,
    }
}
