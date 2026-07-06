using System.Collections;
using UnityEngine;

/// <summary>
/// The six ordering slots don't exist visually until the player walks toward the green
/// light: then glowing green BORDER outlines slowly materialize (fade in) at each snap
/// zone, with a small forming-particle shimmer. Per VR feedback — replaces the old
/// always-visible filled quads.
/// </summary>
public class SlotFormation : MonoBehaviour
{
    [Tooltip("Snap zones from FrameOrderPuzzle (borders are built around each)")]
    public Transform[] snapZones;
    [Tooltip("Player distance (m) at which the slots begin forming")]
    public float formDistance = 6f;
    [Tooltip("Seconds for the outlines to fully materialize")]
    public float formTime = 2.5f;
    [Tooltip("Border bar material (transparent, tintable — GreenGoldParticle clone)")]
    public Material barMaterial;

    private readonly System.Collections.Generic.List<Renderer> _bars = new System.Collections.Generic.List<Renderer>();
    private MaterialPropertyBlock _mpb;
    private bool _forming, _formed;

    void Start()
    {
        _mpb = new MaterialPropertyBlock();
        foreach (var zone in snapZones)
        {
            if (zone == null) continue;
            BuildBorder(zone);
        }
        SetAlpha(0f);
    }

    void BuildBorder(Transform zone)
    {
        // 4 thin bars outlining a 0.95 x 0.95 slot
        var specs = new (Vector3 pos, Vector3 scale)[]
        {
            (new Vector3(0f,  0.475f, 0f), new Vector3(0.99f, 0.045f, 0.02f)), // top
            (new Vector3(0f, -0.475f, 0f), new Vector3(0.99f, 0.045f, 0.02f)), // bottom
            (new Vector3(-0.475f, 0f, 0f), new Vector3(0.045f, 0.99f, 0.02f)), // left
            (new Vector3( 0.475f, 0f, 0f), new Vector3(0.045f, 0.99f, 0.02f)), // right
        };
        foreach (var (pos, scale) in specs)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "SlotBorderBar";
            Destroy(bar.GetComponent<Collider>());
            bar.transform.SetParent(zone, false);
            bar.transform.localPosition = pos;
            bar.transform.localScale = scale;
            var r = bar.GetComponent<MeshRenderer>();
            if (barMaterial != null) r.sharedMaterial = barMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _bars.Add(r);
        }
    }

    void Update()
    {
        if (_formed || _forming) return;
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 a = cam.transform.position, b = transform.position;
        a.y = 0f; b.y = 0f;
        if (Vector3.Distance(a, b) < formDistance)
        {
            _forming = true;
            StartCoroutine(FormRoutine());
        }
    }

    IEnumerator FormRoutine()
    {
        Debug.Log("[SlotFormation] player approached — slots materializing");
        // shimmer burst at each zone as the outline forms
        foreach (var zone in snapZones)
        {
            if (zone == null) continue;
            var go = new GameObject("SlotFormShimmer");
            go.transform.position = zone.position;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 1.4f;
            main.startSpeed = 0.25f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.05f);
            main.startColor = new Color(0.4f, 1f, 0.6f, 0.9f);
            main.maxParticles = 80;
            var em = ps.emission;
            em.rateOverTime = 30f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.95f, 0.95f, 0.05f);
            ParticleMaterialUtil.Apply(ps);
            Destroy(go, formTime + 1.5f);
        }

        float e = 0f;
        while (e < formTime)
        {
            e += Time.deltaTime;
            SetAlpha(Mathf.SmoothStep(0f, 0.9f, e / formTime));
            yield return null;
        }
        SetAlpha(0.9f);
        _formed = true;
    }

    void SetAlpha(float a)
    {
        Color c = new Color(0.35f, 1f, 0.55f, a);
        foreach (var r in _bars)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor("_BaseColor", c);
            _mpb.SetColor("_Color", c);
            _mpb.SetColor("_TintColor", c);
            r.SetPropertyBlock(_mpb);
            r.enabled = a > 0.01f;
        }
    }
}
