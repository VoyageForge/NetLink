namespace LANServiceDiscovery.Runtime
{
    /// <summary>数据包读取器接口，子类可替换实现</summary>
    public interface IReader
    {
        byte Cmd { get; }
        int Available { get; }
        byte ReadByte();
        short ReadInt16();
        int ReadInt32();
        string ReadString();
        string ReadRemainingString();
        byte[] ReadRemainingBytes();
    }
}
