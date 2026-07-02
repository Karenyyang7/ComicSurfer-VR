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
        return _mat;
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
