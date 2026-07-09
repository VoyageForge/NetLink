namespace LANServiceDiscovery.Runtime
{
    /// <summary>
    /// 数据包写入器接口。
    /// <para>
    /// 提供链式写入方法构建负载数据，最后调用 <see cref="Encode(byte)"/> 产出完整的协议帧字节数组。
    /// 所有写入方法返回 <c>this</c>（<see cref="IWriter"/>），支持流式调用。
    /// </para>
    /// <para>
    /// <b>可替换性：</b>继承基类后通过 <c>Writer = new MyProtobufWriter()</c> 替换默认的 <see cref="PacketWriter"/>。
    /// </para>
    /// </summary>
    public interface IWriter
    {
        /// <summary>写入 1 字节</summary>
        IWriter WriteByte(byte value);

        /// <summary>写入 2 字节大端有符号整数</summary>
        IWriter WriteInt16(short value);

        /// <summary>写入 4 字节大端有符号整数</summary>
        IWriter WriteInt32(int value);

        /// <summary>
        /// 写入长度前缀字符串。
        /// 格式：2 字节大端 UTF-8 字节长度 + UTF-8 数据。
        /// </summary>
        IWriter WriteString(string value);

        /// <summary>写入原始字节数组</summary>
        IWriter WriteBytes(byte[] value);

        /// <summary>用指定命令码将已写入的数据编码为完整协议帧</summary>
        byte[] Encode(byte cmd);

        /// <summary>用内置操作码将已写入的数据编码为完整协议帧</summary>
        byte[] Encode(DiscoveryOpcode cmd);
    }
}
