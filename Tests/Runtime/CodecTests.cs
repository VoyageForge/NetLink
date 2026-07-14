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

        private class TestPayload : Payload
        {
            public int Value;
            public override byte[] Serialize() => new byte[] { (byte)Value };
            public override void Deserialize(byte[] data) { if (data.Length > 0) Value = data[0]; }
        }
    }
}
