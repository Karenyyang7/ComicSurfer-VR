using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Displays comic-style instruction text in world space.
/// Creates a speech bubble effect that follows the player.
/// 
/// SETUP:
/// 1. Create an empty Game Object called "UIInstructions" 
/// 2. Add this script to it
/// 3. It will auto-create a world space canvas with styled text
/// 4. OR: create your own Canvas (World Space) with a Text component 
///    and drag it into the customCanvas field
/// </summary>
public class UIInstructions : MonoBehaviour
{
    [Header("=== Settings ===")]
    [Tooltip("Optional: drag your own World Space Canvas here")]
    public Canvas customCanvas;

    [Tooltip("How far in front of the player the text appears")]
    public float distanceFromPlayer = 2f;

    [Tooltip("Height above the player's eye level")]
    public float heightOffset = -0.3f;

    [Tooltip("How long messages stay visible (0 = until next message)")]
    public float autoHideTime = 0f;

    [Tooltip("Text font size")]
    public int fontSize = 32;

    [Header("=== Comic Style ===")]
    public Color textColor = Color.black;
    public Color backgroundColor = new Color(1f, 1f, 0.85f, 0.95f); // cream/comic paper

    // Internal
    private Canvas canvas;
    private Text messageText;
    private Image backgroundImage;
    private Transform playerCamera;
    private CanvasGroup canvasGroup;
    private Coroutine hideCoroutine;

    void Start()
    {
        // Find the player camera
        playerCamera = Camera.main?.transform;
        if (playerCamera == null)
        {
            Debug.LogError("UIInstructions: No main camera found!");
            return;
        }

        if (customCanvas != null)
        {
            canvas = customCanvas;
            messageText = canvas.GetComponentInChildren<Text>();
            backgroundImage = canvas.GetComponentInChildren<Image>();
        }
        else
        {
            CreateComicUI();
        }

        // Start hidden
        if (canvasGroup != null)
            canvasGroup.alpha = 0;
    }

    void CreateComicUI()
    {
        // Create world space canvas
        GameObject canvasObj = new GameObject("InstructionCanvas");
        canvasObj.transform.SetParent(transform);
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;

        canvasGroup = canvasObj.AddComponent<CanvasGroup>();

        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(600, 120);
        canvasRect.localScale = Vector3.one * 0.002f; // scale down for world space

        // Add background (speech bubble style)
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObj.transform, false);
        backgroundImage = bgObj.AddComponent<Image>();
        backgroundImage.color = backgroundColor;

        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = new Vector2(20, 20);
        bgRect.offsetMin = new Vector2(-10, -10);
        bgRect.offsetMax = new Vector2(10, 10);

        // Add outline for comic effect
        Outline outline = bgObj.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(3, 3);

        // Add text
        GameObject textObj = new GameObject("MessageText");
        textObj.transform.SetParent(canvasObj.transform, false);
        messageText = textObj.AddComponent<Text>();
        messageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        messageText.fontSize = fontSize;
        messageText.color = textColor;
        messageText.alignment = TextAnchor.MiddleCenter;
        messageText.fontStyle = FontStyle.Bold;
        messageText.horizontalOverflow = HorizontalWrapMode.Wrap;

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.offsetMin = new Vector2(10, 5);
        textRect.offsetMax = new Vector2(-10, -5);
    }

    void LateUpdate()
    {
        if (canvas == null || playerCamera == null) return;

        // Position the canvas in front of the player
        Vector3 forward = playerCamera.forward;
        forward.y = 0;
        forward.Normalize();

        Vector3 targetPos = playerCamera.position
            + forward * distanceFromPlayer
            + Vector3.up * heightOffset;

        canvas.transform.position = Vector3.Lerp(
            canvas.transform.position,
            targetPos,
            Time.deltaTime * 5f
        );

        // Always face the player
        canvas.transform.LookAt(playerCamera);
        canvas.transform.Rotate(0, 180, 0); // flip so text isnt mirrored
    }

    /// <summary>
    /// Show a message to the player
    /// </summary>
    public void ShowMessage(string message)
    {
        if (messageText == null) return;

        messageText.text = message;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1;
        }

        // Auto-hide if configured
        if (autoHideTime > 0)
        {
            if (hideCoroutine != null)
                StopCoroutine(hideCoroutine);
            hideCoroutine = StartCoroutine(HideAfterDelay(autoHideTime));
        }
    }

    /// <summary>
    /// Hide the message
    /// </summary>
    public void HideMessage()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0;
        }
    }

    IEnumerator HideAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Fade out
        float elapsed = 0;
        float fadeDuration = 0.5f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            if (canvasGroup != null)
                canvasGroup.alpha = 1 - (elapsed / fadeDuration);
            yield return null;
        }

        HideMessage();
    }
}
