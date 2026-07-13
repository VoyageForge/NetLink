using NUnit.Framework;
using VoyageForge.NetLink.Runtime;

namespace VoyageForge.NetLink.Tests
{
    [TestFixture]
    public class DefaultBodyCodecTests
    {
        private DefaultBodyCodec _codec;

        [SetUp]
        public void SetUp() => _codec = new DefaultBodyCodec();

        [Test]
        public void EncodeAndDecode_往返正确()
        {
            byte[] payload = { 0x01, 0x02, 0x03 };
            byte[] body = _codec.Encode("TestType", payload);

            var (typeId, decoded) = _codec.Decode(body);
            Assert.AreEqual("TestType", typeId);
            Assert.AreEqual(payload, decoded);
        }

        [Test]
        public void Encode_空TypeId_Decode正确()
        {
            byte[] payload = { 0xFF };
            byte[] body = _codec.Encode("", payload);

            var (typeId, decoded) = _codec.Decode(body);
            Assert.AreEqual("", typeId);
            Assert.AreEqual(payload, decoded);
        }

        [Test]
        public void Encode_空Payload_Decode正确()
        {
            byte[] body = _codec.Encode("Data", null);

            var (typeId, decoded) = _codec.Decode(body);
            Assert.AreEqual("Data", typeId);
            Assert.AreEqual(0, decoded.Length);
        }

        [Test]
        public void Decode_校验失败_抛异常()
        {
            byte[] body = _codec.Encode("X", new byte[] { 1 });
            body[body.Length - 1] ^= 0xFF; // 破坏校验位

            Assert.Throws<System.InvalidOperationException>(() => _codec.Decode(body));
        }

        [Test]
        public void Encode_泛型_Payload子类()
        {
            var expected = new TestPayload { Value = 42 };
            byte[] body = _codec.Encode(expected);

            var (typeId, decoded) = _codec.Decode(body);
            Assert.AreEqual(nameof(TestPayload), typeId);
            Assert.AreEqual(new byte[] { 42 }, decoded);
        }

        [Test]
        public void Decode_空帧体_抛异常()
        {
            Assert.Throws<System.ArgumentException>(() => _codec.Decode(null));
            Assert.Throws<System.ArgumentException>(() => _codec.Decode(new byte[] { 0 }));
        }

        private class TestPayload : Payload
        {
            public int Value;
            public override byte[] Serialize() => new byte[] { (byte)Value };
            public override void Deserialize(byte[] data) { if (data.Length > 0) Value = data[0]; }
        }
    }
}
