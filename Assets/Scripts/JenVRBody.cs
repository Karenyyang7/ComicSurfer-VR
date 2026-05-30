using UnityEngine;

/// <summary>
/// Drives Jen's Mixamo skeleton to follow the VR headset and controllers.
/// Upper body (head, spine, arms) driven by IK to match VR tracking.
/// Lower body (legs) driven by walk animation when player moves.
///
/// SETUP:
/// 1. Add this script to jen-my-first-character
/// 2. Drag Main Camera into headTarget
/// 3. Drag Left Hand Controller into leftHandTarget
/// 4. Drag Right Hand Controller into rightHandTarget
/// 5. Assign Jen's Animator component (needs a controller with walk blend)
///
/// ANIMATOR SETUP FOR WALKING:
/// Create an Animator Controller with:
///   - A Blend Tree or two states: "Idle" and "Walking"
///   - Float parameter called "MoveSpeed"
///   - Transition: Idle > Walking when MoveSpeed > 0.1
///   - Transition: Walking > Idle when MoveSpeed < 0.1
///   - Set transition duration to 0.05s (not the default 0.25s) to avoid walk glide
///   - Drag the Standing Idle clip into Idle state
///   - Drag the Walking clip into Walking state
///   - IMPORTANT: Disable "Apply Root Motion" on Jen's Animator component
///     (or leave applyRootMotion = false below) so walking doesn't fight the headset position
/// </summary>
public class JenVRBody : MonoBehaviour
{
    [Header("=== VR Tracking Targets ===")]
    public Transform headTarget;
    public Transform leftHandTarget;
    public Transform rightHandTarget;

    [Header("=== Ray Origins ===")]
    [Tooltip("Scale-1 empty under XR Origin, assigned to the LEFT XRRayInteractor.rayOriginTransform (and its line visual). Driven to Jen's left wrist each frame so the laser emits from her hand.")]
    public Transform leftRayOrigin;
    [Tooltip("Scale-1 empty under XR Origin, assigned to the RIGHT XRRayInteractor.rayOriginTransform (and its line visual).")]
    public Transform rightRayOrigin;
    [Tooltip("Optional offset (m) from the wrist along the aim direction, if you want the beam to start at the knuckles instead of the wrist.")]
    public float rayOriginForwardOffset = 0f;

    [Header("=== Tuning ===")]
    public float bodyTurnSpeed = 8f;
    public float bodyTurnThreshold = 50f;
    public Vector3 leftHandOffset = Vector3.zero;
    public Vector3 rightHandOffset = Vector3.zero;
    public Vector3 leftHandRotationOffset = Vector3.zero;
    public Vector3 rightHandRotationOffset = Vector3.zero;

    [Header("=== Walking Animation ===")]
    [Tooltip("Jen's Animator component")]
    public Animator jenAnimator;

    [Tooltip("Name of the float parameter in the Animator")]
    public string moveSpeedParam = "MoveSpeed";

    [Tooltip("Minimum movement to trigger walk animation")]
    public float moveThreshold = 0.01f;

    [Header("=== Head Visibility ===")]
    [Tooltip("Use layer-based hiding instead (recommended). Set to false if using layers.")]
    public bool hideHeadFromCamera = false;

    // Bone references
    private Transform hips;
    private Transform spine;
    private Transform spine1;
    private Transform spine2;
    private Transform neck;
    private Transform head;
    private Transform leftShoulder;
    private Transform leftArm;
    private Transform leftForeArm;
    private Transform leftHand;
    private Transform rightShoulder;
    private Transform rightArm;
    private Transform rightForeArm;
    private Transform rightHand;

    // Internal state
    private float targetBodyYaw;
    private Vector3 lastPosition;
    private float smoothedSpeed;
    private Transform cameraOffsetParent; // = headTarget.parent (Camera Offset). Its localY = CameraYOffset, used to lift controller IK targets out of underground LOCAL-space poses (Device mode).

