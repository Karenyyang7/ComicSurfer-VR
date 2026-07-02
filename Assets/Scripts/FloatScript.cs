using UnityEngine;

/// <summary>
/// Gentle floating animation for comic frames.
/// Each instance gets randomized offsets in Start() so frames don't move in sync.
///
/// Grab-aware: while an XRGrabInteractable on this object is selected, floating pauses;
/// on release the float re-anchors at wherever the player left the frame. The float also
/// composes with the object's base rotation instead of overwriting it, so frames can face
/// any direction (and snap zones can rotate them) without the tilt fighting it.
/// </summary>
public class FloatScript : MonoBehaviour
{
    [Header("=== Vertical Bobbing ===")]
    [Tooltip("Max Y displacement")]
    public float bobAmplitude = 0.1f;
    [Tooltip("Full cycle period in seconds")]
    public float bobPeriod = 4f;

    [Header("=== Horizontal Drift ===")]
    [Tooltip("Max X displacement")]
    public float driftAmplitude = 0.05f;
    [Tooltip("Full cycle period in seconds")]
    public float driftPeriod = 6f;

    [Header("=== Rotation Tilt ===")]
    [Tooltip("Max Z rotation in degrees")]
    public float tiltAmount = 4f;
    [Tooltip("Full cycle period in seconds")]
    public float tiltPeriod = 8f;

    private Vector3 _origin;
    private Quaternion _baseRotation;
    private float _bobOffset;
    private float _driftOffset;
    private float _tiltOffset;
    private float _bobAmp;
    private float _driftAmp;
    private float _tiltAmt;
    private float _bobFreq;
    private float _driftFreq;
    private float _tiltFreq;

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable _grab;
    private bool _wasSelected;

    void Start()
    {
        _origin = transform.position;
        _baseRotation = transform.rotation;
        _grab = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

        // Randomize everything so frames don't sync
        _bobOffset   = Random.Range(0f, Mathf.PI * 2f);
        _driftOffset = Random.Range(0f, Mathf.PI * 2f);
        _tiltOffset  = Random.Range(0f, Mathf.PI * 2f);

        _bobAmp   = Random.Range(bobAmplitude * 0.5f,   bobAmplitude * 1.5f);
        _driftAmp = Random.Range(driftAmplitude * 0.5f, driftAmplitude * 1.5f);
        _tiltAmt  = Random.Range(tiltAmount * 0.5f,     tiltAmount * 1.5f);

        float bobVariance   = Random.Range(0.7f, 1.3f);
        float driftVariance = Random.Range(0.7f, 1.3f);
        float tiltVariance  = Random.Range(0.7f, 1.3f);

        _bobFreq   = (2f * Mathf.PI / bobPeriod)   * bobVariance;
        _driftFreq = (2f * Mathf.PI / driftPeriod) * driftVariance;
        _tiltFreq  = (2f * Mathf.PI / tiltPeriod)  * tiltVariance;
    }

    void Update()
    {
        // While held: let the hand own the transform; re-anchor on release.
        if (_grab != null)
        {
            if (_grab.isSelected) { _wasSelected = true; return; }
            if (_wasSelected)
            {
                _wasSelected = false;
                ResetOrigin();
            }
        }

        float t = Time.time;

        float y = _bobAmp   * Mathf.Sin(t * _bobFreq   + _bobOffset);
        float x = _driftAmp * Mathf.Sin(t * _driftFreq + _driftOffset);
        float z = _tiltAmt  * Mathf.Sin(t * _tiltFreq  + _tiltOffset);

        transform.position = _origin + new Vector3(x, y, 0f);
        transform.rotation = _baseRotation * Quaternion.Euler(0f, 0f, z);
    }

    /// <summary>Re-anchors the float origin/rotation to the current pose (call after teleporting a frame).</summary>
    public void ResetOrigin()
    {
        _origin = transform.position;
        _baseRotation = transform.rotation;
    }
}
