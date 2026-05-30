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
