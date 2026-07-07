using System;
using LANServiceDiscovery.Runtime;
using UnityEngine;

namespace LANServiceDiscovery.Sample
{
    public class Client : MonoBehaviour
    {
        private readonly ExampleClient _client = new ExampleClient();

        private void Start()
        {
            _client.Start();
        }
        
        private void OnDestroy()
        {
            _client.Destroy();
        }
    }
}