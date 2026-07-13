using System;
using System.Collections.Generic;

namespace LANServiceDiscovery.Runtime
{
    /// <summary>
    /// 默认帧层编解码器：SOF(0xAA55) + Len(2) + FrameBody + Tail(0x55AA)。
    /// <para>扩展：重写 <see cref="BuildTail"/> 自定义帧尾（Tag/签名）。</para>
    /// </summary>
    public class DefaultFrameCodec : IFrameCodec
    {
        /// <summary>SOF 高字节</summary>
        protected byte Sof_H = 0xAA;
        /// <summary>SOF 低字节</summary>
        protected byte Sof_L = 0x55;
        /// <summary>EOF 高字节</summary>
        protected byte Eof_H = 0x55;
        /// <summary>EOF 低字节</summary>
        protected byte Eof_L = 0xAA;
        /// <summary>帧头长度：SOF(2) + Len(2)</summary>
        protected const int HeadLen = 4;
        /// <summary>帧尾长度：EOF(2)</summary>
        protected const int TailLen = 2;

        /// <summary>粘包处理缓冲区</summary>
        private readonly List<byte> _buf = new List<byte>();

        // ==================== 提取 ====================

        /// <inheritdoc/>
        public virtual void Feed(byte[] raw)
        {
            if (raw?.Length > 0) _buf.AddRange(raw);
        }

        /// <inheritdoc/>
        public virtual bool TryExtract(out byte[] frameBody)
        {
            frameBody = null;
            while (true)
            {
                int head = FindSof();
                if (head == -1)
                {
                    // 未找到 SOF：保留尾部 3 字节（防止 SOF 被截断跨两次接收）
                    if (_buf.Count > 3) _buf.RemoveRange(0, _buf.Count - 3);
                    return false;
                }
                if (head > 0) _buf.RemoveRange(0, head);          // 丢弃 SOF 前的无效数据

                if (_buf.Count < HeadLen) return false;            // 帧头不完整

                int bodyLen  = (_buf[2] << 8) | _buf[3];          // 包体长度
                int totalLen = HeadLen + bodyLen + TailLen;

                if (_buf.Count < totalLen) return false;           // 拆包：等待更多数据

                if (_buf[totalLen - 2] == Eof_H && _buf[totalLen - 1] == Eof_L) // EOF 校验
                {
                    frameBody = new byte[bodyLen];
                    Array.Copy(_buf.ToArray(), HeadLen, frameBody, 0, bodyLen);
                    _buf.RemoveRange(0, totalLen);
                    return true;
                }
                _buf.RemoveAt(0);                                  // 非法帧：滑动 1 字节重试
            }
        }

        // ==================== 封装 ====================

        /// <inheritdoc/>
        public virtual byte[] Pack(byte[] body)
        {
            byte[] tail = BuildTail(body);
            int tLen = tail?.Length ?? 0;
            ushort bodyLen = (ushort)body.Length;
            int total = HeadLen + bodyLen + tLen;
            byte[] frame = new byte[total];
            int pos = 0;

            frame[pos++] = Sof_H; frame[pos++] = Sof_L;                        // SOF
            frame[pos++] = (byte)(bodyLen >> 8); frame[pos++] = (byte)(bodyLen & 0xFF); // Len
            Array.Copy(body, 0, frame, pos, body.Length); pos += body.Length;  // Body
            if (tLen > 0) Array.Copy(tail, 0, frame, pos, tLen);              // Tail

            return frame;
        }

        /// <inheritdoc/>
        public virtual void Reset() => _buf.Clear();

        // ==================== 钩子 ====================

        /// <summary>查找 SOF 起始位置</summary>
        protected virtual int FindSof()
        {
            for (int i = 0; i <= _buf.Count - 2; i++)
                if (_buf[i] == Sof_H && _buf[i + 1] == Sof_L) return i;
            return -1;
        }

        /// <summary>构建帧尾（默认 EOF）</summary>
        protected virtual byte[] BuildTail(byte[] body) => new byte[] { Eof_H, Eof_L };
    }
}
