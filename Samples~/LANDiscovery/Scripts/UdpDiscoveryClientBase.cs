using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using VoyageForge.NetLink.Runtime;
using UnityEngine;

namespace VoyageForge.NetLink.Samples.LANDiscovery
{
    /// <summary>
    /// UDP 广播发现「客户端」基类。
    /// <list type="bullet">
    /// <item>后台线程接收 UDP 包 → <see cref="Codec"/>.Feed / Dispatch 自动分发回调。</item>
    /// <item>发送广播：<see cref="SendAsync{T}"/>(packet)。</item>
    /// <item>业务逻辑：<c>Codec.On&lt;T&gt;(handler)</c> 注册处理器。</item>
    /// </list>
    ///
    /// <para><b>【踩坑记录 · 广播地址为什么不能直接用 255.255.255.255】</b></para>
    /// <para>
    /// 255.255.255.255 是“有限广播”。在 Windows 上，它会在每一张 Up 的 IPv4 网卡路由表里
    /// 各生成一条 On-link 路由，发送时系统按「接口 metric 越小越优先」挑选其中一张网卡把包发出。
    /// </para>
    /// <para>
    /// 问题在于：装了 Docker Desktop（其 Windows 后端依赖 WSL2）、Hyper-V、VMware 或 VPN 之后，
    /// 系统会多出 vEthernet(WSL) 之类的虚拟网卡，而这些虚拟网卡的 metric 往往比真实的
    /// WLAN / 以太网更低。结果 255.255.255.255 被发进了虚拟网络，局域网里真正的设备根本
    /// 收不到（本机 Wireshark 也只能在回环 / 虚拟网卡一侧抓到包）。
    /// </para>
    /// <para>
    /// 解决办法是发「定向广播」（子网广播地址，如 192.168.2.255）。定向广播在路由表里只有
    /// 唯一一条 On-link 路由，会锁定到对应的物理网卡，不存在多网卡抢 metric 的问题。
    /// 因此本类通过「默认路由接口」定位真实物理网卡，而不是遍历网卡取第一个（后者会撞上虚拟网卡）。
    /// </para>
    /// </summary>
    public abstract class UdpDiscoveryClientBase : IDisposable
    {
        private UdpClient _udpClient;
        private CancellationTokenSource _cts;
        private Task _receiveTask;
        private readonly int _broadcastPort;

        /// <summary>消息编解码器（收发 + 回调分发）。</summary>
        public Codec Codec { get; protected set; } = new Codec();

        /// <summary>最近一次收到包的来源地址（即服务端地址）。</summary>
        protected IPEndPoint RemoteEndPoint { get; private set; }

        /// <summary>
        /// 实际用于发送的广播地址。
        /// 默认由 <see cref="GetSubnetBroadcastAddress"/> 自动检测为「默认路由物理网卡」的
        /// 子网定向广播（如 192.168.2.255）；检测失败时回退为 255.255.255.255。
        /// 也可以手动赋值覆盖。
        /// </summary>
        protected IPAddress BroadcastAddress { get; set; }

        /// <summary>
        /// 是否自动检测广播地址，默认 true。
        /// 设为 false 时，使用手动赋值的 <see cref="BroadcastAddress"/>；若为空则回退 255.255.255.255。
        /// </summary>
        public bool AutoDetectBroadcast { get; set; } = true;

        protected UdpDiscoveryClientBase(int broadcastPort)
        {
            _broadcastPort = broadcastPort;
            // 构造时就先探测一次，拿到初始值；Start() 里还会再探测一次（网卡状态可能变化）。
            BroadcastAddress = GetSubnetBroadcastAddress() ?? IPAddress.Broadcast;
        }

