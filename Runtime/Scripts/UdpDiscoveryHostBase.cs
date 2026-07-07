using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;


namespace LANServiceDiscovery.Runtime
{
   

    /// <summary>
    /// UDP 发现服务端抽象基类。
    /// 子类需实现 OnDiscoveryRequest，用于自定义回复内容（例如返回本机 IP）。
    /// </summary>
    public abstract class UdpDiscoveryHostBase : IDisposable
    {
        private UdpClient _udpServer;
        private CancellationTokenSource _cts;
        private Task _listenTask;
        private readonly int _listenPort;

        /// <summary>
        /// 构造服务端
        /// </summary>
        /// <param name="listenPort">监听的 UDP 端口</param>
        protected UdpDiscoveryHostBase(int listenPort)
        {
            _listenPort = listenPort;
        }

        /// <summary>
        /// 启动 UDP 监听服务
        /// </summary>
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

        /// <summary>
        /// 停止监听
        /// </summary>
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
                    // 解析请求
                    var decoder = new PacketCodec.Decoder();
                    
                    var list = new List<(byte cmd, byte[] data)>();
                    
                    decoder.ParseBytes(result.Buffer, list);

                   
                    
                    foreach (var frame in list)
                    {
                       
                        
                        if (frame.cmd == 0x01) // 发现请求命令
                        {
                            // 交由子类决定回复内容
                            byte[] replyData = OnDiscoveryRequest(result.RemoteEndPoint);
                            if (replyData != null && replyData.Length > 0)
                            {
                                await _udpServer.SendAsync(replyData, replyData.Length, result.RemoteEndPoint);
                            }
                        }
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                /* 正常关闭 */
            }
            catch (Exception ex)
            {
                OnListenError(ex);
            }
        }

        /// <summary>
        /// 当收到发现请求时调用，子类需返回要回复的字节数据（需遵循 PacketCodec 格式）
        /// 例如：return PacketCodec.Encode(0x02, Encoding.UTF8.GetBytes("192.168.1.100"));
        /// </summary>
        protected abstract byte[] OnDiscoveryRequest(IPEndPoint clientEndpoint);

        /// <summary>
        /// 监听出错时调用（可重写）
        /// </summary>
        protected virtual void OnListenError(Exception ex)
        {
        }

        /// <summary>
        /// 监听成功启动时调用（可重写以记录日志）
        /// </summary>
        protected virtual void OnListenStarted(int port)
        {
        }

        /// <summary>
        /// 收到任何 UDP 数据时立即调用（可重写以诊断网络连通性）。
        /// 如果此方法被调用说明网络通畅，问题在解码或命令码匹配。
        /// </summary>
        protected virtual void OnRawDataReceived(IPEndPoint remote, int byteCount)
        {
        }

        public void Dispose()
        {
            Stop();
        }
    }
}