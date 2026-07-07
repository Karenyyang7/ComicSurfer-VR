using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Companion behavior for the freed teddy (VR feedback round 6): when the player
/// isn't holding it, the teddy floats along beside them — off to the side of the
/// view so it never blocks the laser or the frames, gliding lazily to catch up and
/// bobbing in place. Grabbing it any time still works (grab tracking owns the
/// transform while selected). Enabled by TeddyBear2Dto3D on first release.
/// </summary>
public class TeddyCompanionFloat : MonoBehaviour
{
    [Tooltip("Distance from the player's head to the float anchor")]
    public float followDistance = 0.85f;
    [Tooltip("Yaw (deg) of the anchor off the view direction — keeps it out of the interaction cone")]
    public float anchorYaw = 50f;
    [Tooltip("Anchor height relative to the head (negative = chest height)")]
    public float heightOffset = -0.3f;
    [Tooltip("Catch-up rate (higher = tighter follow)")]
    public float followLerp = 1.6f;
    public float bobAmplitude = 0.04f;
    public float bobPeriod = 3.5f;

    private XRGrabInteractable _grab;
    private TeddyBearController _ctrl;
    private float _bobPhase;

    void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        _ctrl = GetComponent<TeddyBearController>();
    }

    void Update()
    {
        if (_grab != null && _grab.isSelected) return; // hand owns it
        var cam = Camera.main;
        if (cam == null) return;

        Vector3 fwd = cam.transform.forward;
        fwd.y = 0f;
        fwd = fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward;
        Vector3 dir = Quaternion.Euler(0f, anchorYaw, 0f) * fwd;

        _bobPhase += Time.deltaTime * (2f * Mathf.PI / bobPeriod);
        Vector3 anchor = cam.transform.position + dir * followDistance
                       + Vector3.up * (heightOffset + Mathf.Sin(_bobPhase) * bobAmplitude);

        // frame-rate-independent lazy glide
        float k = 1f - Mathf.Exp(-followLerp * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, anchor, k);

        // face the player — unless the story has it pointing at the finale
        if (_ctrl == null || _ctrl.currentState != TeddyBearController.TeddyState.Pointing)
        {
            Vector3 look = cam.transform.position - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(look.normalized), Time.deltaTime * 4f);
        }
    }
}
