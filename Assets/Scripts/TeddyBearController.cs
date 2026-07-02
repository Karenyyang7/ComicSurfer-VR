using System.Collections;
using UnityEngine;

/// <summary>
/// Manages the teddy bear's state across all phases.
/// Poses are swapped by toggling poseNormal / poseRaised child GameObjects.
/// Pointing rotates the whole bear so the hand side faces the target.
/// </summary>
public class TeddyBearController : MonoBehaviour
{
    public enum TeddyState { Idle, EyesTransitioning, Glowing, Pointing }
    public TeddyState currentState = TeddyState.Idle;

    [Header("=== Poses ===")]
    [Tooltip("Child GameObject with the normal standing bear mesh")]
    public GameObject poseNormal;
    [Tooltip("Child GameObject with the arm-raised bear mesh")]
    public GameObject poseRaised;

    [Header("=== Pointing ===")]
    [Tooltip("Seconds to smoothly rotate into pointing pose")]
    public float pointingRotateDuration = 1.2f;
    [Tooltip("Yaw offset (degrees) so the hand SIDE faces the target. " +
             "Tune this in Play mode — try -90 or 90 depending on which side the arm is on.")]
    public float handYawOffset = -90f;

    [Header("=== Materials ===")]
    [Tooltip("The SkinnedMeshRenderer that contains the eyes material")]
    public Renderer eyeRenderer;
    [Tooltip("Which material slot index is the eyes (check the renderer's Materials list in Inspector)")]
    public int eyeMaterialIndex = 3;
    [Tooltip("Body renderer (for gold emission glow)")]
    public Renderer bodyRenderer;

    [Header("=== Lighting ===")]
    [Tooltip("Child Point Light for gold glow")]
    public Light goldPointLight;

    [Header("=== Colors ===")]
    public Color eyeColorStart = new Color(0.4f, 0.2f, 0.05f);
    public Color eyeColorEnd   = new Color(0.2f, 1f,  0.3f);

    void Awake()
    {
        if (goldPointLight != null)
        {
            goldPointLight.intensity = 0f;
            goldPointLight.enabled = false;
        }

        // Start in normal pose
        SetPoseNormal();
    }

    // ── Pose switching ────────────────────────────────────────────────────────

    public void SetPoseNormal()
    {
        if (poseNormal != null) poseNormal.SetActive(true);
        if (poseRaised  != null) poseRaised.SetActive(false);
    }

    public void SetPoseRaised()
    {
        if (poseNormal != null) poseNormal.SetActive(false);
        if (poseRaised  != null) poseRaised.SetActive(true);
    }

    // ── Eye transition ────────────────────────────────────────────────────────

    public void StartEyeTransition(float duration)
    {
        currentState = TeddyState.EyesTransitioning;
        StartCoroutine(LerpEyeColor(duration));
    }

    /// <summary>
    /// Renderers of BOTH pose meshes. The two poses are separate meshes — applying eye/glow
    /// changes only to eyeRenderer/bodyRenderer (which point at ONE pose) made the effect
    /// invisible whenever the other pose was active.
    /// </summary>
    System.Collections.Generic.List<Renderer> AllPoseRenderers()
    {
        var list = new System.Collections.Generic.List<Renderer>();
        if (poseNormal != null) list.AddRange(poseNormal.GetComponentsInChildren<Renderer>(true));
        if (poseRaised != null) list.AddRange(poseRaised.GetComponentsInChildren<Renderer>(true));
        if (list.Count == 0 && eyeRenderer != null) list.Add(eyeRenderer);
        return list;
    }

    IEnumerator LerpEyeColor(float duration)
    {
        var renderers = AllPoseRenderers();
        if (renderers.Count == 0) yield break;

        // Use materials[] (not material) to target a specific slot on a multi-material renderer.
        // This creates instanced copies of all slots — intentional, we own these renderers.
        var eyeMats = new System.Collections.Generic.List<Material>();
        foreach (var r in renderers)
        {
            var mats = r.materials;
            int idx = Mathf.Clamp(eyeMaterialIndex, 0, mats.Length - 1);
            eyeMats.Add(mats[idx]);
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            Color c = Color.Lerp(eyeColorStart, eyeColorEnd, t / duration);
            foreach (var m in eyeMats) { m.color = c; if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); }
            yield return null;
        }
        foreach (var m in eyeMats) { m.color = eyeColorEnd; if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", eyeColorEnd); }
        Debug.Log("[TeddyBearController] Eye transition complete.");
    }

    // ── Gold glow ─────────────────────────────────────────────────────────────

    public void StartGoldGlow()
    {
        currentState = TeddyState.Glowing;

        // Whole-body gold glow on BOTH pose meshes (emission on every material slot)
        foreach (var r in AllPoseRenderers())
        {
            foreach (var m in r.materials)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(1f, 0.8f, 0.1f) * 0.45f); // 1.2 was a solid yellow blob - keep the fur readable
            }
        }

        if (goldPointLight != null)
        {
            goldPointLight.enabled = true;
            StartCoroutine(RampLight(0f, 1.5f, 1f));
        }
    }

    IEnumerator RampLight(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            if (goldPointLight != null)
                goldPointLight.intensity = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
    }

    // ── Pointing ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Switches to the raised-arm pose and smoothly rotates the bear so
    /// the hand side faces <paramref name="target"/>. handYawOffset corrects
    /// for which side the arm is on (tune in Inspector).
    /// </summary>
    public void StartPointing(Transform target)
    {
        currentState = TeddyState.Pointing;
        SetPoseRaised();
        StartCoroutine(RotateToward(target));
    }

    IEnumerator RotateToward(Transform target)
    {
        Quaternion startRot = transform.rotation;
        float elapsed = 0f;

        while (elapsed < pointingRotateDuration)
        {
            elapsed += Time.deltaTime;

            if (target != null)
            {
                Vector3 dir = target.position - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                {
                    // LookRotation makes +Z face the target.
                    // handYawOffset rotates so the arm side faces it instead.
                    Quaternion faceTarget = Quaternion.LookRotation(dir.normalized)
                                           * Quaternion.Euler(0f, handYawOffset, 0f);
                    transform.rotation = Quaternion.Slerp(startRot, faceTarget,
                                                          elapsed / pointingRotateDuration);
                }
            }

            yield return null;
        }
    }

    // ── Attach to hand ────────────────────────────────────────────────────────

    public void AttachToHand(Transform hand)
    {
        if (hand == null)
        {
            Debug.LogWarning("[TeddyBearController] AttachToHand called with null hand transform.");
            return;
        }
        transform.SetParent(hand);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }
}
