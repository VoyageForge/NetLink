using System;
using System.Text;

namespace LANServiceDiscovery.Runtime
{
    /// <summary>
    /// 默认帧体编解码器。格式：[TypeIdLen(2)][TypeId(N)][Payload(M)][Check(1)]
    /// <para>扩展：重写 <see cref="Protect"/>/<see cref="Unprotect"/> 加解密。</para>
    /// </summary>
    public class DefaultBodyCodec : IBodyCodec
    {
        /// <summary>校验字节长度</summary>
        protected const int CheckLen = 1;

        // ==================== Decode ====================

        /// <inheritdoc/>
        public virtual (string typeId, byte[] payload) Decode(byte[] frameBody)
        {
            if (frameBody == null || frameBody.Length < 3)
                throw new ArgumentException("帧体无效");

            // 异或校验
            byte check = 0;
            for (int i = 0; i < frameBody.Length - CheckLen; i++)
                check ^= frameBody[i];
            if (check != frameBody[frameBody.Length - CheckLen])
                throw new InvalidOperationException("校验失败");

            // 提取 TypeId
            int typeIdLen = (frameBody[0] << 8) | frameBody[1];
            string typeId = "";
            int pos = 2;
            if (typeIdLen > 0)
            {
                typeId = Encoding.UTF8.GetString(frameBody, pos, typeIdLen);
                pos += typeIdLen;
            }

            // 提取 Payload
            int payloadLen = frameBody.Length - pos - CheckLen;
            byte[] payload = new byte[payloadLen];
            if (payloadLen > 0) Array.Copy(frameBody, pos, payload, 0, payloadLen);

            return (typeId, Unprotect(payload));
        }

        // ==================== Encode ====================

        /// <inheritdoc/>
        public virtual byte[] Encode(string typeId, byte[] payload)
        {
            payload ??= Array.Empty<byte>();
            payload = Protect(payload);
            byte[] tid = string.IsNullOrEmpty(typeId) ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(typeId);

            // [TypeIdLen(2)] [TypeId] [Payload] [Check(1)]
            byte[] body = new byte[2 + tid.Length + payload.Length + 1];
            int pos = 0;
            body[pos++] = (byte)(tid.Length >> 8);
            body[pos++] = (byte)(tid.Length & 0xFF);
            if (tid.Length > 0) { Array.Copy(tid, 0, body, pos, tid.Length); pos += tid.Length; }
            if (payload.Length > 0) { Array.Copy(payload, 0, body, pos, payload.Length); pos += payload.Length; }

            byte check = 0;
            for (int i = 0; i < pos; i++) check ^= body[i];
            body[pos] = check;

            return body;
        }

        /// <inheritdoc/>
        public virtual byte[] Encode<T>(T packet) where T : Payload
        {
            return Encode(typeof(T).Name, packet.Serialize());
        }

        // ==================== 钩子 ====================

        /// <summary>编码时预处理负载（加密等）</summary>
        protected virtual byte[] Protect(byte[] data) => data;

        /// <summary>解码时后处理负载（解密等）</summary>
        protected virtual byte[] Unprotect(byte[] data) => data;
    }
}
