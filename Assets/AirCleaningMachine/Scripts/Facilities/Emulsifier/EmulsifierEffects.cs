using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// VR-замена GeneralManagerForEmul: без матмодуля и UI, просто запускает
/// дым, капли и Alembic-жидкость эмульгатора при старте сцены.
/// </summary>
public class EmulsifierEffects : MonoBehaviour
{
    [SerializeField] private ParticleSystem[] smokes;
    [SerializeField] private PlayableDirector[] fluids;
    [SerializeField] private DropSpawner[] dropSpawners;
    [SerializeField] private ParticleSystem[] bubbles;
    [SerializeField] private bool playOnStart = true;

    public bool IsPlaying { get; private set; }

    public event System.Action StateChanged;

    private float _waterShare = 1f;

    private void Start()
    {
        if (playOnStart) Play();
    }

    public void Play()
    {
        foreach (var smoke in smokes)
            if (smoke != null) smoke.Play();

        IsPlaying = true;
        ApplyWater();
        StateChanged?.Invoke();
    }

    public void Stop()
    {
        foreach (var smoke in smokes)
            if (smoke != null) smoke.Stop();

        IsPlaying = false;
        ApplyWater();
        StateChanged?.Invoke();
    }

    public void Toggle()
    {
        if (IsPlaying) Stop();
        else Play();
    }

    /// <summary>Доля подачи воды 0–1: при нехватке жидкости форсунки пересыхают по одной, газ продолжает идти.</summary>
    public void SetWaterShare(float share)
    {
        _waterShare = Mathf.Clamp01(share);
        ApplyWater();
    }

    private void ApplyWater()
    {
        float share = IsPlaying ? _waterShare : 0f;

        int activeFluids = Mathf.CeilToInt(share * fluids.Length);
        for (int i = 0; i < fluids.Length; i++)
        {
            var fluid = fluids[i];
            if (fluid == null) continue;

            bool flowing = i < activeFluids;
            // Остановленный Alembic застывает на первом кадре комком над форсункой — прячем его.
            foreach (var stream in fluid.GetComponentsInChildren<MeshRenderer>())
                stream.enabled = flowing;

            if (flowing == (fluid.state == PlayState.Playing)) continue;
            if (flowing)
            {
                fluid.extrapolationMode = DirectorWrapMode.Loop;
                fluid.Play();
            }
            else
            {
                fluid.Stop();
            }
        }

        int activeSpawners = Mathf.CeilToInt(share * dropSpawners.Length);
        for (int i = 0; i < dropSpawners.Length; i++)
        {
            if (dropSpawners[i] == null) continue;
            if (i < activeSpawners) dropSpawners[i].startCor();
            else dropSpawners[i].stopCor();
        }

        foreach (var system in bubbles)
        {
            if (system == null) continue;
            if (share > 0f && !system.isPlaying) system.Play();
            else if (share <= 0f && system.isPlaying) system.Stop();
        }
    }
}
