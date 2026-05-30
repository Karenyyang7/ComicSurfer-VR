using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// 90-second final challenge: grab phone, place two cutouts into Frame 1.
/// Win → swap to vibrant material. Lose → swap to black and white.
/// </summary>
public class FinalChallenge : MonoBehaviour
{
    [Header("=== Objectives ===")]
    public GameObject phoneObject;
    public GameObject cutout1;
    public GameObject cutout2;

    [Header("=== Snap Zones ===")]
    public Collider cutoutSnapZone1;
    public Collider cutoutSnapZone2;

    [Header("=== Frame 1 Materials ===")]
    public Material frame1MaterialVibrant;
    public Material frame1MaterialBW;
    public Renderer frame1Renderer;

    [Header("=== UI ===")]
    [Tooltip("Optional world-space TextMeshPro timer display")]
    public TextMeshProUGUI timerText;

    [Header("=== Settings ===")]
    public float challengeDuration = 90f;

    public event System.Action OnWin;
    public event System.Action OnLose;

    private float _timeRemaining;
    private bool _running = false;
    private bool _phoneGrabbed = false;
    private bool _cutout1Placed = false;
    private bool _cutout2Placed = false;
    private bool _finished = false;

    public void StartChallenge()
    {
        _timeRemaining = challengeDuration;
        _running = true;
        _phoneGrabbed = false;
        _cutout1Placed = false;
        _cutout2Placed = false;
        _finished = false;

        if (phoneObject != null) phoneObject.SetActive(true);
        if (cutout1 != null) cutout1.SetActive(true);
        if (cutout2 != null) cutout2.SetActive(true);

        Debug.Log("[FinalChallenge] Challenge started — 90 seconds!");
        StartCoroutine(TimerRoutine());
    }

    IEnumerator TimerRoutine()
    {
        while (_timeRemaining > 0f && _running)
        {
            _timeRemaining -= Time.deltaTime;

            if (timerText != null)
                timerText.text = Mathf.CeilToInt(_timeRemaining).ToString();

            yield return null;
        }

        if (_running && !_finished)
            TriggerLose();
    }

    void Update()
    {
        if (!_running || _finished) return;
        CheckCutoutSnapping();
    }

    void CheckCutoutSnapping()
    {
        // Check cutout 1
        if (!_cutout1Placed && cutout1 != null && cutoutSnapZone1 != null)
        {
            var grab = cutout1.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grab != null && !grab.isSelected)
            {
                if (cutoutSnapZone1.bounds.Contains(cutout1.transform.position))
                    OnCutoutPlaced(1);
            }
        }

        // Check cutout 2
        if (!_cutout2Placed && cutout2 != null && cutoutSnapZone2 != null)
        {
            var grab = cutout2.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grab != null && !grab.isSelected)
            {
                if (cutoutSnapZone2.bounds.Contains(cutout2.transform.position))
                    OnCutoutPlaced(2);
            }
        }
    }

    public void OnPhoneGrabbed()
    {
        _phoneGrabbed = true;
        Debug.Log("[FinalChallenge] Phone grabbed!");
        CheckWinCondition();
    }

    public void OnCutoutPlaced(int index)
    {
        if (index == 1) { _cutout1Placed = true; Debug.Log("[FinalChallenge] Cutout 1 placed!"); }
        if (index == 2) { _cutout2Placed = true; Debug.Log("[FinalChallenge] Cutout 2 placed!"); }
        CheckWinCondition();
    }

    public void CheckWinCondition()
    {
        if (_phoneGrabbed && _cutout1Placed && _cutout2Placed)
            TriggerWin();
    }

    void TriggerWin()
    {
        if (_finished) return;
        _finished = true;
        _running = false;

        Debug.Log("[FinalChallenge] WIN!");

        if (frame1Renderer != null && frame1MaterialVibrant != null)
            frame1Renderer.material = frame1MaterialVibrant;

        OnWin?.Invoke();
        if (ComicWorldManager.Instance != null)
            ComicWorldManager.Instance.StartPhase7(true);
    }

    void TriggerLose()
    {
        if (_finished) return;
        _finished = true;
        _running = false;

        Debug.Log("[FinalChallenge] LOSE — time ran out.");

        if (frame1Renderer != null && frame1MaterialBW != null)
            frame1Renderer.material = frame1MaterialBW;

        OnLose?.Invoke();
        if (ComicWorldManager.Instance != null)
            ComicWorldManager.Instance.StartPhase7(false);
    }
}
