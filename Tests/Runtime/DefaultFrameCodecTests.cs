using NUnit.Framework;
using VoyageForge.NetLink.Runtime;

namespace VoyageForge.NetLink.Tests
{
    [TestFixture]
    public class DefaultFrameCodecTests
    {
        private DefaultFrameCodec _codec;

        [SetUp]
        public void SetUp() => _codec = new DefaultFrameCodec();

        [Test]
        public void Pack_空帧体_产出最小完整帧()
        {
            byte[] frame = _codec.Pack(new byte[0]);
            // SOF(2) + Len=0(2) + Body(0) + EOF(2) = 6 bytes
            Assert.AreEqual(6, frame.Length);
            Assert.AreEqual(0xAA, frame[0]);
            Assert.AreEqual(0x55, frame[1]);
            Assert.AreEqual(0x00, frame[2]); // Len Hi = 0
            Assert.AreEqual(0x00, frame[3]); // Len Lo = 0
            Assert.AreEqual(0x55, frame[4]); // EOF
            Assert.AreEqual(0xAA, frame[5]);
        }

        [Test]
        public void PackAndExtract_单帧_往返正确()
        {
            byte[] body = { 1, 2, 3, 4, 5 };
            byte[] frame = _codec.Pack(body);

            _codec.Feed(frame);
            Assert.IsTrue(_codec.TryExtract(out byte[] extracted));
            Assert.AreEqual(body, extracted);
        }

        [Test]
        public void TryExtract_粘包_逐个提取两个帧()
        {
            byte[] frame1 = _codec.Pack(new byte[] { 1, 2, 3 });
            byte[] frame2 = _codec.Pack(new byte[] { 4, 5 });

            // 粘包：两个帧拼在一起
            byte[] glued = new byte[frame1.Length + frame2.Length];
            frame1.CopyTo(glued, 0);
            frame2.CopyTo(glued, frame1.Length);

            _codec.Feed(glued);

            Assert.IsTrue(_codec.TryExtract(out byte[] body1));
            Assert.AreEqual(new byte[] { 1, 2, 3 }, body1);

            Assert.IsTrue(_codec.TryExtract(out byte[] body2));
            Assert.AreEqual(new byte[] { 4, 5 }, body2);

            Assert.IsFalse(_codec.TryExtract(out _));
        }

        [Test]
        public void TryExtract_拆包_数据不足返回false()
        {
            byte[] fullFrame = _codec.Pack(new byte[] { 1, 2, 3, 4 });

            // 只给前半部分
            byte[] partial = new byte[fullFrame.Length / 2];
            System.Array.Copy(fullFrame, partial, partial.Length);

            _codec.Feed(partial);
            Assert.IsFalse(_codec.TryExtract(out _)); // 半包

            // 补全
            byte[] rest = new byte[fullFrame.Length - partial.Length];
            System.Array.Copy(fullFrame, partial.Length, rest, 0, rest.Length);
            _codec.Feed(rest);

            Assert.IsTrue(_codec.TryExtract(out byte[] body));
            Assert.AreEqual(new byte[] { 1, 2, 3, 4 }, body);
        }

        [Test]
        public void TryExtract_脏数据_自动跳过无效字节()
        {
            // 脏数据：SOF 前面有垃圾字节
            byte[] frame = _codec.Pack(new byte[] { 1, 2 });
            byte[] dirty = new byte[3 + frame.Length];
            dirty[0] = 0x00;
            dirty[1] = 0xFF;
            dirty[2] = 0x00;
            frame.CopyTo(dirty, 3);

            _codec.Feed(dirty);
            Assert.IsTrue(_codec.TryExtract(out byte[] body));
            Assert.AreEqual(new byte[] { 1, 2 }, body);
        }

        [Test]
        public void Reset_清空缓冲区()
        {
            _codec.Feed(new byte[] { 0xAA }); // 半包
            _codec.Reset();
            Assert.IsFalse(_codec.TryExtract(out _));
        }
    }
}
