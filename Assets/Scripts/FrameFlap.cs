using UnityEngine;

/// <summary>
/// One pushable edge flap of Frame 1. Reports to Frame1FoldPuzzle when pushed.
/// Push works two ways:
///   1. Laser: point at the flap and pull the trigger (XRSimpleInteractable select).
///      The collider is SOLID (not a trigger) — XR ray interactors ignore trigger
///      colliders by default, which made the flaps completely un-lasereable.
///   2. Physically: bring either hand/controller within pushDistance of the flap.
/// Hovering tints the flap slightly so the player can see it's interactive.
/// </summary>
[RequireComponent(typeof(Collider))]
public class FrameFlap : MonoBehaviour
{
    public Frame1FoldPuzzle puzzle;
    [Tooltip("Local axis the flap folds around (hinge direction)")]
    public Vector3 foldAxis = Vector3.up;
    [Tooltip("+1/-1 — which way around the axis is 'backward'")]
    public float foldSign = 1f;
    [Tooltip("Hand distance (m) that counts as a push")]
    public float pushDistance = 0.14f;

    private bool _pushed;
    private Collider _col;
    private Renderer _artRenderer;
    private MaterialPropertyBlock _mpb;
    private static Transform[] _hands;

    void Start()
    {
        _col = GetComponent<Collider>();
        _artRenderer = GetComponentInChildren<Renderer>();
        _mpb = new MaterialPropertyBlock();

        var simple = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        if (simple != null)
        {
            simple.selectEntered.AddListener(_ => Push());
            simple.hoverEntered.AddListener(_ => SetHighlight(true));
            simple.hoverExited.AddListener(_ => SetHighlight(false));
        }

        if (_hands == null || _hands.Length == 0)
        {
            var interactors = FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.XRDirectInteractor>(FindObjectsSortMode.None);
            _hands = new Transform[interactors.Length];
            for (int i = 0; i < interactors.Length; i++) _hands[i] = interactors[i].transform;
        }
    }

    void Update()
    {
        if (_pushed || _hands == null) return;
        foreach (var hand in _hands)
        {
            if (hand == null) continue;
            Vector3 closest = _col.ClosestPoint(hand.position);
            if (Vector3.Distance(closest, hand.position) < pushDistance)
            {
                Push();
                return;
            }
        }
    }

    void SetHighlight(bool on)
    {
        if (_artRenderer == null || _pushed) return;
        _artRenderer.GetPropertyBlock(_mpb);
        _mpb.SetColor("_BaseColor", on ? new Color(1.25f, 1.25f, 1.05f) : Color.white);
        _artRenderer.SetPropertyBlock(_mpb);
    }

    public void Push()
    {
        if (_pushed) return;
        _pushed = true;
        SetHighlight(false);
        if (puzzle != null) puzzle.PushFlap(this);
        // collider off so the folded flap can't block hands/rays behind it
        if (_col != null) _col.enabled = false;
    }
}
