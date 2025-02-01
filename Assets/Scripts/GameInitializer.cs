using UnityEngine;
using UnityEngine.Rendering;

public static class GameInitializer
{
    private static readonly int Tex = Shader.PropertyToID("_Tex");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        InstanceFisheyeCamera(1, new Vector3(-0.3f, .3f, -.3f));
        InstanceFisheyeCamera(2, new Vector3(0.3f, .3f, -.3f));

        var material = Resources.Load("materials/red", typeof(Material)) as Material;

        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.transform.position = Vector3.zero;
        ball.transform.localScale = Vector3.one * 0.1f;

        var ballRenderer = ball.GetComponent<Renderer>();
        ballRenderer.material = material;

        // ball.AddComponent<RandomWalk>();

        DragonLoader.LoadAndScaleDragon(true);

        ApplyHdri("hdri/studio_small_08_8k");
    }

    private static void ApplyHdri(string hdriPath)
    {
        var hdriTexture = Resources.Load<Cubemap>(hdriPath);
        if (!hdriTexture)
        {
            Debug.LogError("HDRI texture not found at: " + hdriPath);
            return;
        }

        var skyboxMaterial = new Material(Shader.Find("Skybox/Cubemap"));
        skyboxMaterial.SetTexture(Tex, hdriTexture);
        RenderSettings.skybox = skyboxMaterial;

        RenderSettings.ambientMode = AmbientMode.Skybox;
        DynamicGI.UpdateEnvironment();
    }

    private static void InstanceFisheyeCamera(int number, Vector3 position)
    {
        var camObject = new GameObject("cam" + number);
        camObject.transform.position = position;
        camObject.transform.LookAt(Vector3.zero);

        var cam = camObject.AddComponent<Camera>();
        cam.fieldOfView = 170;
        cam.nearClipPlane = 0.01f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.targetDisplay = --number;

        var camEffect = camObject.AddComponent<CameraEffect>();
        camEffect.material = (Material)Resources.Load("cameraEffect/CameraEffect", typeof(Material));
    }
}
