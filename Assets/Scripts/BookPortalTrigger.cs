using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

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

    [Header("=== Laser Hover ===")]
    [Tooltip("Color the laser beam turns to while it is hovering the book")]
    public Color laserHoverColor = Color.white;

    private bool _triggered = false;
    private bool _wired = false;
    private XRSimpleInteractable simpleInteractable;

    // Per-line-visual saved beam colors, so we can restore them on hover exit.
    private class SavedBeam { public Gradient valid; public Gradient invalid; public Gradient blocked; }
    private readonly Dictionary<XRInteractorLineVisual, SavedBeam> _savedBeams =
        new Dictionary<XRInteractorLineVisual, SavedBeam>();

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
        if (_wired) return; // idempotent — wire glow + listeners at most once
        _wired = true;
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
        // Book glow highlight on the FIRST hover only — ref-counted via _savedBeams so a
        // second hand exiting doesn't drop the highlight while the first is still hovering.
        if (_savedBeams.Count == 0 && portalGlow != null) portalGlow.SetHoverHighlight(true);

        // Turn the hovering laser beam white (override valid/invalid/blocked, restore on exit).
        XRInteractorLineVisual lv = GetLineVisual(args.interactorObject);
        if (lv != null && !_savedBeams.ContainsKey(lv))
        {
            _savedBeams[lv] = new SavedBeam
            {
                valid = lv.validColorGradient,
                invalid = lv.invalidColorGradient,
                blocked = lv.blockedColorGradient
            };
            Gradient white = SolidGradient(laserHoverColor);
            lv.validColorGradient = white;
            lv.invalidColorGradient = white;
            lv.blockedColorGradient = white;
        }
    }

    void OnBookHoverExit(HoverExitEventArgs args)
    {
        // Restore the laser beam's normal color.
        XRInteractorLineVisual lv = GetLineVisual(args.interactorObject);
        if (lv != null && _savedBeams.TryGetValue(lv, out SavedBeam saved))
        {
            lv.validColorGradient = saved.valid;
            lv.invalidColorGradient = saved.invalid;
            lv.blockedColorGradient = saved.blocked;
            _savedBeams.Remove(lv);
        }

        // Drop the book glow highlight only when NO hand is hovering any more.
        if (_savedBeams.Count == 0 && portalGlow != null) portalGlow.SetHoverHighlight(false);
    }

    // The XRInteractorLineVisual lives on the same GameObject as the ray interactor.
    static XRInteractorLineVisual GetLineVisual(object interactor)
    {
        Component comp = interactor as Component;
        if (comp == null) return null;
        return comp.GetComponentInChildren<XRInteractorLineVisual>();
    }

    static Gradient SolidGradient(Color c)
    {
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
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
        // Restore any beams we recolored while hovering (e.g. on scene unload).
        foreach (var kvp in _savedBeams)
        {
            if (kvp.Key == null) continue;
            kvp.Key.validColorGradient = kvp.Value.valid;
            kvp.Key.invalidColorGradient = kvp.Value.invalid;
            kvp.Key.blockedColorGradient = kvp.Value.blocked;
        }
        _savedBeams.Clear();

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
