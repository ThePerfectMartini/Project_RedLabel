using UnityEngine;

/// <summary>
/// 이 컴포넌트가 부착된 오브젝트를 CombatTargetRegistry에 자동 등록·해제합니다.
///
/// 사용법:
///   플레이어, 적 등 타겟이 될 오브젝트의 루트에 부착하고
///   인스펙터에서 registryTag를 설정하세요 (예: "Player", "Enemy").
///
///   OnEnable 시 자동 등록, OnDisable 시 자동 해제되므로
///   풀(Pool) 방식의 오브젝트 재활용에도 안전하게 동작합니다.
/// </summary>
public class CombatTargetRegistrar : MonoBehaviour
{
    [Tooltip("CombatTargetRegistry에 등록할 태그 키 (예: Player, Enemy)")]
    [SerializeField] private string registryTag;

    private void OnEnable()
    {
        CombatTargetRegistry.Register(registryTag, transform);
    }

    private void OnDisable()
    {
        CombatTargetRegistry.Unregister(registryTag, transform);
    }
}
