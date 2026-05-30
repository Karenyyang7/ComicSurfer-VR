using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Component for each comic frame in ComicWorld.
/// Attach to a parent GameObject that has a Quad child for visuals.
/// </summary>
public class ComicFrame : MonoBehaviour
{
    [Header("=== Frame Identity ===")]
    public int frameIndex = 1;

    [Header("=== Capabilities ===")]
    public bool canBeGrabbed = false;
    public bool canBePushed = false;
    public bool canBeEnlarged = false;
    public bool canBeShattered = false;

    [Header("=== Visual ===")]
    public Texture2D frameTexture;    // Assign in Inspector when textures are imported
    public Renderer quadRenderer;     // The child Quad renderer

    [Header("=== Particle ===")]
    public Transform shatterParticleSpawn;

    public event System.Action OnFramePushed;
    public event System.Action OnFrameGrabbed;
    public event System.Action OnFrameShattered;
    public event System.Action<ComicFrame> OnFramePlaced;

    private bool _shattered = false;

    // Applies texture immediately when assigned in Inspector (editor preview)
    void OnValidate()
    {
        if (quadRenderer == null)
            quadRenderer = GetComponentInChildren<Renderer>();
        ApplyTexture();
    }

    void ApplyTexture()
    {
        if (frameTexture == null || quadRenderer == null) return;
        // Works for both URP Lit/Unlit (_BaseMap) and Standard (_MainTex)
        var mat = quadRenderer.sharedMaterial;
        if (mat == null) return;
        if (mat.HasProperty("_BaseMap"))
            mat.SetTexture("_BaseMap", frameTexture);
        else
            mat.mainTexture = frameTexture;
    }

    void Start()
    {
        ApplyTexture();

        var grab = GetComponent<XRGrabInteractable>();
        if (grab != null && canBeGrabbed)
            grab.selectEntered.AddListener(_ => OnFrameGrabbed?.Invoke());

        var simple = GetComponent<XRSimpleInteractable>();
        if (simple != null && canBePushed)
            simple.selectEntered.AddListener(_ => OnFramePushed?.Invoke());
    }

    /// <summary>
    /// Plays a particle burst at the shatter spawn point and hides the frame visual.
    /// </summary>
    public void ShatterFrame()
    {
        if (_shattered) return;
        _shattered = true;

        if (quadRenderer != null)
            quadRenderer.gameObject.SetActive(false);

        if (shatterParticleSpawn != null)
        {
            // Spawn a simple burst particle at the shatter point
            var psGO = new GameObject("ShatterBurst");
            psGO.transform.position = shatterParticleSpawn.position;
            var ps = psGO.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startColor = new Color(1f, 0.8f, 0.2f);
            main.startLifetime = 0.8f;
            main.startSpeed = 3f;
            var emission = ps.emission;
            emission.SetBurst(0, new ParticleSystem.Burst(0f, 30));
            Destroy(psGO, 2f);
        }

        OnFrameShattered?.Invoke();
    }

    /// <summary>
    /// Called by FrameOrderPuzzle when this frame is placed in a snap zone.
    /// </summary>
    public void NotifyPlaced()
    {
        OnFramePlaced?.Invoke(this);
    }
}
