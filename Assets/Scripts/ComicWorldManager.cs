using UnityEngine;
using System.Collections;

/// <summary>
/// Master controller for ComicWorld. Singleton.
///
/// Phase 1: Frame 1 (with FrameEnlarger + TeddyBear2Dto3D) appears + 30-40 grey BG frames spawn.
///          Player pulls Frame 1 edges apart → teddy becomes 3D → player grabs teddy.
///          On grab: Frame 1 shatters → Phase 2.
///
/// Phase 2: Frame 2 appears at Frame 1's position.
///          All grey BG frames shatter simultaneously.
///          New pastel BG frames spawn.
///          Story frames 3-16 activate at their positions.
///
/// Phase 3+: puzzle, thief, spirit, final challenge, ending.
/// </summary>
public class ComicWorldManager : MonoBehaviour
{
    public static ComicWorldManager Instance { get; private set; }

    public enum Phase
    {
        Phase1_Frame1,
        Phase2_Frame2,
        Phase3_Puzzle,
        Phase4_ThiefIntro,
        Phase5_Spirit,
        Phase6_FinalChallenge,
        Phase7_Ending
    }

    [Header("=== Current Phase ===")]
    public Phase currentPhase = Phase.Phase1_Frame1;

    [Header("=== Spawn Points ===")]
    public Transform frame1SpawnPoint;
    public Transform frame2SpawnPoint;
    public Transform framePuzzleArea;
    public Transform greenLightLocation;
    public Transform thiefSpawnArea;
    public Transform finalChallengeArea;

    [Header("=== Frame References (1-16) ===")]
    public ComicFrame[] frames;

    [Header("=== Other References ===")]
    public TeddyBearController teddyBear;
    public ThiefComicWorld thiefComicWorld;
    public FinalChallenge finalChallenge;
    public GoldSpirit goldSpirit;
    public BackgroundFrameSpawner backgroundFrameSpawner;

    [Tooltip("Green/gold flowy light wave over the puzzle area — hidden until Phase 3 (after teddy grab), draws the player toward the ordering puzzle")]
    public GameObject greenGoldWave;

