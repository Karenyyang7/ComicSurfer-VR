using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 6 snap zones in a horizontal row. Player places ComicFrames (3–8) in correct order.
/// Correct order: [3, 4, 5, 6, 7, 8] left to right.
/// </summary>
public class FrameOrderPuzzle : MonoBehaviour
{
    [Header("=== Snap Zones ===")]
    [Tooltip("6 child Transforms acting as snap positions (SnapZone_0 to SnapZone_5)")]
    public Transform[] snapZones;

    [Header("=== Settings ===")]
    public int[] correctOrder = new int[] { 3, 4, 5, 6, 7, 8 };
    public float snapRadius = 0.4f;
    public float wrongHighlightDuration = 1f;

    public event System.Action OnPuzzleSolved;

    // Maps each snap zone index → placed frame index (0 = empty)
    private int[] _placedFrames;
    private Dictionary<int, Vector3> _frameStartPositions = new Dictionary<int, Vector3>();
    private bool _solved = false;

    void Start()
    {
        _placedFrames = new int[snapZones.Length];

        // Record start positions of puzzle frames (3-8) for return-on-wrong
        if (ComicWorldManager.Instance != null && ComicWorldManager.Instance.frames != null)
        {
            foreach (var f in ComicWorldManager.Instance.frames)
            {
                if (f != null && f.frameIndex >= 3 && f.frameIndex <= 8)
                    _frameStartPositions[f.frameIndex] = f.transform.position;
            }
        }
    }

    void Update()
    {
        if (_solved) return;
        CheckSnapping();
    }

    void CheckSnapping()
    {
        if (ComicWorldManager.Instance == null || ComicWorldManager.Instance.frames == null) return;

        foreach (var frame in ComicWorldManager.Instance.frames)
        {
            if (frame == null || frame.frameIndex < 3 || frame.frameIndex > 8) continue;

            // Check if this frame is near a snap zone and not held
            var grab = frame.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grab != null && grab.isSelected) continue;

            for (int z = 0; z < snapZones.Length; z++)
            {
                if (snapZones[z] == null) continue;
                float dist = Vector3.Distance(frame.transform.position, snapZones[z].position);

                if (dist < snapRadius && _placedFrames[z] == 0)
                {
                    // Snap it
                    frame.transform.position = snapZones[z].position;
                    frame.transform.rotation = snapZones[z].rotation;
                    _placedFrames[z] = frame.frameIndex;
                    frame.NotifyPlaced();
                    Debug.Log($"[FrameOrderPuzzle] Frame {frame.frameIndex} snapped to zone {z}");

                    if (AllZonesFilled())
                        StartCoroutine(CheckOrder());
                    break;
                }
            }
        }
    }

    bool AllZonesFilled()
    {
        foreach (int v in _placedFrames)
            if (v == 0) return false;
        return true;
    }

    IEnumerator CheckOrder()
    {
        yield return new WaitForSeconds(0.3f);

        bool correct = true;
        for (int i = 0; i < _placedFrames.Length; i++)
        {
            if (_placedFrames[i] != correctOrder[i])
            {
                correct = false;
                break;
            }
        }

        if (correct)
        {
            _solved = true;
            Debug.Log("[FrameOrderPuzzle] CORRECT ORDER! Puzzle solved.");
            OnPuzzleSolved?.Invoke();
            if (ComicWorldManager.Instance != null)
                ComicWorldManager.Instance.OnPuzzleSolved();
        }
        else
        {
            Debug.Log("[FrameOrderPuzzle] Wrong order — returning frames.");
            yield return StartCoroutine(HighlightWrong());
            ReturnFrames();
        }
    }

    IEnumerator HighlightWrong()
    {
        // Flash wrong frames red
        var renderers = new List<Renderer>();
        if (ComicWorldManager.Instance?.frames != null)
        {
            foreach (var f in ComicWorldManager.Instance.frames)
            {
                if (f != null && f.frameIndex >= 3 && f.frameIndex <= 8)
                {
                    var r = f.GetComponentInChildren<Renderer>();
                    if (r != null) { renderers.Add(r); r.material.color = Color.red; }
                }
            }
        }

        yield return new WaitForSeconds(wrongHighlightDuration);

        foreach (var r in renderers)
            if (r != null) r.material.color = Color.white;
    }

    void ReturnFrames()
    {
        for (int i = 0; i < _placedFrames.Length; i++)
            _placedFrames[i] = 0;

        if (ComicWorldManager.Instance?.frames == null) return;

        foreach (var f in ComicWorldManager.Instance.frames)
        {
            if (f != null && f.frameIndex >= 3 && f.frameIndex <= 8 && _frameStartPositions.ContainsKey(f.frameIndex))
            {
                f.transform.position = _frameStartPositions[f.frameIndex];
                Debug.Log($"[FrameOrderPuzzle] Frame {f.frameIndex} returned to start.");
            }
        }
    }
}
