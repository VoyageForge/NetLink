using System;
using System.Collections.Generic;
using System.Net;

namespace VoyageForge.NetLink.Runtime
{
    /// <summary>
    /// 消息编解码器。FrameCodec + BodyCodec 组合，提供收发和回调分发。
    /// <para>handler 收到 <see cref="ReceivedMessage{T}"/>，含 Data + 发送方 Remote 地址。</para>
    /// </summary>
    public class Codec
    {
        public IFrameCodec FrameCodec { get; set; } = new DefaultFrameCodec();
        public IBodyCodec BodyCodec { get; set; } = new DefaultBodyCodec();

        /// <summary>TypeId → 回调（Action&lt;byte[], IPEndPoint&gt;）</summary>
        private readonly Dictionary<string, Action<byte[], IPEndPoint>> _handlers = new();

        // ==================== 注册 ====================

        /// <summary>注册 Payload 处理器。回调收到 <see cref="ReceivedMessage{T}"/>。</summary>
        public void On<T>(Action<ReceivedMessage<T>> handler) where T : Payload, new()
        {
            _handlers[Protocol<T>.TypeId] = (raw, remote) =>
            {
                var data = new T();
                data.Deserialize(raw);
                handler(new ReceivedMessage<T>(data, remote));
            };
        }

        // ==================== 接收 ====================

        /// <summary>喂入原始字节</summary>
        public void Feed(byte[] raw) => FrameCodec.Feed(raw);

        /// <summary>提取帧 → 匹配 handler 回调（传入发送方地址）</summary>
        public void Dispatch(IPEndPoint remote)
        {
            while (FrameCodec.TryExtract(out byte[] frame))
            {
                (string typeId, byte[] payload) = BodyCodec.Decode(frame);
                if (_handlers.TryGetValue(typeId, out var handler))
                    handler(payload, remote);
            }
        }

        // ==================== 发送 ====================

        public byte[] Encode(string typeId, byte[] payload)
            => FrameCodec.Pack(BodyCodec.Encode(typeId, payload));

        public byte[] Encode<T>(T packet) where T : Payload
            => FrameCodec.Pack(BodyCodec.Encode(packet));

        public void Reset() => FrameCodec.Reset();
    }
}
