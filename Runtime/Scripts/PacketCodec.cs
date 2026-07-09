using System;
using System.Collections.Generic;

namespace LANServiceDiscovery.Runtime
{
    /// <summary>
    /// 双字节起止符协议编解码器。
    /// <para>
    /// <b>协议帧格式（共 8+N 字节）：</b>
    /// <code>
    /// | SOF[2] | Len[2] | Cmd[1] | Data[N] | Check[1] | EOF[2] |
    /// | 0xAA55 | 大端    | 命令码  | 负载    | 异或校验  | 0x55AA |
    /// </code>
    /// </para>
    /// <para>
    /// <b>设计特点：</b>
    /// - 双字节起止符（SOF=0xAA55, EOF=0x55AA），误判概率仅 1/65536
    /// - 1 字节异或校验，校验范围从 Cmd 到 Data 结尾
    /// - 2 字节大端包体长度，最大支持 65535 字节负载
    /// - Decoder 采用滑动窗口，自动处理粘包/拆包/脏数据
    /// </para>
    /// </summary>
    public static class PacketCodec
    {
        /// <summary>起始符：0xAA 0x55（大端序）</summary>
        public static readonly byte[] SOF = { 0xAA, 0x55 };

        /// <summary>结束符：0x55 0xAA（SOF 的按位取反，增强可靠性）</summary>
        public static readonly byte[] EOF = { 0x55, 0xAA };

        /// <summary>帧头固定长度：起始符 2 + 长度字段 2 = 4 字节</summary>
        private const int HEADER_SIZE = 4;

        /// <summary>
        /// 使用内置操作码将负载数据打包为完整的协议帧。
        /// </summary>
        /// <param name="cmd"><see cref="DiscoveryOpcode"/> 内置操作码</param>
        /// <param name="data">负载数据，可为 null（视为空）</param>
        /// <returns>完整的帧字节数组（含 SOF / 长度 / 校验和 / EOF）</returns>
        /// <exception cref="ArgumentException">数据长度超过 65535 字节时抛出</exception>
        public static byte[] Encode(DiscoveryOpcode cmd, byte[] data)
        {
            return Encode((byte)cmd, data);
        }

        /// <summary>
        /// 使用原始命令码将负载数据打包为完整的协议帧。
        /// <para>
        /// <b>扩展用途：</b>当使用自定义操作码（非 <see cref="DiscoveryOpcode"/> 枚举值）时调用此重载。
        /// 例如：<c>PacketCodec.Encode((byte)MyOpcode.CustomCmd, payload);</c>
        /// </para>
        /// </summary>
        /// <param name="cmd">命令码（1 字节，范围 0~255）</param>
        /// <param name="data">负载数据，可为 null（视为空）</param>
        /// <returns>完整的帧字节数组</returns>
        /// <exception cref="ArgumentException">数据长度超过 65535 字节时抛出</exception>
        public static byte[] Encode(byte cmd, byte[] data)
        {
            if (data == null) data = Array.Empty<byte>();
            if (data.Length > ushort.MaxValue)
                throw new ArgumentException($"数据长度超过 {ushort.MaxValue} 字节");

            // 包体长度 = 1（命令码）+ N（数据）
            ushort bodyLen = (ushort)(1 + data.Length);
            // 总帧长 = SOF(2) + Len(2) + Body(bodyLen) + Check(1) + EOF(2)
            int totalLen = 2 + 2 + bodyLen + 1 + 2;

            byte[] frame = new byte[totalLen];
            int idx = 0;

            // [1] 写入起始符 SOF = 0xAA 0x55
            frame[idx++] = SOF[0];
            frame[idx++] = SOF[1];

            // [2] 写入包体长度（大端序）
            frame[idx++] = (byte)(bodyLen >> 8);    // 高字节
            frame[idx++] = (byte)(bodyLen & 0xFF);  // 低字节

            // [3] 写入命令码
            frame[idx++] = cmd;

            // [4] 写入负载数据
            if (data.Length > 0)
            {
                Array.Copy(data, 0, frame, idx, data.Length);
                idx += data.Length;
            }

            // [5] 计算并写入异或校验和（校验范围：从 Cmd 到 Data 结尾，不含 SOF 和 Len）
            byte checksum = 0;
            for (int i = HEADER_SIZE; i < idx; i++)
                checksum ^= frame[i];
            frame[idx++] = checksum;

            // [6] 写入结束符 EOF = 0x55 0xAA
            frame[idx++] = EOF[0];
            frame[idx++] = EOF[1];

            return frame;
        }

