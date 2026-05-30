using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Attach to Frame 2. Detects two simultaneous XR grabs (one per controller edge)
/// and scales the frame outward as controllers separate, up to maxScale.
/// As scale increases, calls TeddyBear2Dto3D.UpdateTransitionState(t).
/// </summary>
public class FrameEnlarger : MonoBehaviour
{
    [Header("=== References ===")]
    [Tooltip("Left edge XRGrabInteractable child")]
    public XRGrabInteractable leftEdgeGrab;
    [Tooltip("Right edge XRGrabInteractable child")]
    public XRGrabInteractable rightEdgeGrab;
    public TeddyBear2Dto3D teddyTransition;

    [Header("=== Settings ===")]
    public float maxScale = 3f;
    public float minControllerDistance = 0.1f;   // distance at scale=1
    public float maxControllerDistance = 1.5f;   // distance at maxScale

    public event System.Action OnFullyEnlarged;

    private bool _leftHeld = false;
    private bool _rightHeld = false;
    private Transform _leftController;
    private Transform _rightController;
    private bool _enlargedFired = false;
    private Vector3 _baseScale;

    void Start()
    {
        _baseScale = transform.localScale;

        if (leftEdgeGrab == null || rightEdgeGrab == null)
        {
            Debug.LogWarning("[FrameEnlarger] TODO: Assign leftEdgeGrab and rightEdgeGrab in Inspector.");
            return;
        }

        leftEdgeGrab.selectEntered.AddListener(args =>
        {
            _leftHeld = true;
            _leftController = args.interactorObject.transform;
        });
        leftEdgeGrab.selectExited.AddListener(_ => { _leftHeld = false; _leftController = null; });

        rightEdgeGrab.selectEntered.AddListener(args =>
        {
            _rightHeld = true;
            _rightController = args.interactorObject.transform;
        });
        rightEdgeGrab.selectExited.AddListener(_ => { _rightHeld = false; _rightController = null; });
    }

    void Update()
    {
        if (!_leftHeld || !_rightHeld) return;
        if (_leftController == null || _rightController == null) return;

        float dist = Vector3.Distance(_leftController.position, _rightController.position);
        float t = Mathf.InverseLerp(minControllerDistance, maxControllerDistance, dist);
        float newScale = Mathf.Lerp(1f, maxScale, t);

        transform.localScale = _baseScale * newScale;

        if (teddyTransition != null)
            teddyTransition.UpdateTransitionState(t);

        if (!_enlargedFired && t >= 0.99f)
        {
            _enlargedFired = true;
            OnFullyEnlarged?.Invoke();
        }
    }
}
