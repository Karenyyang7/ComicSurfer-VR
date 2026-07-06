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

    [Header("=== Finale Frame ===")]
    [Tooltip("The finale comic frame (Frame1_Final). Activated + moved to this object's position when the challenge starts.")]
    public GameObject finaleFrame;

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
    private bool _phoneWired = false;
    private Transform _timerBillboard;

    public void StartChallenge()
    {
        // The manager may call this while our GameObject is inactive (hidden until Phase 6)
        // — coroutines and Update need it active.
        gameObject.SetActive(true);

        _timeRemaining = challengeDuration;
        _running = true;
        _phoneGrabbed = false;
        _cutout1Placed = false;
        _cutout2Placed = false;
        _finished = false;

        if (phoneObject != null) phoneObject.SetActive(true);
        if (cutout1 != null) cutout1.SetActive(true);
        if (cutout2 != null) cutout2.SetActive(true);

        // The finale frame appears here. Frames show their art toward -Z at identity rotation
        // (like Frame1 at z=+3 facing the origin), so aim -Z at the world origin.
        if (finaleFrame != null)
        {
            finaleFrame.transform.position = transform.position;
            Vector3 toOrigin = Vector3.zero - transform.position;
            toOrigin.y = 0f;
            if (toOrigin.sqrMagnitude > 0.001f)
                finaleFrame.transform.rotation = Quaternion.LookRotation(-toOrigin.normalized);
            finaleFrame.SetActive(true);
        }

        // Nothing in the scene called OnPhoneGrabbed — wire the grab event at runtime.
        if (!_phoneWired && phoneObject != null)
        {
            var grab = phoneObject.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grab != null)
            {
                grab.selectEntered.AddListener(_ => OnPhoneGrabbed());
                _phoneWired = true;
            }
        }

        if (timerText == null) BuildTimerBillboard();

        Debug.Log("[FinalChallenge] Challenge started — 90 seconds!");
        StartCoroutine(TimerRoutine());
    }

    /// <summary>World-space countdown above the challenge area, always facing the player.</summary>
    void BuildTimerBillboard()
    {
        var go = new GameObject("TimerBillboard");
        go.transform.position = transform.position + Vector3.up * 1.4f;
        var tmp = go.AddComponent<TMPro.TextMeshPro>();
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.fontSize = 6f;
        tmp.color = new Color(1f, 0.9f, 0.4f);
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.rectTransform.sizeDelta = new Vector2(2f, 0.6f);
        _timerTmp = tmp; // 3D TextMeshPro (timerText stays null — that field is the UGUI variant)
        _timerBillboard = go.transform;
    }

    private TMPro.TextMeshPro _timerTmp;
    private int _lastTickSecond = -1;

    IEnumerator TimerRoutine()
    {
        while (_timeRemaining > 0f && _running)
        {
            _timeRemaining -= Time.deltaTime;

            int whole = Mathf.CeilToInt(_timeRemaining);
            if (whole != _lastTickSecond && whole > 0)
            {
                _lastTickSecond = whole;
                SfxPlayer.Play2D(whole <= 15 ? "timer_urgent" : "timer_tick", whole <= 15 ? 0.7f : 0.35f);
            }
            string display = whole.ToString();
            if (timerText != null) timerText.text = display;
            if (_timerTmp != null) _timerTmp.text = display;

            // billboard the countdown toward the player
            if (_timerBillboard != null && Camera.main != null)
            {
                Vector3 look = _timerBillboard.position - Camera.main.transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.001f)
                    _timerBillboard.rotation = Quaternion.LookRotation(look.normalized);
            }

            yield return null;
        }
        if (_timerTmp != null) _timerTmp.text = "";

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
        // Snap the cutout to the zone center so it visibly clicks into place
        var cutout = index == 1 ? cutout1 : cutout2;
        var zone = index == 1 ? cutoutSnapZone1 : cutoutSnapZone2;
        if (cutout != null && zone != null)
        {
            cutout.transform.position = zone.bounds.center;
            cutout.transform.rotation = zone.transform.rotation;
            var rb = cutout.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
        }

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
        SfxPlayer.Play2D("win_fanfare");

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
        SfxPlayer.Play2D("lose_sting");

        if (frame1Renderer != null && frame1MaterialBW != null)
            frame1Renderer.material = frame1MaterialBW;

        OnLose?.Invoke();
        if (ComicWorldManager.Instance != null)
            ComicWorldManager.Instance.StartPhase7(false);
    }
}
