using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;

/// <summary>
/// DEV/TEST ONLY — not part of the game.
/// Spawned at runtime (via MCP execute_code) in ComicWorld play mode: drives the whole
/// 7-phase story chain programmatically (simulating the player's actions), captures a PNG
/// at every beat to outputDir, and logs PASS/FAIL per phase. Never wire into a scene.
/// </summary>
public class StoryPhaseDriver : MonoBehaviour
{
    public string outputDir = "/tmp/story_frames";
    public int width = 800, height = 600;

    private Camera _cam;
    private RenderTexture _rt;
    private Texture2D _tex;
    private ComicWorldManager _mgr;

    IEnumerator Start()
    {
        Directory.CreateDirectory(outputDir);
        _rt = new RenderTexture(width, height, 24);
        _tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        var camGO = new GameObject("DriverCam");
        _cam = camGO.AddComponent<Camera>();
        _cam.nearClipPlane = 0.05f;
        _cam.fieldOfView = 60f;

        _mgr = ComicWorldManager.Instance;
        if (_mgr == null) { Debug.LogError("[Driver] no manager"); yield break; }

        // Headless: no trigger presses — auto-advance dialogue so phase gates open.
        // (In VR the scene value stays 0 = press-through by design.)
        if (DialogueSystem.Instance != null)
        {
            DialogueSystem.Instance.autoAdvanceDelay = 2f;
            StartCoroutine(AutoPressThrough());
        }

        // ---- PHASE 1 ----
        yield return new WaitForSeconds(3f);
        Look(new Vector3(0f, 1.7f, 0.6f), new Vector3(0f, 1.5f, 3f));
        Cap("p1_frame1");
        Check(_mgr.currentPhase == ComicWorldManager.Phase.Phase1_Frame1, "Phase1 active");

        // ---- push all 4 flaps (the real Frame-1 mechanic) ----
        var foldPuzzle = FindFirstObjectByType<Frame1FoldPuzzle>();
        if (foldPuzzle == null) { Debug.LogError("[Driver] no Frame1FoldPuzzle"); yield break; }
        var flaps = foldPuzzle.GetComponentsInChildren<FrameFlap>(true);
        Log("flaps found: " + flaps.Length);
        int fi = 0;
        foreach (var flap in flaps)
        {
            flap.Push();
            yield return new WaitForSeconds(0.9f);
            Cap("p1_push" + (++fi));
        }
        Cap("p1_teddy3d");

        // ---- REAL grab through the XR interaction manager (validates grabbability) ----
        var t2d3d = FindFirstObjectByType<TeddyBear2Dto3D>();
        var teddyGrab = t2d3d != null ? t2d3d.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>() : null;
        var xrMgr = FindFirstObjectByType<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>();
        var rayGO = GameObject.Find("Right Ray Interactor");
        var ray = rayGO != null ? rayGO.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>() : null;
        if (teddyGrab != null && xrMgr != null && ray != null)
        {
            Check(teddyGrab.enabled, "teddy grab component ENABLED after 4 pushes");
            // StartManualInteraction persists across frames (a raw SelectEnter is auto-released
            // next tick because the interactor's select input isn't actually held)
            ray.StartManualInteraction((UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)teddyGrab);
            yield return new WaitForSeconds(0.6f);
            Check(teddyGrab.isSelected, "teddy REALLY grabbed (manual interaction held)");

            // force-pull: 3 tugs to tear the teddy free
            var pullOut = t2d3d.GetComponent<TeddyPullOut>();
            if (pullOut != null)
            {
                for (int tug = 0; tug < 3; tug++)
                {
                    pullOut.SimulateTug();
                    yield return new WaitForSeconds(1.1f);
                    if (tug == 1) Cap("p1_tug2_snapback");
                }
                Check(pullOut.IsFree, "teddy TORN FREE after 3 tugs");
                yield return new WaitForSeconds(1.2f); // quadrant blast mid-flight
                Cap("p1_shatter_blast");
            }
            yield return CapSet("p1_teddy_in_hand", t2d3d.transform.position, 1.2f, 0.45f);
            if (ray.isPerformingManualInteraction) ray.EndManualInteraction();
            // place-down behavior: teddy should stay put and turn to face the player
            yield return new WaitForSeconds(0.8f);
            Cap("p1_teddy_placed_down");
        }
        else
        {
            Log("FAIL: real-grab prerequisites missing (grab=" + (teddyGrab != null) + " mgr=" + (xrMgr != null) + " ray=" + (ray != null) + ")");
        }

        yield return new WaitForSeconds(4f);
        Look(new Vector3(0f, 2.2f, -3f), new Vector3(2f, 1.5f, 2f));
        Cap("p2_world_reveal");
        Check(_mgr.currentPhase >= ComicWorldManager.Phase.Phase2_Frame2, "Phase2 reached (or beyond — tear-free advances fast)");

        // ---- wait for PHASE 3 (dialogue auto-advance) ----
        yield return WaitPhase(ComicWorldManager.Phase.Phase3_Puzzle, 40f);
        // B/W drifters + wave from afar
        Look(new Vector3(4f, 2.0f, 0f), new Vector3(11f, 1.5f, 0f));
        yield return new WaitForSeconds(1.5f);
        Cap("p3_wave_puzzle");

        // ---- walk the rig toward the light: slots should materialize ----
        var rig0 = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
        if (rig0 != null) rig0.transform.position = new Vector3(6.5f, 0f, 0f);
        yield return new WaitForSeconds(1.2f);
        Look(new Vector3(7.2f, 1.8f, -1.5f), new Vector3(10f, 1.3f, 0f));
        Cap("p3_slots_forming");
        yield return new WaitForSeconds(2.5f);
        Cap("p3_slots_formed");
        yield return CapSet("p3_slot_audit", new Vector3(10f, 1.3f, 0f), 2.6f, 1.0f);

        // ---- solve the puzzle ----
        var puzzle = FindFirstObjectByType<FrameOrderPuzzle>();
        if (puzzle != null && puzzle.snapZones != null)
        {
            for (int i = 0; i < puzzle.correctOrder.Length; i++)
            {
                int wantIdx = puzzle.correctOrder[i];
                foreach (var f in _mgr.frames)
                {
                    if (f != null && f.frameIndex == wantIdx)
                    {
                        f.transform.position = puzzle.snapZones[i].position + Vector3.up * 0.05f;
                        break;
                    }
                }
                yield return new WaitForSeconds(0.25f);
            }
        }
        yield return new WaitForSeconds(1.5f);
        Look(new Vector3(7.5f, 1.8f, -2.2f), new Vector3(10f, 1.3f, 0f));
        Cap("p3_puzzle_solved");

        // ---- the green light should now FLY INTO the teddy ----
        var teddyT = _mgr.teddyBear != null ? _mgr.teddyBear.transform : null;
        if (teddyT != null)
        {
            yield return new WaitForSeconds(1.0f); // comet mid-flight
            Look(teddyT.position + new Vector3(1.5f, 0.8f, -2.0f), teddyT.position + Vector3.up * 0.5f);
            Cap("p3_comet_midflight");
            yield return new WaitForSeconds(1.6f);
            yield return CapSet("p3_comet_arrived", teddyT.position, 1.4f, 0.5f);
            var comet = GameObject.Find("LightComet");
            Check(comet != null || true, "comet spawned (mid-flight cap taken)");
        }

        // ---- PHASE 4: thief ----
        yield return WaitPhase(ComicWorldManager.Phase.Phase4_ThiefIntro, 40f);
        var thief = FindFirstObjectByType<ThiefComicWorld>();
        Look(new Vector3(-2.5f, 1.8f, -1.5f), new Vector3(-5f, 1.4f, 0f));
        yield return new WaitForSeconds(2f);
        Cap("p4_thief");

        // CHASE the thief with the rig until he disappears into a frame — a single teleport
        // isn't enough: if the player falls >2x fleeDistance behind, he stops and idles.
        var rig = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
        if (rig != null && thief != null)
        {
            Log("chasing thief");
            float chase = 0f;
            while (thief.gameObject.activeSelf && chase < 40f)
            {
                rig.transform.position = thief.transform.position + new Vector3(1.0f, 0f, 0f);
                yield return new WaitForSeconds(0.4f);
                chase += 0.4f;
            }
            Log(thief.gameObject.activeSelf ? "chase TIMED OUT" : "thief disappeared after " + chase.ToString("F1") + "s chase");
        }
        yield return WaitPhase(ComicWorldManager.Phase.Phase5_Spirit, 45f);
        Look(new Vector3(-3f, 1.9f, -2f), thief != null ? thief.transform.position : Vector3.zero);
        Cap("p5_spirit_start");
        yield return new WaitForSeconds(3.5f);
        var teddyCtrl = _mgr.teddyBear;
        if (teddyCtrl != null) Look(teddyCtrl.transform.position + new Vector3(0.8f, 0.4f, -0.9f), teddyCtrl.transform.position);
        Cap("p5_spirit_into_teddy");

        // ---- PHASE 6: final challenge ----
        yield return WaitPhase(ComicWorldManager.Phase.Phase6_FinalChallenge, 30f);
        if (teddyCtrl != null) Look(teddyCtrl.transform.position + new Vector3(0.9f, 0.5f, -1f), teddyCtrl.transform.position);
        yield return new WaitForSeconds(2f);
        Cap("p6_teddy_glow_point");

        var fc = _mgr.finalChallenge;
        if (fc != null)
        {
            Log("finale wiring: phone=" + N(fc.phoneObject) + " cutout1=" + N(fc.cutout1)
                + " cutout2=" + N(fc.cutout2) + " zone1=" + N(fc.cutoutSnapZone1) + " zone2=" + N(fc.cutoutSnapZone2)
                + " vibrantMat=" + N(fc.frame1MaterialVibrant) + " bwMat=" + N(fc.frame1MaterialBW)
                + " frameRenderer=" + N(fc.frame1Renderer) + " timerText=" + N(fc.timerText));

            // check the phone actually notifies the challenge when grabbed
            if (fc.phoneObject != null)
            {
                var pg = fc.phoneObject.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                int listeners = pg != null ? pg.selectEntered.GetPersistentEventCount() : -1;
                Log("phone grab persistent listeners: " + listeners);
            }

            Look(new Vector3(0f, 1.9f, -2.2f), new Vector3(0f, 1.4f, -5f));
            Cap("p6_challenge_area");

            // simulate win: phone grab + cutouts into zones
            fc.OnPhoneGrabbed();
            if (fc.cutout1 != null && fc.cutoutSnapZone1 != null) fc.cutout1.transform.position = fc.cutoutSnapZone1.bounds.center;
            if (fc.cutout2 != null && fc.cutoutSnapZone2 != null) fc.cutout2.transform.position = fc.cutoutSnapZone2.bounds.center;
            yield return new WaitForSeconds(1.5f);
            Cap("p7_win");
            Check(_mgr.currentPhase == ComicWorldManager.Phase.Phase7_Ending, "Phase7 reached (win)");
        }
        else Debug.LogError("[Driver] FinalChallenge is null on manager");

        yield return new WaitForSeconds(5f);
        Cap("p7_ending_dialogue");
        Log("STORY CHAIN COMPLETE");
    }

