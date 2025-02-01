Shader "Custom/TripleSphereFisheye_Controlled_HighQuality"
{
    Properties
    {
        _MainTex ("Input (Pinhole) Texture", 2D) = "white" {}
        // Triple-sphere parameters (defaults chosen to approximate a real fisheye)
        _Xi ("ξ (Sphere2 Shift)", Float) = 0.3
        _Lambda ("λ (Sphere3 Shift)", Float) = 0.53
        _Alpha ("α (Pinhole Shift Parameter)", Float) = 0.47
        _PinholeFOV ("Pinhole FOV (deg)", Float) = 170.0
        // Chromatic aberration: a small radial offset for red/blue channels.
        _ChromaAberration ("Chromatic Aberration", Float) = 0.01
        // Blend between a simple (equidistant) mapping and the full triple-sphere mapping.
        _DistortionMix ("Distortion Mix", Range(0,1)) = 1.0
        // Scale the final distorted coordinates.
        _FisheyeScale ("Fisheye Scale", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        Pass
        {
            CGPROGRAM
            // Use Unity's built-in vertex function for full-screen effects.
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Xi;
            float _Lambda;
            float _Alpha;
            float _PinholeFOV;
            float _ChromaAberration;
            float _DistortionMix;
            float _FisheyeScale;

            // Helper: convert degrees to radians.
            float radians(float deg) { return deg * 0.0174532925; }

            // Given an angle theta and a shift parameter (ξ or λ), compute:
            //   t(theta) = param * cos(theta) + sqrt(1 - param^2 * sin^2(theta))
            float sphereProjectionFactor(float theta, float param)
            {
                return param * cos(theta) + sqrt(1.0 - param * param * (sin(theta) * sin(theta)));
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                // (1) Get the output pixel's normalized coordinates.
                float2 uv = i.uv;
                float2 centered = (uv - 0.5) * 2.0; // now in [-1,1] range
                float r_out = length(centered);     // radial distance in fisheye space
                float phi = atan2(centered.y, centered.x);

                // (2) Compute the displacement for the pinhole image plane:
                //     d = α / (1 - α)
                float d = _Alpha / (1.0 - _Alpha);

                // (3) Compute the maximum output radius (at θ = π/2) for the triple-sphere model.
                float t2_max = sphereProjectionFactor(1.5708, _Xi); // ~π/2 in radians.
                float t3_max = sphereProjectionFactor(1.5708, _Lambda);
                float r_max = (t2_max * t3_max) / d;
                if (r_out > r_max)
                    return fixed4(0, 0, 0, 1);

                // (4) Invert the forward mapping to find θ such that:
                //     f(θ) = (t2 * t3 * sinθ) / (t2 * t3 * cosθ + d) equals r_out.
                //     Use binary search over θ ∈ [0, π/2].
                float thetaLow = 0.0;
                float thetaHigh = 1.5708; // π/2 in radians.
                float thetaMid = 0.0;
                const int NUM_ITERS = 10;
                for (int iter = 0; iter < NUM_ITERS; iter++)
                {
                    thetaMid = (thetaLow + thetaHigh) * 0.5;
                    float sTheta = sin(thetaMid);
                    float cTheta = cos(thetaMid);
                    float t2 = sphereProjectionFactor(thetaMid, _Xi);
                    float t3 = sphereProjectionFactor(thetaMid, _Lambda);
                    float f_val = (t2 * t3 * sTheta) / (t2 * t3 * cTheta + d);
                    if (f_val < r_out)
                        thetaLow = thetaMid;
                    else
                        thetaHigh = thetaMid;
                }
                float thetaTS = thetaMid;

                // (5) For additional control, compute a simple equidistant mapping:
                //     In an equidistant fisheye, r_out scales linearly with θ.
                float thetaSimple = (r_out / r_max) * 1.5708; // θ == π/2 when r_out == r_max.
                // Blend between the simple mapping and the triple-sphere mapping.
                float thetaFinal = lerp(thetaSimple, thetaTS, _DistortionMix);

                // (6) Compute the undistorted (pinhole) coordinates.
                //     Standard pinhole model: (x,y) = (tanθ * cosφ, tanθ * sinφ).
                float x = tan(thetaFinal) * cos(phi);
                float y = tan(thetaFinal) * sin(phi);

                // (7) Apply an overall scale to the distorted coordinates.
                x *= _FisheyeScale;
                y *= _FisheyeScale;

                // (8) Map (x,y) into the input texture's UV space.
                //     Assume the undistorted image covers x,y ∈ [–halfWidth, +halfWidth],
                //     with halfWidth = tan(FOV/2) (typically the horizontal FOV).
                float halfWidth = tan(radians(_PinholeFOV) * 0.5);
                float2 undistortedUV = float2(x, y) / (2.0 * halfWidth) + 0.5;

                // (9) Discard fragments whose computed UV falls outside [0,1].
                if (undistortedUV.x < 0.0 || undistortedUV.x > 1.0 ||
                    undistortedUV.y < 0.0 || undistortedUV.y > 1.0)
                {
                    return fixed4(0, 0, 0, 1);
                }

                // (10) Compute chromatic aberration offsets.
                float2 uvR = undistortedUV;
                float2 uvB = undistortedUV;
                {
                    float2 centerUV = float2(0.5, 0.5);
                    float2 toCenter = undistortedUV - centerUV;
                    float len = length(toCenter);
                    float2 dir = (len > 0.0001) ? (toCenter / len) : float2(0.0, 0.0);
                    float aberrationOffset = _ChromaAberration * len;
                    uvR = undistortedUV + dir * aberrationOffset;
                    uvB = undistortedUV - dir * aberrationOffset;
                }

                // (11) Use tex2Dgrad to sample the input texture with explicit derivatives.
                //      This helps ensure that the hardware LOD selection is more appropriate.
                fixed rChannel = tex2Dgrad(_MainTex, uvR, ddx(uvR), ddy(uvR)).r;
                fixed gChannel = tex2Dgrad(_MainTex, undistortedUV, ddx(undistortedUV), ddy(undistortedUV)).g;
                fixed bChannel = tex2Dgrad(_MainTex, uvB, ddx(uvB), ddy(uvB)).b;

                return fixed4(rChannel, gChannel, bChannel, 1.0);
            }
            ENDCG
        }
    }
}