    void Start()
    {
        hips         = FindBoneRecursive(transform, "mixamorig:Hips");
        spine        = FindBoneRecursive(transform, "mixamorig:Spine");
        spine1       = FindBoneRecursive(transform, "mixamorig:Spine1");
        spine2       = FindBoneRecursive(transform, "mixamorig:Spine2");
        neck         = FindBoneRecursive(transform, "mixamorig:Neck");
        head         = FindBoneRecursive(transform, "mixamorig:Head");
        leftShoulder  = FindBoneRecursive(transform, "mixamorig:LeftShoulder");
        leftArm       = FindBoneRecursive(transform, "mixamorig:LeftArm");
        leftForeArm   = FindBoneRecursive(transform, "mixamorig:LeftForeArm");
        leftHand      = FindBoneRecursive(transform, "mixamorig:LeftHand");
        rightShoulder = FindBoneRecursive(transform, "mixamorig:RightShoulder");
        rightArm      = FindBoneRecursive(transform, "mixamorig:RightArm");
        rightForeArm  = FindBoneRecursive(transform, "mixamorig:RightForeArm");
        rightHand     = FindBoneRecursive(transform, "mixamorig:RightHand");

        if (hips == null) Debug.LogError("JenVRBody: Could not find mixamorig:Hips!");
        if (head == null) Debug.LogError("JenVRBody: Could not find mixamorig:Head!");

        // Camera Offset is the headset's parent; its localY equals XROrigin.CameraYOffset.
        // Controllers are siblings of Camera Offset, so in Device/LOCAL tracking their poses are
        // relative to the HMD start (underground); we add this offset to controller IK targets.
        if (headTarget != null)
            cameraOffsetParent = headTarget.parent;

        targetBodyYaw = transform.eulerAngles.y;
        lastPosition  = transform.position;
        smoothedSpeed = 0f;

        if (jenAnimator == null)
            jenAnimator = GetComponent<Animator>();

        if (jenAnimator != null)
        {
            // Prevent the walking clip from moving the character root —
            // the headset position drives movement instead.
            jenAnimator.applyRootMotion = false;
        }

        if (hideHeadFromCamera)
        {
            HideFromMainCamera("mixamorig:Head");
            HideFromMainCamera("eye_LP");
            HideFromMainCamera("ochki_LP");
            HideFromMainCamera("kosa1");
            HideFromMainCamera("kosa2");
            HideFromMainCamera("theethv1");
            HideFromMainCamera("lesh_LP1");
        }
    }

    void LateUpdate()
    {
        if (headTarget == null || hips == null) return;

        UpdateBodyPosition();
        UpdateBodyRotation();
        UpdateWalkAnimation();
        UpdateSpine();
        UpdateHead();
        UpdateArm(leftShoulder,  leftArm,  leftForeArm,  leftHand,
                  leftHandTarget,  leftHandOffset,  leftHandRotationOffset);
        UpdateArm(rightShoulder, rightArm, rightForeArm, rightHand,
                  rightHandTarget, rightHandOffset, rightHandRotationOffset);

        // Pin each ray origin to the (post-IK) wrist, aiming along the controller's forward.
        // Assigned to the XRRayInteractor.rayOriginTransform so both the visible beam and the
        // selection raycast emit from Jen's hand.
        UpdateRayOrigin(leftRayOrigin,  leftHand,  leftHandTarget);
        UpdateRayOrigin(rightRayOrigin, rightHand, rightHandTarget);
    }

    void UpdateRayOrigin(Transform rayOrigin, Transform handBone, Transform controller)
    {
        if (rayOrigin == null || handBone == null) return;
        if (controller != null) rayOrigin.rotation = controller.rotation; // aim where the player points
        rayOrigin.position = handBone.position + rayOrigin.forward * rayOriginForwardOffset;
    }

    void UpdateBodyPosition()
    {
        Vector3 headPos = headTarget.position;
        Vector3 newPos  = transform.position;
        newPos.x = headPos.x;
        newPos.z = headPos.z;
        transform.position = newPos;
    }

