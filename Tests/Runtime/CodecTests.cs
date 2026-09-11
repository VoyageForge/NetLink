using NUnit.Framework;
using VoyageForge.NetLink.Runtime;

namespace VoyageForge.NetLink.Tests
{
    [TestFixture]
    public class CodecTests
    {
        private Codec _codec;

        [SetUp]
        public void SetUp() => _codec = new Codec();

        [Test]
        public void EncodeAndFeedDispatch_往返回调正确()
        {
            var received = false;
            _codec.On<TestPayload>(msg =>
            {
                received = true;
                Assert.AreEqual(123, msg.Data.Value);
            });

            var packet = new TestPayload { Value = 123 };
            byte[] frame = _codec.Encode(packet);

            _codec.Feed(frame);
            _codec.Dispatch(null);

            Assert.IsTrue(received);
        }

        [Test]
        public void Dispatch_多帧_逐帧回调()
        {
            var count = 0;
            _codec.On<TestPayload>(msg =>
            {
                count++;
                Assert.AreEqual(100 + count, msg.Data.Value);
            });

            var p1 = new TestPayload { Value = 101 };
            var p2 = new TestPayload { Value = 102 };

            byte[] frame1 = _codec.Encode(p1);
            byte[] frame2 = _codec.Encode(p2);

            // 粘包
            byte[] glued = new byte[frame1.Length + frame2.Length];
            frame1.CopyTo(glued, 0);
            frame2.CopyTo(glued, frame1.Length);

            _codec.Feed(glued);
            _codec.Dispatch(null);

            Assert.AreEqual(2, count);
        }

        [Test]
        public void Encode_字符串重载()
        {
            byte[] payload = { 0x0A, 0x0B };
            byte[] frame = _codec.Encode("MyType", payload);

            _codec.Feed(frame);
            // 未注册 handler，Dispatch 不会回调（内部丢弃）
            _codec.Dispatch(null);
        }

        [Test]
        public void Dispatch_未注册TypeId_不触发任何回调()
        {
            var called = false;
            _codec.On<TestPayload>(_ => called = true);

            // 构造一个 TypeId 不匹配的帧
            byte[] frame = _codec.Encode("UnknownType", new byte[] { 1 });
            _codec.Feed(frame);
            _codec.Dispatch(null);

            Assert.IsFalse(called);
        }

        [Test]
        public void On_同类型多个订阅者_全部收到()
        {
            int a = 0, b = 0;
            _codec.On<TestPayload>(_ => a++);
            _codec.On<TestPayload>(_ => b++);

            _codec.Feed(_codec.Encode(new TestPayload { Value = 1 }));
            _codec.Dispatch(null);

            Assert.AreEqual(1, a, "第一个订阅者应收到");
            Assert.AreEqual(1, b, "第二个订阅者应收到");
        }

        [Test]
        public void On_返回句柄_Dispose后退订()
        {
            int count = 0;
            var sub = _codec.On<TestPayload>(_ => count++);

            _codec.Feed(_codec.Encode(new TestPayload()));
            _codec.Dispatch(null);
            Assert.AreEqual(1, count);

            sub.Dispose();

            _codec.Feed(_codec.Encode(new TestPayload()));
            _codec.Dispatch(null);
            Assert.AreEqual(1, count, "退订后不应再收到消息");
        }

        [Test]
        public void Dispatch_单个handler抛异常_不影响其他handler()
        {
            int ok = 0;
            System.Exception captured = null;
            _codec.OnHandlerError = ex => captured = ex;

            _codec.On<TestPayload>(_ => { throw new System.InvalidOperationException("boom"); });
            _codec.On<TestPayload>(_ => ok++);

            _codec.Feed(_codec.Encode(new TestPayload()));
            _codec.Dispatch(null);

            Assert.AreEqual(1, ok, "异常 handler 不应影响其他 handler");
            Assert.IsNotNull(captured, "异常应通过 OnHandlerError 上报");
            Assert.IsInstanceOf<System.InvalidOperationException>(captured);
        }

        [Test]
        public void On_退订一个订阅者_不影响其他订阅者()
        {
            int a = 0, b = 0;
            var subA = _codec.On<TestPayload>(_ => a++);
            _codec.On<TestPayload>(_ => b++);

            subA.Dispose();

            _codec.Feed(_codec.Encode(new TestPayload()));
            _codec.Dispatch(null);

            Assert.AreEqual(0, a, "退订的订阅者不应再收到");
            Assert.AreEqual(1, b, "其他订阅者应正常收到");
        }

        [Test]
        public void On_句柄_Dispose幂等()
        {
            int count = 0;
            var sub = _codec.On<TestPayload>(_ => count++);

            sub.Dispose();
            sub.Dispose();   // 二次 Dispose 不应抛异常

            _codec.Feed(_codec.Encode(new TestPayload()));
            _codec.Dispatch(null);
            Assert.AreEqual(0, count, "Dispose 后不应收到");
        }

        [Test]
        public void On_传入null_抛异常()
        {
            Assert.Throws<System.ArgumentNullException>(() => _codec.On<TestPayload>(null));
        }

        [Test]
        public void Dispatch_回调里退订自己_快照遍历不崩且后续不再收到()
        {
            int count = 0;
            System.IDisposable sub = null;
            sub = _codec.On<TestPayload>(_ =>
            {
                count++;
                sub.Dispose();   // 回调里退订自己
            });

            _codec.Feed(_codec.Encode(new TestPayload()));
            _codec.Dispatch(null);
            Assert.AreEqual(1, count, "快照遍历：本轮应执行一次");

            _codec.Feed(_codec.Encode(new TestPayload()));
            _codec.Dispatch(null);
            Assert.AreEqual(1, count, "退订生效：后续不应再收到");
        }

        private class TestPayload : Payload
        {
            public int Value;
            public override byte[] Serialize() => new byte[] { (byte)Value };
            public override void Deserialize(byte[] data) { if (data.Length > 0) Value = data[0]; }
        }
    }
}
