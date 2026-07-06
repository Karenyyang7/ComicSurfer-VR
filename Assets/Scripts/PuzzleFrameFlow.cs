using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Presentation flow for the ordering-puzzle frames (3-8), per VR feedback:
/// they first appear as plain BLACK &amp; WHITE floating frames drifting far from the player,
/// each with a soft green/gold glow-wisp around it; when the puzzle phase begins they
/// "turn into" the real comic frames (art + number labels pop in).
///
/// Wire `puzzleFrames` (the ComicFrame roots for 3-8) in the editor; ComicWorldManager
/// calls RevealFrames() at Phase 3.
/// </summary>
public class PuzzleFrameFlow : MonoBehaviour
{
    [Header("=== References ===")]
    public ComicFrame[] puzzleFrames;
    [Tooltip("Plain light-grey material used for the B/W state")]
    public Material bwMaterial;
    [Tooltip("Particle material for the glow wisps (GreenGoldParticle)")]
    public Material wispMaterial;

    [Header("=== Reveal ===")]
    public float revealPulseScale = 1.18f;
    public float revealDuration = 0.5f;

    private readonly Dictionary<ComicFrame, Material> _artMats = new Dictionary<ComicFrame, Material>();
    private readonly Dictionary<ComicFrame, Texture2D> _artTex = new Dictionary<ComicFrame, Texture2D>();
    private bool _revealed;

    void Start()
    {
        foreach (var fr in puzzleFrames)
        {
            if (fr == null) continue;

            // remember the real art material, swap in the B/W one.
            // Also stash frameTexture: ComicFrame.Start writes it into the SHARED material,
            // which would stamp art onto the shared B/W asset for every frame.
            _artTex[fr] = fr.frameTexture;
            fr.frameTexture = null;
            if (fr.quadRenderer != null)
            {
                _artMats[fr] = fr.quadRenderer.sharedMaterial;
                if (bwMaterial != null) fr.quadRenderer.sharedMaterial = bwMaterial;
            }

            // hide the number label until the reveal
            var label = fr.transform.Find("NumberLabel");
            if (label != null) label.gameObject.SetActive(false);

            AddWisp(fr);
        }
    }

    void AddWisp(ComicFrame fr)
    {
        if (fr.transform.Find("GlowWisp") != null) return;
        var go = new GameObject("GlowWisp");
        go.transform.SetParent(fr.transform, false);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = true;
        main.startLifetime = 2.2f;
        main.startSpeed = 0.08f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.07f);
        var grad = new ParticleSystem.MinMaxGradient(
            new Color(0.35f, 1f, 0.55f), new Color(1f, 0.9f, 0.45f));
        grad.mode = ParticleSystemGradientMode.TwoColors;
        main.startColor = grad;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;

        var em = ps.emission;
        em.rateOverTime = 9f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(1.0f, 1.0f, 0.12f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.3f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = wispMaterial != null ? wispMaterial : ParticleMaterialUtil.Get();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// <summary>Turn the B/W drifters into the real comic frames (art + labels).</summary>
    public void RevealFrames()
    {
        if (_revealed) return;
        _revealed = true;
        StartCoroutine(RevealRoutine());
    }

    IEnumerator RevealRoutine()
    {
        foreach (var fr in puzzleFrames)
        {
            if (fr == null) continue;
            StartCoroutine(RevealOne(fr));
            yield return new WaitForSeconds(0.15f); // staggered pops
        }
    }

    IEnumerator RevealOne(ComicFrame fr)
    {
        // restore art + label with a scale pulse
        if (fr.quadRenderer != null && _artMats.TryGetValue(fr, out var art) && art != null)
            fr.quadRenderer.sharedMaterial = art;
        if (_artTex.TryGetValue(fr, out var tex) && tex != null)
        {
            fr.frameTexture = tex;
            if (fr.quadRenderer != null && fr.quadRenderer.sharedMaterial != null &&
                fr.quadRenderer.sharedMaterial.HasProperty("_BaseMap"))
                fr.quadRenderer.sharedMaterial.SetTexture("_BaseMap", tex);
        }

        var label = fr.transform.Find("NumberLabel");
        if (label != null) label.gameObject.SetActive(true);
        SfxPlayer.Play("frame_reveal", fr.transform.position);

        Transform t = fr.transform;
        Vector3 baseScale = t.localScale;
        float e = 0f;
        while (e < revealDuration)
        {
            e += Time.deltaTime;
            float k = Mathf.Clamp01(e / revealDuration);
            float s = 1f + Mathf.Sin(k * Mathf.PI) * (revealPulseScale - 1f);
            t.localScale = baseScale * s;
            yield return null;
        }
        t.localScale = baseScale;
    }
}
