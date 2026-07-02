using System.Collections;
using System.IO;
using UnityEngine;

/// <summary>
/// DEV/TEST ONLY — not part of the game.
/// Spawned at runtime (via MCP execute_code) in play mode: films what the PLAYER camera sees
/// while the TimeTunnelTransition plays (including across the scene load), one PNG every
/// captureInterval seconds. Survives the scene load via DontDestroyOnLoad.
/// </summary>
public class TransitionRecorder : MonoBehaviour
{
    public string outputDir = "/tmp/tunnel_frames";
    public float captureInterval = 0.3f;
    public float recordSeconds = 12f;
    public int width = 640, height = 480;
    public bool triggerTransition = true;
    public string targetScene = "ComicWorld";

    private RenderTexture _rt;
    private Texture2D _tex;

    IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        Directory.CreateDirectory(outputDir);
        _rt = new RenderTexture(width, height, 24);
        _tex = new Texture2D(width, height, TextureFormat.RGB24, false);

        if (triggerTransition)
        {
            if (TimeTunnelTransition.Instance == null)
            {
                Debug.LogError("[TransitionRecorder] no TimeTunnelTransition.Instance");
                yield break;
            }
            TimeTunnelTransition.Instance.PlayTransition(targetScene);
        }

        float t = 0f;
        int i = 0;
        while (t < recordSeconds)
        {
            var cam = Camera.main;
            if (cam != null) Capture(cam, i);
            i++;
            yield return new WaitForSeconds(captureInterval);
            t += captureInterval;
        }
        Debug.Log($"[TransitionRecorder] DONE — {i} frames in {outputDir}");
    }

    void Capture(Camera cam, int idx)
    {
        var prevTarget = cam.targetTexture;
        cam.targetTexture = _rt;
        cam.Render();
        RenderTexture.active = _rt;
        _tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        _tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = prevTarget;
        File.WriteAllBytes(Path.Combine(outputDir, $"frame_{idx:D3}.png"), _tex.EncodeToPNG());
    }

    void OnDestroy()
    {
        if (_rt != null) _rt.Release();
    }
}
