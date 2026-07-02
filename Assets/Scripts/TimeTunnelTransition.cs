using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// VR "time tunnel" scene transition. Singleton, survives the scene load (DontDestroyOnLoad).
///
/// PlayTransition(sceneName):
///   1. TUNNEL   — 3D streak geometry + URP particles stream PAST the camera (front → behind)
///                 to convey time travel. Built procedurally; uses a URP/Particles/Unlit
///                 material so nothing renders magenta in URP.
///   2. FADE     — full-FOV fade to black. Uses a WORLD-SPACE quad parented in front of the
///                 camera (NOT a Screen-Space canvas — that does not render to the HMD in VR),
///                 so the black truly covers both eyes.
///   3. LOAD     — once fully black, SceneManager.LoadScene(...) as a single load.
///   4. BLINK    — eyelid-style alpha 1→0→1, twice, after the load: "blink and you're there".
///   5. FADE IN  — alpha 1→0 so the player opens their eyes in the new world.
///
/// Everything is generated at runtime, so the only scene wiring needed is:
///   • place this component on one GameObject in Karen Room (build index 0), and
///   • (optional but recommended) assign <see cref="tunnelMaterial"/> to a project material
///     using shader "Universal Render Pipeline/Particles/Unlit" so the shader is guaranteed
///     to ship in the build.
///
/// The overlay rig re-follows Camera.main every LateUpdate, so the fade covers the FOV in
/// whichever scene is currently active (Karen Room before the load, ComicWorld after).
/// </summary>
public class TimeTunnelTransition : MonoBehaviour
{
    public static TimeTunnelTransition Instance { get; private set; }

    [Header("=== Timing (tunable) ===")]
    [Tooltip("Seconds the time-tunnel streams past before it fades out")]
    public float tunnelDuration = 2.2f;

    [Tooltip("Seconds for fade-to-black AND fade-from-black")]
    public float fadeDuration = 0.6f;

    [Tooltip("Seconds for ONE blink (alpha 1 -> 0 -> 1)")]
    public float blinkDuration = 0.15f;

    [Tooltip("How many eyelid blinks after the scene loads")]
    public int blinkCount = 2;

    [Header("=== Overlay placement (VR) ===")]
    [Tooltip("Distance (m) in front of the camera for the fade/blink quad. Must be > camera near clip (~0.01).")]
    public float overlayDistance = 0.5f;

    [Tooltip("World size (m) of the fade quad — large enough to cover the whole FOV in both eyes.")]
    public float overlaySize = 12f;

    [Tooltip("Material for the fade/blink quad. Assign Assets/Materials/VRFadeBlack.mat (shader " +
             "'UI/NoZTest') so the black IGNORES depth — nothing (hands, controllers, the book, " +
             "walls near the camera) can render in front of it. Falls back to Shader.Find at runtime.")]
    public Material fadeMaterial;

    [Header("=== Tunnel look ===")]
    [Tooltip("URP material for tunnel streaks + particles. Assign a material using " +
             "'Universal Render Pipeline/Particles/Unlit'. If left null a runtime fallback is used, " +
             "but assigning the asset is what guarantees the shader ships in the build.")]
    public Material tunnelMaterial;

    [Tooltip("Black backdrop material (shader 'Custom/TunnelBackdrop'). Assign " +
             "Assets/Materials/TunnelBackdrop.mat so the tunnel streams against a black VOID " +
             "instead of the room. Falls back to Shader.Find at runtime.")]
    public Material backdropMaterial;

    [Tooltip("Number of streak bars in the tunnel geometry")]
    public int streakCount = 28;

    [Tooltip("Tunnel radius (m) — bars/particles stream past at roughly this distance from the view axis")]
    public float tunnelRadius = 2.6f;

    [Tooltip("How fast the tunnel streaks fly past (m/s)")]
    public float tunnelSpeed = 14f;

    [Tooltip("Streaks/particles cycle through these colors for a magical feel")]
    public Color[] tunnelColors = new Color[]
    {
        new Color(0.45f, 0.82f, 1f),  // cyan-blue
        new Color(0.80f, 0.45f, 1f),  // violet
        new Color(1f, 0.90f, 0.40f),  // gold
        new Color(0.45f, 1f, 0.85f),  // teal
        Color.white
    };

