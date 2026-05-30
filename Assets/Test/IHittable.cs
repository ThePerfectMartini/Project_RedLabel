using UnityEngine;

/// <summary>
/// 피격 반응 연출의 종류를 결정하는 열거형.
/// isLauncher 플래그에 의해 결정되며, 넉백 Y값으로 자동 판단하지 않습니다.
/// </summary>
public enum HitReactionType
{
    /// <summary>일반 경직. hitStunDuration 만큼 Hit 모션 재생 후 Idle로 복귀.</summary>
    Normal,
    /// <summary>에어 런처. AirHit → Land → GetUp → Idle 순서로 순차 재생.</summary>
    Launched,
}

/// <summary>
/// Test 시스템의 타격 1회분 정보를 캡슐화하는 구조체.
/// (Core의 HitData와 이름 충돌을 피하기 위해 CombatHitData로 명명)
/// AttackCaster / Projectile 이 IHittable 로 전달하는 데이터 컨테이너.
/// 구조체(struct)를 사용해 GC 압력 없이 매 타격마다 생성/폐기합니다.
/// </summary>
public struct CombatHitData
{
    // ── 기본 전투 정보 ──────────────────────────────────────────
    public float     damage;
    public Vector3   knockbackForce;   // ActionData 원본 값 (방향 보정 전)
    public Vector3   hitPoint;         // 타격 발생 월드 좌표
    public Vector3   hitDirection;     // 공격자 → 피격자 방향 (정규화)
    public Transform attacker;         // 공격자 Transform

    // ── 전투 상호작용 확장 필드 ────────────────────────────────
    /// <summary>피격 경직 시간 (② 중단 메커니즘과 연동)</summary>
    public float hitStunDuration;
    /// <summary>히트스톱 연출 시간 (⑩ 연출 시스템에서 사용)</summary>
    public float hitStopDuration;
    /// <summary>그로기 게이지 축적량 (⑨ 그로기 시스템에서 사용)</summary>
    public float staggerValue;
    /// <summary>
    /// 피격 반응 연출 종류. ActionData.isLauncher 플래그에 의해 결정됩니다.
    /// 넉백 Y값이 있다고 자동으로 Launched가 되지 않습니다.
    /// </summary>
    public HitReactionType hitReactionType;
    /// <summary>에어 런처 여부 — true면 피격체를 공중으로 띄움 (⑤ 플레이어 콤보에서 사용)</summary>
    public bool  isLauncher;
    /// <summary>패링(저스트 가드) 가능 여부 (⑦ 패링 시스템에서 사용)</summary>
    public bool  canBeParried;
    /// <summary>가드 자체를 뚫는 가드 불능 여부 (⑦ 패링 시스템에서 사용)</summary>
    public bool  isUnblockable;

    // ── 팩토리 ────────────────────────────────────────────────
    /// <summary>
    /// AttackActionData 로부터 CombatHitData 를 생성합니다.
    /// 타격 로직에서 직접 필드를 채우지 않고 이 메서드를 사용하세요.
    /// </summary>
    public static CombatHitData Create(AttackActionData action, Vector3 hitPoint,
                                        Vector3 hitDir, Transform attacker)
    {
        return new CombatHitData
        {
            damage          = action.damage,
            knockbackForce  = action.knockbackForce,
            hitPoint        = hitPoint,
            hitDirection    = hitDir,
            attacker        = attacker,
            hitStunDuration = action.hitStunDuration,
            hitStopDuration = action.hitStopDuration,
            staggerValue    = action.staggerValue,
            // isLauncher 플래그만으로 피격 반응 종류를 결정합니다 (넓백 Y값 무관)
            hitReactionType = action.isLauncher ? HitReactionType.Launched : HitReactionType.Normal,
            isLauncher      = action.isLauncher,
            canBeParried    = action.canBeParried,
            isUnblockable   = action.isUnblockable,
        };
    }
}

/// <summary>
/// 타격을 받을 수 있는 모든 오브젝트가 구현하는 인터페이스.
/// AttackCaster / Projectile 은 이 인터페이스만 호출합니다.
/// 반환값이 true 이면 실제 피해가 적용된 것 (CombatEventBus 이벤트 발행 기준).
/// 패링 / 회피 / 무적 등으로 피해가 막힌 경우 false 를 반환합니다.
/// </summary>
public interface IHittable
{
    bool OnHit(CombatHitData hitData);
}
