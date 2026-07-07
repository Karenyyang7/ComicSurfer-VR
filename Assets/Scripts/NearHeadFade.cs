using UnityEngine;

/// <summary>
/// Hides this object's renderers when the player's head is about to intersect it.
/// The static story frames (9-16) stand on the player's walking path — without this,
/// walking into one fills the headset with a giant black number/art sheet.
/// Hysteresis (hide at hideDistance, show again at showDistance) prevents flicker
/// when the player hovers right at the threshold.
/// </summary>
public class NearHeadFade : MonoBehaviour
{
    public float hideDistance = 0.45f;
    public float showDistance = 0.60f;

    private Renderer[] _renderers;
    private bool _hidden;

    void Start()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null || _renderers == null || _renderers.Length == 0) return;

        Vector3 head = cam.transform.position;
        float closest = float.MaxValue;
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            float d = Vector3.Distance(r.bounds.ClosestPoint(head), head);
            if (d < closest) closest = d;
        }

        bool hide = _hidden ? closest < showDistance : closest < hideDistance;
        if (hide != _hidden)
        {
            _hidden = hide;
            foreach (var r in _renderers)
                if (r != null) r.enabled = !hide;
        }
    }
}
