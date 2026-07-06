using System.Collections;
using UnityEngine;

/// <summary>
/// Controls the Thief character inside ComicWorld.
/// State machine: Hidden → Appearing → Idle → Fleeing → Disappeared.
/// Thief flees when the player gets too close, cycling through waypoints.
/// </summary>
public class ThiefComicWorld : MonoBehaviour
{
    public enum ThiefState { Hidden, Appearing, Idle, Fleeing, Disappeared }
    public ThiefState currentState = ThiefState.Hidden;

    [Header("=== Waypoints ===")]
    public Transform[] waypoints;

    [Header("=== Settings ===")]
    public float fleeDistance = 2f;
    public float moveSpeed = 3f;
    public float idleCheckInterval = 0.3f;

    [Header("=== References ===")]
    [Tooltip("The sparkle particle system child on this thief's eyes")]
    public ParticleSystem eyeSparkles;

    private int _waypointIndex = 0;
    private Transform _player;
    private Animator _animator;

    void Awake()
    {
        // NOTE: do NOT SetActive(false) here. The thief starts inactive in the scene, so
        // Awake first runs DURING Appear()'s SetActive(true) — deactivating here killed the
        // activation and the appear coroutine, leaving the thief permanently hidden.
        _animator = GetComponentInChildren<Animator>();
    }

    void Start()
    {
        // Find player camera as proxy for player position
        var cam = Camera.main;
        if (cam != null) _player = cam.transform;
    }

    public void Appear()
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogWarning("[ThiefComicWorld] TODO: Assign waypoints in Inspector.");
            return;
        }

        gameObject.SetActive(true);
        transform.position = waypoints[0].position;
        _waypointIndex = 0;
        currentState = ThiefState.Appearing;

        if (eyeSparkles != null) eyeSparkles.Play();
        SfxPlayer.Play("thief_appear", transform.position);

        StartCoroutine(AppearRoutine());
    }

    IEnumerator AppearRoutine()
    {
        yield return new WaitForSeconds(1f);
        currentState = ThiefState.Idle;
        StartCoroutine(IdleLoop());
    }

    IEnumerator IdleLoop()
    {
        while (currentState == ThiefState.Idle)
        {
            if (_player != null)
            {
                // Horizontal distance only — the player's HEAD is ~1.7m above the thief's
                // ground-level root, so 3D distance made a 1m-away player read as 2m+.
                float dist = HorizontalDistance(transform.position, _player.position);
                if (dist < fleeDistance)
                {
                    currentState = ThiefState.Fleeing;
                    StartCoroutine(FleeRoutine());
                    yield break;
                }
            }
            yield return new WaitForSeconds(idleCheckInterval);
        }
    }

    IEnumerator FleeRoutine()
    {
        Debug.Log("[ThiefComicWorld] Thief fleeing!");
        SfxPlayer.Play("thief_flee", transform.position);
        if (_animator != null) _animator.SetBool("Running", true);

        while (_waypointIndex < waypoints.Length - 1)
        {
            _waypointIndex++;
            Transform target = waypoints[_waypointIndex];

            FaceTarget(target.position);

            float dist = Vector3.Distance(transform.position, target.position);
            float duration = dist / moveSpeed;
            float elapsed = 0f;
            Vector3 start = transform.position;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(start, target.position, elapsed / duration);
                yield return null;
            }

            transform.position = target.position;

            // Brief pause at each waypoint
            yield return new WaitForSeconds(0.5f);

            // Check if player is still close
            if (_player != null && HorizontalDistance(transform.position, _player.position) > fleeDistance * 2f)
            {
                // Player backed off — go idle at this waypoint
                if (_animator != null) _animator.SetBool("Running", false);
                currentState = ThiefState.Idle;
                StartCoroutine(IdleLoop());
                yield break;
            }
        }

        // Reached final waypoint — disappear into a frame
        if (_animator != null) _animator.SetBool("Running", false);
        currentState = ThiefState.Disappeared;
        Debug.Log("[ThiefComicWorld] Thief disappeared into a frame.");

        if (eyeSparkles != null) eyeSparkles.Stop();
        yield return new WaitForSeconds(0.5f);
        gameObject.SetActive(false);

        if (ComicWorldManager.Instance != null)
            ComicWorldManager.Instance.StartPhase5();
    }

    static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    void FaceTarget(Vector3 target)
    {
        Vector3 dir = target - transform.position;
        dir.y = 0;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir);
    }
}
