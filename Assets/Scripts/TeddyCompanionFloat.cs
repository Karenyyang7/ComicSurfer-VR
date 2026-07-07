using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Companion behavior for the freed teddy: floats in front of the player, a little to
/// the side, when not held. VR feedback round 7: it must NOT chase the player's head —
/// v1 recomputed the anchor from the view direction every frame, so turning your head
/// pushed the teddy away and you could never look straight at it. Now it SETTLES:
/// once parked it stays put (bobbing, facing you) while you look around; it only glides
/// to a new spot in front when you walk away or leave it far behind your view.
/// Enabled by TeddyBear2Dto3D on first release; grabbing it works any time.
/// </summary>
public class TeddyCompanionFloat : MonoBehaviour
{
    [Tooltip("Distance from the player's head to the float anchor")]
    public float followDistance = 0.8f;
    [Tooltip("Yaw (deg) of the anchor off the view direction — in front, a little to the side")]
    public float anchorYaw = 25f;
    [Tooltip("Anchor height relative to the head (negative = chest height)")]
    public float heightOffset = -0.25f;
    [Tooltip("Re-anchor when the teddy is farther than this from the head")]
    public float maxDistance = 1.4f;
    [Tooltip("Re-anchor when the teddy is more than this many degrees outside the view")]
    public float maxAngle = 110f;
    [Tooltip("Glide speed toward a new anchor (m/s, eased)")]
    public float glideLerp = 2.2f;
    public float bobAmplitude = 0.035f;
    public float bobPeriod = 3.5f;

    private XRGrabInteractable _grab;
    private TeddyBearController _ctrl;
    private float _bobPhase;
    private Vector3 _settled;
    private bool _hasSettled;
    private bool _gliding;

    void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        _ctrl = GetComponent<TeddyBearController>();
    }

    void OnEnable()
    {
        // fresh release: park wherever we are unless out of bounds
        _settled = transform.position;
        _hasSettled = true;
        _gliding = false;
    }

    void Update()
    {
        if (_grab != null && _grab.isSelected) { _hasSettled = false; return; }
        var cam = Camera.main;
        if (cam == null) return;

        Vector3 head = cam.transform.position;
        Vector3 fwd = cam.transform.forward;
        fwd.y = 0f;
        fwd = fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward;

        if (!_hasSettled) { _settled = transform.position; _hasSettled = true; }

        // park check: only re-anchor when the player leaves it behind — head turns alone
        // must not move it, so the player can turn and look at it
        Vector3 flat = _settled - head;
        flat.y = 0f;
        float dist = flat.magnitude;
        float angle = Vector3.Angle(fwd, flat.sqrMagnitude > 0.0001f ? flat.normalized : fwd);
        if (!_gliding && (dist > maxDistance || dist < 0.3f || angle > maxAngle))
            _gliding = true;

        if (_gliding)
        {
            Vector3 dir = Quaternion.Euler(0f, anchorYaw, 0f) * fwd;
            Vector3 anchor = head + dir * followDistance + Vector3.up * heightOffset;
            float k = 1f - Mathf.Exp(-glideLerp * Time.deltaTime);
            _settled = Vector3.Lerp(_settled, anchor, k);
            if ((_settled - anchor).sqrMagnitude < 0.01f) _gliding = false;
        }

        _bobPhase += Time.deltaTime * (2f * Mathf.PI / bobPeriod);
        transform.position = _settled + Vector3.up * (Mathf.Sin(_bobPhase) * bobAmplitude);

        // face the player — unless the story has it pointing at the finale
        if (_ctrl == null || _ctrl.currentState != TeddyBearController.TeddyState.Pointing)
        {
            Vector3 look = head - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(look.normalized), Time.deltaTime * 4f);
        }
    }
}
