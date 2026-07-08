using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace LANServiceDiscovery.Runtime
{
    /// <summary>
    /// UDP 发现服务端抽象基类。默认处理 <see cref="DiscoveryOpcode.DiscoveryRequest"/>。
    /// 子类重写 <see cref="OnDataReceived"/> 扩展自定义命令码，
    /// 可通过 <see cref="Reader"/> / <see cref="Writer"/> 的 setter 替换实现。
    /// </summary>
    public abstract class UdpDiscoveryHostBase : IDisposable
    {
        private UdpClient _udpServer;
        private CancellationTokenSource _cts;
        private Task _listenTask;
        private readonly int _listenPort;

        private IReader _reader;
        private IWriter _writer;

        protected virtual IReader Reader
        {
            get { _reader ??= new PacketReader(); return _reader; }
            set => _reader = value;
        }

        protected virtual IWriter Writer
        {
            get { _writer ??= new PacketWriter(); return _writer; }
            set => _writer = value;
        }

        protected IPEndPoint RemoteEndPoint { get; private set; }

        protected UdpDiscoveryHostBase(int listenPort)
        {
            _listenPort = listenPort;
        }

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

        protected void Stop()
        {
            _cts?.Cancel();
            _udpServer?.Close();
            _listenTask?.Wait(1000);
        }

        private async Task ListenLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var result = await _udpServer.ReceiveAsync();
                    OnRawDataReceived(result.RemoteEndPoint, result.Buffer.Length);

                    var decoder = new PacketCodec.Decoder();
                    var list = new List<(byte cmd, byte[] data)>();
                    decoder.ParseBytes(result.Buffer, list);

                    foreach (var (cmd, data) in list)
                    {
                        RemoteEndPoint = result.RemoteEndPoint;
                        PrepareReader(cmd, data);
                        PrepareWriter();
                        await OnDataReceived();
                    }
                }
            }
            catch (ObjectDisposedException) { }
            catch (OperationCanceledException) { }
            catch (Exception ex) { OnListenError(ex); }
        }

        private void PrepareReader(byte cmd, byte[] data)
        {
            if (_reader is PacketReader pr)
                pr.Reset(cmd, data);
            else
                Reader = new PacketReader(cmd, data);
        }

        private void PrepareWriter()
        {
            if (_writer is PacketWriter pw)
                pw.Reset();
            else
                Writer = new PacketWriter();
        }

        /// <summary>
        /// 收到帧时调用。通过 <see cref="Reader"/> / <see cref="Writer"/> / <see cref="RemoteEndPoint"/> 访问数据。
        /// </summary>
        protected virtual async Task OnDataReceived()
        {
            if (Reader.Cmd == (byte)DiscoveryOpcode.DiscoveryRequest)
            {
                byte[] reply = OnDiscoveryRequest(RemoteEndPoint);
                if (reply != null && reply.Length > 0)
                    await ReplyAsync(reply, RemoteEndPoint);
            }
        }

        protected abstract byte[] OnDiscoveryRequest(IPEndPoint clientEndpoint);

        protected async Task ReplyAsync(byte[] data, IPEndPoint target)
        {
            if (_udpServer != null && data != null && data.Length > 0)
                await _udpServer.SendAsync(data, data.Length, target);
        }

        protected virtual void OnListenStarted(int port) { }
        protected virtual void OnRawDataReceived(IPEndPoint remote, int byteCount) { }
        protected virtual void OnListenError(Exception ex) { }

        public void Dispose() => Stop();
    }
}
