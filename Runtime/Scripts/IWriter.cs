namespace LANServiceDiscovery.Runtime
{
    /// <summary>数据包写入器接口，子类可替换实现</summary>
    public interface IWriter
    {
        IWriter WriteByte(byte value);
        IWriter WriteInt16(short value);
        IWriter WriteInt32(int value);
        IWriter WriteString(string value);
        IWriter WriteBytes(byte[] value);
        byte[] Encode(byte cmd);
        byte[] Encode(DiscoveryOpcode cmd);
    }
}
