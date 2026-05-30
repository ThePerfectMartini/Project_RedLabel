using System.Collections.Generic;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════
// 공용 열거형
// ═══════════════════════════════════════════════════════════════

public enum ActionType        { Move, Wait, VariableAttack, FixedAttack, RangedAttack }
public enum TargetType        { SpecificPosition, TrackObject, Direction }
public enum MoveDirection8    { None, Up, Down, Left, Right, UpLeft, UpRight, DownLeft, DownRight, Forward, Backward }
public enum EaseType          { EaseIn, EaseOut }
public enum DetectOrigin      { Self, Target }
public enum DetectShape       { Sphere, Box }
public enum AttackShape       { Sphere, Box, Cylinder }

// ═══════════════════════════════════════════════════════════════
// ActionData — 추상 기반 클래스
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// 모든 액션 타입의 공통 기반 클래스.
/// [SerializeReference]를 사용해 다형성 직렬화를 지원합니다.
///
/// 서브클래스 목록:
///   MoveActionData         — 이동
///   WaitActionData         — 대기
///   AttackActionData       — 근접 공격 (변동/고정 좌표)
///   RangedAttackActionData — 원거리 투사체 공격
/// </summary>
[System.Serializable]
public abstract class ActionData
{
    /// <summary>이 액션의 타입을 반환합니다. 서브클래스가 구현합니다.</summary>
    public abstract ActionType ActionType { get; }

    public bool showGizmo;
    public bool executeParallel;

    public bool   playAnimation = true;
    public string animationName;

    [Header("방어 상태 설정")]
    [Tooltip("경직 및 넉백에 면역. 데미지·그로기는 정상 적용됩니다.")]
    public bool isSuperArmor = false;

    [Tooltip("피격 판정 자체를 무시. 데미지·그로기·넉백·경직 모두 면역입니다.")]
    public bool isInvincible = false;
}

// ═══════════════════════════════════════════════════════════════
// MoveActionData — 이동 액션
// ═══════════════════════════════════════════════════════════════

[System.Serializable]
public class MoveActionData : ActionData
{
    public override ActionType ActionType => ActionType.Move;

    // ▼ 이동 방식
    [Tooltip("순간이동(Teleport) 모드. 활성화하면 물리 이동 없이 즉시 위치를 변경합니다.")]
    public bool isTeleport;

    [Tooltip("이동 시 점프를 동시에 수행합니다.")]
    public bool useJump;
    public float jumpForce = 5f;

    // ▼ 목표 기준
    public TargetType   targetType;
    public Vector3      targetPosition;
    public string       targetTag;
    public MoveDirection8 moveDirection8 = MoveDirection8.None;

    [Tooltip("X축(좌우)만 추적합니다.")]
    public bool trackXOnly;
    [Tooltip("Z축(앞뒤)만 추적합니다.")]
    public bool trackZOnly;

    [Tooltip("도착 지점에 더하는 오프셋.")]
    public Vector3 targetOffset = Vector3.zero;

    // ▼ 동적 새로고침
    [Tooltip("이동 중 목표 위치를 주기적으로 갱신합니다.")]
    public bool usePositionRefresh;
    public float positionRefreshInterval = 0.5f;
    public bool  repeatRefreshUntilReached = true;
    public int   refreshRepeatCount = 3;

    // ▼ 속도
    public float startSpeed    = 0f;
    public float speed         = 5f;
    public bool  useAcceleration;
    public float acceleration  = 2f;

    // ▼ 출발 가속 이징
    [Tooltip("출발 가속을 활성화합니다.")]
    public bool      useStartEase;
    public EaseType  startEaseType     = EaseType.EaseOut;
    [Range(1f, 5f)]
    public float     startEaseExponent = 2f;
    public float     startEaseDuration = 0.2f;

    // ▼ 종료 조건 1: 목표 좌표 도달
    public bool  stopOnDestinationReached;
    public float destinationStopDistance = 0.1f;

    // ▼ 종료 조건 2: 타겟 감지 (TrackObject 모드 전용)
    public bool        stopOnTargetDetected;
    public DetectOrigin detectOrigin  = DetectOrigin.Self;
    public DetectShape  detectShape   = DetectShape.Sphere;
    public Vector3      detectOffset  = Vector3.zero;
    public float        detectRadius  = 1.5f;
    public Vector3      detectBoxSize = new Vector3(2f, 2f, 2f);

    // ▼ 종료 조건 3: 시간 제한
    public bool  stopOnTimeLimit;
    public float timeLimit = 3f;

    // ▼ 기타
    [Tooltip("액션 종료 후 자연스럽게 미끄러지도록 허용합니다.")]
    public bool allowSlideAfterAction = false;
    [Tooltip("true면 8방향 스냅 이동, false면 360도 자유 이동입니다.")]
    public bool use8DirectionMovement = true;
}

// ═══════════════════════════════════════════════════════════════
// WaitActionData — 대기 액션
// ═══════════════════════════════════════════════════════════════

[System.Serializable]
public class WaitActionData : ActionData
{
    public override ActionType ActionType => ActionType.Wait;

    [Tooltip("대기할 시간(초).")]
    public float timeLimit = 1f;
}

// ═══════════════════════════════════════════════════════════════
// AttackActionData — 근접 공격 액션 (변동/고정 좌표)
// ═══════════════════════════════════════════════════════════════

[System.Serializable]
public class AttackActionData : ActionData
{
    /// <summary>true면 FixedAttack, false면 VariableAttack.</summary>
    public bool isFixedAttack = false;
    public override ActionType ActionType => isFixedAttack ? ActionType.FixedAttack : ActionType.VariableAttack;

