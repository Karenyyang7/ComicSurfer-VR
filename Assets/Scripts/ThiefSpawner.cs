using UnityEngine;
using System.Collections;

/// <summary>
/// Thief sequence: jumps out of book → lands on table edge → grabs phone → turns → jumps back into book.
///
/// ANIMATOR SETTINGS (IMPORTANT - fix these in the clip Import Settings):
///   For JumpOut and JumpBack clips:
///     Root Transform Position (XZ) → Bake Into Pose = OFF  ← was causing "lands beyond table" + teleport
///     Root Transform Position (Y)  → Bake Into Pose = OFF  ← was conflicting with script's arc height
///     Apply Root Motion             = OFF  (already done)
///   For Lifting clip: defaults are fine (no large root motion expected).
///
/// SETUP:
///   1. Add this script to a ThiefSpawner empty GameObject.
///   2. Create and position these empty GameObjects:
///      - WP_BookSurface : on the open book (where thief spawns)
///      - WP_TableEdge   : on the table edge (where thief lands after jumping out)
///      - WP_PhonePos    : where the phone sits (thief walks here)
///      - WP_BookReturn  : on the book (where thief jumps back into)
///   3. Drag each into the matching inspector slot.
///   4. Drag thief model, phone object, and hand bone.
///   5. In "Animator State Names" set the exact state names from your Animator window.
///   6. Thief model starts DISABLED.
/// </summary>
public class ThiefSpawner : MonoBehaviour
{
    [Header("=== Thief Model ===")]
    public GameObject thiefModel;

    [Header("=== Waypoints (create empty GameObjects, position them!) ===")]
    [Tooltip("On the book surface where thief pops out")]
    public Transform wpBookSurface;

    [Tooltip("On the table edge where thief lands after jumping out")]
    public Transform wpTableEdge;

    [Tooltip("Near the phone on the table")]
    public Transform wpPhonePos;

    [Tooltip("On the book where thief jumps back into")]
    public Transform wpBookReturn;

    [Header("=== Phone ===")]
    public GameObject phoneObject;
    public Transform thiefHandBone;
    [Tooltip("Phone position offset relative to the hand bone")]
    public Vector3 phoneLocalOffset = new Vector3(0.1f, 0.05f, 0.12f);
    [Tooltip("Phone rotation offset relative to the hand bone (euler degrees)")]
    public Vector3 phoneLocalRotation = new Vector3(-90f, 0f, 0f);
    [Tooltip("Phone scale when held (1 = original size)")]
    public float phoneScale = 1f;

    [Header("=== Timing ===")]
    [Tooltip("Arc height for jump out")]
    public float jumpOutHeight = 0.5f;

    [Tooltip("World-space Y ceiling clamp for jump arc. Set this to (ceiling height − thief character height) so the head doesn't clip.")]
    public float maxArcY = 3.5f;

    [Tooltip("Approximate height of the thief character from root to top of head, used to offset the ceiling clamp.")]
    public float thiefCharacterHeight = 1.3f;

    [Tooltip("Pause on table before walking to phone")]
    public float standPauseDuration = 2.0f;

    [Tooltip("Pause after turning toward the phone — thief 'sees' it before grabbing")]
    public float preGrabPause = 0.8f;

    [Tooltip("Fraction (0-1) through the Lift animation at which the phone attaches to hand")]
    [Range(0f, 1f)]
    public float phoneAttachFraction = 0.5f;

    [Tooltip("Seconds to turn toward the book after lifting")]
    public float turnDuration = 1.0f;

    [Tooltip("Pause after turning, before jumping back (lets player see the phone)")]
    public float postTurnPause = 2.0f;

    [Tooltip("Arc height for jump back")]
    public float jumpBackHeight = 0.5f;

    [Header("=== Animator State Names (must match states in your Animator window) ===")]
    [Tooltip("Name of the JumpOut state in the Animator")]
    public string jumpOutStateName = "JumpOut";

