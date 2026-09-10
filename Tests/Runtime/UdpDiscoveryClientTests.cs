using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using VoyageForge.NetLink.Runtime;
using VoyageForge.NetLink.Discovery;

namespace VoyageForge.NetLink.Tests
{
    /// <summary>
    /// 覆盖「Editor 停止播放后客户端发现循环未停止」的回归修复：
    /// Stop() 取消 Cts + 置空 socket，广播循环响应取消立即退出。
    /// </summary>
    [TestFixture]
    public class UdpDiscoveryClientTests
    {
        /// <summary>暴露 protected 成员供断言。</summary>
        private sealed class ProbeClient : UdpDiscoveryClientBase
        {
            public ProbeClient(int port) : base(port) { }
            public bool IsCancelled => Cts.IsCancellationRequested;
            public CancellationToken Token => Cts.Token;
            public Task<bool> Broadcast<T>(T probe, int maxRetries, float interval, Func<bool> discovered, CancellationToken token)
                where T : Payload => BroadcastUntilAsync(probe, maxRetries, interval, discovered, token);
        }

        [Test]
        public void Stop_取消令牌()
        {
            var client = new ProbeClient(0);
            client.Start();
            Assert.IsFalse(client.IsCancelled, "Start() 后 token 不应已取消");

            client.Stop();
            Assert.IsTrue(client.IsCancelled, "Stop() 必须取消 Cts —— 发送循环依赖该信号退出");
        }

        [UnityTest]
        public IEnumerator BroadcastUntilAsync_取消后_循环立即退出()
        {
            var client = new ProbeClient(0);
            client.Start();

            // 无限重试、永不“发现”，只能靠取消退出
            var task = client.Broadcast(new DiscoveryRequest(), 0, 0.05f, () => false, client.Token);

            // 让主线程让出若干帧，使循环进入并开始发送
            for (int i = 0; i < 30; i++) yield return null;

            client.Stop(); // → 取消 token

            var deadline = System.DateTime.UtcNow.AddSeconds(3);
            while (!task.IsCompleted && System.DateTime.UtcNow < deadline)
                yield return null;

            Assert.IsTrue(task.IsCompleted, "取消后广播循环应立即退出（回归：修复前会无限循环）");
            Assert.AreEqual(TaskStatus.RanToCompletion, task.Status, "循环应正常退出而非异常");
            Assert.IsFalse(task.Result, "未发现时应返回 false");
        }
    }
}
