using System;
using System.Collections.Generic;

namespace LANServiceDiscovery.Runtime
{
    /// <summary>
    /// 消息编解码器 —— 组合帧层编解码和帧体层编解码，提供消息收发和回调分发。
    /// <para>
    /// <b>发送链路：</b><c>Encode(packet)</c> → BodyCodec.Encode（帧体）→ FrameCodec.Pack（帧层）→ 完整帧 byte[]
    /// <b>接收链路：</b><c>Feed(raw)</c> → FrameCodec.TryExtract（帧层解包）→ BodyCodec.Decode（帧体解码）→ Dispatch 回调
    /// </para>
    /// <para>
    /// <b>注册处理器：</b><c>On&lt;MyPayload&gt;(msg => Handle(msg))</c>，TypeId = typeof(T).Name 自动获取。
    /// 收到匹配 TypeId 的帧时自动 new T().Deserialize() → 回调。
    /// <b>替换组件：</b><c>Codec.FrameCodec</c> / <c>Codec.BodyCodec</c> 可单独替换。
    /// </para>
    /// </summary>
    public class Codec
    {
        /// <summary>帧层编解码器（SOF/EOF）</summary>
        public IFrameCodec FrameCodec { get; set; } = new DefaultFrameCodec();
        /// <summary>帧体层编解码器（TypeId/Check）</summary>
        public IBodyCodec BodyCodec { get; set; } = new DefaultBodyCodec();

        /// <summary>TypeId → 回调</summary>
        private readonly Dictionary<string, Action<byte[]>> _handlers = new();

        // ==================== 注册 ====================

        /// <summary>
        /// 注册 Payload 处理器。收到 TypeId == typeof(T).Name 的帧时，
        /// 自动 new T().Deserialize(payload) → <paramref name="handler"/>。
        /// </summary>
        public void On<T>(Action<Protocol<T>> handler) where T : Payload, new()
        {
            var typeId = Protocol<T>.TypeId;
            _handlers[typeId] = raw =>
            {
                var data = new T();
                data.Deserialize(raw);
                handler(new Protocol<T>(data));
            };
        }

        // ==================== 接收 ====================

        /// <summary>喂入原始字节</summary>
        public void Feed(byte[] raw) => FrameCodec.Feed(raw);

        /// <summary>提取帧 → 匹配 handler 回调</summary>
        public void Dispatch()
        {
            while (FrameCodec.TryExtract(out byte[] frame))
            {
                (string typeId, byte[] payload) = BodyCodec.Decode(frame);
                if (_handlers.TryGetValue(typeId, out var handler))
                    handler(payload);
            }
        }

        // ==================== 发送 ====================

        /// <summary>编码：(TypeId, Payload字节) → 完整帧</summary>
        public byte[] Encode(string typeId, byte[] payload)
            => FrameCodec.Pack(BodyCodec.Encode(typeId, payload));

        /// <summary>编码：Payload 子类 → 完整帧</summary>
        public byte[] Encode<T>(T packet) where T : Payload
            => FrameCodec.Pack(BodyCodec.Encode(packet));

        /// <summary>清空缓冲区</summary>
        public void Reset() => FrameCodec.Reset();
    }
}
