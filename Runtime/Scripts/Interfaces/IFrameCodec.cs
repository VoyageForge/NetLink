namespace LANServiceDiscovery.Runtime
{
    /// <summary>帧层编解码器：原始字节 ↔ 帧体（剥离/包裹 SOF/Len/EOF）</summary>
    public interface IFrameCodec
    {
        /// <summary>喂入原始字节，内部缓冲处理粘包</summary>
        void Feed(byte[] raw);
        /// <summary>提取完整帧体（剥离帧头/帧尾），false = 需要更多数据</summary>
        bool TryExtract(out byte[] frameBody);
        /// <summary>将帧体包裹为完整帧（添加帧头/帧尾）</summary>
        byte[] Pack(byte[] frameBody);
        /// <summary>清空内部缓冲区</summary>
        void Reset();
    }
}