    // ---------------------------------------------------------------------
    // SFX HOOKS — assign clips later in the Inspector. Calls below are guarded
    // (null clip == no-op) so the transition works with or without audio now.
    // ---------------------------------------------------------------------
    [Header("=== SFX hooks (optional — assign clips later) ===")]
    [Tooltip("Looping whoosh while the tunnel streams")]
    public AudioClip whooshClip;

    [Tooltip("Short blink / eyelid sound")]
    public AudioClip blinkClip;

    [Tooltip("Arrival sting as the player opens their eyes in the new world")]
    public AudioClip arrivalClip;

    // ---- camera takeover (pure-black void) ----
    // During the tunnel the camera is switched to render ONLY the overlay layer against
    // solid black. The backdrop quad alone could not guarantee a black void: transparent
    // room geometry (e.g. window glass, queue 3000) draws AFTER the backdrop (2400) and
    // punched through it, as did world-space UI. Culling everything but the overlay layer
    // is airtight regardless of other objects' shaders/queues.
    private int _overlayLayer = -1;
    private CameraClearFlags _savedClear;
    private Color _savedBg;
    private int _savedMask;
    private Camera _takeoverCam;       // camera whose settings we saved

    // ---- runtime-built overlay ----
    private Transform _rig;            // root that follows the camera every frame
    private Transform _tunnelRoot;     // holds streak geometry + particles
    private ParticleSystem _particles;
    private Transform[] _streaks;
    private RawImage _fadeImage;       // full-FOV black quad (world-space canvas)
    private Canvas _fadeCanvas;        // world-space canvas holding the fade quad
    private AudioSource _sfxLoop;      // whoosh loop
    private AudioSource _sfxOneShot;   // blink / arrival
    private Camera _cam;

    private bool _playing = false;
    private bool _active = false;        // when true, the rig tracks the camera each frame
    private bool _tunnelActive = false;  // when true, streaks animate each frame