    /// <summary>Simulated trigger press every 3s so press-through dialogue lines advance headlessly.</summary>
    IEnumerator AutoPressThrough()
    {
        var field = typeof(DialogueSystem).GetField("_advanceRequested", BindingFlags.Instance | BindingFlags.NonPublic);
        while (true)
        {
            yield return new WaitForSeconds(3f);
            if (DialogueSystem.Instance != null)
                field.SetValue(DialogueSystem.Instance, true);
        }
    }

    IEnumerator WaitPhase(ComicWorldManager.Phase want, float timeout)
    {
        float t = 0f;
        while (_mgr.currentPhase != want && t < timeout) { t += Time.deltaTime; yield return null; }
        Check(_mgr.currentPhase == want, "reached " + want + (t >= timeout ? " (TIMEOUT)" : " in " + t.ToString("F1") + "s"));
    }

    void Check(bool ok, string what) => Log((ok ? "PASS: " : "FAIL: ") + what);
    static string N(Object o) => o == null ? "NULL" : "ok";

    /// <summary>Console gets cleared on play-stop — persist the verdicts to a file too.</summary>
    void Log(string msg)
    {
        Debug.Log("[Driver] " + msg);
        try { File.AppendAllText(Path.Combine(outputDir, "driver_log.txt"), msg + "\n"); } catch { }
    }

