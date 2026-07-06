using UnityEngine;

/// <summary>
/// One pushable edge flap of Frame 1. Reports to Frame1FoldPuzzle when pushed.
/// Push works two ways:
///   1. Physically: the player's hand (sphere collider on the controllers) enters the
///      flap's trigger collider.
///   2. Laser: XRSimpleInteractable select (added alongside), for ray users.
/// </summary>
[RequireComponent(typeof(Collider))]
public class FrameFlap : MonoBehaviour
{
    public Frame1FoldPuzzle puzzle;
    [Tooltip("Local axis the flap folds around (hinge direction)")]
    public Vector3 foldAxis = Vector3.up;
    [Tooltip("+1/-1 — which way around the axis is 'backward'")]
    public float foldSign = 1f;

    private bool _pushed;

    void Start()
    {
        var simple = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        if (simple != null)
            simple.selectEntered.AddListener(_ => Push());
    }

    void OnTriggerEnter(Collider other)
    {
        // Only react to the player's hands/controllers, not stray physics
        if (other.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor>() != null
            || other.name.Contains("Sphere") || other.name.Contains("Hand"))
            Push();
    }

    public void Push()
    {
        if (_pushed) return;
        _pushed = true;
        if (puzzle != null) puzzle.PushFlap(this);
        // collider off so the folded flap can't block hands/rays behind it
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
    }
}