    void UpdateBodyRotation()
    {
        float headYaw   = headTarget.eulerAngles.y;
        float angleDiff = Mathf.DeltaAngle(targetBodyYaw, headYaw);

        if (Mathf.Abs(angleDiff) > bodyTurnThreshold)
            targetBodyYaw = headYaw;

        Vector3 euler = transform.eulerAngles;
        euler.y = Mathf.LerpAngle(euler.y, targetBodyYaw, bodyTurnSpeed * Time.deltaTime);
        transform.eulerAngles = euler;
    }

    /// <summary>
    /// Detect movement and drive walk animation.
    /// Uses asymmetric smoothing: fast to decelerate (prevents glide) and moderate to accelerate.
    /// SetFloat only — no CrossFadeInFixedTime (conflicts with animator's own threshold transitions).
    /// Reduce transition durations in JenAnimator.controller to 0.05s for tight response.
    /// </summary>
    void UpdateWalkAnimation()
    {
        if (jenAnimator == null) return;

        Vector3 currentPos = transform.position;
        Vector3 delta = currentPos - lastPosition;
        delta.y = 0;
        float speed = delta.magnitude / Time.deltaTime;
        lastPosition = currentPos;

        // Asymmetric smoothing: fast stop (no glide), moderate start
        float lerpFactor = (speed < moveThreshold) ? Time.deltaTime * 35f : Time.deltaTime * 15f;
        smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed, lerpFactor);
        if (smoothedSpeed < 0.005f) smoothedSpeed = 0f; // snap to zero immediately

