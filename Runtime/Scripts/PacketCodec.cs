using System;
using System.Collections.Generic;
using System.Text;


namespace LANServiceDiscovery.Runtime
{
    /// <summary>
    /// 双字节起止符协议编解码器（SOF = 0xAA55, EOF = ~SOF = 0x55AA）
    /// 提供打包（Encode）和流式解析（Decoder）功能，自动处理粘包/拆包/脏数据。
    /// </summary>
    public static class PacketCodec
    {
        // 起始符：0xAA 0x55（大端序）
        public static readonly byte[] SOF = new byte[] { 0xAA, 0x55 };

        // 结束符：0x55 0xAA（SOF 的按位取反）
        public static readonly byte[] EOF = new byte[] { 0x55, 0xAA };

        private const int HEADER_SIZE = 4; // 起始符2 + 长度字段2

        /// <summary>
        /// 将内置操作码和数据打包成协议帧（字节数组）
        /// </summary>
        /// <param name="cmd">内置操作码</param>
        /// <param name="data">负载数据（可为null）</param>
        /// <returns>完整的帧字节数组</returns>
        public static byte[] Encode(DiscoveryOpcode cmd, byte[] data)
        {
            return Encode((byte)cmd, data);
        }

        /// <summary>
        /// 将命令码和数据打包成协议帧（字节数组）。
        /// 用于自定义操作码扩展 —— 当使用非 <see cref="DiscoveryOpcode"/> 的自定义命令码时调用此重载。
        /// </summary>
        /// <param name="cmd">命令码（1字节）</param>
        /// <param name="data">负载数据（可为null）</param>
        /// <returns>完整的帧字节数组</returns>
        public static byte[] Encode(byte cmd, byte[] data)
        {
            if (data == null) data = new byte[0];
            if (data.Length > ushort.MaxValue)
                throw new ArgumentException("数据长度超过 65535 字节");

            ushort bodyLen = (ushort)(1 + data.Length); // 命令码1字节 + 数据长度
            int totalLen = 2 + 2 + bodyLen + 1 + 2; // SOF + Len + Body + Check + EOF

            byte[] frame = new byte[totalLen];
            int idx = 0;

            // 1. 起始符
            frame[idx++] = SOF[0];
            frame[idx++] = SOF[1];

            // 2. 包体长度（大端序）
            frame[idx++] = (byte)(bodyLen >> 8);
            frame[idx++] = (byte)(bodyLen & 0xFF);

            // 3. 命令码
            frame[idx++] = cmd;

            // 4. 数据
            if (data.Length > 0)
            {
                Array.Copy(data, 0, frame, idx, data.Length);
                idx += data.Length;
            }

            // 5. 校验和（异或，从命令码到数据结尾）
            byte checksum = 0;
            int checkStart = 4; // 跳过 SOF(2) + Len(2)
            for (int i = checkStart; i < idx; i++)
                checksum ^= frame[i];
            frame[idx++] = checksum;

            // 6. 结束符
            frame[idx++] = EOF[0];
            frame[idx++] = EOF[1];

            return frame;
        }

        /// <summary>
        /// 解码器（带滑动窗口缓存），用于从字节流中提取完整帧。
        /// 建议为每个网络连接（UDP/TCP）单独维护一个 Decoder 实例。
        /// </summary>
        public class Decoder
        {
            private List<byte> _buffer = new List<byte>();

            /// <summary>
            /// 输入新接收到的原始数据，尝试解析出所有完整的合法帧
            /// </summary>
            /// <param name="receivedBytes">本次收到的原始字节</param>
            /// <param name="resultList">输出的帧列表，每个元素为 (命令码, 数据)</param>
            public void ParseBytes(byte[] receivedBytes, List<(byte cmd, byte[] data)> resultList)
            {
                if (receivedBytes == null || receivedBytes.Length == 0) return;

                _buffer.AddRange(receivedBytes);

                while (true)
                {
                    // ---- 步骤1：查找双字节起始符 0xAA 0x55 ----
                    int headIndex = -1;
                    for (int i = 0; i <= _buffer.Count - 2; i++)
                    {
                        if (_buffer[i] == SOF[0] && _buffer[i + 1] == SOF[1])
                        {
                            headIndex = i;
                            break;
                        }
                    }

                    // 没找到：仅保留尾部3个字节（防止跨包匹配），清理已确认的无效数据
                    if (headIndex == -1)
                    {
                        if (_buffer.Count > 3)
                            _buffer.RemoveRange(0, _buffer.Count - 3);
                        return;
                    }

                    // 丢弃起始符之前的数据（脏数据）
                    if (headIndex > 0)
                    {
                        _buffer.RemoveRange(0, headIndex);
                        headIndex = 0;
                    }

                    // ---- 步骤2：检查是否足够读取长度字段 ----
                    if (_buffer.Count < HEADER_SIZE)
                        return; // 数据不够，等待更多

                    // 读取包体长度（大端序）
                    int bodyLen = (_buffer[2] << 8) | _buffer[3];
                    int totalFrameLen = 2 + 2 + bodyLen + 1 + 2; // SOF+Len+Body+Check+EOF

                    // ---- 步骤3：检查完整帧是否已到达 ----
                    if (_buffer.Count < totalFrameLen)
                        return; // 半包，等待更多

                    // ---- 步骤4：校验结束符和校验和 ----
                    bool valid = true;
                    int tailStart = totalFrameLen - 2;
                    if (_buffer[tailStart] != EOF[0] || _buffer[tailStart + 1] != EOF[1])
                        valid = false;

                    if (valid)
                    {
                        byte checksum = 0;
                        // 校验范围：从命令(索引4) 到 数据结尾（总长-4），不含校验位和EOF
                        for (int i = 4; i < totalFrameLen - 3; i++)
                            checksum ^= _buffer[i];
                        if (checksum != _buffer[totalFrameLen - 3]) // 校验位位于总长-3
                            valid = false;
                    }

                    // ---- 步骤5：处理结果 ----
                    if (valid)
                    {
                        // 提取命令和数据
                        byte cmd = _buffer[4];
                        int dataLen = bodyLen - 1;
                        byte[] data = new byte[dataLen];
                        if (dataLen > 0)
                            Array.Copy(_buffer.ToArray(), 5, data, 0, dataLen);

                        resultList.Add((cmd, data));

                        // 移除已处理的帧
                        _buffer.RemoveRange(0, totalFrameLen);
                    }
                    else
                    {
                        // 非法帧：滑动窗口向右移一个字节（丢弃当前头字节0xAA，尝试重新匹配）
                        // 这样能正确处理 0xAA 0xAA 0x55 ... 等边界情况
                        _buffer.RemoveAt(0);
                    }
                }
            }

            /// <summary>
            /// 清空内部缓存（例如连接断开时）
            /// </summary>
            public void Clear() => _buffer.Clear();
        }
    }
}