    [Tooltip("B/W-to-art presentation flow for the puzzle frames (revealed at Phase 3)")]
    public PuzzleFrameFlow puzzleFrameFlow;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        StartCoroutine(BeginGame());
    }

    IEnumerator BeginGame()
    {
        yield return new WaitForSeconds(1f);

        // Show arrival dialogue but DON'T wait for it — start Phase 1 immediately
        // so Frame 1 and background frames appear regardless of controller state
        DialogueSystem.Instance?.ShowDialogue(GameDialogue.ArrivalDialogue);
        StartPhase1();
    }

    // ── PHASE 1 ──────────────────────────────────────────────────────────────────
    public void StartPhase1()
    {
        if (DialogueSystem.Instance != null)
            DialogueSystem.Instance.OnDialogueComplete -= StartPhase1;

        currentPhase = Phase.Phase1_Frame1;
        Debug.Log("[ComicWorldManager] Phase 1: Frame 1 + grey BG frames");

        // Spawn grey background atmosphere
        if (backgroundFrameSpawner != null)
            backgroundFrameSpawner.SpawnInitialBackgroundFrames();

        // Activate Frame 1
        SetFrameActive(1, true);

        // Subscribe to teddy grabbed so we know when to shatter and advance
        var frame1 = GetFrameByIndex(1);
        if (frame1 != null)
        {
            var teddy = frame1.GetComponentInChildren<TeddyBear2Dto3D>(true);
            if (teddy != null)
                teddy.OnTeddyGrabbed += OnTeddyGrabbedPhase1;
            else
                Debug.LogWarning("[ComicWorldManager] Frame 1 has no TeddyBear2Dto3D child — FrameEnlarger must be set up on Frame 1.");
        }
    }

    void OnTeddyGrabbedPhase1()
    {
        // Unsubscribe immediately
        var frame1 = GetFrameByIndex(1);
        if (frame1 != null)
        {
            var teddy = frame1.GetComponentInChildren<TeddyBear2Dto3D>(true);
            if (teddy != null) teddy.OnTeddyGrabbed -= OnTeddyGrabbedPhase1;
        }

        // Shatter Frame 1
        if (frame1 != null) frame1.ShatterFrame();

        StartCoroutine(DelayedPhase2(0.5f));
    }

    IEnumerator DelayedPhase2(float delay)
    {
        yield return new WaitForSeconds(delay);
        StartPhase2();
    }

    // ── PHASE 2 ──────────────────────────────────────────────────────────────────
    public void StartPhase2()
    {
        currentPhase = Phase.Phase2_Frame2;
        Debug.Log("[ComicWorldManager] Phase 2: World transforms");

        // Frame 2 appears at Frame 1's position (same spot, memories replace action)
        SetFrameActive(2, true);
        var frame2 = GetFrameByIndex(2);
        if (frame2 != null && frame1SpawnPoint != null)
            frame2.transform.position = frame1SpawnPoint.position;

        // All grey BG frames shatter — the world breaks apart around the player
        SfxPlayer.Play2D("world_break");
        if (backgroundFrameSpawner != null)
            backgroundFrameSpawner.ShatterAllBackgroundFrames();

        if (DialogueSystem.Instance != null)
            DialogueSystem.Instance.ShowDialogue(GameDialogue.Frame2Dialogue);

        StartCoroutine(Phase2Setup());
    }

    IEnumerator Phase2Setup()
    {
        // The world-break is a MOMENT: BG frames shatter in a ~3s cascade (stagger set on
        // the spawner), then Harriet's story pops in frame by frame.
        yield return new WaitForSeconds(3.2f);

        // Spawn new pastel BG frames
        if (backgroundFrameSpawner != null)
            backgroundFrameSpawner.SpawnNewBackgroundFrames();

        // Story frames 3-16 pop in one after another
        for (int i = 3; i <= 16; i++)
        {
            SetFrameActive(i, true);
            var fr = GetFrameByIndex(i);
            if (fr != null)
            {
                SfxPlayer.Play("frame_reveal", fr.transform.position, 0.4f);
                // 9-16 are static displays on the walk path — hide them when the
                // player's head is about to clip through (3-8 are grabbable, skip)
                if (i >= 9 && fr.GetComponent<NearHeadFade>() == null)
                    fr.gameObject.AddComponent<NearHeadFade>();
            }
            yield return new WaitForSeconds(0.12f);
        }

        // Advance to Phase 3 after dialogue
        if (DialogueSystem.Instance != null)
            DialogueSystem.Instance.OnDialogueComplete += StartPhase3;
        else
            StartPhase3();
    }

    // ── PHASE 3 ──────────────────────────────────────────────────────────────────
    public void StartPhase3()
    {
        if (DialogueSystem.Instance != null)
            DialogueSystem.Instance.OnDialogueComplete -= StartPhase3;

        currentPhase = Phase.Phase3_Puzzle;
        Debug.Log("[ComicWorldManager] Phase 3: Frame puzzle");

        // The green/gold wave appears in the distance, beckoning the player to the puzzle
        if (greenGoldWave != null)
            greenGoldWave.SetActive(true);

        // The drifting B/W frames turn into Harriet's real comic frames
        if (puzzleFrameFlow != null)
            puzzleFrameFlow.RevealFrames();

        if (DialogueSystem.Instance != null)
            DialogueSystem.Instance.ShowDialogue(GameDialogue.FramePuzzleIntro);
    }

    public void OnPuzzleSolved()
    {
        Debug.Log("[ComicWorldManager] Puzzle solved!");

        // Story beat: the green light gathers and FLIES INTO the teddy bear; on arrival
        // the teddy's eyes begin turning green/gold over the next minute.
        if (teddyBear != null)
        {
            Vector3 from = greenLightLocation != null ? greenLightLocation.position
                          : (framePuzzleArea != null ? framePuzzleArea.position : teddyBear.transform.position + Vector3.up * 2f);
            var teddy = teddyBear; // capture
            LightComet.Fly(from, teddy.transform, 2.2f, () => teddy.StartEyeTransition(60f));
        }

        if (DialogueSystem.Instance != null)
        {
            DialogueSystem.Instance.ShowDialogue(GameDialogue.PuzzleSolvedDialogue);
            DialogueSystem.Instance.OnDialogueComplete += StartPhase4;
        }
        else StartPhase4();
    }

    // ── PHASE 4 ──────────────────────────────────────────────────────────────────
    public void StartPhase4()
    {
        if (DialogueSystem.Instance != null)
            DialogueSystem.Instance.OnDialogueComplete -= StartPhase4;

        currentPhase = Phase.Phase4_ThiefIntro;
        Debug.Log("[ComicWorldManager] Phase 4: Thief intro");

        if (thiefComicWorld != null)
            thiefComicWorld.Appear();

        if (DialogueSystem.Instance != null)
            DialogueSystem.Instance.ShowDialogue(GameDialogue.ThiefAppearsDialogue);
    }

    // ── PHASE 5 ──────────────────────────────────────────────────────────────────
    public void StartPhase5()
    {
        currentPhase = Phase.Phase5_Spirit;
        Debug.Log("[ComicWorldManager] Phase 5: Spirit rises");

        if (DialogueSystem.Instance != null)
            DialogueSystem.Instance.ShowDialogue(GameDialogue.SpiritRisesDialogue);

        if (goldSpirit != null && teddyBear != null)
            goldSpirit.PlaySpiritRise(teddyBear.transform.position + Vector3.up * 0.5f, teddyBear.transform);
    }

    // ── PHASE 6 ──────────────────────────────────────────────────────────────────
    public void StartPhase6()
    {
        currentPhase = Phase.Phase6_FinalChallenge;
        Debug.Log("[ComicWorldManager] Phase 6: Final challenge");

        // The spirit has entered the teddy: full-body gold glow, arm rises and points
        // the player toward the final challenge area (Frame 2's story location).
        if (teddyBear != null)
        {
            teddyBear.StartGoldGlow();
            teddyBear.SetPoseRaised();
            if (finalChallengeArea != null)
                teddyBear.StartPointing(finalChallengeArea);
        }

        if (DialogueSystem.Instance != null)
            DialogueSystem.Instance.ShowDialogue(GameDialogue.FinalChallengeDialogue);

        if (finalChallenge != null)
            finalChallenge.StartChallenge();
    }

    // ── PHASE 7 ──────────────────────────────────────────────────────────────────
    public void StartPhase7(bool won)
    {
        currentPhase = Phase.Phase7_Ending;
        Debug.Log("[ComicWorldManager] Phase 7: Ending — won: " + won);

        string[] dialogue = won ? GameDialogue.WinDialogue : GameDialogue.LoseDialogue;
        if (DialogueSystem.Instance != null)
        {
            DialogueSystem.Instance.ShowDialogue(dialogue);
            if (won)
                DialogueSystem.Instance.OnDialogueComplete += ShowHarrietEnding;
        }
    }

    void ShowHarrietEnding()
    {
        if (DialogueSystem.Instance != null)
        {
            DialogueSystem.Instance.OnDialogueComplete -= ShowHarrietEnding;
            DialogueSystem.Instance.ShowDialogue(GameDialogue.HarrietEnding);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    void SetFrameActive(int frameIndex, bool active)
    {
        if (frames == null) return;
        foreach (var f in frames)
            if (f != null && f.frameIndex == frameIndex)
                f.gameObject.SetActive(active);
    }

    ComicFrame GetFrameByIndex(int frameIndex)
    {
        if (frames == null) return null;
        foreach (var f in frames)
            if (f != null && f.frameIndex == frameIndex) return f;
        return null;
    }
}
