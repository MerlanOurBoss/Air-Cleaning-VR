using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Узел установки с подписью: под лучом контроллера или руки вокруг него появляется рамка, а карточка
/// NodeLabelCard показывает подсказку. Курок, хват или сведение пальцев открывают пояснение,
/// повторное нажатие на этот же узел скрывает его, нажатие на другой — переключает.
/// Коллайдер узла лежит на слое Inspectable, который ни с чем не сталкивается и виден только лучам.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class InspectableNode : MonoBehaviour
{
    private const float FrameWidth = 0.012f;

    [Tooltip("Ключ названия в таблице локализации.")]
    [SerializeField] private string title;
    [Tooltip("Ключ описания в таблице локализации.")]
    [SerializeField] private string description;
    [Tooltip("Ключ подсказки под названием.")]
    [SerializeField] private string hint = "card.hint";
    [Tooltip("Нажатие выполняет действие (событие Pressed), а не открывает описание.")]
    [SerializeField] private bool actionOnly;

    public string Title => L10n.Get(title);
    public string Description => L10n.Get(description);
    public string Hint => L10n.Get(hint);

    public event System.Action Pressed;

    public void SetHint(string key)
    {
        hint = key;
        if (_card != null) _card.Invalidate();
    }

    public Bounds Bounds => _collider.bounds;

    private XRBaseInteractable _interactable;
    private BoxCollider _collider;
    private NodeLabelCard _card;
    private LineRenderer _frame;

    private void Awake()
    {
        _interactable = GetComponent<XRBaseInteractable>();
        _collider = GetComponent<BoxCollider>();
    }

    private void Start()
    {
        _card = FindAnyObjectByType<NodeLabelCard>(FindObjectsInactive.Include);
        BuildFrame();
    }

    private void OnEnable()
    {
        _interactable.hoverEntered.AddListener(OnHoverEntered);
        _interactable.hoverExited.AddListener(OnHoverExited);
        _interactable.selectEntered.AddListener(OnSelectEntered);
    }

    private void OnDisable()
    {
        _interactable.hoverEntered.RemoveListener(OnHoverEntered);
        _interactable.hoverExited.RemoveListener(OnHoverExited);
        _interactable.selectEntered.RemoveListener(OnSelectEntered);
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        // До Start луч ещё не мог навестись осознанно: первый кадр сцены пропускаем.
        if (_frame == null || _interactable.isSelected) return;
        _frame.enabled = true;
        _card.HoverEnter(this);
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        if (_frame == null || _interactable.isHovered) return;
        _frame.enabled = false;
        _card.HoverExit(this);
    }

    // Предмет взяли в руку: рамка и подсказка у него перед глазами только мешают.
    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (_frame == null || !actionOnly) return;
        _frame.enabled = false;
        _card.HoverExit(this);
    }

    // Читаем кнопки напрямую: выбор в XRI срабатывает только от хвата, а за планшетом игрок привык к курку.
    private void Update()
    {
        foreach (var hovering in _interactable.interactorsHovering)
        {
            if (hovering is NearFarInteractor interactor &&
                (interactor.selectInput.ReadWasPerformedThisFrame() || interactor.uiPressInput.ReadWasPerformedThisFrame()))
            {
                if (actionOnly) Pressed?.Invoke();
                else _card.Toggle(this);
                return;
            }
        }
    }

    // Одна линия обходит все 12 рёбер коробки коллайдера и едет вместе с узлом при смене размера.
    private void BuildFrame()
    {
        var c = _collider.center;
        var e = _collider.size * 0.5f;
        var a = c + new Vector3(-e.x, -e.y, -e.z);
        var b = c + new Vector3(e.x, -e.y, -e.z);
        var d = c + new Vector3(-e.x, -e.y, e.z);
        var f = c + new Vector3(e.x, -e.y, e.z);
        var g = c + new Vector3(-e.x, e.y, -e.z);
        var h = c + new Vector3(e.x, e.y, -e.z);
        var i = c + new Vector3(-e.x, e.y, e.z);
        var j = c + new Vector3(e.x, e.y, e.z);

        _frame = gameObject.AddComponent<LineRenderer>();
        _frame.useWorldSpace = false;
        _frame.loop = false;
        _frame.widthMultiplier = FrameWidth;
        _frame.sharedMaterial = _card.FrameMaterial;
        _frame.shadowCastingMode = ShadowCastingMode.Off;
        _frame.receiveShadows = false;
        _frame.lightProbeUsage = LightProbeUsage.Off;
        _frame.reflectionProbeUsage = ReflectionProbeUsage.Off;
        var path = new[] { a, b, f, d, a, g, h, b, h, j, f, j, i, d, i, g };
        _frame.positionCount = path.Length;
        _frame.SetPositions(path);
        _frame.enabled = _interactable.isHovered;
        if (_frame.enabled) _card.HoverEnter(this);
    }
}
