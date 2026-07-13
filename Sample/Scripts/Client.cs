using LANServiceDiscovery.Runtime;
using UnityEngine;

namespace LANServiceDiscovery.Sample
{
    public class Client : MonoBehaviour
    {
        private readonly ExampleClient _client = new ExampleClient();
        private async void Start() => await _client.StartDiscoveryAsync().ConfigureAwait(false);
        private void OnDestroy() => _client.Destroy();
    }
}
