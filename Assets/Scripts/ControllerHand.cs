using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Рука вместо модели контроллера: ставит меш руки по grip-позе контроллера и сгибает пальцы
/// от кнопок. Хват (grip) сжимает средний, безымянный и мизинец; курок — указательный
/// (без касания курка палец вытянут, как при указании); касание стика или кнопок опускает большой.
/// Объект лежит внутри Left/Right Controller, поэтому XRInputModalityManager сам прячет его,
/// когда контроллеры отложены и включается отслеживание рук.
/// </summary>
public class ControllerHand : MonoBehaviour
{
    [SerializeField] private bool isLeft = true;
    [Tooltip("Корень костей руки (L_Wrist / R_Wrist).")]
    [SerializeField] private Transform wrist;
    [Tooltip("Сдвиг ладони от grip-позы в её осях, м. Подбирается в шлеме.")]
    [SerializeField] private Vector3 palmOffset = new Vector3(-0.035f, 0f, -0.02f);
    [Tooltip("Поворот ладони относительно grip-позы, градусы.")]
    [SerializeField] private Vector3 palmRotation = new Vector3(0f, 0f, 90f);
    [Tooltip("Сгиб пальцев без нажатий (0 — прямые, 1 — кулак).")]
    [SerializeField, Range(0f, 1f)] private float restCurl = 0.15f;
    [SerializeField] private float curlSpeed = 20f;

    // Максимальный сгиб суставов поверх позы покоя, градусы: проксимальная, средняя, дистальная фаланги.
    private static readonly float[] FingerAngles = { 70f, 90f, 55f };
    private static readonly float[] ThumbAngles = { 12f, 35f, 50f };
    private static readonly string[] Fingers = { "Index", "Middle", "Ring", "Little" };

    private Transform _palm;
    private Pose _palmInRoot;
    private Transform[][] _joints;     // [finger][joint]; finger 4 — большой
    private Quaternion[][] _bind;
    private float[] _curl = new float[5];
    private float[] _curlTarget = new float[5];

    private InputAction _gripPosition, _gripRotation, _grip, _trigger, _triggerTouch, _thumbTouch;

    public Transform IndexTip { get; private set; }

    private void Awake()
    {
        string p = isLeft ? "L_" : "R_";
        _palm = wrist.Find(p + "Palm");
        IndexTip = FindDeep(wrist, p + "IndexTip");
        _palmInRoot = new Pose(transform.InverseTransformPoint(_palm.position),
                               Quaternion.Inverse(transform.rotation) * _palm.rotation);

        _joints = new Transform[5][];
        _bind = new Quaternion[5][];
        for (int f = 0; f < 4; f++)
            CacheChain(f, p + Fingers[f] + "Proximal", p + Fingers[f] + "Intermediate", p + Fingers[f] + "Distal");
        CacheChain(4, p + "ThumbMetacarpal", p + "ThumbProximal", p + "ThumbDistal");

        string h = isLeft ? "{LeftHand}" : "{RightHand}";
        _gripPosition = new InputAction(binding: $"<XRController>{h}/devicePosition");
        _gripRotation = new InputAction(binding: $"<XRController>{h}/deviceRotation");
        _grip = new InputAction(binding: $"<XRController>{h}/{{Grip}}");
        _trigger = new InputAction(binding: $"<XRController>{h}/{{Trigger}}");
        _triggerTouch = new InputAction(binding: $"<XRController>{h}/triggerTouched");
        _thumbTouch = new InputAction();
        foreach (var control in new[] { "thumbstickTouched", "primaryTouched", "secondaryTouched", "thumbrestTouched" })
            _thumbTouch.AddBinding($"<XRController>{h}/{control}");
    }

    private void CacheChain(int finger, params string[] names)
    {
        _joints[finger] = new Transform[names.Length];
        _bind[finger] = new Quaternion[names.Length];
        for (int j = 0; j < names.Length; j++)
        {
            _joints[finger][j] = FindDeep(wrist, names[j]);
            _bind[finger][j] = _joints[finger][j].localRotation;
        }
    }

    private void OnEnable()
    {
        foreach (var a in Actions()) a.Enable();
        Application.onBeforeRender += FollowGrip;
    }

    private void OnDisable()
    {
        Application.onBeforeRender -= FollowGrip;
        foreach (var a in Actions()) a.Disable();
    }

    private void OnDestroy()
    {
        foreach (var a in Actions()) a.Dispose();
    }

    private InputAction[] Actions() => new[] { _gripPosition, _gripRotation, _grip, _trigger, _triggerTouch, _thumbTouch };

    private void Update()
    {
        FollowGrip();

        float grip = _grip.ReadValue<float>();
        float trigger = _trigger.ReadValue<float>();
        bool triggerTouched = _triggerTouch.IsPressed() || trigger > 0.05f;
        bool thumbTouched = _thumbTouch.IsPressed();

        // Указательный: без касания курка — вытянут, при касании ложится на курок, нажатие сгибает до конца.
        float index = triggerTouched ? Mathf.Lerp(0.35f, 1f, trigger) : 0f;
        float others = Mathf.Lerp(restCurl, 1f, grip);
        float thumb = Mathf.Max(thumbTouched ? 0.6f : restCurl, grip * 0.5f);

        float k = 1f - Mathf.Exp(-curlSpeed * Time.deltaTime);
        _curlTarget[0] = index;
        _curlTarget[1] = _curlTarget[2] = _curlTarget[3] = others;
        _curlTarget[4] = thumb;
        for (int f = 0; f < 5; f++)
        {
            _curl[f] = Mathf.Lerp(_curl[f], _curlTarget[f], k);
            var angles = f == 4 ? ThumbAngles : FingerAngles;
            for (int j = 0; j < _joints[f].Length; j++)
                // Кости руки в осях OpenXR: +Z к кончику пальца, +Y тыльная сторона — поворот вокруг +X сгибает к ладони.
                _joints[f][j].localRotation = _bind[f][j] * Quaternion.AngleAxis(angles[j] * _curl[f], Vector3.right);
        }
    }

    // Grip-поза приходит в координатах трекинга: переводим через Camera Offset (родитель контроллера).
    private void FollowGrip()
    {
        var space = transform.parent != null ? transform.parent.parent : null;
        if (space == null) return;

        var gripPos = space.TransformPoint(_gripPosition.ReadValue<Vector3>());
        var gripRot = space.rotation * _gripRotation.ReadValue<Quaternion>();
        var palmRot = gripRot * Quaternion.Euler(palmRotation);
        var palmPos = gripPos + gripRot * palmOffset;

        var rootRot = palmRot * Quaternion.Inverse(_palmInRoot.rotation);
        transform.SetPositionAndRotation(palmPos - rootRot * _palmInRoot.position, rootRot);
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            var found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
