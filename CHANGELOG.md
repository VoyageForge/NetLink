## v0.0.13
- feat(Codec): 事件机制改为多播 + 退订句柄
  - 同一类型支持多个订阅者（Dictionary<string, List<Action>>）
  - On<T> 返回 IDisposable 退订句柄，Dispose 即移除（幂等）
  - Dispatch 快照遍历 + 异常隔离（单个 handler 抛异常不影响其他）
  - 补充 Codec 测试至 11 项（多播 / 退订 / 异常隔离 / 幂等 / 快照边界）
- fix(Discovery): 修复 Stop() 同步 Wait 阻塞主线程导致搜索循环不停止
  - 去掉 Stop() 的同步 Wait(1000)，接收线程靠取消令牌 + 关闭 socket 自行退出
- sample: 示例保存订阅句柄并在 Destroy 退订
- docs: 更新 README（目录结构、遍历/退化、精细控制钩子、抓包示例）

## v0.0.12
- fix(Discovery): 忽略 UDP ConnectionReset（ICMP Port Unreachable），接收循环异常后继续运行
  - 广播 / 遍历探测时，无监听端口的主机会回 ICMP Port Unreachable，Windows 将其映射为 ConnectionReset
  - 修复前一次异常即中断接收循环；修复后忽略并继续接收
- feat(Sample): 新增抓包示例 ExampleCapture / Capture（跳过广播、直接遍历真实网段）
- test(Discovery): 测试端口统一改为 8888 便于抓包

## v0.0.11
- feat(Discovery): 重构 UDP 发现模块，新增遍历 / 退化模式
  - 核心代码从 Sample 移入 Runtime/Scripts/Discovery（命名空间 VoyageForge.NetLink.Discovery），与编解码框架分离
  - 新增局域网遍历查找（EnumerateHosts / ScanHostsAsync / ScanNetworkAsync），并发分批遍历
  - 新增广播 → 遍历退化（DiscoverWithFallbackAsync），子类可精细控制（并发度 / 网段 / 超时）
  - 修复客户端停止后广播循环未停止（Stop 取消令牌 + 置空 socket）
  - 补充示例与单元测试

## v0.0.10
- fix(LANDiscovery): 修复 UDP 广播发现在多虚拟网卡环境下发不出去的问题
  - 广播地址由 255.255.255.255 改为「默认路由物理网卡」的子网定向广播（如 192.168.2.255），避免被 Docker Desktop(依赖 WSL2)/Hyper-V/VPN 等虚拟网卡按更低的 metric 抢走
  - 新增 GetDefaultInterfaceAddress()：通过 UDP connect 8.8.8.8 探测默认路由接口
  - 重写 GetSubnetBroadcastAddress()：用默认接口 + 掩码计算定向广播
  - 补充完整中文注释与踩坑记录

## v0.0.9
- chore(NetLink): set version to 0.0.9
- Merge remote-tracking branch 'origin/dev' into dev
- chore(NetLink): update changelog for v0.0.8

## v0.0.8
- chore(NetLink): set version to 0.0.8
- Rename workflow from Depot to NetLink
- chore: add publish workflow for depot
- add licencs
- Add author and license details
