using System.Net;

namespace VoyageForge.NetLink.Runtime
{
    /// <summary>协议消息体，TypeId 自动取自 typeof(T).Name</summary>
    public class Protocol<T> where T : Payload
    {
        public static readonly string TypeId = typeof(T).Name;
        public T Data { get; }
        public Protocol(T data) => Data = data;
    }

    /// <summary>收到的消息：Payload + 发送方地址</summary>
    public readonly struct ReceivedMessage<T> where T : Payload
    {
        /// <summary>负载数据</summary>
        public T Data { get; }
        /// <summary>发送方地址</summary>
        public IPEndPoint Remote { get; }

        public ReceivedMessage(T data, IPEndPoint remote) { Data = data; Remote = remote; }
    }
}
