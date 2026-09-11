using System;
using System.Collections.Generic;
using System.Net;

namespace VoyageForge.NetLink.Runtime
{
    /// <summary>
    /// 消息编解码器。FrameCodec + BodyCodec 组合，提供收发和回调分发。
    /// <para>handler 收到 <see cref="ReceivedMessage{T}"/>，含 Data + 发送方 Remote 地址。</para>
    /// <para>同一类型支持多个订阅者（多播）；<see cref="On{T}"/> 返回退订句柄，Dispose 即移除。</para>
    /// </summary>
    public class Codec
    {
        public IFrameCodec FrameCodec { get; set; } = new DefaultFrameCodec();
        public IBodyCodec BodyCodec { get; set; } = new DefaultBodyCodec();

        /// <summary>TypeId → 回调列表（多播，每个类型可挂多个 handler）</summary>
        private readonly Dictionary<string, List<Action<byte[], IPEndPoint>>> _handlers = new();

        /// <summary>
        /// handler 处理消息时抛出的异常回调（用于异常隔离后的上报）。
        /// 默认 null 表示静默忽略。可接入日志系统，避免 Codec 直接依赖 Unity 引擎。
        /// </summary>
        public Action<Exception> OnHandlerError { get; set; }

        // ==================== 注册 ====================

        /// <summary>
        /// 注册 Payload 处理器。同一类型可注册多个，均会收到消息。
        /// <para>返回用于退订的句柄，调用 <see cref="IDisposable.Dispose"/> 即移除该处理器（幂等）。</para>
        /// </summary>
        public IDisposable On<T>(Action<ReceivedMessage<T>> handler) where T : Payload, new()
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            string typeId = Protocol<T>.TypeId;
            if (!_handlers.TryGetValue(typeId, out var list))
                _handlers[typeId] = list = new List<Action<byte[], IPEndPoint>>();

            Action<byte[], IPEndPoint> wrapped = (raw, remote) =>
            {
                var data = new T();
                data.Deserialize(raw);
                handler(new ReceivedMessage<T>(data, remote));
            };
            list.Add(wrapped);

            return new Subscription(() => RemoveHandler(typeId, wrapped));
        }

        /// <summary>从指定类型移除某个处理器（一般通过 <see cref="On{T}"/> 返回的句柄退订）。</summary>
        private void RemoveHandler(string typeId, Action<byte[], IPEndPoint> handler)
        {
            if (_handlers.TryGetValue(typeId, out var list))
            {
                list.Remove(handler);
                if (list.Count == 0) _handlers.Remove(typeId);
            }
        }

        // ==================== 接收 ====================

        /// <summary>喂入原始字节</summary>
        public void Feed(byte[] raw) => FrameCodec.Feed(raw);

        /// <summary>提取帧 → 分发到该类型的所有 handler（传入发送方地址），单个 handler 异常不影响其他。</summary>
        public void Dispatch(IPEndPoint remote)
        {
            while (FrameCodec.TryExtract(out byte[] frame))
            {
                (string typeId, byte[] payload) = BodyCodec.Decode(frame);
                if (!_handlers.TryGetValue(typeId, out var list)) continue;

                // 拷贝快照遍历：handler 回调里再注册/退订时，本轮遍历不受影响
                var snapshot = list.ToArray();
                foreach (var handler in snapshot)
                {
                    try { handler(payload, remote); }
                    catch (Exception ex)
                    {
                        // 异常隔离：单个 handler 抛异常不影响其他 handler 与后续帧
                        OnHandlerError?.Invoke(ex);
                    }
                }
            }
        }

        // ==================== 发送 ====================

        public byte[] Encode(string typeId, byte[] payload)
            => FrameCodec.Pack(BodyCodec.Encode(typeId, payload));

        public byte[] Encode<T>(T packet) where T : Payload
            => FrameCodec.Pack(BodyCodec.Encode(packet));

        public void Reset() => FrameCodec.Reset();

        /// <summary>退订句柄：Dispose 时从 Codec 移除对应处理器。幂等、可多次调用。</summary>
        private sealed class Subscription : IDisposable
        {
            private Action _dispose;

            public Subscription(Action dispose) => _dispose = dispose;

            public void Dispose()
            {
                var d = _dispose;
                _dispose = null;   // 置空实现幂等：二次 Dispose 无副作用
                d?.Invoke();
            }
        }
    }
}