        /// <summary>
        /// 启动：创建 UDP socket 并开始后台接收。
        /// socket 绑定 0.0.0.0:随机端口（只用于接收）；发送时由 <see cref="SendAsync{T}"/>
        /// 显式指定目标地址，因此收发可共用同一个 socket。
        /// </summary>
        public void Start()
        {
            if (_receiveTask != null && !_receiveTask.IsCompleted) return;

            // 重新探测广播地址：优先用「默认路由物理网卡」的定向广播；
            // 探测失败才回退 255.255.255.255（在多虚拟网卡环境下可能发错，仅作兜底）。
            if (AutoDetectBroadcast)
            {
                BroadcastAddress = GetSubnetBroadcastAddress() ?? IPAddress.Broadcast;
            }
            else if (BroadcastAddress == null)
            {
                BroadcastAddress = IPAddress.Broadcast;
            }

            _cts = new CancellationTokenSource();
            _udpClient = new UdpClient();
            _udpClient.EnableBroadcast = true;                                                    // Windows 下发送广播必须开启
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true); // 允许多个 socket 复用同一端口
            _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 0));                              // 绑定任意地址 + 随机端口（仅用于接收）
            _receiveTask = Task.Run(() => ReceiveLoop(_cts.Token));

            // ==== 诊断日志（排查用，稳定后可移除）====
            Debug.Log($"[UDP发现] 广播地址={BroadcastAddress}  目标端口={_broadcastPort}");
            Debug.Log("[UDP发现] 本机网卡列表:\n" + GetLocalAddresses());

            OnStarted();
        }

        /// <summary>停止：取消接收循环并释放 socket。</summary>
        public void Stop() { _cts?.Cancel(); _udpClient?.Close(); _receiveTask?.Wait(1000); }

        /// <summary>
        /// 把 <typeparamref name="T"/> 编码成帧，广播到 <see cref="BroadcastAddress"/>:目标端口。
        /// </summary>
        public async Task SendAsync<T>(T packet) where T : Payload
        {
            if (_udpClient == null)
            {
                Debug.LogWarning("[UDP发现] SendAsync 被跳过：_udpClient 为 null（Start 未成功？）");
                return;
            }

            byte[] frame = Codec.Encode(packet);
            try
            {
                await _udpClient.SendAsync(frame, frame.Length, new IPEndPoint(BroadcastAddress, _broadcastPort));
                Debug.Log($"[UDP发现] 已发送 {typeof(T).Name} -> {BroadcastAddress}:{_broadcastPort} (帧长={frame.Length})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UDP发现] 发送失败: {ex.GetType().Name} - {ex.Message}");
            }
        }

        private async Task ReceiveLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var result = await _udpClient.ReceiveAsync();
                    RemoteEndPoint = result.RemoteEndPoint;

                    Codec.Feed(result.Buffer);          // 喂入原始字节（内部处理粘包 / 拆包）
                    Codec.Dispatch(result.RemoteEndPoint); // 提取完整帧并回调对应处理器
                }
            }
            catch (ObjectDisposedException) { }   // Stop() 关闭 socket 触发的正常退出
            catch (OperationCanceledException) { }
            catch (Exception ex) { OnError(ex); }
        }

        /// <summary>
        /// 计算「默认路由物理网卡」的子网定向广播地址（IPv4），找不到返回 null。
        ///
        /// <para><b>为什么用默认路由接口，而不是遍历网卡取第一个？</b></para>
        /// <para>
        /// “遍历网卡取第一个 Up 的”会在 WSL2 / Hyper-V vEthernet、虚拟机、VPN 等虚拟网卡
        /// 前面就命中并返回错误地址（例如算出 172.17.255.255，发进了虚拟网络）。
        /// 而「默认路由接口」是真正能上外网的物理网卡，天然排除没有默认路由的虚拟网卡。
        /// </para>
        /// </summary>
        private IPAddress GetSubnetBroadcastAddress()
        {
            try
            {
                // 先拿到默认路由接口的本地 IP（例如 WLAN 的 192.168.2.16）
                IPAddress defaultIp = GetDefaultInterfaceAddress();
                if (defaultIp == null) return null;

                // 再遍历网卡，找到与 defaultIp 匹配的那张，用其掩码计算定向广播地址
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    foreach (var uni in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (uni.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        if (!uni.Address.Equals(defaultIp)) continue;   // 只认默认路由那张网卡
                        if (uni.IPv4Mask == null) return null;

                        byte[] ipBytes = uni.Address.GetAddressBytes();
                        byte[] maskBytes = uni.IPv4Mask.GetAddressBytes();
                        byte[] broadcastBytes = new byte[4];

                        for (int i = 0; i < 4; i++)
                        {
                            // 广播地址 = 网络号(ip & mask) | 主机位全 1(~mask & 0xFF)
                            broadcastBytes[i] = (byte)((ipBytes[i] & maskBytes[i]) | (~maskBytes[i] & 0xFF));
                        }

                        var result = new IPAddress(broadcastBytes);
                        Debug.Log($"[UDP发现] 默认路由网卡=[{ni.Name}] {defaultIp}，定向广播={result}");
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"自动检测广播地址失败: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// 探测默认路由接口的本机 IPv4。
        ///
        /// <para><b>为什么是 Connect("8.8.8.8")？它真的发包了吗？</b></para>
        /// <para>
        /// UDP 本身无连接，这里对 UDP socket 调 Connect 并不会握手、也不会真正发出任何数据包。
        /// Connect 的唯一作用是让操作系统执行一次「路由查找」：去 8.8.8.8 该走哪张网卡？
        /// </para>
        /// <para>
        /// 8.8.8.8 是公网地址，本机没有它的直连路由，只能走默认路由（0.0.0.0/0）。
        /// 默认路由只挂在真正上外网的那张物理网卡上（如 WLAN 192.168.2.16），而 WSL2 / Hyper-V
        /// 等虚拟网卡没有默认路由、去不了公网，自然被排除。系统于是选中物理网卡，并把 socket 的
        /// 本地地址绑定到该网卡，读 socket.LocalEndPoint 就拿到了默认接口的 IP。
        /// </para>
        /// </summary>
        private IPAddress GetDefaultInterfaceAddress()
        {
            try
            {
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    // UDP connect：不发包、不握手，只是触发路由查找并确定本地出口地址。
                    // 端口 65530 随便填即可，UDP connect 不会真正建立连接。
                    socket.Connect(new IPEndPoint(IPAddress.Parse("8.8.8.8"), 65530));
                    return (socket.LocalEndPoint as IPEndPoint)?.Address;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"探测默认路由接口失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>诊断：列出本机所有 Up 的 IPv4 网卡，方便对照 Wireshark 抓包接口。</summary>
        private string GetLocalAddresses()
        {
            var lines = new System.Collections.Generic.List<string>();
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    foreach (var uni in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (uni.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        string mask = uni.IPv4Mask?.ToString() ?? "无掩码";
                        lines.Add($"  [{ni.Name}] {ni.Description} => {uni.Address} / {mask}");
                    }
                }
            }
            catch (Exception ex)
            {
                lines.Add($"  获取失败: {ex.Message}");
            }
            return lines.Count > 0 ? string.Join("\n", lines) : "  (无 IPv4 网卡)";
        }

        /// <summary>客户端已启动。</summary>
        protected virtual void OnStarted() { }

        /// <summary>接收循环异常回调。</summary>
        protected virtual void OnError(Exception ex) { }

        public void Dispose() => Stop();
    }
}
