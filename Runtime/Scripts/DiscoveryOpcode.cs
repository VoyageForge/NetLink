namespace LANServiceDiscovery.Runtime
{
    /// <summary>
    /// 内置协议操作码（命令码），底层存储类型为 <see cref="byte"/>（范围 0~255）。
    /// <para>
    /// <b>为什么用枚举而不是类常量？</b>
    /// 枚举提供编译期类型检查，防止意外传入无效值，同时保留 (byte) 转换兼容自定义扩展。
    /// </para>
    /// <para>
    /// <b>如何扩展自定义操作码？</b>
    /// C# 枚举不支持继承，框架提供两条扩展路径：
    /// </para>
    /// <list type="number">
    /// <item>
    /// <b>定义自己的枚举 + 转为 byte：</b>
    /// <code>
    /// public enum MyOpcode : byte { CustomCmd = 0x10 }
    /// PacketCodec.Encode((byte)MyOpcode.CustomCmd, data);
    /// </code>
    /// </item>
    /// <item>
    /// <b>重写基类 <c>OnDataReceived</c> 虚方法：</b>
    /// <code>
    /// protected override Task OnDataReceived()
    /// {
    ///     if (Reader.Cmd == (byte)MyOpcode.CustomCmd)
    ///     {
    ///         // 自定义处理逻辑
    ///         return;
    ///     }
    ///     await base.OnDataReceived();
    /// }
    /// </code>
    /// </item>
    /// </list>
    /// </summary>
    public enum DiscoveryOpcode : byte
    {
        /// <summary>
        /// 发现请求（0x01）。
        /// 客户端广播此操作码以搜索局域网内的服务端。
        /// </summary>
        DiscoveryRequest = 0x01,

        /// <summary>
        /// 发现回复（0x02）。
        /// 服务端收到 DiscoveryRequest 后以此操作码回复，负载数据为服务端 IP 地址字符串。
        /// </summary>
        DiscoveryReply = 0x02,
    }
}
