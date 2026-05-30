using UnityEngine;

/// <summary>
/// 애니메이션 클립의 이벤트 함수에서 AttackCaster.CastDamage() 를
/// 자동으로 대리 호출하는 범용 릴레이 컴포넌트.
///
/// 사용법:
///   1. 이 컴포넌트를 Animator가 있는 Visual 자식 오브젝트에 부착하세요.
///   2. 애니메이션 클립 에디터에서 원하는 프레임에 이벤트를 추가합니다.
///      - 함수명: OnAttackImpact  (근접 타격 판정)
///      - 함수명: OnProjectileFire (투사체 발사 타이밍)
///   3. 해당 ActionData(AttackActionData)의 castDamageOnStart = false 로 설정하세요.
///
/// 이렇게 하면 기획자가 코드를 수정하지 않고도
/// 애니메이션 타임라인에서 타격 판정 프레임을 직접 조절할 수 있습니다.
/// </summary>
public class AnimationAttackEventRelay : MonoBehaviour
{
    private AttackCaster attackCaster;

    private void Awake()
    {
        // 부모 계층에서 AttackCaster를 탐색합니다.
        attackCaster = GetComponentInParent<AttackCaster>();

        if (!attackCaster)
            Debug.LogWarning($"[AnimationAttackEventRelay] {gameObject.name}: 부모 계층에서 AttackCaster를 찾을 수 없습니다.", this);
    }

    // ─────────────────────────────────────────────────────────
    // 애니메이션 이벤트 수신 함수
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// 애니메이션 이벤트: 근접 공격 타격 판정.
    /// 애니메이션 클립에서 이 함수명으로 이벤트를 추가하세요.
    /// </summary>
    public void OnAttackImpact()
    {
        if (attackCaster)
            attackCaster.CastDamage();
    }

    /// <summary>
    /// 애니메이션 이벤트: 투사체 발사.
    /// 원거리 공격의 발사 타이밍을 애니메이션에 정확하게 맞출 때 사용하세요.
    /// </summary>
    public void OnProjectileFire()
    {
        if (attackCaster)
            attackCaster.CastDamage();
    }
}
