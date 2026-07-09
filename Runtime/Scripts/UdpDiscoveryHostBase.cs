using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace LANServiceDiscovery.Runtime
{
    /// <summary>
    /// UDP 发现服务端抽象基类。
    /// <para>
    /// <b>职责：</b>在指定端口上异步监听 UDP 数据报，自动解码协议帧，
    /// 并将每个帧通过 <see cref="OnDataReceived"/> 分发给子类处理。
    /// </para>
    /// <para>
    /// <b>默认行为：</b>识别 <see cref="DiscoveryOpcode.DiscoveryRequest"/>（0x01），
    /// 调用抽象方法 <see cref="OnDiscoveryRequest"/> 获取回复数据并发送。
    /// </para>
    /// <para>
    /// <b>扩展方式：</b>
    /// - 简单场景：只重写 <see cref="OnDiscoveryRequest"/> 返回自定义回复
    /// - 复杂场景：重写 <see cref="OnDataReceived"/> 处理任意自定义命令码
    /// - 替换协议：设置 <see cref="Reader"/> / <see cref="Writer"/> 为自定义实现
    /// </para>
    /// <para>
    /// <b>生命周期：</b>构造 → <see cref="StartSync"/> → 循环监听 → <see cref="Stop"/> → <see cref="Dispose"/>
    /// </para>
    /// </summary>
    public abstract class UdpDiscoveryHostBase : IDisposable
    {
        /// <summary>UDP 监听 Socket</summary>
        private UdpClient _udpServer;
        /// <summary>取消令牌源，用于优雅关闭监听循环</summary>
        private CancellationTokenSource _cts;
        /// <summary>监听后台任务</summary>
        private Task _listenTask;
        /// <summary>监听端口号</summary>
        private readonly int _listenPort;

        /// <summary>读取器实例（延迟初始化）</summary>
        private IReader _reader;
        /// <summary>写入器实例（延迟初始化）</summary>
        private IWriter _writer;

        /// <summary>
        /// 当前帧读取器。第一次访问时自动创建默认的 <see cref="PacketReader"/>。
        /// 子类可通过 setter 替换为自定义实现：<c>Reader = new MyJsonReader();</c>
        /// </summary>
        protected virtual IReader Reader
        {
            get { _reader ??= new PacketReader(); return _reader; }
            set => _reader = value;
        }

        /// <summary>
        /// 当前帧写入器。第一次访问时自动创建默认的 <see cref="PacketWriter"/>。
        /// 子类可通过 setter 替换为自定义实现：<c>Writer = new MyProtobufWriter();</c>
        /// </summary>
        protected virtual IWriter Writer
        {
            get { _writer ??= new PacketWriter(); return _writer; }
            set => _writer = value;
        }

        /// <summary>当前帧的发送方网络地址（IP + 端口）</summary>
        protected IPEndPoint RemoteEndPoint { get; private set; }

        /// <summary>
        /// 构造服务端。
        /// </summary>
        /// <param name="listenPort">监听的 UDP 端口号</param>
        protected UdpDiscoveryHostBase(int listenPort)
        {
            _listenPort = listenPort;
        }

        /// <summary>
        /// 启动 UDP 监听服务（非阻塞，内部启动后台 Task）。
        /// <para>
        /// 会设置 SO_REUSEADDR 端口复用选项，允许多个进程同时监听同一端口。
        /// 重复调用会静默忽略。
        /// </para>
        /// </summary>
        protected void StartSync()
        {
            if (_listenTask != null && !_listenTask.IsCompleted) return;

            _cts = new CancellationTokenSource();
            _udpServer = new UdpClient();

            // 设置端口复用，允许多进程共享同一端口
            _udpServer.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpServer.Client.Bind(new IPEndPoint(IPAddress.Any, _listenPort));

            _listenTask = Task.Run(() => ListenLoop(_cts.Token));
            OnListenStarted(_listenPort);
        }

        /// <summary>停止监听，取消后台任务并关闭 Socket</summary>
        protected void Stop()
        {
            _cts?.Cancel();
            _udpServer?.Close();
            _listenTask?.Wait(1000);
        }

        /// <summary>
        /// 监听主循环（后台线程执行）。
        /// 循环接收 UDP 数据报 → 解码帧 → 逐个调用 <see cref="OnDataReceived"/>。
        /// </summary>
        private async Task ListenLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    // 阻塞等待下一个 UDP 数据报
                    var result = await _udpServer.ReceiveAsync();
                    OnRawDataReceived(result.RemoteEndPoint, result.Buffer.Length);

                    // 用 PacketCodec 解码原始字节为帧列表
                    var decoder = new PacketCodec.Decoder();
                    var list = new List<(byte cmd, byte[] data)>();
                    decoder.ParseBytes(result.Buffer, list);

                    // 逐个帧分发给子类处理
                    foreach (var (cmd, data) in list)
                    {
                        RemoteEndPoint = result.RemoteEndPoint;
                        PrepareReader(cmd, data);   // 准备当前帧的读取器
                        PrepareWriter();             // 准备当前帧的写入器
                        await OnDataReceived();      // 交给子类处理
                    }
                }
            }
            catch (ObjectDisposedException) { /* Socket 被关闭，正常退出 */ }
            catch (OperationCanceledException) { /* 取消令牌触发，正常退出 */ }
            catch (Exception ex) { OnListenError(ex); }
        }

        /// <summary>
        /// 为当前帧准备读取器。
        /// 如果当前 Reader 是 PacketReader 则复用 Reset，否则创建新实例。
        /// 确保 <see cref="OnDataReceived"/> 调用时 Reader 已绑定到正确的帧数据。
        /// </summary>
        private void PrepareReader(byte cmd, byte[] data)
        {
            if (_reader is PacketReader pr)
                pr.Reset(cmd, data);               // 复用已有实例
            else
                Reader = new PacketReader(cmd, data); // 子类替换了实现，创建新的
        }

        /// <summary>
        /// 为当前帧准备写入器。
        /// 如果当前 Writer 是 PacketWriter 则复用 Reset，否则创建新实例。
        /// 这允许子类在每帧处理中使用 Writer 构建回复数据。
        /// </summary>
        private void PrepareWriter()
        {
            if (_writer is PacketWriter pw)
                pw.Reset();                         // 复用已有实例
            else
                Writer = new PacketWriter();         // 子类替换了实现，创建新的
        }

        /// <summary>
        /// 收到解码后的帧时调用（核心扩展点）。
        /// <para>
        /// <b>默认实现：</b>识别 <see cref="DiscoveryOpcode.DiscoveryRequest"/>，
        /// 调用 <see cref="OnDiscoveryRequest"/> 获取回复 → 通过 <see cref="ReplyAsync"/> 发送。
        /// </para>
        /// <para>
        /// <b>子类重写：</b>可通过 <see cref="Reader"/> 读取数据、<see cref="Writer"/> 构建回复、
        /// <see cref="RemoteEndPoint"/> 获取发送方地址。处理自定义命令码后记得调用 <c>base.OnDataReceived()</c> 兜底。
        /// </para>
        /// <code>
        /// protected override async Task OnDataReceived()
        /// {
        ///     if (Reader.Cmd == (byte)MyOpcode.Custom)
        ///     {
        ///         string name = Reader.ReadString();
        ///         var reply = Writer.WriteString("ok").Encode(MyOpcode.Reply);
        ///         await ReplyAsync(reply, RemoteEndPoint);
        ///         return;
        ///     }
        ///     await base.OnDataReceived(); // 默认 DiscoveryRequest 处理
        /// }
        /// </code>
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

        /// <summary>
        /// 收到发现请求时调用，子类必须重写以返回回复数据帧。
        /// <para>
        /// <b>注意：</b>如果子类重写了 <see cref="OnDataReceived"/> 且不调用 base，
        /// 此方法可能不会被触发。建议在 <see cref="OnDataReceived"/> 中处理。
        /// </para>
        /// </summary>
        /// <param name="clientEndpoint">请求方的网络地址</param>
        /// <returns>要发送的完整协议帧字节数组（通常由 <see cref="PacketWriter"/> 构建），返回 null 则不发送回复</returns>
        protected abstract byte[] OnDiscoveryRequest(IPEndPoint clientEndpoint);

        /// <summary>
        /// 向指定地址异步发送回复数据。
        /// </summary>
        /// <param name="data">要发送的完整协议帧</param>
        /// <param name="target">目标网络地址</param>
        protected async Task ReplyAsync(byte[] data, IPEndPoint target)
        {
            if (_udpServer != null && data != null && data.Length > 0)
                await _udpServer.SendAsync(data, data.Length, target);
        }

        /// <summary>监听成功启动时调用，参数为实际绑定的端口号</summary>
        protected virtual void OnListenStarted(int port) { }

        /// <summary>
        /// 收到任何原始 UDP 数据报时立即调用（在解码之前）。
        /// 用于网络诊断：如果此方法被调用说明网络通畅，问题在解码或命令码匹配。
        /// </summary>
        /// <param name="remote">发送方地址</param>
        /// <param name="byteCount">原始字节数</param>
        protected virtual void OnRawDataReceived(IPEndPoint remote, int byteCount) { }

        /// <summary>监听循环发生未处理异常时调用</summary>
        protected virtual void OnListenError(Exception ex) { }

        /// <summary>释放资源：停止监听、关闭 Socket</summary>
        public void Dispose() => Stop();
    }
}
