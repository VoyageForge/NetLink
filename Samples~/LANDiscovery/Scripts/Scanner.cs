using UnityEngine;

namespace VoyageForge.NetLink.Samples.LANDiscovery
{
    /// <summary>遍历 / 退化查找客户端的场景挂载点（MonoBehaviour 壳）。</summary>
    public class Scanner : MonoBehaviour
    {
        private readonly ExampleScanner _scanner = new ExampleScanner();

        private async void Start() => await _scanner.StartScanAsync().ConfigureAwait(false);

        private void OnDestroy() => _scanner.Destroy();
    }
}
