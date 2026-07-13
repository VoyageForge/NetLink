using System.Text;
using VoyageForge.NetLink.Runtime;
using Newtonsoft.Json;

namespace VoyageForge.NetLink.Runtime
{
    /// <summary>通用 JSON 负载基类。子类无需重写 Serialize/Deserialize。</summary>
    public abstract class JsonPayload : Payload
    {
        /// <inheritdoc/>
        public override byte[] Serialize() =>
            Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(this));

        /// <inheritdoc/>
        public override void Deserialize(byte[] data) =>
            JsonConvert.PopulateObject(Encoding.UTF8.GetString(data), this);
    }
}