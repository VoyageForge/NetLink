using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using VoyageForge.NetLink.Runtime;
using VoyageForge.NetLink.Discovery;

namespace VoyageForge.NetLink.Tests
{
    /// <summary>局域网「遍历查找设备」模式：网段枚举 + 回环端到端发现。</summary>
    [TestFixture]
    public class UdpDiscoveryScannerTests
    {
        private sealed class TestHost : UdpDiscoveryHostBase
        {
            public TestHost(int port) : base(port) { }
            public void StartListen() => StartSync();
            public void Shutdown() => Stop();
            public Task Reply(byte[] frame, IPEndPoint target) => ReplyAsync(frame, target);
        }

        private sealed class TestScanner : UdpDiscoveryScannerBase
        {
            public TestScanner(int port) : base(port) { }
        }

        // ==================== EnumerateHosts 纯函数 ====================

        [Test]
        public void EnumerateHosts_24位掩码_返回254主机且排除网络与广播()
        {
            var hosts = UdpDiscoveryScannerBase.EnumerateHosts(
                IPAddress.Parse("192.168.2.0"), IPAddress.Parse("255.255.255.0"));

            Assert.AreEqual(254, hosts.Count);
            Assert.AreEqual("192.168.2.1", hosts[0].ToString());
            Assert.AreEqual("192.168.2.254", hosts[253].ToString());
            Assert.IsFalse(hosts.Contains(IPAddress.Parse("192.168.2.0")), "不应含网络地址");
            Assert.IsFalse(hosts.Contains(IPAddress.Parse("192.168.2.255")), "不应含广播地址");
        }

        [Test]
        public void EnumerateHosts_32位掩码_返回单主机()
        {
            var hosts = UdpDiscoveryScannerBase.EnumerateHosts(
                IPAddress.Parse("10.0.0.5"), IPAddress.Parse("255.255.255.255"));

            Assert.AreEqual(1, hosts.Count);
            Assert.AreEqual("10.0.0.5", hosts[0].ToString());
        }

        [Test]
        public void EnumerateHosts_30位掩码_返回两个主机()
        {
            var hosts = UdpDiscoveryScannerBase.EnumerateHosts(
                IPAddress.Parse("10.0.0.0"), IPAddress.Parse("255.255.255.252"));

            Assert.AreEqual(2, hosts.Count);
            Assert.AreEqual("10.0.0.1", hosts[0].ToString());
            Assert.AreEqual("10.0.0.2", hosts[1].ToString());
        }

        [Test]
        public void EnumerateHosts_非IPv4_抛异常()
        {
            Assert.Throws<ArgumentException>(() => UdpDiscoveryScannerBase.EnumerateHosts(
                IPAddress.IPv6Loopback, IPAddress.Parse("255.255.255.0")));
        }

        // ==================== 遍历查找设备（回环端到端） ====================

        [UnityTest]
        public IEnumerator ScanHostsAsync_发现本机回环设备()
        {
            const int port = 8888;

            var host = new TestHost(port);
            host.Codec.On<DiscoveryRequest>(async msg =>
            {
                await host.Reply(host.Codec.Encode(new DiscoveryReply()), msg.Remote);
            });
            host.StartListen();

            var scanner = new TestScanner(port);
            var devices = new List<IPEndPoint>();
            scanner.Codec.On<DiscoveryReply>(msg =>
            {
                lock (devices) devices.Add(msg.Remote);
            });
            scanner.Start();

            // 遍历只含 127.0.0.1 的“网段”，定向探测并等待回复
            var task = scanner.ScanHostsAsync(
                new[] { IPAddress.Loopback }, new DiscoveryRequest(), 100, CancellationToken.None);

            var deadline = System.DateTime.UtcNow.AddSeconds(3);
            while ((!task.IsCompleted || devices.Count == 0) && System.DateTime.UtcNow < deadline)
                yield return null;

            scanner.Stop();
            host.Shutdown();

            Assert.IsTrue(task.IsCompleted, "遍历应在超时内完成");
            lock (devices)
            {
                Assert.IsTrue(devices.Any(d => d.Address.Equals(IPAddress.Loopback)), "应发现回环设备");
            }
        }

