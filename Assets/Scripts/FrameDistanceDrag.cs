using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Ordering-puzzle QoL (VR feedback round 6): grabbing a comic frame with the laser
/// should NOT yank it into the hand — it's much easier to order the frames by
/// selecting one from afar and sweeping it into a slot at a distance.
/// Attached to each XRRayInteractor by ComicWorldManager; while hovering a ComicFrame
/// it turns Force Grab off (the frame stays at the laser hit distance), and restores
/// the interactor's normal behavior for everything else (teddy, phone, cutouts).
/// </summary>
public class FrameDistanceDrag : MonoBehaviour
{
    private XRRayInteractor _ray;
    private bool _savedForceGrab;
    private int _frameHovers;

    void Awake()
    {
        _ray = GetComponent<XRRayInteractor>();
    }

    void OnEnable()
    {
        if (_ray == null) return;
        _ray.hoverEntered.AddListener(OnHoverEntered);
        _ray.hoverExited.AddListener(OnHoverExited);
    }

    void OnDisable()
    {
        if (_ray == null) return;
        _ray.hoverEntered.RemoveListener(OnHoverEntered);
        _ray.hoverExited.RemoveListener(OnHoverExited);
        if (_frameHovers > 0) { _ray.useForceGrab = _savedForceGrab; _frameHovers = 0; }
    }

    static bool IsFrame(UnityEngine.XR.Interaction.Toolkit.Interactables.IXRInteractable interactable)
    {
        var c = interactable as Component;
        if (c == null) return false;
        // the teddy lives under Frame1 until torn free — it must keep normal force-grab
        // (tear-free relies on the re-select flying it into the hand)
        if (c.GetComponent<TeddyBear2Dto3D>() != null) return false;
        return c.GetComponentInParent<ComicFrame>() != null;
    }

    void OnHoverEntered(HoverEnterEventArgs args)
    {
        if (!IsFrame(args.interactableObject)) return;
        // Force Grab is only read at select time, so flipping it during hover is safe.
        if (_frameHovers == 0)
        {
            _savedForceGrab = _ray.useForceGrab;
            _ray.useForceGrab = false;
        }
        _frameHovers++;
    }

    void OnHoverExited(HoverExitEventArgs args)
    {
        if (!IsFrame(args.interactableObject)) return;
        _frameHovers = Mathf.Max(0, _frameHovers - 1);
        if (_frameHovers == 0)
            _ray.useForceGrab = _savedForceGrab;
    }
}
