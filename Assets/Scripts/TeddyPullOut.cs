using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Force interaction for pulling the teddy out of Frame 1 (VR feedback v2):
/// Until torn free the teddy RESISTS — grabbing does NOT bring it to your hand
/// (track position/rotation off, like a fixed handle). As you pull, the teddy leans
/// toward your hand and the frame stretches; past the tug distance it slips your grip
/// with a crack and a jolt. Three tugs — or one hard continuous yank — tears it free:
/// the quadrants blast apart as art shards and the teddy flies into your hand.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class TeddyPullOut : MonoBehaviour
{
    [Tooltip("Hand travel (m) that counts as one tug")]
    public float tugDistance = 0.22f;
    [Tooltip("Tugs needed to tear free")]
    public int tugsNeeded = 3;
    [Tooltip("Hand travel that tears it free in ONE continuous yank")]
    public float hardYankDistance = 0.5f;
    [Tooltip("How far the teddy visually leans toward the pulling hand")]
    public float maxLean = 0.09f;
    [Tooltip("Fold root whose quadrants stretch with the pull and blast apart at the end")]
    public Transform foldRoot;

    public event System.Action OnTornFree;

    private XRGrabInteractable _grab;
    private Vector3 _anchorLocalPos;
    private Quaternion _anchorLocalRot;
    private Transform _anchorParent;
    private int _tugs;
    private bool _free;
    private bool _watching;
    private bool _hintShown;

    void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();

        // RESIST: grabbing must not move the teddy until it's torn free.
        // (Default XRGrab snapped it instantly into the hand — no pull sensation at all.)
        _grab.trackPosition = false;
        _grab.trackRotation = false;
        _grab.throwOnDetach = false;

        // Held orientation: attach transform rotated so the bear FACES the player
        // when it finally flies to the hand (default alignment showed its back).
        var attach = new GameObject("GrabAttach").transform;
        attach.SetParent(transform, false);
        attach.localPosition = Vector3.zero;
        attach.localRotation = Quaternion.Euler(0f, 180f, 0f);
        _grab.attachTransform = attach;
        _grab.useDynamicAttach = false;

        _anchorParent = transform.parent;
        _anchorLocalPos = transform.localPosition;
        _anchorLocalRot = transform.localRotation;
    }

    private Vector3 _baseline;
    private bool _hasBaseline;
    private bool _jolting;

    void Update()
    {
        if (_free || _grab == null) return;

        if (!_grab.isSelected || _grab.interactorsSelecting.Count == 0)
        {
            // released without tearing: settle back to the anchor
            _hasBaseline = false;
            if (!_jolting)
            {
                transform.localPosition = Vector3.Lerp(transform.localPosition, _anchorLocalPos, Time.deltaTime * 10f);
                transform.localRotation = Quaternion.Slerp(transform.localRotation, _anchorLocalRot, Time.deltaTime * 10f);
                if (foldRoot != null)
                    foldRoot.localScale = Vector3.Lerp(foldRoot.localScale, Vector3.one, Time.deltaTime * 10f);
            }
            return;
        }

        var hand = _grab.interactorsSelecting[0].transform;
        if (!_hasBaseline)
        {
            _baseline = hand.position;
            _hasBaseline = true;
            return;
        }

        float pull = Vector3.Distance(hand.position, _baseline);
        Vector3 anchorWorld = _anchorParent != null ? _anchorParent.TransformPoint(_anchorLocalPos) : transform.position;

        // strain toward the hand; frame stretches with the effort
        if (!_jolting)
        {
            Vector3 toHand = hand.position - anchorWorld;
            if (toHand.sqrMagnitude > 0.0001f)
                transform.position = anchorWorld + toHand.normalized * Mathf.Min(pull * 0.4f, maxLean);
        }
        if (foldRoot != null)
        {
            float s = 1f + Mathf.Clamp01(pull / tugDistance) * 0.14f;
            foldRoot.localScale = new Vector3(s, s, 1f);
        }

        if (pull > hardYankDistance)
        {
            TearFree();
            return;
        }

        if (pull > tugDistance)
        {
            _tugs++;
            Debug.Log($"[TeddyPullOut] tug {_tugs}/{tugsNeeded} (pull {pull:F2}m)");
            if (_tugs >= tugsNeeded)
            {
                TearFree();
                return;
            }

            // slip: crack + jolt, RESET the baseline and keep watching — never depend on
            // the selection actually ending (fast re-squeezes / manual grips persist)
            _baseline = hand.position;
            SfxPlayer.Play("teddy_strain", transform.position);
            ShatterFX.Burst(transform.position, new Color(0.95f, 0.8f, 0.75f), 8, 1.2f, 0.9f, 0.05f);
            var mgr = _grab.interactionManager;
            if (mgr != null && _grab.isSelected)
                mgr.SelectExit(_grab.interactorsSelecting[0], (IXRSelectInteractable)_grab);
            StartCoroutine(JoltBack());

            if (!_hintShown)
            {
                _hintShown = true;
                DialogueSystem.Instance?.ShowDialogue(new string[] { "It's stuck to the page... PULL harder!" });
            }
        }
    }

    IEnumerator JoltBack()
    {
        _jolting = true;
        Vector3 from = transform.localPosition;
        float dur = 0.18f, e = 0f;
        while (e < dur)
        {
            e += Time.deltaTime;
            float k = e / dur;
            float s = 1f - Mathf.Pow(1f - k, 2f) * Mathf.Cos(k * 18f);
            transform.localPosition = Vector3.LerpUnclamped(from, _anchorLocalPos, Mathf.Clamp01(s));
            yield return null;
        }
        transform.localPosition = _anchorLocalPos;
        transform.localRotation = _anchorLocalRot;
        if (foldRoot != null) foldRoot.localScale = Vector3.one;
        _jolting = false;
    }

    void TearFree()
    {
        if (_free) return;
        _free = true;
        Debug.Log("[TeddyPullOut] TORN FREE!");
        SfxPlayer.Play("frame_shatter", transform.position);

        // XRGrab caches its tracking setup at select time — enabling trackPosition
        // mid-grab does nothing. Force a re-select so the teddy actually flies to the
        // hand (with the GrabAttach rotation = facing the player).
        _grab.trackPosition = true;
        _grab.trackRotation = true;
        if (_grab.isSelected && _grab.interactorsSelecting.Count > 0)
            StartCoroutine(ReSelect(_grab.interactorsSelecting[0]));

        StartCoroutine(BlastQuadrants());

        var teddy = GetComponent<TeddyBear2Dto3D>();
        if (teddy != null) teddy.CompleteGrab();

        OnTornFree?.Invoke();
    }

    IEnumerator ReSelect(UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor interactor)
    {
        var mgr = _grab.interactionManager;
        if (mgr == null) yield break;
        mgr.SelectExit(interactor, (IXRSelectInteractable)_grab);
        yield return null;
        mgr.SelectEnter(interactor, (IXRSelectInteractable)_grab);
    }

    IEnumerator BlastQuadrants()
    {
        if (foldRoot == null) yield break;
        var pieces = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in foldRoot) pieces.Add(child);

        Vector3 center = foldRoot.position;
        float dur = 1.8f, e = 0f;
        var vels = new Vector3[pieces.Count];
        var spins = new Vector3[pieces.Count];
        for (int i = 0; i < pieces.Count; i++)
        {
            Vector3 dir = (pieces[i].position - center).normalized + Random.insideUnitSphere * 0.4f - Vector3.forward * 0.4f;
            vels[i] = dir.normalized * Random.Range(1.8f, 3.2f);
            spins[i] = Random.insideUnitSphere * 260f;
        }
        ShatterFX.Burst(center, new Color(0.95f, 0.75f, 0.75f), 34, 3.0f, 2.8f, 0.11f);

        while (e < dur)
        {
            e += Time.deltaTime;
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i] == null) continue;
                vels[i] += Vector3.down * 1.2f * Time.deltaTime;
                pieces[i].position += vels[i] * Time.deltaTime;
                pieces[i].rotation = Quaternion.Euler(spins[i] * Time.deltaTime) * pieces[i].rotation;
                pieces[i].localScale *= 1f - 0.5f * Time.deltaTime;
            }
            yield return null;
        }
        foldRoot.gameObject.SetActive(false);
    }

    /// <summary>Test hook — one simulated tug (headless driver fallback).</summary>
    public void SimulateTug()
    {
        _tugs++;
        Debug.Log($"[TeddyPullOut] simulated tug {_tugs}/{tugsNeeded}");
        if (_tugs >= tugsNeeded) TearFree();
        else SfxPlayer.Play("teddy_strain", transform.position);
    }

    public bool IsFree => _free;
}
