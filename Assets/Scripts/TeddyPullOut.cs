using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Force interaction for pulling the teddy out of Frame 1 (VR feedback):
/// the first two grabs let the player DRAG the teddy a short way — the frame stretches
/// with it — then the teddy slips free of the hand and snaps back with a jolt. On the
/// THIRD pull it tears free: quadrant flaps blast apart as art shards (big shatter),
/// and the normal grabbed-teddy flow continues in the player's hand.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class TeddyPullOut : MonoBehaviour
{
    [Tooltip("Pull distance (m) at which a tug 'slips' (or tears free on the last one)")]
    public float tugDistance = 0.26f;
    [Tooltip("Number of tugs before the teddy comes free")]
    public int tugsNeeded = 3;
    [Tooltip("Fold root whose quadrants stretch with the pull and blast apart at the end")]
    public Transform foldRoot;

    public event System.Action OnTornFree;

    private XRGrabInteractable _grab;
    private Vector3 _anchorPos;
    private Quaternion _anchorRot;
    private Transform _anchorParent;
    private int _tugs;
    private bool _free;
    private bool _watching;

    void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        _grab.selectEntered.AddListener(OnSelect);

        _anchorParent = transform.parent;
        _anchorPos = transform.localPosition;
        _anchorRot = transform.localRotation;
    }

    void OnSelect(SelectEnterEventArgs args)
    {
        if (_free || _watching) return;
        StartCoroutine(WatchPull(args.interactorObject));
    }

    IEnumerator WatchPull(UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor interactor)
    {
        _watching = true;
        Vector3 anchorWorld = _anchorParent != null ? _anchorParent.TransformPoint(_anchorPos) : _anchorPos;

        while (_grab.isSelected && !_free)
        {
            float pull = Vector3.Distance(transform.position, anchorWorld);

            // the frame stretches with the pull
            if (foldRoot != null)
            {
                float s = 1f + Mathf.Clamp01(pull / tugDistance) * 0.16f;
                foldRoot.localScale = new Vector3(s, s, 1f);
            }

            if (pull > tugDistance)
            {
                _tugs++;
                if (_tugs >= tugsNeeded)
                {
                    TearFree();
                    _watching = false;
                    yield break;
                }

                // slip: force-release and snap back with a jolt
                var mgr = _grab.interactionManager;
                if (mgr != null && _grab.isSelected)
                    mgr.SelectExit(interactor, (IXRSelectInteractable)_grab);
                yield return StartCoroutine(SnapBack());
                break;
            }
            yield return null;
        }

        if (foldRoot != null) foldRoot.localScale = Vector3.one;
        _watching = false;
    }

    IEnumerator SnapBack()
    {
        SfxPlayer.Play("teddy_strain", transform.position);
        transform.SetParent(_anchorParent);
        Vector3 from = transform.localPosition;
        float dur = 0.22f, e = 0f;
        while (e < dur)
        {
            e += Time.deltaTime;
            float k = e / dur;
            // elastic overshoot
            float s = 1f - Mathf.Pow(1f - k, 2f) * Mathf.Cos(k * 14f) * 0.5f - Mathf.Pow(1f - k, 2f) * 0.5f;
            transform.localPosition = Vector3.LerpUnclamped(from, _anchorPos, s);
            yield return null;
        }
        transform.localPosition = _anchorPos;
        transform.localRotation = _anchorRot;
        if (foldRoot != null) foldRoot.localScale = Vector3.one;
        Debug.Log($"[TeddyPullOut] tug {_tugs}/{tugsNeeded} — teddy slipped back");
    }

    void TearFree()
    {
        if (_free) return;
        _free = true;
        Debug.Log("[TeddyPullOut] TORN FREE!");
        SfxPlayer.Play("frame_shatter", transform.position);

        // quadrants blast apart carrying their art
        StartCoroutine(BlastQuadrants());

        // the teddy is truly free — run the grabbed-teddy story flow (detach, shrink, Phase 2)
        var teddy = GetComponent<TeddyBear2Dto3D>();
        if (teddy != null) teddy.CompleteGrab();

        OnTornFree?.Invoke();
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

    /// <summary>Test hook for the headless driver — one simulated tug.</summary>
    public void SimulateTug()
    {
        _tugs++;
        Debug.Log($"[TeddyPullOut] simulated tug {_tugs}/{tugsNeeded}");
        if (_tugs >= tugsNeeded) TearFree();
        else SfxPlayer.Play("teddy_strain", transform.position);
    }

    public bool IsFree => _free;
}
