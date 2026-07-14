using VoyageForge.NetLink.Runtime;
using UnityEngine;

namespace VoyageForge.NetLink.Samples.LANDiscovery
{
    public class Host : MonoBehaviour
    {
        private readonly ExampleHost _host = new ExampleHost();
        private void Start() => _host.Start();
        private void OnDestroy() => _host.Destroy();
    }
}
