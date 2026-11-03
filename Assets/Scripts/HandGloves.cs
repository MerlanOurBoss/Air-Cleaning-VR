using UnityEngine;

/// <summary>
/// Перчатки на руках: пока они надеты, меш руки заменён сеткой перчатки на тех же костях, поэтому пальцы
/// сгибаются как обычно. Подменяется меш, а не включается второй объект: видимость рук отслеживаемых кистей
/// переключает сам XR Hands, и он не должен вернуть голую руку поверх перчатки.
/// </summary>
public class HandGloves : MonoBehaviour
{
    [SerializeField] private PpeItem gloves;
    [SerializeField] private Material gloveMaterial;
    [Tooltip("Все меши левых рук: на контроллере и отслеживаемые кисти.")]
    [SerializeField] private SkinnedMeshRenderer[] leftHands;
    [SerializeField] private Mesh leftGlove;
    [Tooltip("Все меши правых рук.")]
    [SerializeField] private SkinnedMeshRenderer[] rightHands;
    [SerializeField] private Mesh rightGlove;

    private Mesh _leftHand, _rightHand;
    private Material[] _handMaterials;
    private Bounds _handBounds;

    private void Awake()
    {
        _leftHand = leftHands[0].sharedMesh;
        _rightHand = rightHands[0].sharedMesh;
        _handMaterials = leftHands[0].sharedMaterials;
        _handBounds = leftHands[0].localBounds;
        gloves.Changed += Apply;
    }

    private void OnDestroy()
    {
        gloves.Changed -= Apply;
    }

    private void Apply()
    {
        Dress(leftHands, gloves.Worn ? leftGlove : _leftHand);
        Dress(rightHands, gloves.Worn ? rightGlove : _rightHand);
    }

    private void Dress(SkinnedMeshRenderer[] hands, Mesh mesh)
    {
        foreach (var hand in hands)
        {
            hand.sharedMesh = mesh;
            hand.sharedMaterials = gloves.Worn ? new[] { gloveMaterial } : _handMaterials;
            hand.localBounds = gloves.Worn ? mesh.bounds : _handBounds;
        }
    }
}