    // ▼ FixedAttack 전용 타겟
    [Tooltip("고정 좌표 타격 시 타겟 기준.")]
    public TargetType targetType = TargetType.SpecificPosition;
    public Vector3    targetPosition;
    public string     targetTag;

    // ▼ 타격 기본
    public float     damage         = 10f;
    public Vector3   knockbackForce = new Vector3(15f, 5f, 15f);
    public AttackShape attackShape  = AttackShape.Sphere;
    public float     attackRadius   = 1.5f;
    public float     attackHeight   = 2f;
    public Vector3   attackHitBoxSize = new Vector3(2f, 1f, 2f);
    public Vector3   attackOffset   = new Vector3(0f, 1f, 1f);

    // ▼ 전투 상호작용 (CombatHitData와 연동)
    [Header("전투 상호작용")]
    [Tooltip("피격 경직 시간(초).")]
    public float hitStunDuration = 0.15f;
    [Tooltip("히트스톱 시간(초). 0이면 HitStopHandler 기본값을 사용합니다.")]
    public float hitStopDuration = 0f;
    [Tooltip("그로기 게이지 축적량.")]
    public float staggerValue    = 10f;
    [Tooltip("에어 런처 여부. true면 피격체를 공중으로 띄웁니다.")]
    public bool  isLauncher      = false;
    [Tooltip("패링(저스트 가드) 가능 여부.")]
    public bool  canBeParried    = true;
    [Tooltip("가드 불능 여부. true면 가드/패링을 뚫습니다.")]
    public bool  isUnblockable   = false;

    // ▼ 타격 판정 제어
    [Header("타격 판정 제어")]
    [Tooltip("액션 시작 시 즉시 타격 판정. false면 애니메이션 이벤트(OnAttackImpact)로 제어하세요.")]
    public bool  castDamageOnStart = true;
    [Tooltip("공격과 동시에 점프를 수행합니다 (승룡권 등).")]
    public bool  useJumpInAttack   = false;
    public float attackJumpForce   = 5f;

    // ▼ 지속 타격
    [Header("지속 타격")]
    [Tooltip("일정 시간 동안 타격 판정을 유지합니다.")]
    public bool  isContinuousAttack = false;
    public float continuousDuration = 2f;
    [Tooltip("동일 타겟이 다시 피격될 때까지의 쿨다운(초). 0 이하면 1회만 타격합니다.")]
    public float hitTickCooldown    = 1f;
}

// ═══════════════════════════════════════════════════════════════
// RangedAttackActionData — 원거리 투사체 공격
// ═══════════════════════════════════════════════════════════════

[System.Serializable]
public class RangedAttackActionData : AttackActionData
{
    public override ActionType ActionType => ActionType.RangedAttack;

    // ▼ 투사체 목표 (부모의 targetType/targetPosition/targetTag 재활용)
    [Tooltip("X축 방향으로만 스냅 발사 (좌/우 자동 판단). TrackObject 모드에서만 유효합니다.")]
    public bool           snapToPlayerXAxis;
    [Tooltip("Direction 모드일 때 발사 방향.")]
    public MoveDirection8 moveDirection8 = MoveDirection8.None;

    // ▼ 투사체 설정
    [Header("투사체 설정")]
    public GameObject projectilePrefab;
    [Tooltip("한 번에 발사할 투사체 수.")]
    public int   projectileCount    = 1;
    [Tooltip("투사체 간 발사 간격(초).")]
    public float projectileInterval = 0.1f;
    public float projectileSpeed    = 15f;
    public float projectileLifeTime = 3f;
}

// ═══════════════════════════════════════════════════════════════
// ActionSequenceSO — 컨테이너 ScriptableObject
// ═══════════════════════════════════════════════════════════════

[CreateAssetMenu(fileName = "NewActionSequence", menuName = "Actions/액션 시퀀스")]
public class ActionSequenceSO : ScriptableObject
{
    [SerializeReference]
    public List<ActionData> actions = new List<ActionData>();

    private void OnValidate()
    {
        foreach (var action in actions)
        {
            if (action == null) continue;

            // 슈퍼 아머와 무적은 동시에 체크될 수 없도록 제어
            if (action.isInvincible && action.isSuperArmor)
                action.isSuperArmor = false;

            // 기본 애니메이션 이름 자동 설정
            if (action.playAnimation && string.IsNullOrEmpty(action.animationName))
            {
                action.animationName = action.ActionType switch
                {
                    ActionType.Move           => "Walk",
                    ActionType.VariableAttack => "Attack",
                    ActionType.FixedAttack    => "Attack",
                    ActionType.RangedAttack   => "Shoot",
                    ActionType.Wait           => "Idle",
                    _                         => "Idle",
                };
            }

            // 타입별 유효성 검사
            if (action is MoveActionData move)
            {
                if (move.speed <= 0f)         move.speed = 0.01f;
                if (move.timeLimit < 0f)      move.timeLimit = 0f;
                if (move.startSpeed < 0f)     move.startSpeed = 0f;
                if (move.acceleration <= 0f)  move.acceleration = 0.01f;
                move.startEaseExponent = Mathf.Clamp(move.startEaseExponent, 1f, 5f);
                if (move.startEaseDuration < 0f) move.startEaseDuration = 0f;
            }
            else if (action is RangedAttackActionData ranged)
            {
                if (ranged.projectileCount < 1)    ranged.projectileCount = 1;
                if (ranged.projectileInterval < 0f) ranged.projectileInterval = 0f;
            }
            else if (action is AttackActionData atk)
            {
                if (atk.isContinuousAttack)
                {
                    if (atk.continuousDuration < 0f) atk.continuousDuration = 0f;
                    if (atk.hitTickCooldown < 0f)    atk.hitTickCooldown = 0f;
                }
            }
        }
    }
}