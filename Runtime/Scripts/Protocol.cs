using System;

namespace LANServiceDiscovery.Runtime
{
    /// <summary>协议消息体，TypeId 自动取自 typeof(T).Name</summary>
    public readonly struct Protocol<T> where T : Payload
    {
        /// <summary>负载类型标识，自动为 typeof(T).Name</summary>
        public static readonly string TypeId = typeof(T).Name;

        /// <summary>负载数据</summary>
        public T Data { get; }

        public Protocol(T data) => Data = data;
    }
}
