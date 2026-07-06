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

    [Tooltip("Fraction (0-1) through the Lift animation at which the phone attaches to hand. " +
             "Only used as FALLBACK when no hand bone is assigned — with a hand bone the phone " +
             "attaches at the hand's closest approach to the phone (no more teleport look).")]
    [Range(0f, 1f)]
    public float phoneAttachFraction = 0.5f;

    [Tooltip("Hand-to-phone distance (m) at which the phone snaps into the hand during Lift")]
    public float attachDistance = 0.09f;

    [Tooltip("Max seconds to wait for the Lift state to exit after the clip finishes (was hardcoded 2s — caused a visible stall after pickup)")]
    public float liftExitMaxWait = 0.5f;

    [Tooltip("Fraction of the Lift clip to play before moving on. The 7.2s clip: reach down ~15%, grab ~20%, admire phone at face height 30-70%, then a long idle tail. Cutting at ~0.72 keeps the admire beat and skips the dead tail (the turn plays over the hand-lowering).")]
    [Range(0.3f, 0.95f)]
    public float liftEndFraction = 0.72f;

    [Tooltip("Animator speed during the Lift (1 = authored speed). 0.6 looked sluggish; 0.75 keeps the deliberate feel but tightens the beat.")]
    public float liftAnimSpeed = 0.75f;

    [Tooltip("Seconds to turn toward the book after lifting")]
    public float turnDuration = 0.6f;

    [Tooltip("Extra yaw (deg) past the phone when turning toward it. The Lift animation twists the torso back slightly, so overshooting the root turn keeps him visually facing the phone at the grab.")]
    public float phoneTurnOvershoot = 20f;

    [Tooltip("Pause after turning, before jumping back (lets player see the phone)")]
    public float postTurnPause = 0.8f;

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
        // Aim at the ACTUAL phone object (not the waypoint) so the body squarely faces
        // what the hand is about to grab.
        Debug.Log("Phase 3: Turning toward phone");
        Vector3 phoneLookTarget = phoneObject != null ? phoneObject.transform.position : phonePos;
        // Overshoot the turn: rotate the look target around the thief by phoneTurnOvershoot
        // degrees so the lift animation's torso twist-back still leaves him facing the phone.
        Vector3 toPhone = phoneLookTarget - thiefModel.transform.position;
        toPhone = Quaternion.Euler(0f, phoneTurnOvershoot, 0f) * toPhone;
        yield return StartCoroutine(SmoothTurn(thiefModel.transform.position + toPhone, turnDuration));

        // ── PHASE 4: Dramatic pause — thief spots the phone ───────────────────
        yield return new WaitForSeconds(preGrabPause);

        // ── PHASE 5: Lift phone (slow, in place) ──────────────────────────────
        Debug.Log("Phase 5: Lifting phone");

        if (thiefAnimator != null) thiefAnimator.speed = liftAnimSpeed;
        if (thiefAnimator != null) thiefAnimator.SetTrigger("Lift");

        // Wait to enter Lifting state (up to 1s for transition)
        yield return StartCoroutine(WaitToEnterState(liftStateName, 1f));

        // Attach the phone the moment the HAND actually reaches it (closest approach),
        // instead of at a fixed clip fraction — kills the "phone teleports into hand" look.
        if (thiefHandBone != null && phoneObject != null)
            yield return StartCoroutine(AttachPhoneOnHandContact());
        else
        {
            yield return StartCoroutine(WaitForNormalizedTime(liftStateName, phoneAttachFraction));
            AttachPhone();
        }

        // Play the Lift only up to liftEndFraction — the rest of the clip is a slow idle tail;
        // the turn toward the book (next phase) plays naturally over the hand-lowering motion.
        yield return StartCoroutine(WaitForNormalizedTime(liftStateName, liftEndFraction));
        // Then wait until the state has actually exited (exit-time transition finishes).
        // Capped by liftExitMaxWait — the old hardcoded 2s cap caused a visible stall
        // after the pickup when the exit transition was slow.
        float exitWait = 0f;
        while (exitWait < liftExitMaxWait && thiefAnimator != null && thiefAnimator.GetCurrentAnimatorStateInfo(0).IsName(liftStateName))
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
        // he takes the phone with him — the scale-1 carrier isn't under the thief hierarchy
        if (_phoneCarrier != null) _phoneCarrier.gameObject.SetActive(false);

        // BookPortalTrigger.OnThiefSequenceComplete() handles glow + interaction wiring
        Debug.Log("=== THIEF SEQUENCE COMPLETE ===");
        OnSequenceComplete?.Invoke();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// During the Lift state, watches the distance between the hand bone and the phone and
    /// attaches the phone at the hand's CLOSEST APPROACH: either when the hand comes within
    /// attachDistance, or when the distance starts increasing again after its minimum
    /// (whichever happens first). Falls back to attaching at 90% through the clip.
    /// </summary>
    IEnumerator AttachPhoneOnHandContact()
    {
        float minDist = float.MaxValue;
        bool attached = false;

        while (thiefAnimator != null &&
               thiefAnimator.GetCurrentAnimatorStateInfo(0).IsName(liftStateName) &&
               thiefAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.9f)
        {
            float dist = Vector3.Distance(thiefHandBone.position, phoneObject.transform.position);

            if (dist <= attachDistance || (dist > minDist + 0.02f && minDist < 0.35f))
            {
                Debug.Log($"[ThiefSpawner] runtime hand-phone gap at attach: {Mathf.Min(dist, minDist):F3}m");
                yield return StartCoroutine(MagnetizePhoneToHand(0.1f));
                AttachPhone();
                attached = true;
                break;
            }

            minDist = Mathf.Min(minDist, dist);
            yield return null;
        }

        if (!attached)
        {
            yield return StartCoroutine(MagnetizePhoneToHand(0.1f));
            AttachPhone(); // fallback: clip nearly done, make sure the phone leaves the table
        }
    }

    private Transform _phoneCarrier; // scale-1 proxy that follows the hand bone

    /// <summary>
    /// Slides the phone the last few cm into the hand — covers the small vertical gap
    /// between the animation's lowest hand point and the tabletop, so the pickup reads
    /// as actual contact instead of a teleport.
    /// </summary>
    IEnumerator MagnetizePhoneToHand(float duration)
    {
        if (phoneObject == null || thiefHandBone == null) yield break;
        Vector3 start = phoneObject.transform.position;
        float e = 0f;
        while (e < duration)
        {
            e += Time.deltaTime;
            phoneObject.transform.position = Vector3.Lerp(start, thiefHandBone.position, Mathf.Clamp01(e / duration));
            yield return null;
        }
    }

    void AttachPhone()
    {
        if (phoneObject == null) return;

        if (thiefHandBone != null)
        {
            // NEVER parent the phone directly to a Mixamo bone: the rig's bones carry
            // 100x import scale (x0.7 thief root), which distorts the phone into a
            // stretched white slab. Instead parent to a scale-1 carrier that follows
            // the bone in LateUpdate (same pattern as the player's wrist ray origins).
            if (_phoneCarrier == null)
            {
                var go = new GameObject("PhoneCarrier");
                _phoneCarrier = go.transform;
            }
            _phoneCarrier.SetPositionAndRotation(thiefHandBone.position, thiefHandBone.rotation);
            _phoneCarrier.localScale = Vector3.one;

            Vector3 keepWorldScale = phoneObject.transform.lossyScale;
            phoneObject.transform.SetParent(_phoneCarrier, true);
            phoneObject.transform.localPosition = phoneLocalOffset;
            phoneObject.transform.localRotation = Quaternion.Euler(phoneLocalRotation);
            phoneObject.transform.localScale = keepWorldScale * phoneScale; // carrier is scale-1: local == world

            Rigidbody phoneRb = phoneObject.GetComponent<Rigidbody>();
            if (phoneRb != null) phoneRb.isKinematic = true;

            var phoneGrab = phoneObject.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (phoneGrab != null) phoneGrab.enabled = false;

            Debug.Log("Phone attached to scale-1 carrier following thief hand");
        }
        else
        {
            phoneObject.SetActive(false);
            Debug.Log("Phone hidden (no hand bone assigned)");
        }
    }

    void LateUpdate()
    {
        // carrier follows the hand bone while the phone is held
        if (_phoneCarrier != null && thiefHandBone != null && thiefModel != null && thiefModel.activeSelf)
            _phoneCarrier.SetPositionAndRotation(thiefHandBone.position, thiefHandBone.rotation);
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
        // Threshold must be TINY: the thief lands right next to the phone, so a 10cm (0.01 sqr)
        // dead-zone silently skipped the whole turn — the "doesn't turn toward the phone" bug.
        if (dir.sqrMagnitude < 0.0001f) yield break;

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
