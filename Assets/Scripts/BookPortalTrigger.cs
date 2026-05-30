using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// After ThiefSpawner.OnSequenceComplete fires, this component becomes the single owner of:
///   1. Starting the open_book glow (via the GlowingBook component on open_book)
///   2. Showing the "Touch the glowing book" UI instruction
///   3. Wiring hover highlight and player-select → TriggerPortal()
///
/// The player must actively touch/select the book to enter the comic world.
/// No auto-trigger after the thief sequence — player has agency.
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public class BookPortalTrigger : MonoBehaviour
{
    [Header("=== References ===")]
    public ThiefSpawner thiefSpawner;
    public UIInstructions instructions;
    [Tooltip("GlowingBook on open_book — starts dark, glows after thief sequence ends")]
    public GlowingBook portalGlow;

    private bool _triggered = false;
    private XRSimpleInteractable simpleInteractable;

    void Start()
    {
        simpleInteractable = GetComponent<XRSimpleInteractable>();

        if (thiefSpawner == null)
            thiefSpawner = FindFirstObjectByType<ThiefSpawner>();

        if (thiefSpawner != null)
            thiefSpawner.OnSequenceComplete += OnThiefSequenceComplete;
        else
            Debug.LogWarning("[BookPortalTrigger] ThiefSpawner not found!");
    }

    void OnThiefSequenceComplete()
    {
        Debug.Log("[BookPortalTrigger] Sequence done — starting portal glow, awaiting player interaction");

        // Start the open_book glow (single owner — ThiefSpawner no longer calls this)
        if (portalGlow != null)
            portalGlow.StartGlowing();

        // Show instruction
        if (instructions != null)
            instructions.ShowMessage("Touch the glowing book to follow the thief!");

        // Wire player interaction — hover highlight + select = enter portal
        if (simpleInteractable != null)
        {
            simpleInteractable.selectEntered.AddListener(OnBookSelected);
            simpleInteractable.hoverEntered.AddListener(OnBookHoverEnter);
            simpleInteractable.hoverExited.AddListener(OnBookHoverExit);
        }
    }

    void OnBookSelected(SelectEnterEventArgs args)
    {
        TriggerPortal();
    }

    void OnBookHoverEnter(HoverEnterEventArgs args)
    {
        if (portalGlow != null) portalGlow.SetHoverHighlight(true);
    }

    void OnBookHoverExit(HoverExitEventArgs args)
    {
        if (portalGlow != null) portalGlow.SetHoverHighlight(false);
    }

    public void TriggerPortal()
    {
        if (_triggered) return;
        _triggered = true;
        Debug.Log("[BookPortalTrigger] Portal triggered!");
        StartCoroutine(DoTransition());
    }

    System.Collections.IEnumerator DoTransition()
    {
        yield return new WaitForSeconds(0.5f);
        if (TimeTunnelTransition.Instance != null)
            TimeTunnelTransition.Instance.PlayTransition("ComicWorld");
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene("ComicWorld");
    }

    void OnDestroy()
    {
        if (thiefSpawner != null)
            thiefSpawner.OnSequenceComplete -= OnThiefSequenceComplete;

        if (simpleInteractable != null)
        {
            simpleInteractable.selectEntered.RemoveListener(OnBookSelected);
            simpleInteractable.hoverEntered.RemoveListener(OnBookHoverEnter);
            simpleInteractable.hoverExited.RemoveListener(OnBookHoverExit);
        }
    }
}