        // ==================== 退化模式（广播 → 遍历） ====================

        /// <summary>覆盖退化网段为回环 /32，便于测试（只遍历 127.0.0.1）。</summary>
        private sealed class FallbackClient : UdpDiscoveryClientBase
        {
            public FallbackClient(int port) : base(port) { }
            public CancellationToken Token => Cts.Token;

            protected override (IPAddress Network, IPAddress SubnetMask) GetFallbackNetwork()
                => (IPAddress.Parse("127.0.0.1"), IPAddress.Parse("255.255.255.255"));

            protected override int GetFallbackPerHostTimeoutMs() => 100;
        }

        [UnityTest]
        public IEnumerator DiscoverWithFallbackAsync_广播未发现_退化为遍历并发现设备()
        {
            const int port = 8888;

            var host = new TestHost(port);
            host.Codec.On<DiscoveryRequest>(async msg =>
            {
                await host.Reply(host.Codec.Encode(new DiscoveryReply()), msg.Remote);
            });
            host.StartListen();

            var client = new FallbackClient(port);
            var devices = new List<IPEndPoint>();
            client.Codec.On<DiscoveryReply>(msg =>
            {
                lock (devices) devices.Add(msg.Remote);
            });
            client.Start();

            // discovered 只认回环设备：广播阶段收到的是本机 WLAN 地址（如 192.168.2.16），不满足；
            // 退化遍历 127.0.0.1 后才收到回环设备 → 发现
            var task = client.DiscoverWithFallbackAsync(
                new DiscoveryRequest(), 1, 0.05f,
                () => { lock (devices) return devices.Any(d => d.Address.Equals(IPAddress.Loopback)); },
                client.Token);

            var deadline = System.DateTime.UtcNow.AddSeconds(8);
            while (!task.IsCompleted && System.DateTime.UtcNow < deadline)
                yield return null;

            client.Stop();
            host.Shutdown();

            Assert.IsTrue(task.IsCompleted, "退化流程应在超时内完成");
            Assert.IsTrue(task.Result, "退化遍历后应发现设备");
            lock (devices)
            {
                Assert.IsTrue(devices.Any(d => d.Address.Equals(IPAddress.Loopback)), "应发现回环设备");
            }
        }

        // ==================== 并发遍历 ====================

        /// <summary>覆盖并发度为 2，验证子类可精细控制并发粒度。</summary>
        private sealed class ConcurrentScanner : UdpDiscoveryScannerBase
        {
            public ConcurrentScanner(int port) : base(port) { }
            protected override int GetScanConcurrency() => 2;
        }

        [UnityTest]
        public IEnumerator ScanHostsAsync_并发遍历多主机_仍能发现设备()
        {
            const int port = 8888;

            var host = new TestHost(port);
            host.Codec.On<DiscoveryRequest>(async msg =>
            {
                await host.Reply(host.Codec.Encode(new DiscoveryReply()), msg.Remote);
            });
            host.StartListen();

            var scanner = new ConcurrentScanner(port);
            var devices = new List<IPEndPoint>();
            scanner.Codec.On<DiscoveryReply>(msg =>
            {
                lock (devices) devices.Add(msg.Remote);
            });
            scanner.Start();

            // 并发度 2，遍历两个回环主机（127.0.0.1 与 127.0.0.2 同属回环网段）
            var task = scanner.ScanHostsAsync(
                new[] { IPAddress.Loopback, IPAddress.Parse("127.0.0.2") },
                new DiscoveryRequest(), 100, CancellationToken.None);

            var deadline = System.DateTime.UtcNow.AddSeconds(3);
            while ((!task.IsCompleted || devices.Count == 0) && System.DateTime.UtcNow < deadline)
                yield return null;

            scanner.Stop();
            host.Shutdown();

            Assert.IsTrue(task.IsCompleted, "并发遍历应在超时内完成");
            lock (devices)
            {
                Assert.IsTrue(devices.Count >= 1, "并发遍历后应发现设备");
            }
        }
    }
}
