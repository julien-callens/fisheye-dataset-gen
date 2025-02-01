using System;
using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class CameraEffect : MonoBehaviour
{
    public Material material;

    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (material != null)
            Graphics.Blit(src, dest, material);
        else
            Graphics.Blit(src, dest);
    }
}