        jenAnimator.SetFloat(moveSpeedParam, smoothedSpeed);
    }

    void UpdateSpine()
    {
        if (spine2 == null || spine1 == null) return;

        float headPitch       = NormalizeAngle(headTarget.eulerAngles.x);
        float headYawRelative = Mathf.DeltaAngle(transform.eulerAngles.y, headTarget.eulerAngles.y);

        spine1.localRotation = Quaternion.Euler(headPitch * 0.3f, headYawRelative * 0.2f, 0);
        spine2.localRotation = Quaternion.Euler(headPitch * 0.4f, headYawRelative * 0.3f, 0);
    }

    void UpdateHead()
    {
        if (head == null || neck == null) return;

        float headYawRelative = Mathf.DeltaAngle(transform.eulerAngles.y, headTarget.eulerAngles.y);
        float headPitch       = NormalizeAngle(headTarget.eulerAngles.x);

        neck.localRotation = Quaternion.Euler(headPitch * 0.3f, headYawRelative * 0.3f, 0);
        head.localRotation = Quaternion.Euler(headPitch * 0.2f, headYawRelative * 0.2f, 0);
    }

    /// <summary>
    /// Two-bone IK that works with Mixamo bone orientations.
    ///
    /// Mixamo bones don't have their Z+ axis aligned toward the child joint, so the
    /// classic LookRotation approach causes wild spinning. Instead we use
    /// FromToRotation to rotate each bone FROM its current natural direction
    /// TO the desired direction. This is orientation-agnostic.
    ///
    /// Elbow hint is anchored to the character body axes (outward + backward from torso)
    /// rather than the arm direction — this is stable at all arm elevations, including
    /// when the player raises their arms overhead (where the old Cross(armDir, Vector3.up)
    /// approach approached zero and caused wild spinning).
    /// </summary>
    void UpdateArm(Transform shoulder, Transform upperArm, Transform foreArm,
                   Transform hand, Transform target, Vector3 posOffset, Vector3 rotOffset)
    {
        if (hand == null || target == null || upperArm == null || foreArm == null) return;

        // World-space target (offset applied in controller's local space)
        Vector3    targetPos = target.TransformPoint(posOffset);
        Quaternion targetRot = target.rotation * Quaternion.Euler(rotOffset);

        // Device/LOCAL tracking: the controllers (siblings of Camera Offset) report Y relative to
        // the HMD start → underground. Lift the IK target by CameraYOffset (= Camera Offset localY)
        // so Jen's arms reach the controllers in world space instead of through the floor.
        if (cameraOffsetParent != null)
            targetPos.y += cameraOffsetParent.localPosition.y;

        float upperArmLength = Vector3.Distance(upperArm.position, foreArm.position);
        float foreArmLength  = Vector3.Distance(foreArm.position,  hand.position);
        float totalArmLength = upperArmLength + foreArmLength;

        Vector3 shoulderToTarget = targetPos - upperArm.position;
        float   distanceToTarget = shoulderToTarget.magnitude;

        // Guard against zero-length vector (controller exactly at shoulder pivot)
        if (distanceToTarget < 0.001f)
        {
            hand.rotation = targetRot;
            return;
        }

        // Clamp target to maximum arm reach
        if (distanceToTarget > totalArmLength * 0.999f)
        {
            distanceToTarget  = totalArmLength * 0.999f;
            shoulderToTarget  = shoulderToTarget.normalized * distanceToTarget;
            targetPos         = upperArm.position + shoulderToTarget;
        }

        // Law of cosines: angle at the shoulder between (shoulder→target) and (shoulder→elbow)
        float cosAngle = Mathf.Clamp(
            (upperArmLength * upperArmLength + distanceToTarget * distanceToTarget
             - foreArmLength * foreArmLength)
            / (2f * upperArmLength * distanceToTarget),
            -1f, 1f
        );
        float elbowAngle = Mathf.Acos(cosAngle) * Mathf.Rad2Deg;

        Vector3 armDir = shoulderToTarget.normalized;

        Vector3 elbowHintDir = (shoulder == leftShoulder)
            ? (-transform.right * 0.8f + Vector3.down * 0.6f).normalized
            : ( transform.right * 0.8f + Vector3.down * 0.6f).normalized;
        Vector3 bendAxis = Vector3.Cross(armDir, elbowHintDir).normalized;
        if (bendAxis.sqrMagnitude < 0.001f)
            bendAxis = (shoulder == leftShoulder) ? -transform.right : transform.right;

        // ── Upper Arm ───────────────────────────────────────────────────────────
        // Read the bone's current natural direction (toward foreArm, set by Animator).
        // Rotate FROM that direction TO armDir, then tilt by elbowAngle to create the bend.
        Vector3    upperNatural  = (foreArm.position - upperArm.position).normalized;
        Quaternion alignToTarget = Quaternion.FromToRotation(upperNatural, armDir);
        upperArm.rotation = Quaternion.AngleAxis(elbowAngle, bendAxis)
                            * (alignToTarget * upperArm.rotation);

        // ── Forearm ─────────────────────────────────────────────────────────────
        // Read positions AFTER the upper arm was repositioned (foreArm.position updated).
        Vector3 foreNatural   = (hand.position - foreArm.position).normalized;
        Vector3 foreToTarget  = targetPos - foreArm.position;
        if (foreToTarget.sqrMagnitude > 0.001f)
        {
            Quaternion alignFore = Quaternion.FromToRotation(foreNatural, foreToTarget.normalized);
            foreArm.rotation = alignFore * foreArm.rotation;
        }

        // ── Hand ────────────────────────────────────────────────────────────────
        // Match the controller rotation exactly (plus any configured offset).
        hand.rotation = targetRot;
    }

    Transform FindBoneRecursive(Transform parent, string boneName)
    {
        if (parent.name == boneName) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindBoneRecursive(parent.GetChild(i), boneName);
            if (result != null) return result;
        }
        return null;
    }

    void HideFromMainCamera(string objectName)
    {
        Transform bone = FindBoneRecursive(transform, objectName);
        if (bone == null) return;
        foreach (Renderer r in bone.GetComponentsInChildren<Renderer>())
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
    }

    float NormalizeAngle(float angle)
    {
        if (angle > 180f) angle -= 360f;
        return angle;
    }
}
