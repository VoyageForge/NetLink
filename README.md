# LANServiceDiscovery

基于可替换协议的 UDP 局域网服务发现框架。

## 架构

```
Codec                    ← 消息编解码器（收发 + 回调分发）
├── FrameCodec           ← IFrameCodec / DefaultFrameCodec（帧层：SOF/Len/EOF）
└── BodyCodec            ← IBodyCodec / DefaultBodyCodec（帧体层：TypeId/Payload/Check）

Protocol<T>              ← 泛型消息体 { TypeId, Data:T }
Payload                  ← 负载基类（Cmd + Serialize/Deserialize 配对）
DiscoveryRequest/Reply   ← 内置发现负载
```

## 帧格式

```
SOF(2) + Len(2) + Body(Len) + EOF(2)
Body = [TypeIdLen(2)] [TypeId(N)] [Payload(M)] [Check(1)]
```

- SOF: `0xAA 0x55`，EOF: `0x55 0xAA`
- Len: 2 字节大端，Body 长度
- TypeId: 2 字节大端长度前缀 + UTF-8 字符串
- Payload: 负载数据
- Check: 1 字节异或校验（TypeIdLen → Payload 末）

## 快速开始

### 服务端

```csharp
public class MyHost : UdpDiscoveryHostBase
{
    public MyHost() : base(8888) { }

    public void Start()
    {
        Codec.On<DiscoveryRequest>(async msg =>
        {
            byte[] frame = Codec.Encode(new DiscoveryReply("192.168.1.1"));
            await ReplyAsync(frame, RemoteEndPoint);
        });
        StartSync();
    }
}
```

### 客户端

```csharp
public class MyClient : UdpDiscoveryClientBase
{
    public MyClient() : base(8888) { }

    public async Task Discover()
    {
        Codec.On<DiscoveryReply>(msg =>
        {
            foreach (var ip in msg.Data.Ips)
                Debug.Log($"发现: {ip}");
        });

        Start();
        while (true)
        {
            await SendAsync(new DiscoveryRequest());
            await Task.Delay(2000);
        }
    }
}
```

## 自定义负载

```csharp
public class ChatMessage : Payload
{
    public string Name;
    public string Text;

    public ChatMessage() => Cmd = 0x10;

    public override byte[] Serialize() =>
        Encoding.UTF8.GetBytes(
            JsonConvert.SerializeObject(new { Name, Text }));

    public override void Deserialize(byte[] data)
    {
        var obj = JsonConvert.DeserializeAnonymousType(
            Encoding.UTF8.GetString(data), new { Name = "", Text = "" });
        Name = obj.Name; Text = obj.Text;
    }
}

// 注册
Codec.On<ChatMessage>(msg => Debug.Log($"{msg.Data.Name}: {msg.Data.Text}"));

// 发送
await SendAsync(new ChatMessage { Name = "Me", Text = "Hello" });
```

## 替换协议组件

```csharp
// 替换帧层（自定义帧尾 Tag）
Codec.FrameCodec = new MyFrameCodec();

// 替换帧体层（加密）
Codec.BodyCodec = new MyEncryptedBodyCodec();
```

## Windows 防火墙

首次运行时 UDP 入站可能被拦：

```powershell
New-NetFirewallRule -DisplayName "Unity UDP 8888" -Direction Inbound -Protocol UDP -LocalPort 8888 -Action Allow
```

## 许可证

MIT
