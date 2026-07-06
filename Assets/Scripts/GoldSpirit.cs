using System.Collections;
using UnityEngine;

/// <summary>
/// Particle-based Harriet spirit effect. Rises up then travels into the teddy bear.
/// No 3D model — pure particles.
/// </summary>
public class GoldSpirit : MonoBehaviour
{
    [Header("=== References ===")]
    public ParticleSystem spiritParticles;

    public event System.Action OnSpiritEntered;

    // NOTE: no SetActive(false) in Awake! The spirit starts inactive in the scene, so Awake
    // first runs DURING PlaySpiritRise's SetActive(true) — deactivating here killed the
    // activation and the coroutine (same bug as ThiefComicWorld). The scene keeps it inactive.

    public void PlaySpiritRise(Vector3 startPos, Transform teddyTarget)
    {
        gameObject.SetActive(true);
        transform.position = startPos;
        StartCoroutine(SpiritRoutine(startPos, teddyTarget));
    }

    IEnumerator SpiritRoutine(Vector3 startPos, Transform teddyTarget)
    {
        if (spiritParticles != null)
            spiritParticles.Play();
        SfxPlayer.Play("spirit_rise", transform.position);

        // Phase 1: rise upward over 2 seconds
        float riseDuration = 2f;
        float elapsed = 0f;
        Vector3 riseEnd = startPos + Vector3.up * 1.5f;

        while (elapsed < riseDuration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, riseEnd, elapsed / riseDuration);
            yield return null;
        }

        // Phase 2: move toward teddy target over 1 second
        float moveDuration = 1f;
        elapsed = 0f;
        Vector3 moveStart = transform.position;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            Vector3 dest = teddyTarget != null ? teddyTarget.position : riseEnd;
            transform.position = Vector3.Lerp(moveStart, dest, elapsed / moveDuration);
            yield return null;
        }

        // Entered the teddy bear
        if (spiritParticles != null)
            spiritParticles.Stop();

        Debug.Log("[GoldSpirit] Spirit entered teddy bear.");
        OnSpiritEntered?.Invoke();

        yield return new WaitForSeconds(0.5f);
        gameObject.SetActive(false);

        if (ComicWorldManager.Instance != null)
            ComicWorldManager.Instance.StartPhase6();
    }
}
