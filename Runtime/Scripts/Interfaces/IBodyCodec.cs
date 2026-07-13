namespace VoyageForge.NetLink.Runtime
{
    /// <summary>帧体层编解码器：帧体 ↔ (TypeId, Payload)</summary>
    public interface IBodyCodec
    {
        /// <summary>解码：帧体 → (TypeId, Payload字节)</summary>
        (string typeId, byte[] payload) Decode(byte[] frameBody);
        /// <summary>编码：(TypeId, Payload字节) → 帧体</summary>
        byte[] Encode(string typeId, byte[] payload);
        /// <summary>泛型编码：TypeId = typeof(T).Name，payload = packet.Serialize()</summary>
        byte[] Encode<T>(T packet) where T : Payload;
    }
}
