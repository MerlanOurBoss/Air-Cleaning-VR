using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Прозрачный корпус колонны: плавно растворяет корпус, чтобы было видно, что происходит внутри.
/// Прозрачный материал стоит только на время показа и перехода, в обычном виде корпус непрозрачный.
/// </summary>
public class EmulsifierXRay : MonoBehaviour
{
    [Tooltip("Части корпуса, которые становятся прозрачными.")]
    [SerializeField] private Renderer[] shell;
    [Tooltip("Прозрачная копия материала корпуса. Очередь отрисовки ниже дыма и воды, чтобы они не мерцали за корпусом.")]
    [SerializeField] private Material transparentMaterial;
    [SerializeField, Range(0f, 1f)] private float alpha = 0.15f;
    [Tooltip("Время перехода, с.")]
    [SerializeField] private float fadeTime = 0.8f;

    public bool IsOn { get; private set; }

    public event Action Changed;

    private Material[] _opaque;
    private ShadowCastingMode[] _shadows;
    // Общая runtime-копия: ассет материала не меняется.
    private Material _fade;
    private float _blend;

    private void Awake()
    {
        _opaque = new Material[shell.Length];
        _shadows = new ShadowCastingMode[shell.Length];
        for (int i = 0; i < shell.Length; i++)
        {
            _opaque[i] = shell[i].sharedMaterial;
            _shadows[i] = shell[i].shadowCastingMode;
        }
        _fade = new Material(transparentMaterial);
    }

    private void OnDestroy()
    {
        Destroy(_fade);
    }

    private void Update()
    {
        float target = IsOn ? 1f : 0f;
        if (Mathf.Approximately(_blend, target)) return;

        _blend = Mathf.MoveTowards(_blend, target, Time.deltaTime / Mathf.Max(fadeTime, 0.01f));
        Apply();
    }

    public void Toggle()
    {
        IsOn = !IsOn;
        Changed?.Invoke();
    }

    private void Apply()
    {
        var color = _fade.color;
        color.a = Mathf.Lerp(1f, alpha, Mathf.SmoothStep(0f, 1f, _blend));
        _fade.color = color;

        bool solid = _blend <= 0f;
        for (int i = 0; i < shell.Length; i++)
        {
            shell[i].sharedMaterial = solid ? _opaque[i] : _fade;
            // Тень от прозрачного корпуса затемняла бы всё внутри.
            shell[i].shadowCastingMode = solid ? _shadows[i] : ShadowCastingMode.Off;
        }
    }
}
