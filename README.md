# NetLink

可替换协议的二进制消息编解码框架，内置基于 UDP 的局域网设备发现（广播 / 遍历 / 退化）。

## 目录结构

```
Runtime/
  Scripts/                       ← 核心编解码框架（VoyageForge.NetLink.Runtime）
    Codec.cs
    Protocol.cs / Payload.cs / JsonPayload.cs
    DefaultFrameCodec.cs / DefaultBodyCodec.cs
    Interfaces/
    Discovery/                   ← UDP 设备发现（VoyageForge.NetLink.Discovery）
      UdpDiscoveryHostBase.cs
      UdpDiscoveryClientBase.cs
      UdpDiscoveryScannerBase.cs
      DiscoveryRequest.cs / DiscoveryOpcode.cs
Samples~/LANDiscovery/           ← 使用示例（广播 / 遍历 / 退化 / 抓包）
Tests/Runtime/                   ← 单元测试
```

两个命名空间：

- `VoyageForge.NetLink.Runtime` —— 编解码框架，与传输无关。
- `VoyageForge.NetLink.Discovery` —— 基于 UDP 的设备发现，依赖前者。

## 架构（编解码框架）

```
Codec                       ← 消息编解码器（收发 + 回调分发）
├── FrameCodec              ← IFrameCodec / DefaultFrameCodec（帧层：SOF/Len/EOF）
└── BodyCodec               ← IBodyCodec / DefaultBodyCodec（帧体层：TypeId/Payload/Check）

Protocol<T>                 ← 泛型消息体 { TypeId, Data:T }
ReceivedMessage<T>          ← 收到的消息 { Data, Remote }
Payload / JsonPayload       ← 负载基类（Cmd + Serialize/Deserialize）
```

## 帧格式

```
SOF(2) + Len(2) + Body(Len) + EOF(2)
Body = [TypeIdLen(2)] [TypeId(N)] [Payload(M)] [Check(1)]
```

- SOF: `0xAA 0x55`，EOF: `0x55 0xAA`
- Check: 1 字节异或校验
- TypeId 自动取 `typeof(T).Name`，收发两端一致即可命中回调

## 快速开始：编解码

```csharp
using VoyageForge.NetLink.Runtime;

var codec = new Codec();
codec.On<ChatMessage>(msg => Console.WriteLine($"{msg.Data.Name}: {msg.Data.Text}"));

byte[] frame = codec.Encode(new ChatMessage { Name = "Me", Text = "Hello" });
codec.Feed(frame);
codec.Dispatch(remoteEndPoint);   // 触发回调
```

## UDP 设备发现

### 服务端（UdpDiscoveryHostBase）

```csharp
using VoyageForge.NetLink.Discovery;
using VoyageForge.NetLink.Runtime;

public class MyHost : UdpDiscoveryHostBase
{
    public MyHost() : base(8888) { }

    public void Start()
    {
        // 收到发现请求后回复
        Codec.On<DiscoveryRequest>(async msg =>
        {
            byte[] frame = Codec.Encode(new DiscoveryReply());
            await ReplyAsync(frame, msg.Remote);
        });
        StartSync();   // 绑定 0.0.0.0:8888，后台监听
    }
}
```

### 客户端广播发现（UdpDiscoveryClientBase）

```csharp
public class MyClient : UdpDiscoveryClientBase
{
    private bool _found;

    public MyClient() : base(8888)
    {
        Codec.On<DiscoveryReply>(msg =>
        {
            _found = true;
            Debug.Log($"发现服务端: {msg.Remote.Address}");
        });
    }

    public async Task DiscoverAsync()
    {
        Start();
        // 定时广播，直到发现 / 达到重试上限 / 取消
        bool found = await BroadcastUntilAsync(
            new DiscoveryRequest(), maxRetries: 3, retryIntervalSeconds: 1f,
            discovered: () => _found, token: Cts.Token);
    }
}
```

### 遍历查找（ScanHostsAsync / ScanNetworkAsync）

