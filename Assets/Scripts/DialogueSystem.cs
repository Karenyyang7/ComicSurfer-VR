using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// World-space dialogue UI. Singleton.
/// Call ShowDialogue(string[]) to queue lines.
/// Player presses any trigger to advance.
/// </summary>
public class DialogueSystem : MonoBehaviour
{
    public static DialogueSystem Instance { get; private set; }

    [Header("=== UI References ===")]
    public Canvas dialogueCanvas;
    public Image backgroundPanel;
    public TextMeshProUGUI dialogueText;

    [Header("=== Settings ===")]
    [Tooltip("Seconds per character while typing")]
    public float typeSpeed = 0.04f;
    [Tooltip("Auto-advance after this many seconds if no input. 0 = wait forever for input.")]
    public float autoAdvanceDelay = 4f;

    [Header("=== Input ===")]
    public InputActionReference advanceAction;

    public event System.Action OnDialogueComplete;

    private string[] _lines;
    private int _lineIndex = 0;
    private bool _isShowing = false;
    private bool _advanceRequested = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (dialogueCanvas != null)
            dialogueCanvas.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        if (advanceAction != null)
            advanceAction.action.performed += OnAdvance;
    }

    void OnDisable()
    {
        if (advanceAction != null)
            advanceAction.action.performed -= OnAdvance;
    }

    void OnAdvance(InputAction.CallbackContext ctx)
    {
        _advanceRequested = true;
    }

    // Also support button press fallback via keyboard space for editor testing
    void Update()
    {
        if (_isShowing && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            _advanceRequested = true;
    }

    [Header("=== Follow Player ===")]
    [Tooltip("Dialogue floats this many meters in front of the player's view")]
    public float followDistance = 2.2f;
    [Tooltip("Vertical offset from eye height (negative = below eye line)")]
    public float followHeightOffset = -0.35f;
    [Tooltip("How quickly the panel catches up to head movement (higher = snappier)")]
    public float followLerp = 4f;

    // The comic world is ~20m across — a world-pinned dialogue is unreadable once the
    // player walks off. Softly follow the camera instead (lazy lerp, no head-lock nausea).
    void LateUpdate()
    {
        if (!_isShowing || dialogueCanvas == null) return;
        var cam = Camera.main;
        if (cam == null) return;

        Vector3 fwd = cam.transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) return;
        fwd.Normalize();

        Vector3 target = cam.transform.position + fwd * followDistance + Vector3.up * followHeightOffset;
        var t = dialogueCanvas.transform;
        t.position = Vector3.Lerp(t.position, target, Time.deltaTime * followLerp);
        Vector3 look = t.position - cam.transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.001f)
            t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(look.normalized), Time.deltaTime * followLerp);
    }

    public void ShowDialogue(string[] lines)
    {
        if (lines == null || lines.Length == 0) return;
        if (_isShowing) StopAllCoroutines();

        _lines = lines;
        _lineIndex = 0;
        _isShowing = true;
        _advanceRequested = false;

        if (dialogueCanvas != null)
            dialogueCanvas.gameObject.SetActive(true);

        StartCoroutine(RunDialogue());
    }

    IEnumerator RunDialogue()
    {
        for (_lineIndex = 0; _lineIndex < _lines.Length; _lineIndex++)
        {
            yield return StartCoroutine(TypeLine(_lines[_lineIndex]));

            _advanceRequested = false;

            if (autoAdvanceDelay > 0f)
                yield return new WaitForSeconds(autoAdvanceDelay);
            else
                yield return new WaitUntil(() => _advanceRequested);
        }

        HideDialogue();
        OnDialogueComplete?.Invoke();
    }

    IEnumerator TypeLine(string line)
    {
        if (dialogueText == null) yield break;
        dialogueText.text = "";

        foreach (char c in line)
        {
            dialogueText.text += c;
            // If player pressed advance, skip typing
            if (_advanceRequested)
            {
                dialogueText.text = line;
                _advanceRequested = false;
                break;
            }
            yield return new WaitForSeconds(typeSpeed);
        }

    }

    public void HideDialogue()
    {
        _isShowing = false;
        StopAllCoroutines();
        if (dialogueCanvas != null)
            dialogueCanvas.gameObject.SetActive(false);
    }
}
