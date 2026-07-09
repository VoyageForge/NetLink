using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace LANServiceDiscovery.Runtime
{
    /// <summary>
    /// UDP 发现客户端抽象基类。
    /// <para>
    /// <b>职责：</b>通过 UDP 广播发送发现请求，等待服务端回复，
    /// 将回复帧通过 <see cref="OnDataReceived"/> 分发给子类处理。支持超时自动重试。
    /// </para>
    /// <para>
    /// <b>默认行为：</b>广播 <see cref="DiscoveryOpcode.DiscoveryRequest"/>（0x01），
    /// 识别 <see cref="DiscoveryOpcode.DiscoveryReply"/>（0x02），
    /// 从回复中读取 IP 字符串并调用 <see cref="OnHostDiscovered"/>。
    /// </para>
    /// <para>
    /// <b>重试机制：</b>超时后调用 <see cref="OnDiscoveryTimeout"/>，
    /// 返回 true 则继续循环重试，false 则停止。支持 <see cref="CancellationToken"/> 优雅取消。
    /// </para>
    /// <para>
    /// <b>扩展方式：</b>
    /// - 简单场景：只重写 <see cref="OnHostDiscovered"/> 处理发现的 IP
    /// - 重试场景：重写 <see cref="OnDiscoveryTimeout"/> 自定义重试策略
    /// - 复杂场景：重写 <see cref="OnDataReceived"/> 处理任意自定义命令码
    /// - 替换协议：设置 <see cref="Reader"/> / <see cref="Writer"/> 为自定义实现
    /// </para>
    /// </summary>
    public abstract class UdpDiscoveryClientBase : IDisposable
    {
        /// <summary>UDP 广播 Socket（每次重试时重建）</summary>
        private UdpClient _udpClient;
        /// <summary>取消令牌源，用于停止重试循环</summary>
        private CancellationTokenSource _cts;
        /// <summary>广播目标端口号</summary>
        private readonly int _broadcastPort;
        /// <summary>单次等待回复的超时时间（秒）</summary>
        private readonly float _timeoutSeconds;

        /// <summary>读取器实例（延迟初始化）</summary>
        private IReader _reader;
        /// <summary>写入器实例（延迟初始化）</summary>
        private IWriter _writer;

        /// <summary>
        /// 当前帧读取器。第一次访问时自动创建默认的 <see cref="PacketReader"/>。
        /// 子类可通过 setter 替换：<c>Reader = new MyReader();</c>
        /// </summary>
        protected virtual IReader Reader
        {
            get { _reader ??= new PacketReader(); return _reader; }
            set => _reader = value;
        }

        /// <summary>
        /// 当前帧写入器。第一次访问时自动创建默认的 <see cref="PacketWriter"/>。
        /// 子类可通过 setter 替换：<c>Writer = new MyWriter();</c>
        /// </summary>
        protected virtual IWriter Writer
        {
            get { _writer ??= new PacketWriter(); return _writer; }
            set => _writer = value;
        }

        /// <summary>当前回复帧的发送方网络地址</summary>
        protected IPEndPoint RemoteEndPoint { get; private set; }

        /// <summary>
        /// 构造客户端。
        /// </summary>
        /// <param name="broadcastPort">广播目标端口（需与服务端监听端口一致）</param>
        /// <param name="timeoutSeconds">单次等待回复的超时时间（秒），默认 3 秒</param>
        protected UdpDiscoveryClientBase(int broadcastPort, float timeoutSeconds = 3.0f)
        {
            _broadcastPort = broadcastPort;
            _timeoutSeconds = timeoutSeconds;
        }

        /// <summary>
        /// 启动发现过程（异步、非阻塞）。
        /// <para>
        /// 内部循环：发送广播 → 等待回复（可取消超时）→ 收到则处理，超时则根据
        /// <see cref="OnDiscoveryTimeout"/> 返回值决定是否重试。
        /// 重复调用会静默忽略（同一时间只有一个发现任务运行）。
        /// </para>
        /// </summary>
        public async Task StartDiscoveryAsync()
        {
            if (_cts != null) return; // 已在运行，忽略重复调用

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    _udpClient = new UdpClient();
                    try
                    {
                        // 必须开启广播权限
                        _udpClient.EnableBroadcast = true;
                        IPEndPoint broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, _broadcastPort);

                        // 发送发现请求广播（负载为空）
                        byte[] requestData = PacketCodec.Encode(DiscoveryOpcode.DiscoveryRequest, null);
                        await _udpClient.SendAsync(requestData, requestData.Length, broadcastEndpoint);

                        // 同时等待回复和超时，谁先完成用谁
                        var receiveTask = _udpClient.ReceiveAsync();
                        var timeoutTask = Task.Delay((int)(_timeoutSeconds * 1000), token);

                        var completedTask = await Task.WhenAny(receiveTask, timeoutTask);
                        if (completedTask == timeoutTask)
                        {
                            // 超时：检查是否被取消，然后问子类要不要重试
                            token.ThrowIfCancellationRequested();
                            bool retry = await OnDiscoveryTimeout(token);
                            if (!retry) return;     // 放弃重试
                            continue;               // 重试：创建新的 UdpClient 再发一次
                        }

                        // 收到回复：解码并处理
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
                            if (shouldStop) return; // 子类决定停止发现
                        }

                        // 收到了包但没有匹配的帧，也触发超时逻辑
                        bool fallbackRetry = await OnDiscoveryTimeout(token);
                        if (!fallbackRetry) return;
                    }
                    finally
                    {
                        // 每次重试后释放旧的 UdpClient
                        _udpClient?.Close();
                        _udpClient = null;
                    }
                }
            }
            catch (OperationCanceledException) { /* 正常取消，不报错 */ }
            catch (ObjectDisposedException) { /* Socket 已关闭，正常退出 */ }
            catch (Exception ex) { OnDiscoveryError(ex); }
            finally
            {
                // 清理资源
                _udpClient?.Close();
                _udpClient = null;
                _cts?.Dispose();
                _cts = null;
            }
        }

        /// <summary>停止发现过程（取消当前重试循环）</summary>
        public void Stop() => _cts?.Cancel();

        /// <summary>准备当前帧的读取器（复用或新建）</summary>
        private void PrepareReader(byte cmd, byte[] data)
        {
            if (_reader is PacketReader pr)
                pr.Reset(cmd, data);
            else
                Reader = new PacketReader(cmd, data);
        }

        /// <summary>准备当前帧的写入器（复用或新建）</summary>
        private void PrepareWriter()
        {
            if (_writer is PacketWriter pw)
                pw.Reset();
            else
                Writer = new PacketWriter();
        }

        /// <summary>
        /// 收到服务端回复帧时调用（核心扩展点）。
        /// <para>
        /// <b>默认实现：</b>识别 <see cref="DiscoveryOpcode.DiscoveryReply"/>（0x02），
        /// 读取剩余数据为 IP 字符串 → 调用 <see cref="OnHostDiscovered"/>。
        /// </para>
        /// <para>
        /// <b>子类重写：</b>可通过 <see cref="Reader"/> 读取数据、<see cref="Writer"/> 构建数据、
        /// <see cref="RemoteEndPoint"/> 获取发送方地址。
        /// </para>
        /// <code>
        /// protected override Task&lt;bool&gt; OnDataReceived()
        /// {
        ///     if (Reader.Cmd == (byte)MyOpcode.CustomReply)
        ///     {
        ///         string info = Reader.ReadString();
        ///         return Task.FromResult(true); // true = 停止发现
        ///     }
        ///     return base.OnDataReceived();
        /// }
        /// </code>
        /// </summary>
        /// <returns>
        /// true 表示发现成功，停止重试循环；
        /// false 表示未识别的帧，触发 <see cref="OnDiscoveryTimeout"/> 决定是否继续
        /// </returns>
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

        /// <summary>
        /// 默认收到 DiscoveryReply 时调用（由 <see cref="OnDataReceived"/> 触发）。
        /// 如果子类重写了 <see cref="OnDataReceived"/> 且不调用 base，此方法可能不会被触发。
        /// </summary>
        /// <param name="ip">发现的 IP 地址字符串</param>
        protected abstract void OnHostDiscovered(string ip);

        /// <summary>
        /// 发现超时时调用。
        /// <para>
        /// 子类可重写以记录日志、实现自定义重试策略。
        /// 重写时请将 <paramref name="token"/> 传给 <see cref="Task.Delay(int, CancellationToken)"/>
        /// 等异步操作，确保 <see cref="Stop"/> 被调用时能立即退出。
        /// </para>
        /// </summary>
        /// <param name="token">取消令牌，程序关闭时触发</param>
        /// <returns>true 表示重试，false 表示放弃</returns>
        protected virtual Task<bool> OnDiscoveryTimeout(CancellationToken token) => Task.FromResult(false);

        /// <summary>发现过程中发生未处理异常时调用</summary>
        protected virtual void OnDiscoveryError(Exception ex) { }

        /// <summary>释放资源：停止发现循环</summary>
        public void Dispose() => Stop();
    }
}
