using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Взрыв колонны: корпус исчезает и разлетается обломками, крышку подбрасывает, вспышка, огонь и дым.
/// Камера намеренно не трясётся — в шлеме от этого укачивает; удар передаётся звуком и вибрацией контроллеров.
/// </summary>
public class EmulsifierExplosion : MonoBehaviour
{
    [SerializeField] private EmulsifierFault fault;
    [Tooltip("Корпус колонны, из его размера рассчитываются обломки.")]
    [SerializeField] private Renderer body;
    [Tooltip("Крышка: взлетает целиком.")]
    [SerializeField] private Renderer lid;
    [SerializeField] private Material shardMaterial;
    [SerializeField] private int shardCount = 36;
    [SerializeField] private float shardSpeedMin = 5f;
    [SerializeField] private float shardSpeedMax = 13f;
    [SerializeField] private float lidSpeed = 16f;

    [Tooltip("Эффекты однократного взрыва.")]
    [SerializeField] private ParticleSystem[] burst;
    [Tooltip("Огонь и дым, которые продолжаются до восстановления.")]
    [SerializeField] private ParticleSystem[] lingering;
    [SerializeField] private Light flash;
    [SerializeField] private float flashIntensity = 400f;
    [SerializeField] private float flashTime = 0.7f;
    [SerializeField] private AudioSource boom;

    private readonly List<GameObject> _debris = new List<GameObject>();
    private CharacterController _player;
    private float _flashLeft;

    private void Awake()
    {
        _player = FindAnyObjectByType<CharacterController>();
        flash.enabled = false;
        fault.Exploded += Explode;
        fault.Restored += Restore;
    }

    private void OnDestroy()
    {
        fault.Exploded -= Explode;
        fault.Restored -= Restore;
    }

    private void Update()
    {
        if (_flashLeft <= 0f) return;

        _flashLeft -= Time.deltaTime;
        float share = Mathf.Clamp01(_flashLeft / flashTime);
        flash.intensity = flashIntensity * share * share;
        if (_flashLeft <= 0f) flash.enabled = false;
    }

    private void Explode()
    {
        var bounds = body.bounds;
        transform.position = bounds.center;

        SpawnShards(bounds);
        SpawnLid();
        body.enabled = false;
        lid.enabled = false;

        foreach (var system in burst) system.Play();
        foreach (var system in lingering) system.Play();

        flash.enabled = true;
        flash.intensity = flashIntensity;
        _flashLeft = flashTime;
        boom.Play();

        InputDevices.GetDeviceAtXRNode(XRNode.LeftHand).SendHapticImpulse(0, 1f, 0.8f);
        InputDevices.GetDeviceAtXRNode(XRNode.RightHand).SendHapticImpulse(0, 1f, 0.8f);
    }

    private void SpawnShards(Bounds bounds)
    {
        float radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
        float scale = radius / 0.5f;  // обломки растут вместе с аппаратом

        for (int i = 0; i < shardCount; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            float height = Random.Range(-bounds.extents.y, bounds.extents.y);

            var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.name = "Shard";
            shard.transform.SetPositionAndRotation(bounds.center + radial * radius + Vector3.up * height,
                                                   Quaternion.LookRotation(radial));
            shard.transform.localScale = new Vector3(Random.Range(0.12f, 0.45f), Random.Range(0.12f, 0.55f), 0.02f) * scale;
            shard.GetComponent<Renderer>().sharedMaterial = shardMaterial;

            var velocity = radial * Random.Range(shardSpeedMin, shardSpeedMax) + Vector3.up * Random.Range(1f, 7f);
            Launch(shard, velocity, 8f);
        }
    }

    private void SpawnLid()
    {
        var copy = new GameObject("Lid");
        copy.transform.SetPositionAndRotation(lid.transform.position, lid.transform.rotation);
        copy.transform.localScale = lid.transform.lossyScale;
        copy.AddComponent<MeshFilter>().sharedMesh = lid.GetComponent<MeshFilter>().sharedMesh;
        copy.AddComponent<MeshRenderer>().sharedMaterial = lid.sharedMaterial;
        copy.AddComponent<BoxCollider>();

        var sideways = Random.insideUnitCircle * 3f;
        Launch(copy, new Vector3(sideways.x, lidSpeed, sideways.y), 60f);
    }

    private void Launch(GameObject piece, Vector3 velocity, float mass)
    {
        var rigidbody = piece.AddComponent<Rigidbody>();
        rigidbody.mass = mass;
        // Физика идёт с шагом 50 Гц, а шлем показывает 72–90: без сглаживания обломки дёргаются.
        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        rigidbody.linearVelocity = velocity;
        rigidbody.angularVelocity = Random.insideUnitSphere * 12f;

        // Обломки не толкают игрока и не застревают в его капсуле.
        if (_player != null) Physics.IgnoreCollision(piece.GetComponent<Collider>(), _player);
        _debris.Add(piece);
    }

    private void Restore()
    {
        foreach (var piece in _debris) Destroy(piece);
        _debris.Clear();

        foreach (var system in burst) system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        foreach (var system in lingering) system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        flash.enabled = false;
        _flashLeft = 0f;
        boom.Stop();
        body.enabled = true;
        lid.enabled = true;
    }
}
