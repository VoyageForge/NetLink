using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using VoyageForge.NetLink.Runtime;
using UnityEngine;

namespace VoyageForge.NetLink.Samples.LANDiscovery
{
    /// <summary>
    /// UDP 客户端基类。后台接收 → <see cref="Codec"/>.Feed/Dispatch 自动分发。
    /// <para>发送：<c>SendAsync(packet)</c>。业务逻辑：<c>Codec.On&lt;T&gt;(handler)</c> 注册。</para>
    /// </summary>
    public abstract class UdpDiscoveryClientBase : IDisposable
    {
        private UdpClient _udpClient;
        private CancellationTokenSource _cts;
        private Task _receiveTask;
        private readonly int _broadcastPort;

        /// <summary>消息编解码器（收发 + 回调分发）</summary>
        protected Codec Codec { get; set; } = new Codec();
        /// <summary>最近收到包的发送方地址</summary>
        protected IPEndPoint RemoteEndPoint { get; private set; }

        protected UdpDiscoveryClientBase(int broadcastPort) { _broadcastPort = broadcastPort; }

        /// <summary>启动后台接收</summary>
        public void Start()
        {
            if (_receiveTask != null && !_receiveTask.IsCompleted) return;
            _cts = new CancellationTokenSource();
            _udpClient = new UdpClient();
            _udpClient.EnableBroadcast = true;
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
            _receiveTask = Task.Run(() => ReceiveLoop(_cts.Token));
            OnStarted();
        }

        /// <summary>停止</summary>
        public void Stop() { _cts?.Cancel(); _udpClient?.Close(); _receiveTask?.Wait(1000); }

        /// <summary>发送 Payload 广播</summary>
        public async Task SendAsync<T>(T packet) where T : Payload
        {
            if (_udpClient == null) return;
            byte[] frame = Codec.Encode(packet);

            Debug.Log(BitConverter.ToString(frame));
            
            await _udpClient.SendAsync(frame, frame.Length, new IPEndPoint(IPAddress.Broadcast, _broadcastPort));
        }

        private async Task ReceiveLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var result = await _udpClient.ReceiveAsync();
                    RemoteEndPoint = result.RemoteEndPoint;

                    Codec.Feed(result.Buffer);
                    Codec.Dispatch();
                }
            }
            catch (ObjectDisposedException) { }
            catch (OperationCanceledException) { }
            catch (Exception ex) { OnError(ex); }
        }

        /// <summary>客户端已启动</summary>
        protected virtual void OnStarted() { }
        /// <summary>接收异常</summary>
        protected virtual void OnError(Exception ex) { }
        public void Dispose() => Stop();
    }
}
