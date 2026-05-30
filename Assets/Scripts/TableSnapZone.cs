using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections;

/// <summary>
/// Detects when the player drops the book on the table.
/// After the open book appears, a point light above it pulses with
/// accelerating mysterious colors, then the thief bursts out.
/// Uses a dynamic Point Light so no shader changes needed.
/// </summary>
public class TableSnapZone : MonoBehaviour
{
    [Header("=== Book References ===")]
    public GameObject closedBook;
    public GameObject openBook;

    [Header("=== Light Position ===")]
    public Transform lightTarget;

    [Header("=== Open Book Placement ===")]
    public Transform openBookPosition;

    [Header("=== Light Show Settings ===")]
    [Tooltip("How long before the thief bursts out")]
    public float buildUpDuration = 5f;

    [Header("=== References ===")]
    public ThiefSpawner thiefSpawner;
    public UIInstructions instructions;

    [HideInInspector]
    public bool bookPlaced = false;

    private bool snapZoneActive = false;
    private bool bookIsInZone = false;

    // Light show colors
    private Color[] glowColors = new Color[]
    {
        new Color(0.2f, 0.5f, 1f),    // blue
        new Color(1f, 0.3f, 0.8f),    // pink
        new Color(0.3f, 1f, 0.5f),    // green
        new Color(1f, 0.8f, 0.1f),    // gold
        new Color(1f, 0.1f, 0.1f),    // red
        new Color(1f, 1f, 1f)         // white
    };

    void Start()
    {
        if (openBook != null)
            openBook.SetActive(false);
    }

    public void EnableSnapZone()
    {
        snapZoneActive = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!snapZoneActive || bookPlaced) return;

        GlowingBook book = other.GetComponent<GlowingBook>();
        if (book == null) book = other.GetComponentInParent<GlowingBook>();

        if (book != null)
        {
            bookIsInZone = true;

            UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab = other.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grab == null) grab = other.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

            if (grab != null && grab.isSelected)
            {
                grab.selectExited.AddListener(OnBookReleased);
                return;
            }

            PlaceBook();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!snapZoneActive || bookPlaced) return;

        GlowingBook book = other.GetComponent<GlowingBook>();
        if (book == null) book = other.GetComponentInParent<GlowingBook>();

        if (book != null)
        {
            bookIsInZone = false;

            UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab = other.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grab == null) grab = other.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grab != null)
                grab.selectExited.RemoveListener(OnBookReleased);
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (!snapZoneActive || bookPlaced) return;

        GlowingBook book = other.GetComponent<GlowingBook>();
        if (book == null) book = other.GetComponentInParent<GlowingBook>();

        if (book != null)
            bookIsInZone = true;
    }

    void Update()
    {
        if (!snapZoneActive || bookPlaced || !bookIsInZone) return;

        if (closedBook != null)
        {
            UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab = closedBook.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grab != null && !grab.isSelected)
                PlaceBook();
        }
    }

    void OnBookReleased(SelectExitEventArgs args)
    {
        UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab = args.interactableObject as UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;
        if (grab != null)
            grab.selectExited.RemoveListener(OnBookReleased);

        StartCoroutine(DelayedPlace());
    }

    IEnumerator DelayedPlace()
    {
        yield return new WaitForSeconds(0.1f);
        if (!bookPlaced) PlaceBook();
    }

    void PlaceBook()
    {
        if (bookPlaced) return;
        bookPlaced = true;

        if (closedBook != null)
        {
            Rigidbody rb = closedBook.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab = closedBook.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grab != null) grab.enabled = false;

            closedBook.SetActive(false);
        }

        if (openBook != null)
        {
            if (openBookPosition != null)
            {
                openBook.transform.position = openBookPosition.position;
                openBook.transform.rotation = openBookPosition.rotation;
            }

            openBook.SetActive(true);
        }
        if (instructions != null)
            instructions.HideMessage();

        StartCoroutine(MysteriousLightShow());
    }

    IEnumerator MysteriousLightShow()
    {
        // Create a point light above the book
        GameObject lightObj = new GameObject("BookGlowLight");
        Vector3 bookPos = lightTarget != null ? lightTarget.position : transform.position;
        lightObj.transform.position = bookPos + Vector3.up * 0.5f;

        Light glowLight = lightObj.AddComponent<Light>();
        glowLight.type = LightType.Point;
        glowLight.range = 5f;
        glowLight.intensity = 0f;
        glowLight.shadows = LightShadows.None;

        // Also create a second light below for dramatic underlight
        GameObject lightObj2 = new GameObject("BookGlowLight2");
        lightObj2.transform.position = bookPos + Vector3.up * 0.1f;
        Light glowLight2 = lightObj2.AddComponent<Light>();
        glowLight2.type = LightType.Point;
        glowLight2.range = 3f;
        glowLight2.intensity = 0f;
        glowLight2.shadows = LightShadows.None;

        float elapsed = 0;

        // Phase 1: Slow mysterious pulsing (first 60% of duration)
        // Phase 2: Faster cycling (next 30%)
        // Phase 3: Frantic strobing (final 10%)

        while (elapsed < buildUpDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / buildUpDuration;

            // Speed increases exponentially
            float cycleSpeed = Mathf.Lerp(1.5f, 20f, progress * progress * progress);

            // Pulse wave
            float pulse = (Mathf.Sin(Time.time * cycleSpeed) + 1f) / 2f;

            // Intensity builds from gentle to intense
            float maxIntensity = Mathf.Lerp(1f, 15f, progress * progress);
            float intensity = pulse * maxIntensity;

            // Color cycles faster as it builds
            float colorSpeed = Mathf.Lerp(0.3f, 8f, progress * progress);
            int colorIndex = ((int)(Time.time * colorSpeed)) % glowColors.Length;
            Color currentColor = glowColors[colorIndex];

            // In the final 20%, add flickering
            if (progress > 0.8f)
            {
                float flicker = Random.Range(0.5f, 1.5f);
                intensity *= flicker;
            }

            glowLight.color = currentColor;
            glowLight.intensity = intensity;
            glowLight2.color = currentColor;
            glowLight2.intensity = intensity * 0.7f;

            yield return null;
        }

        // FINAL FLASH: blinding white
        glowLight.color = Color.white;
        glowLight.intensity = 30f;
        glowLight.range = 10f;
        glowLight2.color = Color.white;
        glowLight2.intensity = 20f;

        yield return new WaitForSeconds(0.3f);

        // Kill the lights
        glowLight.intensity = 0;
        glowLight2.intensity = 0;

        yield return new WaitForSeconds(0.2f);

        // Clean up
        Destroy(lightObj);
        Destroy(lightObj2);

        // SPAWN THE THIEF!
        if (thiefSpawner != null)
            thiefSpawner.SpawnThief();
    }
}