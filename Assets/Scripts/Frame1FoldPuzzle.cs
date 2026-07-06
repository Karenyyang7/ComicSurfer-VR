using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Frame 1 opening puzzle (redesigned per VR feedback): the player PUSHES the frame's four
/// edge flaps back. Each push folds that flap away with a crumple wobble — the art scrambles
/// while the teddy in the center stays intact and becomes 25% more 3D per push. After all
/// 4 pushes the teddy is fully 3D and grabbable.
///
/// Scene structure (built by editor tooling):
///   Frame1/FoldRoot/FlapL|FlapR|FlapT|FlapB   (pivot at the flap's inner hinge edge)
///   Frame1/FoldRoot/CenterArt                 (stays intact behind the teddy)
/// Each flap has a FrameFlap component that reports pushes here.
/// </summary>
public class Frame1FoldPuzzle : MonoBehaviour
{
    [Header("=== References ===")]
    public TeddyBear2Dto3D teddyTransition;
    [Tooltip("Root holding flaps + center art; hidden when the teddy is grabbed (frame 'shatters')")]
    public GameObject foldRoot;

    [Header("=== Fold feel ===")]
    [Tooltip("Degrees the flap folds backward (away from the player)")]
    public float foldAngle = 135f;
    public float foldDuration = 0.6f;
    [Tooltip("Extra chaotic tilt per fold — the 'scramble' feel")]
    public float wobbleTilt = 14f;

    public event System.Action OnFullyFolded;

    private readonly HashSet<FrameFlap> _folded = new HashSet<FrameFlap>();
    private int _totalFlaps;
    private bool _completed;

    void Start()
    {
        _totalFlaps = GetComponentsInChildren<FrameFlap>(true).Length;
        if (_totalFlaps == 0) Debug.LogWarning("[Frame1FoldPuzzle] no FrameFlap children found");

        if (teddyTransition == null)
            teddyTransition = GetComponentInChildren<TeddyBear2Dto3D>(true);
        if (teddyTransition != null)
            teddyTransition.OnTeddyGrabbed += HideFoldVisuals;
    }

    void OnDestroy()
    {
        if (teddyTransition != null)
            teddyTransition.OnTeddyGrabbed -= HideFoldVisuals;
    }

    public void PushFlap(FrameFlap flap)
    {
        if (_completed || flap == null || _folded.Contains(flap)) return;
        _folded.Add(flap);

        StartCoroutine(FoldRoutine(flap));
        SfxPlayer.Play("frame_fold", flap.transform.position);

        float t = _totalFlaps > 0 ? (float)_folded.Count / _totalFlaps : 1f;
        if (teddyTransition != null)
            teddyTransition.UpdateTransitionState(t);

        Debug.Log($"[Frame1FoldPuzzle] flap {flap.name} pushed ({_folded.Count}/{_totalFlaps})");

        if (_folded.Count >= _totalFlaps && !_completed)
        {
            _completed = true;
            Debug.Log("[Frame1FoldPuzzle] all flaps folded — teddy fully 3D and grabbable");
            OnFullyFolded?.Invoke();
        }
    }

    IEnumerator FoldRoutine(FrameFlap flap)
    {
        Transform t = flap.transform;
        Quaternion start = t.localRotation;
        // Fold backward around the flap's hinge axis, plus a random crumple tilt
        Quaternion end = start
            * Quaternion.AngleAxis(foldAngle * flap.foldSign, flap.foldAxis)
            * Quaternion.Euler(Random.Range(-wobbleTilt, wobbleTilt), 0f, Random.Range(-wobbleTilt, wobbleTilt));

        float dur = Mathf.Max(0.05f, foldDuration);
        float e = 0f;
        while (e < dur)
        {
            e += Time.deltaTime;
            float k = e / dur;
            // ease-out with a small overshoot bounce
            float s = 1f - Mathf.Pow(1f - Mathf.Clamp01(k), 2.2f);
            t.localRotation = Quaternion.SlerpUnclamped(start, end, s * 1.06f - (k >= 1f ? 0.06f : 0f));
            yield return null;
        }
        t.localRotation = end;
    }

    void HideFoldVisuals()
    {
        if (foldRoot != null) foldRoot.SetActive(false);
        // the number badge is a sibling of the fold root — hide it too or it lingers over Frame 2
        var label = transform.Find("NumberLabel");
        if (label != null) label.gameObject.SetActive(false);
    }
}
