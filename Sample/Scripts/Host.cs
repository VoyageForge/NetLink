using LANServiceDiscovery.Runtime;
using UnityEngine;

namespace LANServiceDiscovery.Sample
{
    public class Host : MonoBehaviour
    {
        private readonly ExampleHost _host = new ExampleHost();

        private void Start()
        {
            _host.Start();
        }
        
        private void OnDestroy()
        {
            _host.Destroy();
        }
    }
}