广播快但可能被虚拟网卡 / 交换机策略吞掉；遍历逐台探测更可靠但更慢。

```csharp
using System.Net;

// 枚举网段所有主机地址（排除网络地址与广播地址）
var hosts = UdpDiscoveryClientBase.EnumerateHosts(
    IPAddress.Parse("192.168.2.0"), IPAddress.Parse("255.255.255.0")); // 254 个

// 按网段并发遍历：分批定向探测，响应由 Codec.On<DiscoveryReply> 收集
await ScanNetworkAsync(
    IPAddress.Parse("192.168.2.0"), IPAddress.Parse("255.255.255.0"),
    new DiscoveryRequest(), perHostTimeoutMs: 100, token: Cts.Token);
```

### 退化模式（DiscoverWithFallbackAsync）

先广播，广播没搜到时自动退化到「并发遍历网段」。

```csharp
public async Task DiscoverAsync()
{
    Start();
    bool found = await DiscoverWithFallbackAsync(
        new DiscoveryRequest(), maxRetries: 3, retryIntervalSeconds: 1f,
        discovered: () => _found, token: Cts.Token);
}
```

### 精细控制（子类可覆盖的钩子）

| 钩子 | 默认 | 作用 |
|---|---|---|
| `FallbackEnabled` | `true` | 广播失败后是否退化到遍历 |
| `GetFallbackNetwork()` | 本机默认路由网段 | 退化遍历的网段 |
| `GetFallbackPerHostTimeoutMs()` | `50` | 每批响应等待超时(ms) |
| `GetScanConcurrency()` | `64` | 并发度（每批主机数，`1` = 串行） |

```csharp
// 示例：自定义退化网段与并发度
protected override (IPAddress, IPAddress) GetFallbackNetwork()
    => (IPAddress.Parse("192.168.2.0"), IPAddress.Parse("255.255.255.0"));

protected override int GetScanConcurrency() => 128;
```

## 自定义负载

```csharp
public class ChatMessage : JsonPayload   // 默认 JSON 序列化
{
    public string Name;
    public string Text;
    public ChatMessage() => Cmd = 0x10;
}

// 注册
Codec.On<ChatMessage>(msg =>
    Debug.Log($"{msg.Data.Name}: {msg.Data.Text} [from {msg.Remote}]"));

// 发送
await SendAsync(new ChatMessage { Name = "Me", Text = "Hello" });
```

## 扩展点

```csharp
// 替换帧层（自定义帧尾）
Codec.FrameCodec = new MyFrameCodec();

// 替换帧体层（加密 / 压缩）
Codec.BodyCodec = new MyBodyCodec();

// 替换 Payload 序列化（默认 JsonPayload）
public class MyPayload : Payload { ... }  // 手写 Serialize/Deserialize
```

## 示例（Samples~/LANDiscovery）

| 示例 | 说明 |
|---|---|
| `ExampleHost` / `Host` | 服务端：监听 8888，回复发现请求 |
| `ExampleClient` / `Client` | 客户端：广播发现（自动退化） |
| `ExampleScanner` / `Scanner` | 客户端：遍历 / 退化，可配置并发度、网段、超时 |
| `ExampleCapture` / `Capture` | 抓包示例：直接遍历真实网段，供另一台机器抓包观察（`udp.port == 8888`） |

## 注意事项

- UDP 广播 / 遍历时，没有监听目标端口的主机会回 ICMP Port Unreachable，Windows 会将其映射为 `ConnectionReset`。NetLink 已在接收循环中忽略该错误并继续接收，属正常现象。
- 广播地址默认取「默认路由物理网卡」的定向广播（如 `192.168.2.255`），避免 `255.255.255.255` 在多虚拟网卡（WSL2 / Hyper-V / VPN）环境下发错网卡。

## 安装

```
https://github.com/VoyageForge/NetLink.git#v0.0.12
```

或通过 Package Manager → Add package from git URL 粘贴上面的地址。

## 许可证

MIT
