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
