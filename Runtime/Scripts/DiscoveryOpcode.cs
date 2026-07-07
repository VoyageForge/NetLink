namespace LANServiceDiscovery.Runtime
{
    /// <summary>
    /// 内置协议操作码（命令码）。
    /// <para>
    /// <b>如何扩展自定义操作码：</b>
    /// C# 枚举不支持继承，框架为扩展提供了两条路径：
    /// </para>
    /// <list type="number">
    /// <item>
    /// <b>定义自己的枚举并转换为 byte：</b>
    /// <code>public enum MyOpcode : byte { MyCmd = 0x10 }
    /// PacketCodec.Encode((byte)MyOpcode.MyCmd, data);</code>
    /// </item>
    /// <item>
    /// <b>重写基类的虚方法以识别新操作码：</b>
    /// 在 <see cref="UdpDiscoveryHostBase"/> 子类中重写 <c>IsDiscoveryRequest(byte)</c>，
    /// 在 <see cref="UdpDiscoveryClientBase"/> 子类中重写 <c>IsDiscoveryReply(byte)</c>。
    /// </item>
    /// </list>
    /// </summary>
    public enum DiscoveryOpcode : byte
    {
        /// <summary>客户端广播的发现请求</summary>
        DiscoveryRequest = 0x01,

        /// <summary>服务端回复的 IP 地址</summary>
        DiscoveryReply = 0x02,
    }
}
