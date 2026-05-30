using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Singleton. DontDestroyOnLoad.
/// GameObject "TimeTunnelTransition" lives in Karen Room.
/// Contains a Canvas with 8 colored spinning Images + black blink/fade overlays.
/// </summary>
public class TimeTunnelTransition : MonoBehaviour
{
    public static TimeTunnelTransition Instance { get; private set; }

    [Header("=== Tunnel Images (8 colored spinning panels) ===")]
    public RectTransform[] tunnelImages;

    [Header("=== Blink / Fade ===")]
    public Image blinkOverlay;
    public Image fadeOverlay;

    [Header("=== Timing ===")]
    public float tunnelDuration = 8f;
    public float blinkDuration = 0.15f;
    public int blinkCount = 3;

    private bool _playing = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SetTunnelVisible(false);
        if (blinkOverlay != null) blinkOverlay.gameObject.SetActive(false);
        if (fadeOverlay != null)
        {
            fadeOverlay.gameObject.SetActive(false);
            fadeOverlay.color = new Color(0, 0, 0, 0);
        }
    }

    public void PlayTransition(string sceneName)
    {
        if (_playing) return;
        _playing = true;
        StartCoroutine(TransitionRoutine(sceneName));
    }

    IEnumerator TransitionRoutine(string sceneName)
    {
        SetTunnelVisible(true);

        float elapsed = 0f;
        while (elapsed < tunnelDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / tunnelDuration;

            for (int i = 0; i < tunnelImages.Length; i++)
            {
                if (tunnelImages[i] == null) continue;

                float speed = 60f + i * 30f;
                tunnelImages[i].Rotate(Vector3.forward, speed * Time.deltaTime);

                float scale = Mathf.Lerp(1f, 0.05f, t);
                tunnelImages[i].localScale = Vector3.one * scale;

                Vector2 startPos = GetStartPosition(i);
                tunnelImages[i].anchoredPosition = Vector2.Lerp(startPos, Vector2.zero, t * t);
            }

            yield return null;
        }

        SetTunnelVisible(false);

        // Rapid blinks before scene load
        if (blinkOverlay != null)
        {
            blinkOverlay.gameObject.SetActive(true);
            for (int b = 0; b < blinkCount; b++)
            {
                yield return StartCoroutine(FadeImage(blinkOverlay, 0f, 1f, blinkDuration));
                yield return StartCoroutine(FadeImage(blinkOverlay, 1f, 0f, blinkDuration));
            }
            blinkOverlay.gameObject.SetActive(false);
        }

        SceneManager.LoadScene(sceneName);

        // Fade out black overlay after load
        if (fadeOverlay != null)
        {
            fadeOverlay.gameObject.SetActive(true);
            fadeOverlay.color = new Color(0, 0, 0, 1f);
            yield return StartCoroutine(FadeImage(fadeOverlay, 1f, 0f, 1f));
            fadeOverlay.gameObject.SetActive(false);
        }

        _playing = false;
    }

    IEnumerator FadeImage(Image img, float from, float to, float duration)
    {
        float t = 0f;
        Color c = img.color;
        while (t < duration)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(from, to, t / duration);
            img.color = c;
            yield return null;
        }
        c.a = to;
        img.color = c;
    }

    Vector2 GetStartPosition(int index)
    {
        float radius = 400f;
        float angle = (index / (float)tunnelImages.Length) * Mathf.PI * 2f;
        return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
    }

    void SetTunnelVisible(bool visible)
    {
        foreach (var rt in tunnelImages)
            if (rt != null) rt.gameObject.SetActive(visible);
    }
}