    [Tooltip("Name of the Lifting state in the Animator")]
    public string liftStateName = "Lift";

    [Tooltip("Name of the JumpBack state in the Animator")]
    public string jumpBackStateName = "JumpBack";

    [Header("=== References ===")]
    public UIInstructions instructions;

    /// <summary>Fires when the full thief sequence ends. BookPortalTrigger listens to this.</summary>
    public event System.Action OnSequenceComplete;

    private Animator thiefAnimator;
    private AudioSource audioSource;
    private bool hasSpawned = false;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        if (thiefModel != null)
        {
            thiefModel.SetActive(false);
            thiefAnimator = thiefModel.GetComponent<Animator>();
            if (thiefAnimator != null)
                thiefAnimator.applyRootMotion = false;
        }
    }

    public void SpawnThief()
    {
        if (hasSpawned) return;
        hasSpawned = true;
        StartCoroutine(ThiefSequence());
    }

    IEnumerator ThiefSequence()
    {
        Debug.Log("=== THIEF SEQUENCE START ===");

        Vector3 startPos  = wpBookSurface != null ? wpBookSurface.position : transform.position;
        Vector3 landPos   = wpTableEdge   != null ? wpTableEdge.position   : startPos + Vector3.right;
        Vector3 phonePos  = wpPhonePos    != null ? wpPhonePos.position    : landPos;
        Vector3 returnPos = wpBookReturn  != null ? wpBookReturn.position  : startPos;

        // Activate thief at book surface
        thiefModel.SetActive(true);
        thiefModel.transform.position = startPos;

        if (thiefAnimator == null)
            thiefAnimator = thiefModel.GetComponent<Animator>();
        if (thiefAnimator != null)
            thiefAnimator.applyRootMotion = false;

        // ── PHASE 1: Jump out of book to table edge ────────────────────────────
        Debug.Log("Phase 1: JumpOut");
        FaceTarget(landPos);

        float jumpOutLen = GetClipLength(jumpOutStateName, 1f);
        if (thiefAnimator != null) thiefAnimator.SetTrigger("JumpOut");

        // Move the character via arc for exactly the clip's duration
        yield return StartCoroutine(ArcMove(startPos, landPos, jumpOutHeight, jumpOutLen));
        thiefModel.transform.position = landPos; // snap to exact waypoint

        // Wait for JumpOut to finish
        if (thiefAnimator != null)
        {
            yield return StartCoroutine(WaitToEnterState(jumpOutStateName, 1f));
            yield return StartCoroutine(WaitForNormalizedTime(jumpOutStateName, 0.95f));
        }

        Debug.Log("Phase 1 complete: landed on table");

        // ── PHASE 2: Stand pause (landing reaction) ────────────────────────────
        yield return new WaitForSeconds(standPauseDuration);

        // ── PHASE 3: Turn toward phone ─────────────────────────────────────────
        Debug.Log("Phase 3: Turning toward phone");
        yield return StartCoroutine(SmoothTurn(phonePos, turnDuration));

        // ── PHASE 4: Dramatic pause — thief spots the phone ───────────────────
        yield return new WaitForSeconds(preGrabPause);

        // ── PHASE 5: Lift phone (slow, in place) ──────────────────────────────
        Debug.Log("Phase 5: Lifting phone");

        if (thiefAnimator != null) thiefAnimator.speed = 0.6f;
        if (thiefAnimator != null) thiefAnimator.SetTrigger("Lift");

        // Wait to enter Lifting state (up to 1s for transition)
        yield return StartCoroutine(WaitToEnterState(liftStateName, 1f));

        // Poll until Lifting's normalizedTime reaches the attach point, then attach phone
        yield return StartCoroutine(WaitForNormalizedTime(liftStateName, phoneAttachFraction));
        AttachPhone();

        // Wait for the full Lifting clip to play through (normalizedTime >= 0.95)
        yield return StartCoroutine(WaitForNormalizedTime(liftStateName, 0.95f));
        // Then wait until the state has actually exited (exit-time transition finishes)
        float exitWait = 0f;
        while (exitWait < 2f && thiefAnimator != null && thiefAnimator.GetCurrentAnimatorStateInfo(0).IsName(liftStateName))
        {
            exitWait += Time.deltaTime;
            yield return null;
        }
        if (thiefAnimator != null) thiefAnimator.speed = 1f;

        Debug.Log("Phase 5 complete: phone grabbed");

        // ── PHASE 6: Turn toward book ──────────────────────────────────────────
        Debug.Log("Phase 6: Turning toward book");
        yield return StartCoroutine(SmoothTurn(returnPos, turnDuration));
        yield return new WaitForSeconds(postTurnPause);

        // ── PHASE 7: Jump back into book ───────────────────────────────────────
        // IMPORTANT (Animator): there must be a transition from Lifting (or Any State)
        // to JumpBack triggered by the "JumpBack" parameter.
        Debug.Log("Phase 7: JumpBack");

        // Snap Y to landing height so any Y-drift from root motion doesn't affect the arc start
        Vector3 snapPos = thiefModel.transform.position;
        snapPos.y = landPos.y;
        thiefModel.transform.position = snapPos;

        float jumpBackLen = GetClipLength(jumpBackStateName, 1f);
        if (thiefAnimator != null) thiefAnimator.SetTrigger("JumpBack");

        yield return StartCoroutine(ArcMove(thiefModel.transform.position, returnPos, jumpBackHeight, jumpBackLen));
        thiefModel.transform.position = returnPos; // snap to book waypoint

        // ── PHASE 8: Disappear ─────────────────────────────────────────────────
        Debug.Log("Phase 8: Disappearing");
        thiefModel.SetActive(false);

        // BookPortalTrigger.OnThiefSequenceComplete() handles glow + interaction wiring
        Debug.Log("=== THIEF SEQUENCE COMPLETE ===");
        OnSequenceComplete?.Invoke();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    void AttachPhone()
    {
        if (phoneObject == null) return;

        if (thiefHandBone != null)
        {
            phoneObject.transform.SetParent(thiefHandBone);
            phoneObject.transform.localPosition = phoneLocalOffset;
            phoneObject.transform.localRotation = Quaternion.Euler(phoneLocalRotation);
            phoneObject.transform.localScale = Vector3.one * phoneScale;

            Rigidbody phoneRb = phoneObject.GetComponent<Rigidbody>();
            if (phoneRb != null) phoneRb.isKinematic = true;

            var phoneGrab = phoneObject.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (phoneGrab != null) phoneGrab.enabled = false;

            Debug.Log("Phone attached to thief hand");
        }
        else
        {
            phoneObject.SetActive(false);
            Debug.Log("Phone hidden (no hand bone assigned)");
        }
    }

    /// <summary>
    /// Returns the length in seconds of an animation clip whose name matches clipName.
    /// Falls back to fallbackDuration if the clip is not found.
    /// </summary>
    float GetClipLength(string clipName, float fallbackDuration = 1f)
    {
        if (thiefAnimator == null || thiefAnimator.runtimeAnimatorController == null)
            return fallbackDuration;

        foreach (AnimationClip clip in thiefAnimator.runtimeAnimatorController.animationClips)
        {
            if (clip.name == clipName)
                return clip.length;
        }

        Debug.LogWarning($"[ThiefSpawner] Clip '{clipName}' not found in animator controller. Using fallback {fallbackDuration}s.");
        return fallbackDuration;
    }

    /// Waits until the animator enters the named state. Times out after maxWait seconds.
    IEnumerator WaitToEnterState(string stateName, float maxWait, int layer = 0)
    {
        if (thiefAnimator == null) yield break;
        float t = 0f;
        while (t < maxWait && !thiefAnimator.GetCurrentAnimatorStateInfo(layer).IsName(stateName))
        {
            t += Time.deltaTime;
            yield return null;
        }
        if (!thiefAnimator.GetCurrentAnimatorStateInfo(layer).IsName(stateName))
            Debug.LogWarning($"[ThiefSpawner] Never entered state '{stateName}' — check the Animator State Name field matches exactly.");
    }

    /// Waits until the current state's normalizedTime reaches targetT (0–1).
    /// If the animator is no longer in stateName, returns immediately.
    IEnumerator WaitForNormalizedTime(string stateName, float targetT, int layer = 0)
    {
        if (thiefAnimator == null) yield break;
        while (thiefAnimator.GetCurrentAnimatorStateInfo(layer).IsName(stateName) &&
               thiefAnimator.GetCurrentAnimatorStateInfo(layer).normalizedTime < targetT)
        {
            yield return null;
        }
    }

    /// <summary>
    /// Moves the thief in a parabolic arc. Duration matches the animation clip length.
    /// maxArcY clamps the peak so the thief doesn't clip through the ceiling.
    /// </summary>
    IEnumerator ArcMove(Vector3 start, Vector3 end, float height, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = t * t * (3f - 2f * t);         // smooth-step horizontal
            Vector3 pos = Vector3.Lerp(start, end, smoothT);
            pos.y += height * 4f * t * (1f - t);                          // parabolic arc
            pos.y = Mathf.Min(pos.y, maxArcY - thiefCharacterHeight);    // clamp so head stays below ceiling
            thiefModel.transform.position = pos;
            yield return null;
        }
        thiefModel.transform.position = end;
    }

    /// <summary>
    /// Slides the thief horizontally from start to end (walking on table).
    /// Y is locked to start.y so root-motion Y drift doesn't lift/sink the character.
    /// </summary>
    IEnumerator SmoothMove(Vector3 start, Vector3 end, float duration)
    {
        float elapsed = 0f;
        float fixedY = start.y;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = t * t * (3f - 2f * t);
            Vector3 pos = Vector3.Lerp(start, end, smoothT);
            pos.y = fixedY;
            thiefModel.transform.position = pos;
            yield return null;
        }
        Vector3 finalPos = end;
        finalPos.y = fixedY;
        thiefModel.transform.position = finalPos;
    }

    /// <summary>
    /// Smoothly rotates the thief to face targetPos over duration seconds.
    /// </summary>
    IEnumerator SmoothTurn(Vector3 targetPos, float duration)
    {
        Vector3 dir = targetPos - thiefModel.transform.position;
        dir.y = 0;
        if (dir.sqrMagnitude < 0.01f) yield break;

        Quaternion endRot      = Quaternion.LookRotation(dir.normalized); // face TOWARD target (+Z forward, same convention as FaceTarget)
        float      totalAngle  = Quaternion.Angle(thiefModel.transform.rotation, endRot);
        float      degsPerSec  = duration > 0f ? totalAngle / duration : 720f;

        while (Quaternion.Angle(thiefModel.transform.rotation, endRot) > 0.5f)
        {
            thiefModel.transform.rotation = Quaternion.RotateTowards(
                thiefModel.transform.rotation, endRot, degsPerSec * Time.deltaTime);
            yield return null;
        }
        thiefModel.transform.rotation = endRot;
    }

    /// <summary>
    /// Instantly snaps the thief's forward direction toward targetPos.
    /// Note: no-op when thief is already AT targetPos (dir is zero).
    /// </summary>
    void FaceTarget(Vector3 targetPos)
    {
        Vector3 dir = targetPos - thiefModel.transform.position;
        dir.y = 0;
        if (dir.sqrMagnitude > 0.01f)
            thiefModel.transform.rotation = Quaternion.LookRotation(dir);
    }

    public void ResetThief()
    {
        hasSpawned = false;
        if (thiefModel != null) thiefModel.SetActive(false);
    }
}
