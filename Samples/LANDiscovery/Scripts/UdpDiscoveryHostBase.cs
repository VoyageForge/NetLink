using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using VoyageForge.NetLink.Runtime;

namespace VoyageForge.NetLink.Samples.LANDiscovery
{
    /// <summary>
    /// UDP 服务端基类。后台监听 → <see cref="Codec"/>.Feed/Dispatch 自动分发。
    /// <para>业务逻辑通过 <c>Codec.On&lt;T&gt;(handler)</c> 注册，不再需要重写抽象方法。</para>
    /// </summary>
    public abstract class UdpDiscoveryHostBase : IDisposable
    {
        private UdpClient _udpServer;
        private CancellationTokenSource _cts;
        private Task _listenTask;
        private readonly int _listenPort;

        /// <summary>消息编解码器（收发 + 回调分发）</summary>
        protected Codec Codec { get; set; } = new Codec();
        /// <summary>最近收到包的发送方地址</summary>
        private IPEndPoint _remoteEndPoint;

        protected UdpDiscoveryHostBase(int listenPort) { _listenPort = listenPort; }

        /// <summary>启动监听（非阻塞，设置端口复用）</summary>
        protected void StartSync()
        {
            if (_listenTask != null && !_listenTask.IsCompleted) return;
            _cts = new CancellationTokenSource();
            _udpServer = new UdpClient();
            _udpServer.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpServer.Client.Bind(new IPEndPoint(IPAddress.Any, _listenPort));
            _listenTask = Task.Run(() => ListenLoop(_cts.Token));
            OnListenStarted(_listenPort);
        }

        /// <summary>停止监听</summary>
        protected void Stop() { _cts?.Cancel(); _udpServer?.Close(); _listenTask?.Wait(1000); }

        private async Task ListenLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var result = await _udpServer.ReceiveAsync();
                    _remoteEndPoint = result.RemoteEndPoint;

                    Codec.Feed(result.Buffer);
                    Codec.Dispatch(result.RemoteEndPoint);
                }
            }
            catch (ObjectDisposedException) { }
            catch (OperationCanceledException) { }
            catch (Exception ex) { OnListenError(ex); }
        }

        /// <summary>发送帧到指定地址</summary>
        protected async Task ReplyAsync(byte[] frame, IPEndPoint target)
        {
            if (_udpServer != null && frame?.Length > 0)
                await _udpServer.SendAsync(frame, frame.Length, target);
        }

        /// <summary>监听已启动</summary>
        protected virtual void OnListenStarted(int port) { }
        /// <summary>监听异常</summary>
        protected virtual void OnListenError(Exception ex) { }
        public void Dispose() => Stop();
    }
}
