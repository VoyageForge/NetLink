using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace LANServiceDiscovery.Runtime
{
    public abstract class UdpDiscoveryClientBase : IDisposable
    {
        private UdpClient _udpClient;
        private CancellationTokenSource _cts;
        private readonly int _broadcastPort;
        private readonly float _timeoutSeconds;

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

        protected UdpDiscoveryClientBase(int broadcastPort, float timeoutSeconds = 3.0f)
        {
            _broadcastPort = broadcastPort;
            _timeoutSeconds = timeoutSeconds;
        }

        public async Task StartDiscoveryAsync()
        {
            if (_cts != null) return;

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    _udpClient = new UdpClient();
                    try
                    {
                        _udpClient.EnableBroadcast = true;
                        IPEndPoint broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, _broadcastPort);

                        byte[] requestData = PacketCodec.Encode(DiscoveryOpcode.DiscoveryRequest, null);
                        await _udpClient.SendAsync(requestData, requestData.Length, broadcastEndpoint);

                        var receiveTask = _udpClient.ReceiveAsync();
                        var timeoutTask = Task.Delay((int)(_timeoutSeconds * 1000), token);

                        var completedTask = await Task.WhenAny(receiveTask, timeoutTask);
                        if (completedTask == timeoutTask)
                        {
                            token.ThrowIfCancellationRequested();
                            bool retry = await OnDiscoveryTimeout(token);
                            if (!retry) return;
                            continue;
                        }

                        var result = await receiveTask;
                        var decoder = new PacketCodec.Decoder();
                        var frameList = new List<(byte cmd, byte[] data)>();
                        decoder.ParseBytes(result.Buffer, frameList);

                        foreach (var (cmd, data) in frameList)
                        {
                            RemoteEndPoint = result.RemoteEndPoint;
                            PrepareReader(cmd, data);
                            PrepareWriter();
                            bool shouldStop = await OnDataReceived();
                            if (shouldStop) return;
                        }

                        bool fallbackRetry = await OnDiscoveryTimeout(token);
                        if (!fallbackRetry) return;
                    }
                    finally
                    {
                        _udpClient?.Close();
                        _udpClient = null;
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (Exception ex) { OnDiscoveryError(ex); }
            finally
            {
                _udpClient?.Close();
                _udpClient = null;
                _cts?.Dispose();
                _cts = null;
            }
        }

        public void Stop() => _cts?.Cancel();

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
        /// <returns>true 停止发现，false 继续重试</returns>
        protected virtual Task<bool> OnDataReceived()
        {
            if (Reader.Cmd == (byte)DiscoveryOpcode.DiscoveryReply)
            {
                string ip = Reader.ReadRemainingString();
                OnHostDiscovered(ip);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        protected abstract void OnHostDiscovered(string ip);

        protected virtual Task<bool> OnDiscoveryTimeout(CancellationToken token) => Task.FromResult(false);
        protected virtual void OnDiscoveryError(Exception ex) { }

        public void Dispose() => Stop();
    }
}
