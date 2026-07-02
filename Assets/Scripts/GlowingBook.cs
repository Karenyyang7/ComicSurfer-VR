using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Makes a book glow with a pulsing emission effect.
/// When the player grabs it, the glow stops and the UI updates.
/// Can also be attached to open_book (no XRGrabInteractable) as a portal glow target.
///
/// SETUP (closedBook):
/// 1. Add this script to Book_C_Gray
/// 2. Add an XR Grab Interactable component to Book_C_Gray
/// 3. Add a Rigidbody to Book_C_Gray (check "Use Gravity")
/// 4. Make sure Book_C_Gray has a Collider (Box Collider works great)
/// 5. Drag your UIInstructions object into the instructions slot
/// 6. Drag your TableSnapZone object into the snapZone slot
///
/// SETUP (openBook / portal):
/// 1. Add this script to open_book
/// 2. No XRGrabInteractable needed — starts dark, glows when StartGlowing() is called
/// 3. BookPortalTrigger (also on open_book) drives glow + hover highlight
/// </summary>
public class GlowingBook : MonoBehaviour
{
    [Header("=== Glow Settings ===")]
    [Tooltip("Color of the glow")]
    public Color glowColor = new Color(0.5f, 0.8f, 1f, 1f); // soft blue

    [Tooltip("How bright the glow gets at its peak")]
    public float glowIntensity = 2f;

    [Tooltip("How fast the glow pulses")]
    public float pulseSpeed = 2f;

    [Header("=== Hover State ===")]
    [Tooltip("Glow color when hovered (portal highlight)")]
    public Color hoverGlowColor = new Color(1f, 0.9f, 0.2f); // gold

    [Tooltip("Intensity multiplier when hovered")]
    public float hoverIntensityMultiplier = 2.5f;

    [Header("=== Portal Halo Light ===")]
    [Tooltip("Add a real point light so the book visibly glows (used by open_book portal). " +
             "A light reads as a glow even without bloom/post-processing.")]
    public bool useGlowLight = false;

    [Tooltip("Peak intensity of the halo light")]
    public float glowLightIntensity = 1.6f;

    [Tooltip("Range (m) of the halo light")]
    public float glowLightRange = 2f;

    [Header("=== References ===")]
    [Tooltip("Drag UIInstructions object here")]
    public UIInstructions instructions;

    [Tooltip("Drag TableSnapZone object here")]
    public TableSnapZone snapZone;

    // Internal
    private Material bookMaterial;
    private Material originalMaterial;
    private bool isGlowing = true;
    private bool hasBeenGrabbed = false;
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;
    private Color originalEmissionColor;
    private bool useEmissionProperty = false; // false for shaders like GLTFast that lack _EmissionColor
    private Light glowLight; // point-light fallback when emission property is unavailable

    // Saved base values (restored on hover exit)
    private float _baseGlowIntensity;
    private Color _baseGlowColor;

    void Start()
    {
        // Save Inspector-configured base values before any runtime changes
        _baseGlowIntensity = glowIntensity;
        _baseGlowColor = glowColor;

        // Material setup — runs for BOTH closedBook AND openBook
        Renderer renderer = GetComponent<Renderer>();
        if (renderer == null)
            renderer = GetComponentInChildren<Renderer>();

        if (renderer != null)
        {
            // Clone the material instance so we don't affect other objects using the same material
            bookMaterial = renderer.material; // auto-clones

            useEmissionProperty = bookMaterial.HasProperty("_EmissionColor");
            if (useEmissionProperty)
            {
                originalEmissionColor = bookMaterial.GetColor("_EmissionColor");
                bookMaterial.EnableKeyword("_EMISSION");
                bookMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                // Shader (e.g. GLTFast glTF-pbrMetallicRoughness) has no _EmissionColor —
                // animate a point light above the book instead.
                GameObject lightGO = new GameObject("GlowLight");
                lightGO.transform.SetParent(transform);
                lightGO.transform.localPosition = new Vector3(0f, 0.3f, 0f);
                glowLight = lightGO.AddComponent<Light>();
                glowLight.type = LightType.Point;
                glowLight.range = 1.5f;
                glowLight.shadows = LightShadows.None;
                glowLight.intensity = 0f;
            }
        }

        // Optional real point light so the portal book visibly glows (emission alone is subtle
        // without bloom). Driven each frame in Update while glowing.
        if (useGlowLight && glowLight == null)
        {
            GameObject haloGO = new GameObject("GlowHaloLight");
            haloGO.transform.SetParent(transform);
            haloGO.transform.position = transform.position + Vector3.up * 0.15f;
            glowLight = haloGO.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.range = glowLightRange;
            glowLight.shadows = LightShadows.None;
            glowLight.intensity = 0f;
        }

        // Grab interaction — closedBook only (openBook has no XRGrabInteractable)
        grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnGrab);
            grabInteractable.selectExited.AddListener(OnRelease);

            if (instructions != null)
                instructions.ShowMessage("Pick up the glowing book!");
        }

        // openBook starts dark — waits for BookPortalTrigger to call StartGlowing()
        isGlowing = (grabInteractable != null);
    }

    void Update()
    {
        if (!isGlowing) return;

        float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f; // 0–1
        float lit   = 0.45f + 0.55f * pulse;                          // stays clearly lit (never fully dark)

        if (useEmissionProperty && bookMaterial != null)
        {
            bookMaterial.SetColor("_EmissionColor", glowColor * glowIntensity * lit);
        }

        // Drive the halo light (when present) alongside the emission so the glow is always visible.
        if (glowLight != null)
        {
            glowLight.color     = glowColor;
            glowLight.intensity = (useGlowLight ? glowLightIntensity : glowIntensity) * lit;
        }
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        hasBeenGrabbed = true;

        // Stop the glow
        isGlowing = false;
        if (useEmissionProperty && bookMaterial != null)
            bookMaterial.SetColor("_EmissionColor", Color.black);
        if (glowLight != null)
            glowLight.intensity = 0f;

        // Update instructions
        if (instructions != null)
        {
            instructions.ShowMessage("Place the book on the table!");
        }

        // Enable the snap zone so the table is ready to receive the book
        if (snapZone != null)
        {
            snapZone.EnableSnapZone();
        }
    }

    void OnRelease(SelectExitEventArgs args)
    {
        // If the book was released but NOT on the table, remind the player
        if (hasBeenGrabbed && instructions != null && snapZone != null && !snapZone.bookPlaced)
        {
            instructions.ShowMessage("Place the book on the table!");
        }
    }

    /// <summary>
    /// Call this to make the book glow (used by BookPortalTrigger after thief sequence).
    /// </summary>
    public void StartGlowing()
    {
        isGlowing = true;
    }

    public void StopGlowing()
    {
        isGlowing = false;
        if (useEmissionProperty && bookMaterial != null)
            bookMaterial.SetColor("_EmissionColor", Color.black);
        if (glowLight != null)
            glowLight.intensity = 0f;
    }

    /// <summary>
    /// Called by BookPortalTrigger on hover enter/exit. Restores Inspector-configured
    /// base values on unhover — never overwrites with hardcoded defaults.
    /// </summary>
    public void SetHoverHighlight(bool hovered)
    {
        glowIntensity = hovered ? _baseGlowIntensity * hoverIntensityMultiplier : _baseGlowIntensity;
        glowColor     = hovered ? hoverGlowColor : _baseGlowColor;
    }

    void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrab);
            grabInteractable.selectExited.RemoveListener(OnRelease);
        }
    }
}
