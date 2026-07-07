using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LANServiceDiscovery.Runtime
{
    /// <summary>
    /// UDP 发现客户端抽象基类。
    /// 子类需实现 OnHostDiscovered，决定发现服务端 IP 后要做什么。
    /// </summary>
    public abstract class UdpDiscoveryClientBase : IDisposable
    {
        private UdpClient _udpClient;
        private CancellationTokenSource _cts;
        private readonly int _broadcastPort;
        private readonly float _timeoutSeconds;

        /// <summary>
        /// 构造客户端
        /// </summary>
        /// <param name="broadcastPort">广播目标端口（需与服务端一致）</param>
        /// <param name="timeoutSeconds">单次等待回复的超时时间（秒）</param>
        protected UdpDiscoveryClientBase(int broadcastPort, float timeoutSeconds = 3.0f)
        {
            _broadcastPort = broadcastPort;
            _timeoutSeconds = timeoutSeconds;
        }

        /// <summary>
        /// 启动发现过程（发送广播并等待回复），超时时根据 <see cref="OnDiscoveryTimeout"/> 返回值决定是否重试。
        /// </summary>
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

                        byte[] requestData = PacketCodec.Encode(0x01, null);
                        await _udpClient.SendAsync(requestData, requestData.Length, broadcastEndpoint);

                        var receiveTask = _udpClient.ReceiveAsync();
                        var timeoutTask = Task.Delay((int)(_timeoutSeconds * 1000), token);

                        var completedTask = await Task.WhenAny(receiveTask, timeoutTask);
                        if (completedTask == timeoutTask)
                        {
                            token.ThrowIfCancellationRequested();
                            bool retry = await OnDiscoveryTimeout();
                            if (!retry) return;
                            continue;
                        }

                        var result = await receiveTask;
                        var decoder = new PacketCodec.Decoder();
                        var frameList = new List<(byte cmd, byte[] data)>();
                        decoder.ParseBytes(result.Buffer, frameList);

                        foreach (var frame in frameList)
                        {
                            if (frame.cmd == 0x02)
                            {
                                string hostIP = Encoding.UTF8.GetString(frame.data);
                                OnHostDiscovered(hostIP);
                                return;
                            }
                        }

                        // 收到包但没有匹配的 cmd，也视为超时
                        bool fallbackRetry = await OnDiscoveryTimeout();
                        if (!fallbackRetry) return;
                    }
                    finally
                    {
                        _udpClient?.Close();
                        _udpClient = null;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消，不报错
            }
            catch (ObjectDisposedException)
            {
                // UdpClient 已被 Dispose，正常退出
            }
            catch (Exception ex)
            {
                OnDiscoveryError(ex);
            }
            finally
            {
                _udpClient?.Close();
                _udpClient = null;
                _cts?.Dispose();
                _cts = null;
            }
        }

        /// <summary>
        /// 停止发现过程
        /// </summary>
        public void Stop()
        {
            _cts?.Cancel();
        }

        /// <summary>
        /// 当成功发现服务端 IP 时调用（由子类实现具体逻辑）
        /// </summary>
        protected abstract void OnHostDiscovered(string ip);

        /// <summary>
        /// 当单次发现超时时调用。
        /// 子类可重写以记录日志并决定是否重试。
        /// </summary>
        /// <returns>返回 <c>true</c> 表示重试，<c>false</c> 表示放弃。</returns>
        protected virtual Task<bool> OnDiscoveryTimeout()
        {
            return Task.FromResult(false);
        }

        /// <summary>
        /// 当发生网络异常时调用（可重写）
        /// </summary>
        protected virtual void OnDiscoveryError(Exception ex)
        {
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
