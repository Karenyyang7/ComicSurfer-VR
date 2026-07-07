using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages all non-interactable background atmosphere frames.
/// Phase 1: 30-40 grey/white/black frames.
/// Phase 2+: shatter those, spawn 20-30 mixed pastel + monochrome.
/// All spawned frames get FloatScript automatically.
/// </summary>
public class BackgroundFrameSpawner : MonoBehaviour
{
    [Header("=== Spawn Settings ===")]
    [Tooltip("Number of initial (grey) background frames")]
    public int initialFrameCount = 35;
    [Tooltip("Number of new (pastel) background frames after Phase 2")]
    public int newFrameCount = 25;

    [Tooltip("Radius around origin to scatter frames")]
    public float spawnRadius = 12f;
    [Tooltip("Minimum Y height of spawned frames")]
    public float minHeight = 0.8f;
    [Tooltip("Maximum Y height of spawned frames")]
    public float maxHeight = 3.5f;

    [Tooltip("Minimum distance from player spawn (to avoid cluttering foreground)")]
    public float minDistanceFromCenter = 3f;

    [Header("=== Frame Size ===")]
    public float frameWidth = 0.7f;
    public float frameHeight = 0.9f;

    [Header("=== Shatter Settings ===")]
    public float shatterStaggerMax = 0.4f;   // max seconds between each frame shattering

    private readonly List<GameObject> _activeFrames = new List<GameObject>();

    // ── Phase 1: grey/white/black ────────────────────────────────────────────────
    public void SpawnInitialBackgroundFrames()
    {
        Color[] greyPalette = new Color[]
        {
            new Color(0.15f, 0.15f, 0.15f),
            new Color(0.25f, 0.25f, 0.25f),
            new Color(0.55f, 0.55f, 0.55f),
            new Color(0.75f, 0.75f, 0.75f),
            Color.white,
        };
        SpawnFrameSet(initialFrameCount, greyPalette, "BG_Initial");
    }

    // ── Phase 2: shatter all, then spawn pastel ──────────────────────────────────
    public void ShatterAllBackgroundFrames()
    {
        StartCoroutine(ShatterRoutine());
    }

    public void SpawnNewBackgroundFrames()
    {
        // 40% grey/white/black, 30% pastel pink, 15% pastel blue, 15% pastel yellow
        Color[] mixedPalette = new Color[]
        {
            new Color(0.15f, 0.15f, 0.15f),
            new Color(0.45f, 0.45f, 0.45f),
            Color.white,
            new Color(1.0f, 0.82f, 0.86f),   // pastel pink #FFD1DC
            new Color(1.0f, 0.82f, 0.86f),   // pastel pink (weighted 2x)
            new Color(0.68f, 0.78f, 0.81f),   // pastel blue #AEC6CF
            new Color(1.0f, 0.98f, 0.80f),   // pastel yellow #FFFACD
        };
        SpawnFrameSet(newFrameCount, mixedPalette, "BG_Pastel");
    }

    // ── Internal helpers ─────────────────────────────────────────────────────────

    void SpawnFrameSet(int count, Color[] palette, string prefix)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = GetRandomSpawnPosition();
            float rotY = Random.Range(0f, 360f);
            float rotZ = Random.Range(-15f, 15f);

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = prefix + "_" + i;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, rotY, rotZ);
            go.transform.localScale = new Vector3(frameWidth, frameHeight, 1f);

            // Remove auto-collider — these are purely visual
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Random color from palette
            Color c = palette[Random.Range(0, palette.Length)];
            var mat = new Material(shader);
            mat.color = c;
            go.GetComponent<Renderer>().material = mat;

            // FloatScript with randomized parameters
            var fs = go.AddComponent<FloatScript>();
            fs.bobAmplitude   = Random.Range(0.05f, 0.15f);
            fs.bobPeriod      = Random.Range(3f, 5f);
            fs.driftAmplitude = Random.Range(0.03f, 0.08f);
            fs.driftPeriod    = Random.Range(4f, 7f);
            fs.tiltAmount     = Random.Range(2f, 5f);
            fs.tiltPeriod     = Random.Range(6f, 10f);

            // Frames spawn around the ORIGIN but the player roams the whole map
            // (order puzzle is at x≈10) — without this, a frame can sit centimeters
            // from the player's face as a giant blank sheet.
            go.AddComponent<PlayerClearance>();

            _activeFrames.Add(go);
        }
    }

    IEnumerator ShatterRoutine()
    {
        // Snapshot current list (we'll clear it as we go)
        var toShatter = new List<GameObject>(_activeFrames);
        _activeFrames.Clear();

        foreach (var frame in toShatter)
        {
            if (frame == null) continue;

            // Brief stagger
            yield return new WaitForSeconds(Random.Range(0f, shatterStaggerMax));

            SpawnShatterParticles(frame.transform.position);
            Destroy(frame);
        }
    }

    void SpawnShatterParticles(Vector3 pos)
    {
        var psGO = new GameObject("BG_ShatterBurst");
        psGO.transform.position = pos;
        var ps = psGO.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.startColor = new Color(0.7f, 0.7f, 0.9f);
        main.startLifetime = 0.6f;
        main.startSpeed = 2f;
        main.startSize = 0.04f;

        var emission = ps.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) }); // SetBurst(0,..) no-ops on an empty bursts array

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;

        ParticleMaterialUtil.Apply(ps);
        Destroy(psGO, 1.5f);
    }

    Vector3 GetRandomSpawnPosition()
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(minDistanceFromCenter, spawnRadius);
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            float y = Random.Range(minHeight, maxHeight);
            var pos = new Vector3(x, y, z);
            // keep clear of Frame 1's spot (0,1.5,3) — BG frames were spawning through it
            Vector3 f1 = new Vector3(0f, pos.y, 3f);
            if (Vector3.Distance(pos, f1) < 2.0f) continue;
            if (pos.magnitude >= minDistanceFromCenter) return pos;
        }
        // Fallback — behind the player, away from Frame 1
        return new Vector3(Random.Range(-spawnRadius, -4f), Random.Range(minHeight, maxHeight), Random.Range(-spawnRadius, -4f));
    }
}

/// <summary>
/// Keeps a background frame out of the player's personal space: when the player
/// (Camera.main) comes within clearRadius, the frame glides away horizontally.
/// Added at runtime by BackgroundFrameSpawner — purely visual frames only.
/// </summary>
public class PlayerClearance : MonoBehaviour
{
    public float clearRadius = 2.5f;

    private FloatScript _float;

    void Start()
    {
        _float = GetComponent<FloatScript>();
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;

        Vector3 away = transform.position - cam.transform.position;
        away.y = 0f;
        float dist = away.magnitude;
        if (dist >= clearRadius) return;

        Vector3 dir = dist > 0.01f ? away / dist : transform.forward;
        // exponential ease toward the clearance ring — smooth glide, no pop
        Vector3 delta = dir * (clearRadius - dist) * Mathf.Clamp01(Time.deltaTime * 2.5f);
        if (_float != null) _float.NudgeOrigin(delta);
        else transform.position += delta;
    }
}
