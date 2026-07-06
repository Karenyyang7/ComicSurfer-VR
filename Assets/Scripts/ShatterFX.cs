using System.Collections;
using UnityEngine;

/// <summary>
/// Dramatic, readable shatter: spawns spinning shard quads that explode outward with
/// gravity and fade over `duration`, plus a bright flash light. Far more visible than
/// a brief particle puff — built after VR feedback that shatters were blink-and-miss.
/// </summary>
public class ShatterFX : MonoBehaviour
{
    /// <param name="tint">Base color of the shards (sample the frame's art tone)</param>
    public static void Burst(Vector3 center, Color tint, int shardCount = 26, float force = 2.6f, float duration = 2.6f, float shardSize = 0.09f)
    {
        var go = new GameObject("ShatterFX");
        go.transform.position = center;
        var fx = go.AddComponent<ShatterFX>();
        fx.StartCoroutine(fx.Run(tint, shardCount, force, duration, shardSize));
    }

    static Material _shardMat;
    static Material ShardMat()
    {
        if (_shardMat == null)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null) s = Shader.Find("Sprites/Default");
            _shardMat = new Material(s) { name = "Shard (runtime)" };
        }
        return _shardMat;
    }

    IEnumerator Run(Color tint, int count, float force, float duration, float shardSize)
    {
        var shards = new Transform[count];
        var vels = new Vector3[count];
        var spins = new Vector3[count];
        var mpb = new MaterialPropertyBlock();

        for (int i = 0; i < count; i++)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(transform, false);
            q.transform.localPosition = Random.insideUnitSphere * 0.25f;
            q.transform.localRotation = Random.rotation;
            float s = shardSize * Random.Range(0.5f, 1.6f);
            q.transform.localScale = new Vector3(s, s * Random.Range(0.6f, 1.4f), 1f);
            var r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = ShardMat();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Color c = tint * Random.Range(0.75f, 1.15f); c.a = 1f;
            mpb.SetColor("_BaseColor", c);
            mpb.SetColor("_Color", c);
            r.SetPropertyBlock(mpb);

            shards[i] = q.transform;
            Vector3 dir = (q.transform.localPosition.normalized + Random.insideUnitSphere * 0.5f).normalized;
            vels[i] = dir * force * Random.Range(0.5f, 1.3f);
            spins[i] = Random.insideUnitSphere * 420f;
        }

        // flash
        var flash = new GameObject("Flash").AddComponent<Light>();
        flash.transform.SetParent(transform, false);
        flash.type = LightType.Point;
        flash.color = new Color(1f, 0.95f, 0.85f);
        flash.range = 5f;
        flash.intensity = 3f;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = t / duration;
            for (int i = 0; i < count; i++)
            {
                if (shards[i] == null) continue;
                vels[i] += Vector3.down * 1.6f * Time.deltaTime;   // gentle gravity
                vels[i] *= 1f - 0.4f * Time.deltaTime;             // drag
                shards[i].localPosition += vels[i] * Time.deltaTime;
                shards[i].localRotation = Quaternion.Euler(spins[i] * Time.deltaTime) * shards[i].localRotation;
                float fade = Mathf.Clamp01(1.6f - 1.6f * k);       // hold, then fade
                shards[i].localScale = Vector3.Lerp(shards[i].localScale, shards[i].localScale * 0.97f, Time.deltaTime * 2f);
                var r = shards[i].GetComponent<MeshRenderer>();
                r.GetPropertyBlock(_sharedMpb);
                Color c = _sharedMpb.GetColor("_BaseColor"); c.a = fade;
                _sharedMpb.SetColor("_BaseColor", c);
                _sharedMpb.SetColor("_Color", c);
                r.SetPropertyBlock(_sharedMpb);
            }
            flash.intensity = Mathf.Lerp(3f, 0f, Mathf.Min(1f, k * 2.2f));
            yield return null;
        }
        Destroy(gameObject);
    }

    static readonly MaterialPropertyBlock _sharedMpb = new MaterialPropertyBlock();
}