        /// <summary>
        /// 流式解码器。内部维护一个滑动窗口缓冲区，从连续字节流中提取完整合法帧。
        /// <para>
        /// <b>使用建议：</b>为每个网络连接（UDP/TCP）单独维护一个 Decoder 实例。
        /// UDP 场景每次收到完整数据报也可新建实例。
        /// </para>
        /// <para>
        /// <b>粘包/拆包处理：</b>
        /// - 粘包：解析完一个帧后移除，继续解析下一个
        /// - 拆包：数据不足时保留在缓冲区，等待下次追加
        /// - 脏数据：通过滑动窗口跳过无效字节，自动重新同步
        /// </para>
        /// </summary>
        public class Decoder
        {
            /// <summary>内部滑动窗口缓冲区，存储未解析的原始字节</summary>
            private readonly List<byte> _buffer = new List<byte>();

            /// <summary>
            /// 输入最新收到的原始字节数据，尝试从中解析出所有完整的合法帧。
            /// <para>
            /// 解析成功的帧会追加到 <paramref name="resultList"/>，已消费的字节从内部缓冲区移除。
            /// 不完整或非法的数据会保留/丢弃，等待下一次调用。
            /// </para>
            /// </summary>
            /// <param name="receivedBytes">本次新收到的原始字节数组</param>
            /// <param name="resultList">
            /// 输出列表，每个元素为 (命令码, 负载数据)。
            /// 注意：此列表不会被清空，新帧会追加到末尾。
            /// </param>
            public void ParseBytes(byte[] receivedBytes, List<(byte cmd, byte[] data)> resultList)
            {
                if (receivedBytes == null || receivedBytes.Length == 0) return;

                // 追加新数据到内部缓冲区
                _buffer.AddRange(receivedBytes);

                while (true)
                {
                    // ===== 步骤 1：滑动窗口查找双字节起始符 0xAA 0x55 =====
                    int headIndex = -1;
                    for (int i = 0; i <= _buffer.Count - 2; i++)
                    {
                        if (_buffer[i] == SOF[0] && _buffer[i + 1] == SOF[1])
                        {
                            headIndex = i;
                            break;
                        }
                    }

                    // 未找到起始符：只保留尾部 3 字节（防止 SOF 被截断跨两次接收），其余丢弃
                    if (headIndex == -1)
                    {
                        if (_buffer.Count > 3)
                            _buffer.RemoveRange(0, _buffer.Count - 3);
                        return;
                    }

                    // 丢弃起始符之前的脏数据（滑动窗口同步）
                    if (headIndex > 0)
                    {
                        _buffer.RemoveRange(0, headIndex);
                        headIndex = 0;
                    }

                    // ===== 步骤 2：检查是否收到足够的帧头（SOF + Len = 4 字节）=====
                    if (_buffer.Count < HEADER_SIZE)
                        return; // 数据不足，等待下次追加

                    // 读取包体长度（大端序）
                    int bodyLen = (_buffer[2] << 8) | _buffer[3];
                    // 完整帧长度 = SOF(2) + Len(2) + Body(bodyLen) + Check(1) + EOF(2)
                    int totalFrameLen = 2 + 2 + bodyLen + 1 + 2;

                    // ===== 步骤 3：检查完整帧是否全部到达 =====
                    if (_buffer.Count < totalFrameLen)
                        return; // 半包，等待更多数据

                    // ===== 步骤 4：校验帧的合法性（结束符 + 异或校验和）=====
                    bool valid = true;

                    // 4a. 校验结束符 EOF = 0x55 0xAA
                    int tailStart = totalFrameLen - 2;
                    if (_buffer[tailStart] != EOF[0] || _buffer[tailStart + 1] != EOF[1])
                        valid = false;

                    if (valid)
                    {
                        // 4b. 校验异或校验和（校验范围：从 Cmd 到 Data 结尾，不含 SOF/Len/EOF）
                        byte checksum = 0;
                        for (int i = HEADER_SIZE; i < totalFrameLen - 3; i++)
                            checksum ^= _buffer[i];
                        if (checksum != _buffer[totalFrameLen - 3])  // 帧中 Checksum 位置 = 倒数第 3 字节
                            valid = false;
                    }

                    // ===== 步骤 5：根据校验结果处理 =====
                    if (valid)
                    {
                        // 合法帧：提取命令码和负载数据
                        byte cmd = _buffer[HEADER_SIZE];          // 命令码在索引 4
                        int dataLen = bodyLen - 1;                // 减去命令码 1 字节
                        byte[] data = new byte[dataLen];
                        if (dataLen > 0)
                            Array.Copy(_buffer.ToArray(), HEADER_SIZE + 1, data, 0, dataLen);

                        resultList.Add((cmd, data));

                        // 从缓冲区移除已处理的帧，继续解析下一个帧
                        _buffer.RemoveRange(0, totalFrameLen);
                    }
                    else
                    {
                        // 非法帧：丢弃当前头部 1 字节，窗口右移重新搜索 SOF。
                        // 这样能正确处理 0xAA 0xAA 0x55 等边界情况（SOF 被干扰）。
                        _buffer.RemoveAt(0);
                    }
                }
            }

            /// <summary>
            /// 清空内部缓冲区。在连接断开或需要重置解析状态时调用。
            /// </summary>
            public void Clear() => _buffer.Clear();
        }
    }
}
