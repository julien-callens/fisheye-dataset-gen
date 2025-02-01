using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;  // For async/await and Task.
using UnityEngine;
using Random = UnityEngine.Random;

public class RandomWalk : MonoBehaviour
{
    private Vector3 _initialPosition;
    public float speed = 1.0f;
    public float amplitude = 1.0f;
    public float frequencyX = 1.0f;
    public float frequencyY = 1.0f;
    public float frequencyZ = 1.0f;

    private List<Vector3> _positions = new();
    private int _currentIndex;
    private int _captureCounter;

    private readonly Dictionary<Camera, RenderTexture> _cameraRenderTextures = new();

    private const int RenderWidth = 2160;
    private const int RenderHeight = 2160;
    private const int RenderDepth = 24;

    private void Start()
    {
        _initialPosition = transform.position;
        _positions = PoissonDiskSampling3D.GeneratePoints(0.1f, new Vector3(10, 10, 10));

        foreach (var cam in Camera.allCameras)
        {
            if (!cam.isActiveAndEnabled) continue;

            var rt = new RenderTexture(RenderWidth, RenderHeight, RenderDepth);
            rt.Create();
            _cameraRenderTextures[cam] = rt;
        }
    }

    private void Update()
    {
        if (_currentIndex < _positions.Count - 1)
            _currentIndex++;
        else
        {
            Debug.Log("Walked all positions.");
            enabled = false;
            return;
        }

        transform.position = _positions[_currentIndex];

        CaptureAndStoreCameraDataAsync();
    }

    /// <summary>
    /// Captures an image from each active camera and stores the image along with the ball's position.
    /// This version uses async/await to offload file writing to a background thread.
    /// </summary>
    private async void CaptureAndStoreCameraDataAsync()
    {
        var folderPath = Path.Combine(Application.persistentDataPath, "CapturedImages");
        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        foreach (var cam in Camera.allCameras)
        {
            if (!cam.isActiveAndEnabled)
                continue;

            var image = CaptureCamera(cam);
            if (image == null)
                continue;

            var fileName = $"Frame_{_captureCounter}_{cam.name}.png";
            var filePath = Path.Combine(folderPath, fileName);

            var bytes = image.EncodeToPNG();

            await File.WriteAllBytesAsync(filePath, bytes);

            Destroy(image);
        }

        var logFilePath = Path.Combine(folderPath, "ball_positions.csv");
        var logEntry = $"{_captureCounter},{transform.position.x:F6},{transform.position.y:F6},{transform.position.z:F6}\n";
        await File.AppendAllTextAsync(logFilePath, logEntry);

        _captureCounter++;
    }

    /// <summary>
    /// Captures the view of the specified camera using a cached RenderTexture.
    /// </summary>
    /// <param name="cam">The camera to capture.</param>
    /// <returns>A Texture2D containing the captured image.</returns>
    private Texture2D CaptureCamera(Camera cam)
    {
        if (!_cameraRenderTextures.TryGetValue(cam, out RenderTexture rt))
        {
            rt = new RenderTexture(RenderWidth, RenderHeight, RenderDepth);
            rt.Create();
            _cameraRenderTextures[cam] = rt;
        }

        cam.targetTexture = rt;

        var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);

        cam.Render();

        var previousRT = RenderTexture.active;
        RenderTexture.active = rt;

        image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        image.Apply();

        RenderTexture.active = previousRT;
        cam.targetTexture = null;

        return image;
    }

    private void OnDestroy()
    {
        foreach (var rt in _cameraRenderTextures.Values.Where(rt => rt != null))
        {
            rt.Release();
            Destroy(rt);
        }

        _cameraRenderTextures.Clear();
    }
}