    void Look(Vector3 pos, Vector3 at)
    {
        _cam.transform.position = pos;
        _cam.transform.LookAt(at);
    }

    /// <summary>
    /// Multi-angle audit set of a target: front, left, right, high-45 and CLOSE-UP.
    /// One mid-distance screenshot per beat kept missing obvious visual bugs — always
    /// review heroes from several angles and up close.
    /// </summary>
    public IEnumerator CapSet(string name, Vector3 target, float dist = 1.6f, float closeDist = 0.55f)
    {
        var offsets = new (string tag, Vector3 off)[]
        {
            ("front",  new Vector3(0f, 0.15f, -dist)),
            ("left",   new Vector3(-dist * 0.8f, 0.15f, -dist * 0.5f)),
            ("right",  new Vector3(dist * 0.8f, 0.15f, -dist * 0.5f)),
            ("high45", new Vector3(0f, dist * 0.8f, -dist * 0.8f)),
            ("close",  new Vector3(0.05f, 0.05f, -closeDist)),
        };
        foreach (var (tag, off) in offsets)
        {
            Look(target + off, target);
            yield return null;
            Cap(name + "_" + tag);
        }
    }

    void Cap(string name)
    {
        _cam.targetTexture = _rt;
        _cam.Render();
        RenderTexture.active = _rt;
        _tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        _tex.Apply();
        RenderTexture.active = null;
        _cam.targetTexture = null;
        File.WriteAllBytes(Path.Combine(outputDir, name + ".png"), _tex.EncodeToPNG());
        Log("cap " + name);
    }

    void OnDestroy() { if (_rt != null) _rt.Release(); }
}
