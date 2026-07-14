# NetLink

可替换协议的二进制消息编解码框架。

## 架构

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
            byte[] frame = Codec.Encode(new DiscoveryReply());
            await ReplyAsync(frame, msg.Remote);
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
            Debug.Log($"发现: {msg.Remote.Address}"));

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
public class ChatMessage : JsonPayload
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

// 替换帧体层（加密/压缩）
Codec.BodyCodec = new MyBodyCodec();

// 替换 Payload 序列化（默认 JsonPayload）
public class MyPayload : Payload { ... }  // 手写 Serialize/Deserialize
```

## 安装

```
https://github.com/VoyageForge/NetLink.git#v0.0.3
```

## 许可证

MIT
