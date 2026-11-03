using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Предмет на стойке СИЗ. С XRGrabInteractable его берут рукой и подносят к голове: рядом с нужной точкой предмет
/// надевается сам, а отпущенный раньше возвращается на стол. Без XRGrabInteractable он надевается и снимается
/// нажатием луча, как перчатки: их модель видна на руках, поэтому на голову подносить нечего.
/// </summary>
[RequireComponent(typeof(InspectableNode))]
public class PpeItem : MonoBehaviour
{
    [Tooltip("Модель на столе.")]
    [SerializeField] private GameObject model;
    [Tooltip("Модель на игроке: лежит внутри камеры и включается, когда предмет надет.")]
    [SerializeField] private GameObject wornVisual;

    [Header("Надевание рукой")]
    [Tooltip("Куда поднести предмет: точка относительно камеры, м.")]
    [SerializeField] private Vector3 wearPoint = new Vector3(0f, 0.1f, 0f);
    [Tooltip("На каком расстоянии от этой точки предмет надевается, м.")]
    [SerializeField] private float wearDistance = 0.2f;
    [Tooltip("За сколько секунд отпущенный предмет возвращается на стол.")]
    [SerializeField] private float returnTime = 0.25f;

    public bool Worn { get; private set; }

    public event Action Changed;

    private InspectableNode _node;
    private XRGrabInteractable _grab;
    private BoxCollider _collider;
    private Transform _head;
    private Pose _home;
    private Pose _releasedAt;
    private float _returnShare;
    private bool _returning;

    private void Awake()
    {
        _node = GetComponent<InspectableNode>();
        _grab = GetComponent<XRGrabInteractable>();
        _collider = GetComponent<BoxCollider>();
        _head = Camera.main.transform;
        _home = new Pose(transform.position, transform.rotation);

        if (_grab != null) _grab.selectExited.AddListener(OnReleased);
        else _node.Pressed += Toggle;
        Apply();
    }

    private void OnDestroy()
    {
        if (_grab != null) _grab.selectExited.RemoveListener(OnReleased);
        else _node.Pressed -= Toggle;
    }

    private void Update()
    {
        if (_grab == null || Worn) return;

        if (_grab.isSelected)
        {
            var center = transform.TransformPoint(_collider.center);
            if (Vector3.Distance(center, _head.TransformPoint(wearPoint)) <= wearDistance) SetWorn(true);
            return;
        }

        if (!_returning) return;
        _returnShare = Mathf.Min(1f, _returnShare + Time.deltaTime / Mathf.Max(returnTime, 0.01f));
        float share = Mathf.SmoothStep(0f, 1f, _returnShare);
        transform.SetPositionAndRotation(Vector3.Lerp(_releasedAt.position, _home.position, share),
                                         Quaternion.Slerp(_releasedAt.rotation, _home.rotation, share));
        if (_returnShare >= 1f) _returning = false;
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        if (Worn) return;
        _releasedAt = new Pose(transform.position, transform.rotation);
        _returnShare = 0f;
        _returning = true;
    }

    private void Toggle() => SetWorn(!Worn);

    public void SetWorn(bool worn)
    {
        if (worn == Worn) return;
        Worn = worn;

        if (_grab != null && _grab.isSelected)
        {
            if (worn && _grab.firstInteractorSelecting is XRBaseInputInteractor hand) hand.SendHapticImpulse(0.5f, 0.1f);
            _grab.interactionManager.CancelInteractableSelection((UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)_grab);
        }

        Apply();
        Changed?.Invoke();
    }

    private void Apply()
    {
        model.SetActive(!Worn);
        if (wornVisual != null) wornVisual.SetActive(Worn);

        if (_grab == null)
        {
            _node.SetHint(Worn ? "card.hint.remove" : "card.hint.wear");
            return;
        }

        // Надетый предмет снимается только сбросом сценария: под рукой у лица он мешал бы брать другие вещи.
        _grab.enabled = !Worn;
        _collider.enabled = !Worn;
        _returning = false;
        transform.SetPositionAndRotation(_home.position, _home.rotation);
    }
}
