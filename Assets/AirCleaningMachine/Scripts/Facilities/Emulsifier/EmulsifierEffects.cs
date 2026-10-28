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
    [SerializeField] private bool playOnStart = true;

    public bool IsPlaying { get; private set; }

    public event System.Action StateChanged;

    private void Start()
    {
        if (playOnStart) Play();
    }

    public void Play()
    {
        foreach (var smoke in smokes)
            if (smoke != null) smoke.Play();

        foreach (var fluid in fluids)
        {
            if (fluid == null) continue;
            fluid.extrapolationMode = DirectorWrapMode.Loop;
            fluid.Play();
        }

        foreach (var spawner in dropSpawners)
            if (spawner != null) spawner.startCor();

        IsPlaying = true;
        StateChanged?.Invoke();
    }

    public void Stop()
    {
        foreach (var smoke in smokes)
            if (smoke != null) smoke.Stop();

        foreach (var fluid in fluids)
            if (fluid != null) fluid.Stop();

        foreach (var spawner in dropSpawners)
            if (spawner != null) spawner.stopCor();

        IsPlaying = false;
        StateChanged?.Invoke();
    }

    public void Toggle()
    {
        if (IsPlaying) Stop();
        else Play();
    }
}
