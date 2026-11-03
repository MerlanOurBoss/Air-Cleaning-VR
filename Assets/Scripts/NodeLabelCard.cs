using TMPro;
using UnityEngine;

/// <summary>
/// Общая карточка узла. При задержке луча на узле показывает короткую подсказку, по нажатию — название
/// с пояснением, которое остаётся до повторного нажатия. Стоит перед узлом на уровне глаз, смотрит на игрока
/// и увеличивается с расстоянием, чтобы текст читался издалека.
/// </summary>
public class NodeLabelCard : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    [SerializeField] private Transform background;
    [Tooltip("Материал рамки вокруг узла под лучом.")]
    [SerializeField] private Material frameMaterial;
    [Tooltip("Отступ фона от текста, м.")]
    [SerializeField] private Vector2 padding = new Vector2(0.03f, 0.025f);
    [Tooltip("Расстояние, на котором карточка имеет исходный размер, м.")]
    [SerializeField] private float referenceDistance = 1.5f;
    [SerializeField] private float maxScale = 3f;
    [Tooltip("Сколько луч должен задержаться на узле до подсказки, с: при быстром движении луча текст не мелькает.")]
    [SerializeField] private float hintDelay = 0.5f;

    public Material FrameMaterial => frameMaterial;

    private InspectableNode _pinned;
    private InspectableNode _hovered;
    private InspectableNode _shown;
    private bool _shownFull;
    private float _hintAt;
    private Transform _head;
    private ActiveDriver _driver;

    private void Awake()
    {
        _head = Camera.main.transform;
        // Сама карточка выключается, а отсчёт задержки подсказки должен идти и в это время.
        _driver = new GameObject("Node Label Card Driver").AddComponent<ActiveDriver>();
        _driver.Card = this;
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_driver != null) Destroy(_driver.gameObject);
    }

    public void HoverEnter(InspectableNode node)
    {
        _hovered = node;
        _hintAt = Time.time + hintDelay;
    }

    public void HoverExit(InspectableNode node)
    {
        if (node == _hovered) _hovered = null;
    }

    public void Toggle(InspectableNode node)
    {
        _pinned = node == _pinned ? null : node;
        // Только что закрытую карточку не заменяем подсказкой: игрок уже знает, что узел нажимается.
        if (_pinned == null) _hovered = null;
    }

    private void Refresh()
    {
        var node = _pinned;
        bool full = node != null;
        if (node == null && _hovered != null && Time.time >= _hintAt) node = _hovered;

        if (node == null)
        {
            _shown = null;
            if (gameObject.activeSelf) gameObject.SetActive(false);
            return;
        }

        if (node != _shown || full != _shownFull)
        {
            _shown = node;
            _shownFull = full;
            Present(full
                ? "<b>" + node.Title + "</b>\n<size=75%>" + node.Description + "</size>"
                : "<b>" + node.Title + "</b>\n<size=75%>Нажмите, чтобы узнать подробнее</size>");
        }

        Follow(node);
    }

    private void Present(string content)
    {
        // Скрытый текст не считается, поэтому сначала включаем карточку, потом меряем текст для фона.
        gameObject.SetActive(true);
        text.text = content;
        text.ForceMeshUpdate();
        var size = text.GetRenderedValues(false);
        background.localPosition = new Vector3(0f, 0f, 0.002f);
        background.localScale = new Vector3(size.x + padding.x * 2f, size.y + padding.y * 2f, 0.002f);
    }

    private void Follow(InspectableNode node)
    {
        var bounds = node.Bounds;
        var toHead = _head.position - bounds.center;
        toHead.y = 0f;
        toHead = toHead.sqrMagnitude > 1e-4f ? toHead.normalized : Vector3.back;

        // Перед узлом со стороны игрока, по высоте — у глаз, но в пределах самого узла.
        var position = bounds.center + toHead * (Mathf.Max(bounds.extents.x, bounds.extents.z) + 0.15f);
        position.y = Mathf.Clamp(_head.position.y, bounds.min.y, bounds.max.y + 0.2f);
        transform.position = position;

        var look = position - _head.position;
        float distance = look.magnitude;
        if (distance > 1e-3f) transform.rotation = Quaternion.LookRotation(look, Vector3.up);
        transform.localScale = Vector3.one * Mathf.Clamp(distance / referenceDistance, 1f, maxScale);
    }

    private class ActiveDriver : MonoBehaviour
    {
        public NodeLabelCard Card;

        private void LateUpdate()
        {
            if (Card != null) Card.Refresh();
        }
    }
}
