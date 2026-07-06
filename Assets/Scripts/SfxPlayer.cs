using UnityEngine;

/// <summary>
/// One-line positional SFX: SfxPlayer.Play("frame_shatter", pos).
/// Clips live in Resources/Audio/ (procedurally generated placeholders for now —
/// drop in better files with the same names to upgrade). Missing clips no-op silently.
/// </summary>
public static class SfxPlayer
{
    static readonly System.Collections.Generic.Dictionary<string, AudioClip> _cache =
        new System.Collections.Generic.Dictionary<string, AudioClip>();

    public static void Play(string name, Vector3 pos, float volume = 1f)
    {
        var clip = Get(name);
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, pos, volume);
    }

    /// <summary>Play 2D (non-positional) — for UI/global stingers.</summary>
    public static void Play2D(string name, float volume = 1f)
    {
        var clip = Get(name);
        if (clip == null) return;
        var go = new GameObject("Sfx2D_" + name);
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.spatialBlend = 0f;
        src.volume = volume;
        src.Play();
        Object.Destroy(go, clip.length + 0.2f);
    }

    public static AudioClip Get(string name)
    {
        if (_cache.TryGetValue(name, out var c)) return c;
        c = Resources.Load<AudioClip>("Audio/" + name);
        _cache[name] = c; // cache null too — avoids repeated failed loads
        return c;
    }
}
