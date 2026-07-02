using System.Collections;
using System.IO;
using UnityEngine;

/// <summary>
/// DEV/TEST ONLY — not part of the game.
/// Spawned at runtime (via MCP execute_code) in play mode to film the thief sequence:
///   1. Creates its own camera aimed at the thief/table area.
///   2. Triggers ThiefSpawner.SpawnThief().
///   3. Renders a PNG frame every captureInterval seconds to outputDir for visual review.
/// Never wire this into a scene. Safe to leave in the project (does nothing unless created).
/// </summary>
public class ThiefCamRecorder : MonoBehaviour
{
    public string outputDir = "/tmp/thief_frames";
    public float captureInterval = 0.35f;
    public float recordSeconds = 16f;
    public int width = 640, height = 480;

    [Tooltip("Optional world-space camera position override; zero = auto from table")]
    public Vector3 camPosOverride = Vector3.zero;
    public Vector3 lookAtOverride = Vector3.zero;

    private Camera _cam;
    private RenderTexture _rt;
    private Texture2D _tex;

    IEnumerator Start()
    {
        var spawner = FindFirstObjectByType<ThiefSpawner>();
        if (spawner == null) { Debug.LogError("[ThiefCamRecorder] no ThiefSpawner"); yield break; }

        Directory.CreateDirectory(outputDir);

        Vector3 focus = lookAtOverride != Vector3.zero ? lookAtOverride
                       : (spawner.wpTableEdge != null ? spawner.wpTableEdge.position : spawner.transform.position);

        var camGO = new GameObject("ThiefRecorderCam");
        _cam = camGO.AddComponent<Camera>();
        // Slightly above and to the side, like a spectator standing at the table
        _cam.transform.position = camPosOverride != Vector3.zero ? camPosOverride
                                 : focus + new Vector3(-0.9f, 0.55f, -0.9f);
        _cam.transform.LookAt(focus + Vector3.up * 0.25f);
        _cam.nearClipPlane = 0.05f;
        _cam.fieldOfView = 55f;

        _rt = new RenderTexture(width, height, 24);
        _tex = new Texture2D(width, height, TextureFormat.RGB24, false);

        spawner.SpawnThief();

        float t = 0f;
        int i = 0;
        while (t < recordSeconds)
        {
            Capture(i++);
            yield return new WaitForSeconds(captureInterval);
            t += captureInterval;
        }
        Debug.Log($"[ThiefCamRecorder] DONE — {i} frames in {outputDir}");
    }

    void Capture(int idx)
    {
        _cam.targetTexture = _rt;
        _cam.Render();
        RenderTexture.active = _rt;
        _tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        _tex.Apply();
        RenderTexture.active = null;
        _cam.targetTexture = null;
        File.WriteAllBytes(Path.Combine(outputDir, $"frame_{idx:D3}.png"), _tex.EncodeToPNG());
    }

    void OnDestroy()
    {
        if (_rt != null) _rt.Release();
    }
}
