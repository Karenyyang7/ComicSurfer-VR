using UnityEngine;

/// <summary>
/// Runtime-created ParticleSystems get Unity's default particle material, which renders
/// MAGENTA under URP. Every code-spawned particle system should use this shared material.
/// </summary>
public static class ParticleMaterialUtil
{
    private static Material _mat;

    public static Material Get()
    {
        if (_mat != null) return _mat;

        Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
        if (s == null) s = Shader.Find("Sprites/Default");
        _mat = new Material(s != null ? s : Shader.Find("Unlit/Color"));
        _mat.name = "RuntimeParticles (shared)";
        // soft radial sprite — untextured particles render as hard squares
        var soft = SoftCircle();
        if (_mat.HasProperty("_BaseMap")) _mat.SetTexture("_BaseMap", soft);
        else _mat.mainTexture = soft;
        return _mat;
    }

    static Texture2D _soft;
    static Texture2D SoftCircle()
    {
        if (_soft != null) return _soft;
        const int S = 64;
        _soft = new Texture2D(S, S, TextureFormat.RGBA32, false);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x - S / 2f) / (S / 2f), dy = (y - S / 2f) / (S / 2f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a); // smoothstep falloff
                _soft.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        _soft.Apply();
        return _soft;
    }

    /// <summary>Assign the URP-safe material to a particle system's renderer.</summary>
    public static void Apply(ParticleSystem ps)
    {
        var r = ps.GetComponent<ParticleSystemRenderer>();
        if (r != null)
        {
            r.sharedMaterial = Get();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }
}
