using UnityEngine;

namespace VoyageForge.NetLink.Samples.LANDiscovery
{
    /// <summary>抓包示例的场景挂载点（MonoBehaviour 壳）。</summary>
    public class Capture : MonoBehaviour
    {
        private readonly ExampleCapture _capture = new ExampleCapture();

        private async void Start() => await _capture.StartCaptureAsync().ConfigureAwait(false);

        private void OnDestroy() => _capture.Destroy();
    }
}
