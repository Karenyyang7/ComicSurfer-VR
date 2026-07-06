using System.Collections;
using UnityEngine;

/// <summary>
/// A streak of green/gold light that flies from a point to a (moving) target — used when
/// the ordering puzzle is solved: the green light gathers and FLIES INTO THE TEDDY BEAR,
/// kicking off the eye transition. Spawn via LightComet.Fly(...).
/// </summary>
public class LightComet : MonoBehaviour
{
    public static void Fly(Vector3 from, Transform target, float duration, System.Action onArrive)
    {
        var go = new GameObject("LightComet");
        go.transform.position = from;
        var comet = go.AddComponent<LightComet>();
        comet._target = target;
        comet._duration = Mathf.Max(0.2f, duration);
        comet._onArrive = onArrive;
    }

    private Transform _target;
    private float _duration;
    private System.Action _onArrive;

    IEnumerator Start()
    {
        // trail particles
        var ps = gameObject.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 0.9f;
        main.startSpeed = 0.05f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.10f);
        var grad = new ParticleSystem.MinMaxGradient(
            new Color(0.35f, 1f, 0.55f), new Color(1f, 0.9f, 0.4f));
        grad.mode = ParticleSystemGradientMode.TwoColors;
        main.startColor = grad;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // particles trail behind
        main.maxParticles = 400;
        var em = ps.emission;
        em.rateOverTime = 160f;
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.06f;
        ParticleMaterialUtil.Apply(ps);

        // small green light traveling with the comet
        var l = gameObject.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(0.5f, 1f, 0.6f);
        l.intensity = 1.6f;
        l.range = 4f;

        SfxPlayer.Play("comet_fly", transform.position);
        Vector3 start = transform.position;
        Vector3 arcUp = Vector3.up * Mathf.Max(1.2f, Vector3.Distance(start, _target != null ? _target.position : start) * 0.25f);
        float e = 0f;
        while (e < _duration)
        {
            e += Time.deltaTime;
            float k = Mathf.Clamp01(e / _duration);
            Vector3 dest = _target != null ? _target.position : start;
            // quadratic bezier arc: start -> high mid -> target (target may move — re-eval each frame)
            Vector3 mid = (start + dest) * 0.5f + arcUp;
            Vector3 a = Vector3.Lerp(start, mid, k);
            Vector3 b = Vector3.Lerp(mid, dest, k);
            transform.position = Vector3.Lerp(a, b, k);
            yield return null;
        }

        SfxPlayer.Play("comet_arrive", transform.position);
        _onArrive?.Invoke();

        // linger + fade
        em.rateOverTime = 0f;
        float f = 0f;
        while (f < 1f)
        {
            f += Time.deltaTime;
            l.intensity = Mathf.Lerp(1.6f, 0f, f);
            yield return null;
        }
        Destroy(gameObject);
    }
}
