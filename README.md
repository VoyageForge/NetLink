# 双字节起止符 UDP 发现框架

## 概述
本框架提供了一套基于 **双字节起止符（SOF=0xAA55, EOF=~SOF=0x55AA）** 的二进制协议，用于局域网内的服务发现。  
它包括：
- **PacketCodec**：编解码器，自动处理粘包/拆包/脏数据。
- **UdpDiscoveryClientBase**：客户端抽象基类，负责广播发现请求，并回调发现结果。
- **UdpDiscoveryHostBase**：服务端抽象基类，负责监听请求并回调回复逻辑。

## 设计特点
- **双字节起止符**：大幅降低误判概率（1/65536）。
- **滑动窗口解析**：高效、抗干扰，自动跳过无效数据。
- **抽象回调设计**：解耦网络层与业务逻辑，您只需继承基类并实现抽象方法。
- **跨平台**：纯 C#，支持 Unity 2020+ 及 .NET Standard 2.0。

## 快速开始

### 1. 服务端（Host）
创建一个脚本继承 `UdpDiscoveryHostBase`，实现 `OnDiscoveryRequest` 方法，回复本机 IP：

```csharp
public class MyHost : UdpDiscoveryHostBase
{
    public MyHost(int port) : base(port) { }
    protected override byte[] OnDiscoveryRequest(IPEndPoint client)
    {
        string ip = GetLocalIP();
        return PacketCodec.Encode(DiscoveryOpcode.DiscoveryReply, Encoding.UTF8.GetBytes(ip));
    }
}
```

然后在 `Start` 中调用 `Start()` 开始监听。

### 2. 客户端（Client）
创建一个脚本继承 `UdpDiscoveryClientBase`，实现 `OnHostDiscovered` 方法，决定如何处理发现的 IP：

```csharp
public class MyClient : UdpDiscoveryClientBase
{
    public MyClient(int port) : base(port) { }
    protected override void OnHostDiscovered(string ip)
    {
        // 例如：建立 TCP 连接
        ConnectToHost(ip);
    }
}
```

调用 `await StartDiscoveryAsync()` 开始发现。

### 3. 协议格式
| 字段 | 长度 | 说明 |
|------|------|------|
| SOF  | 2    | 0xAA 0x55 |
| Len  | 2    | 包体长度（大端），含命令码 + 数据 |
| Cmd  | 1    | 命令码，参见 `DiscoveryOpcode` 枚举 |
| Data | N    | 负载数据（如 IP 字符串） |
| Check| 1    | 异或校验（从 Cmd 到 Data） |
| EOF  | 2    | 0x55 0xAA |

### 4. 内置操作码（`DiscoveryOpcode` 枚举）

| 枚举值 | 值 | 说明 |
|--------|-----|------|
| `DiscoveryRequest` | 0x01 | 客户端广播的发现请求 |
| `DiscoveryReply` | 0x02 | 服务端回复的 IP 地址 |

### 5. 扩展自定义操作码

C# 枚举不支持继承，框架通过以下两种方式支持操作码扩展：

**方式一：定义自己的枚举 + 使用 `byte` 重载**

```csharp
public enum MyOpcode : byte
{
    DeviceInfoRequest = 0x10,
    DeviceInfoReply   = 0x11,
}

// 编码时使用 byte 重载
PacketCodec.Encode((byte)MyOpcode.DeviceInfoRequest, payload);
```

**方式二：重写基类虚方法以识别新操作码**

在 `UdpDiscoveryHostBase` 子类中：

```csharp
protected override bool IsDiscoveryRequest(byte cmd) =>
    base.IsDiscoveryRequest(cmd) || cmd == (byte)MyOpcode.DeviceInfoRequest;
```

在 `UdpDiscoveryClientBase` 子类中：

```csharp
protected override bool IsDiscoveryReply(byte cmd) =>
    base.IsDiscoveryReply(cmd) || cmd == (byte)MyOpcode.DeviceInfoReply;
```

### 6. 注意事项
- **Windows 防火墙**：首次运行时防火墙可能拦截 UDP 入站包。若 Wireshark 能抓到包但应用收不到，以管理员身份执行：
  ```powershell
  New-NetFirewallRule -DisplayName "Unity UDP 8888" -Direction Inbound -Protocol UDP -LocalPort 8888 -Action Allow
  ```
  或临时关闭防火墙验证，确认后重新开启并添加规则。
- 确保防火墙允许 UDP 广播和 TCP 连接端口。
- 在 Unity 中，回调方法可能不在主线程执行，如需操作 GameObject 请使用 `UnityMainThreadDispatcher` 或 `SynchronizationContext`。
- 超时和重试机制可在子类中自定义实现。

## 扩展建议
- 可通过重写 `OnDiscoveryTimeout` 实现重试逻辑。
- 可修改 `PacketCodec.Encode` 以支持加密或压缩。
- 可扩展命令码以支持更多交互（如设备信息交换）。

## 许可证
MIT