    private const float SpawnFrontZ = 20f;   // streaks (re)spawn this far ahead
    private const float RecycleBackZ = -4f;   // ...and recycle once they pass this far behind

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _overlayLayer = FindUnusedLayer();
        BuildOverlay();
        if (_overlayLayer >= 0) SetLayerRecursively(_rig.gameObject, _overlayLayer);
        SetOverlayActive(false);
    }

    /// <summary>Highest unnamed user layer (31..8) — unnamed layers are valid and safe to use.</summary>
    static int FindUnusedLayer()
    {
        for (int i = 31; i >= 8; i--)
            if (string.IsNullOrEmpty(LayerMask.LayerToName(i)))
                return i;
        return -1; // all layers named/in use — fall back to backdrop-only behavior
    }

    static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    void BeginCameraTakeover()
    {
        if (_overlayLayer < 0 || _cam == null) return;
        _takeoverCam = _cam;
        _savedClear = _cam.clearFlags;
        _savedBg = _cam.backgroundColor;
        _savedMask = _cam.cullingMask;
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = Color.black;
        _cam.cullingMask = 1 << _overlayLayer;
    }

    void EndCameraTakeover()
    {
        if (_takeoverCam == null) return;
        _takeoverCam.clearFlags = _savedClear;
        _takeoverCam.backgroundColor = _savedBg;
        _takeoverCam.cullingMask = _savedMask;
        _takeoverCam = null;
    }

    /// <summary>The fade quad lives on the overlay layer — make sure the (new) camera renders it.</summary>
    void EnsureOverlayVisibleTo(Camera cam)
    {
        if (_overlayLayer >= 0 && cam != null)
            cam.cullingMask |= 1 << _overlayLayer;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public void PlayTransition(string sceneName)
    {
        if (_playing) return;
        _playing = true;
        StartCoroutine(Run(sceneName, -1));
    }

    public void PlayTransition(int buildIndex)
    {
        if (_playing) return;
        _playing = true;
        StartCoroutine(Run(null, buildIndex));
    }

    // -------------------------------------------------------------------------
    // Build the procedural overlay (runs once, in Awake)
    // -------------------------------------------------------------------------

    void BuildOverlay()
    {
        Material mat = ResolveTunnelMaterial();

        var rigGO = new GameObject("TunnelRig");
        rigGO.transform.SetParent(transform, false);
        _rig = rigGO.transform;

        // --- Tunnel geometry (streak bars) + particles ---
        var tunGO = new GameObject("Tunnel");
        tunGO.transform.SetParent(_rig, false);
        _tunnelRoot = tunGO.transform;

        // Black backdrop (far, ZTest-Always opaque quad) so the tunnel streams against a black
        // VOID instead of the room. Paints over the close room geometry and resets depth so the
        // transparent streaks/particles still render in front of it.
        BuildBackdrop();

        _streaks = new Transform[Mathf.Max(0, streakCount)];
        for (int i = 0; i < _streaks.Length; i++)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "Streak" + i;
            var col = bar.GetComponent<Collider>();
            if (col != null) Destroy(col);

            bar.transform.SetParent(_tunnelRoot, false);

            var r = bar.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            // Per-bar color without instancing the material
            Color c = tunnelColors.Length > 0 ? tunnelColors[i % tunnelColors.Length] : Color.white;
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_BaseColor", c);
            mpb.SetColor("_Color", c);
            mpb.SetColor("_TintColor", c);
            r.SetPropertyBlock(mpb);

            ResetStreak(bar.transform, true);
            _streaks[i] = bar.transform;
        }

        // --- URP particle system streaming toward / past the camera ---
        var psGO = new GameObject("TunnelParticles");
        psGO.transform.SetParent(_rig, false);
        psGO.transform.localPosition = new Vector3(0f, 0f, SpawnFrontZ * 0.9f);
        psGO.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // emit back toward the camera
        _particles = psGO.AddComponent<ParticleSystem>();
        ConfigureParticles(_particles, mat);

        _tunnelRoot.gameObject.SetActive(false);

        // --- Full-FOV fade/blink quad (world-space canvas in front of the camera) ---
        var canvasGO = new GameObject("FadeCanvas");
        canvasGO.transform.SetParent(_rig, false);
        canvasGO.transform.localPosition = new Vector3(0f, 0f, overlayDistance);
        canvasGO.transform.localRotation = Quaternion.identity;
        canvasGO.transform.localScale = Vector3.one;

        _fadeCanvas = canvasGO.AddComponent<Canvas>();
        _fadeCanvas.renderMode = RenderMode.WorldSpace;
        _fadeCanvas.sortingOrder = 32760; // draw on top of everything else
        var crt = _fadeCanvas.GetComponent<RectTransform>();
        crt.sizeDelta = new Vector2(overlaySize, overlaySize);

        var imgGO = new GameObject("FadeImage");
        imgGO.transform.SetParent(canvasGO.transform, false);
        _fadeImage = imgGO.AddComponent<RawImage>(); // RawImage fills solid even with no texture
        _fadeImage.color = new Color(0f, 0f, 0f, 0f);
        _fadeImage.raycastTarget = false;

        // CRITICAL for VR: use a depth-ignoring (ZTest Off) UI material so nothing — the player's
        // own hands/controllers, the open_book mesh, or a wall within overlayDistance — can render
        // in FRONT of the black-out. A default world-space UI material is ZTest LEqual and would let
        // near geometry punch through the "fully black" screen.
        Material fm = ResolveFadeMaterial();
        if (fm != null) _fadeImage.material = fm;
        var irt = _fadeImage.rectTransform;
        irt.anchorMin = Vector2.zero;
        irt.anchorMax = Vector2.one;
        irt.offsetMin = Vector2.zero;
        irt.offsetMax = Vector2.zero;

        // --- Audio sources for the SFX hooks ---
        _sfxLoop = gameObject.AddComponent<AudioSource>();
        _sfxLoop.playOnAwake = false;
        _sfxLoop.loop = true;
        _sfxLoop.spatialBlend = 0f;

        _sfxOneShot = gameObject.AddComponent<AudioSource>();
        _sfxOneShot.playOnAwake = false;
        _sfxOneShot.loop = false;
        _sfxOneShot.spatialBlend = 0f;
    }

    Material ResolveTunnelMaterial()
    {
        if (tunnelMaterial != null) return tunnelMaterial;

        // Fallback only — assigning the asset in the Inspector is the build-safe path.
        Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
        if (s == null) s = Shader.Find("Sprites/Default");
        var m = new Material(s != null ? s : Shader.Find("Unlit/Color"));
        m.name = "TimeTunnel_Streak (runtime fallback)";
        return m;
    }

    void BuildBackdrop()
    {
        Material bm = backdropMaterial;
        if (bm == null)
        {
            Shader s = Shader.Find("Custom/TunnelBackdrop");
            if (s == null) return; // no backdrop available — tunnel still plays (over the room)
            bm = new Material(s);
            bm.name = "TunnelBackdrop (runtime fallback)";
        }

        var backGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        backGO.name = "TunnelBackdrop";
        var col = backGO.GetComponent<Collider>();
        if (col != null) Destroy(col);

        backGO.transform.SetParent(_tunnelRoot, false);
        backGO.transform.localPosition = new Vector3(0f, 0f, 45f); // far ahead, beyond all streaks
        backGO.transform.localRotation = Quaternion.identity;
        backGO.transform.localScale = new Vector3(400f, 400f, 1f); // huge — covers the whole FOV

        var r = backGO.GetComponent<MeshRenderer>();
        r.sharedMaterial = bm;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    Material ResolveFadeMaterial()
    {
        if (fadeMaterial != null) return fadeMaterial;

        // Fallback only — assigning Assets/Materials/VRFadeBlack.mat in the Inspector is the
        // build-safe path. "UI/NoZTest" ships with the XR Interaction Toolkit Starter Assets.
        Shader s = Shader.Find("UI/NoZTest");
        if (s == null) return null; // leave the default UI material (depth-tested) as a last resort
        var m = new Material(s);
        m.name = "VRFadeBlack (runtime fallback)";
        return m;
    }

    void ConfigureParticles(ParticleSystem ps, Material mat)
    {
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.startLifetime = 1.7f;
        main.startSpeed = tunnelSpeed;
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.16f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local; // stream relative to the head
        main.maxParticles = 600;
        var grad = new ParticleSystem.MinMaxGradient(
            tunnelColors.Length > 0 ? tunnelColors[0] : new Color(0.5f, 0.85f, 1f),
            Color.white);
        grad.mode = ParticleSystemGradientMode.TwoColors;
        main.startColor = grad;

        var emission = ps.emission;
        emission.rateOverTime = 130f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 6f;
        shape.radius = 0.2f;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = mat;
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.08f;
        renderer.lengthScale = 2.2f;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    // -------------------------------------------------------------------------
    // Transition coroutine
    // -------------------------------------------------------------------------

    IEnumerator Run(string sceneName, int buildIndex)
    {
        _cam = Camera.main;
        _active = true;
        SetOverlayActive(true);
        SnapToCamera();

        // Pure black void: camera renders ONLY the tunnel/fade overlay layer from the very
        // first tunnel frame — the room (opaque, transparent, UI, hands) fully disappears.
        BeginCameraTakeover();

        // ---- PHASE 1: time tunnel ----
        if (_tunnelRoot != null) _tunnelRoot.gameObject.SetActive(true);
        _tunnelActive = true;
        if (_particles != null) _particles.Play();

        // SFX HOOK: start the looping whoosh/tunnel sound
        if (whooshClip != null) { _sfxLoop.clip = whooshClip; _sfxLoop.Play(); }

        float t = 0f;
        while (t < tunnelDuration)
        {
            t += Time.deltaTime;
            yield return null;
        }

        // ---- PHASE 2: fade to black (tunnel keeps streaming underneath) ----
        yield return Fade(0f, 1f, fadeDuration);

        // Tunnel finished — stop streaming + whoosh
        _tunnelActive = false;
        if (_particles != null) _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (_tunnelRoot != null) _tunnelRoot.gameObject.SetActive(false);
        if (_sfxLoop != null && _sfxLoop.isPlaying) _sfxLoop.Stop();

        // Screen is fully black — release the old camera before it's destroyed by the load.
        EndCameraTakeover();

        // ---- PHASE 3: load the new scene while fully black ----
        if (!string.IsNullOrEmpty(sceneName)) SceneManager.LoadScene(sceneName);
        else SceneManager.LoadScene(buildIndex);

        // Wait until the new scene's camera resolves before re-anchoring the overlay, so the
        // black quad never stays anchored to the destroyed old-scene camera for a frame (which
        // could flash the new scene). Bounded so we can never hang.
        yield return null;          // let the new scene wake up + its camera appear
        float camWait = 0f;
        while (Camera.main == null && camWait < 2f)
        {
            camWait += Time.unscaledDeltaTime;
            yield return null;
        }
        _cam = Camera.main;         // re-acquire the camera in the loaded scene
        EnsureOverlayVisibleTo(_cam); // fade quad is on the overlay layer — new camera must render it
        SnapToCamera();

        // ---- PHASE 4: blink to reveal the new world ----
        for (int b = 0; b < Mathf.Max(0, blinkCount); b++)
        {
            // SFX HOOK: blink / eyelid sound
            if (blinkClip != null) _sfxOneShot.PlayOneShot(blinkClip);

            yield return Fade(1f, 0f, blinkDuration * 0.5f); // eyes peek open
            yield return Fade(0f, 1f, blinkDuration * 0.5f); // eyes close
        }

        // ---- PHASE 5: fade in — open the eyes in the comic world ----
        // SFX HOOK: arrival sting
        if (arrivalClip != null) _sfxOneShot.PlayOneShot(arrivalClip);

        yield return Fade(1f, 0f, fadeDuration);

        SetOverlayActive(false);
        _active = false;
        _playing = false;
    }

    IEnumerator Fade(float from, float to, float dur)
    {
        if (_fadeImage == null) yield break;
        SnapToCamera();

        Color c = _fadeImage.color;
        c.r = c.g = c.b = 0f;

        if (dur <= 0f)
        {
            c.a = to;
            _fadeImage.color = c;
            yield break;
        }

        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(from, to, t / dur);
            _fadeImage.color = c;
            yield return null;
        }
        c.a = to;
        _fadeImage.color = c;
    }

    // -------------------------------------------------------------------------
    // Per-frame follow + streak animation
    // -------------------------------------------------------------------------

    void LateUpdate()
    {
        if (!_active) return;
        SnapToCamera();
        if (_tunnelActive) AnimateStreaks(Time.deltaTime);
    }

    void SnapToCamera()
    {
        if (_cam == null || !_cam.isActiveAndEnabled) _cam = Camera.main;
        if (_cam == null || _rig == null) return;
        _rig.SetPositionAndRotation(_cam.transform.position, _cam.transform.rotation);
        if (_fadeCanvas != null) _fadeCanvas.worldCamera = _cam;
    }

    void AnimateStreaks(float dt)
    {
        if (_streaks == null) return;
        for (int i = 0; i < _streaks.Length; i++)
        {
            Transform s = _streaks[i];
            if (s == null) continue;
            Vector3 p = s.localPosition;
            p.z -= tunnelSpeed * dt;
            if (p.z < RecycleBackZ)
                ResetStreak(s, false);
            else
                s.localPosition = p;
        }
    }

    void ResetStreak(Transform s, bool randomDepth)
    {
        float ang = Random.Range(0f, Mathf.PI * 2f);
        float rad = tunnelRadius * Random.Range(0.55f, 1.15f);
        float z = randomDepth ? Random.Range(RecycleBackZ, SpawnFrontZ)
                              : SpawnFrontZ + Random.Range(0f, 4f);
        s.localPosition = new Vector3(Mathf.Cos(ang) * rad, Mathf.Sin(ang) * rad, z);
        s.localRotation = Quaternion.identity;
        s.localScale = new Vector3(0.05f, 0.05f, Random.Range(1.0f, 2.2f));
    }

    void SetOverlayActive(bool on)
    {
        if (_rig != null) _rig.gameObject.SetActive(on);
    }
}
