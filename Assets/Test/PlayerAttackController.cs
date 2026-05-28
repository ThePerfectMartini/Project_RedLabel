using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Test 폴더 기반 플레이어 콤보 전투 컨트롤러.
/// (기존 PlayerCombatController와 이름 충돌 방지를 위해 PlayerAttackController로 명명)
///
/// 역할:
///  - ComboNodeSO 트리를 순회하며 현재 활성 노드를 관리
///  - 입력 버퍼 윈도우 내 공격 입력을 받아 콤보를 연장
///  - 지상 / 공중 상태에 따라 유효한 노드만 필터링
///  - AttackCaster + CapsuleController 를 통해 실제 액션 실행
///  - CombatEventBus 를 통해 런처, 에어 피니셔 이벤트 발행
/// </summary>
[RequireComponent(typeof(CapsuleController))]
[RequireComponent(typeof(AttackCaster))]
public class PlayerAttackController : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════
    // 인스펙터 설정
    // ═══════════════════════════════════════════════════════════

    [Header("콤보 루트 노드 목록")]
    [Tooltip("지상 공격의 시작점(루트) ComboNodeSO 목록. 첫 공격 입력 시 allowedState 조건에 맞는 첫 번째 노드를 실행합니다.")]
    public List<ComboNodeSO> groundRoots = new List<ComboNodeSO>();

    [Tooltip("공중 공격의 시작점(루트) ComboNodeSO 목록.")]
    public List<ComboNodeSO> airRoots = new List<ComboNodeSO>();

    [Header("지상 감지")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.25f;
    public LayerMask groundLayer;

    [Header("공중 이동 옵션")]
    [Tooltip("공중 공격 중 중력을 일시 무시할지 여부 (에어 콤보 안정감 향상)")]
    public bool useGravityOverride = true;
    [Tooltip("공중 공격 중 중력 배율 (0 = 완전 무중력, 0.3 = 약한 중력)")]
    [Range(0f, 1f)]
    public float airAttackGravityScale = 0.15f;

    [Header("히트스톱 기본값")]
    [Tooltip("hitStopDuration = 0 인 공격의 기본 히트스톱 시간(초)")]
    public float defaultHitStopDuration = 0.06f;

    // ═══════════════════════════════════════════════════════════
    // 내부 상태
    // ═══════════════════════════════════════════════════════════

    private CapsuleController capsuleController;
    private AttackCaster attackCaster;
    private Rigidbody rb;

    // ── 콤보 상태 ──
    private ComboNodeSO activeNode;          // 현재 실행 중인 노드
    private ActionData currentExecutingAction; // 현재 실행 중인 단일 액션
    private bool isAttacking;               // 공격 시퀀스 실행 중 여부
    private bool inputBuffered;             // 다음 공격 입력이 버퍼에 쌓였는지
    private float attackStartTime;          // 현재 공격 시작 시각
    private InterruptToken comboToken;      // 시퀀스 중단용 토큰

    // ── 상태 ──
    private bool isGrounded;
    private bool wasGrounded;
    private bool isInAirCombo;

    // ── 캐싱된 이동 제어 값 (액션 코루틴이 끝난 후딜레이/콤보 윈도우 대기 시간용) ──
    private bool cachedAllowsMove;
    private bool cachedForwardOnly = true;
    private float cachedAttackMoveSpeed;

    private float cachedStartMoveSpeed;
    private float cachedAutoMoveSpeed;
    private bool cachedBrakeOnOppositeInput;
    private float cachedOppositeBrakeSpeed;
    private bool cachedAccelerateOnForwardInput;
    private float cachedForwardAccelerationSpeed;

    private bool cachedCastDamageOnStart = true;
    private bool cachedUseJumpInAttack;
    private float cachedAttackJumpForce;
    private bool cachedUseLerpMovement;

    private bool cachedEnableDynamicMovement;

    // ── 액션 진행 시간 비율 측정용 (가감속 보간 연동) ──
    private float currentActionStartTime;
    private float currentActionDuration;

    // ── 외부 쿼리 프로퍼티 ──
    /// <summary>지금 공격 시퀀스가 실행 중인지 여부.</summary>
    public bool IsAttacking => isAttacking;
    /// <summary>현재 액션이 공격 중 이동을 허용하는지 여부.</summary>
    public bool CurrentAllowsMove => isAttacking && (currentExecutingAction != null ? currentExecutingAction.allowMoveWhileAttacking : cachedAllowsMove);
    /// <summary>현재 액션이 앞 방향으로만 이동을 허용하는지 여부.</summary>
    public bool CurrentForwardOnly => currentExecutingAction != null ? currentExecutingAction.forwardMoveOnly : cachedForwardOnly;
    /// <summary>현재 액션의 공격 중 이동 속도.</summary>
    public float CurrentAttackMoveSpeed => currentExecutingAction != null ? currentExecutingAction.attackMoveSpeed : cachedAttackMoveSpeed;

    /// <summary>공격 시작 시점의 자동 이동 속도.</summary>
    public float CurrentStartMoveSpeed => currentExecutingAction != null ? currentExecutingAction.startMoveSpeed : cachedStartMoveSpeed;
    /// <summary>공격 종료 시점의 최종 이동 속도.</summary>
    public float CurrentAutoMoveSpeed => currentExecutingAction != null ? currentExecutingAction.autoMoveSpeed : cachedAutoMoveSpeed;

    /// <summary>반대 방향 입력 시 제동 여부.</summary>
    public bool CurrentBrakeOnOppositeInput => currentExecutingAction != null ? currentExecutingAction.brakeOnOppositeInput : cachedBrakeOnOppositeInput;
    /// <summary>반대 방향 입력 시 제동 속도 값.</summary>
    public float CurrentOppositeBrakeSpeed => currentExecutingAction != null ? currentExecutingAction.oppositeBrakeSpeed : cachedOppositeBrakeSpeed;
    /// <summary>순방향 입력 시 가속 여부.</summary>
    public bool CurrentAccelerateOnForwardInput => currentExecutingAction != null ? currentExecutingAction.accelerateOnForwardInput : cachedAccelerateOnForwardInput;
    /// <summary>순방향 입력 시 증가할 속도.</summary>
    public float CurrentForwardAccelerationSpeed => currentExecutingAction != null ? currentExecutingAction.forwardAccelerationSpeed : cachedForwardAccelerationSpeed;

    /// <summary>공격 시작 시 즉시 타격 판정 여부.</summary>
    public bool CurrentCastDamageOnStart => currentExecutingAction != null ? currentExecutingAction.castDamageOnStart : cachedCastDamageOnStart;
    /// <summary>공격 시 점프(도약) 사용 여부.</summary>
    public bool CurrentUseJumpInAttack => currentExecutingAction != null ? currentExecutingAction.useJumpInAttack : cachedUseJumpInAttack;
    /// <summary>공격 시 점프력.</summary>
    public float CurrentAttackJumpForce => currentExecutingAction != null ? currentExecutingAction.attackJumpForce : cachedAttackJumpForce;
    /// <summary>공격 중 Lerp 보간 적용 여부.</summary>
    public bool CurrentUseLerpMovement => currentExecutingAction != null ? currentExecutingAction.useLerpMovement : cachedUseLerpMovement;

    /// <summary>동적 이동 제어 허용 여부.</summary>
    public bool CurrentEnableDynamicMovement => currentExecutingAction != null ? currentExecutingAction.enableDynamicMovement : cachedEnableDynamicMovement;

    /// <summary>현재 실행 중인 액션의 시간 진행 비율 (0 ~ 1).</summary>
    public float CurrentActionNormalizedTime => currentActionDuration > 0f ? Mathf.Clamp01((Time.time - currentActionStartTime) / currentActionDuration) : 1f;

    /// <summary>현재 실행 중인 액션 데이터.</summary>
    public ActionData CurrentExecutingAction => currentExecutingAction;

    // ═══════════════════════════════════════════════════════════
    // 초기화
    // ═══════════════════════════════════════════════════════════

    private void Awake()
    {
        capsuleController = GetComponent<CapsuleController>();
        attackCaster = GetComponent<AttackCaster>();
        rb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        attackCaster.OnHitConfirmed += HandleHitConfirmed;
    }

    private void OnDisable()
    {
        attackCaster.OnHitConfirmed -= HandleHitConfirmed;
    }

    // ═══════════════════════════════════════════════════════════
    // 매 프레임 지상 판정
    // ═══════════════════════════════════════════════════════════

    private void FixedUpdate()
    {
        wasGrounded = isGrounded;
        isGrounded = groundCheck
            ? Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundLayer)
            : true;

        // 착지 시 에어 콤보 종료 + 중력 즉시 복구
        if (!wasGrounded && isGrounded)
        {
            if (isInAirCombo && useGravityOverride)
                rb.useGravity = true;

            isInAirCombo = false;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 외부 API
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// TestPlayerController 등 입력 핸들러에서 공격 버튼이 눌렸을 때 호출합니다.
    /// </summary>
    public void RequestAttack()
    {
        // 공격 중이 아니면 → 루트 노드부터 시작
        if (!isAttacking)
        {
            TryStartRootAttack();
            return;
        }

        // 공격 중이면 → 입력 버퍼에 쌓아 다음 콤보 연장 대기
        float elapsed = Time.time - attackStartTime;
        if (activeNode && activeNode.IsInputWindowOpen(elapsed))
        {
            inputBuffered = true;
        }
        else
        {
            // 윈도우가 아직 열리지 않았거나 닫혔으면 버퍼에 일단 저장
            // (Execute 내부에서 윈도우가 열리면 소비)
            inputBuffered = true;
        }
    }

    /// <summary>
    /// 콤보를 강제로 리셋합니다. 피격 / 그로기 / 페이즈 전환 시 호출하세요.
    /// </summary>
    public void CancelCombo()
    {
        comboToken?.Interrupt(InterruptReason.External);
        StopAllCoroutines();
        ResetComboState();
    }

    // ═══════════════════════════════════════════════════════════
    // 내부 로직
    // ═══════════════════════════════════════════════════════════

    private void TryStartRootAttack()
    {
        List<ComboNodeSO> roots = isGrounded ? groundRoots : airRoots;
        if (roots == null || roots.Count == 0) return;

        ComboNodeSO startNode = FindValidNode(roots);
        if (!startNode) return;

        StartComboNode(startNode);
    }

    private void StartComboNode(ComboNodeSO node)
    {
        activeNode = node;
        isAttacking = true;
        inputBuffered = false;
        attackStartTime = Time.time;

        comboToken = new InterruptToken();
        StartCoroutine(ExecuteComboNode(node, comboToken));
    }

    private IEnumerator ExecuteComboNode(ComboNodeSO node, InterruptToken token)
    {
        // 공중 상태 진입 처리
        if (!isGrounded)
        {
            isInAirCombo = true;
            if (useGravityOverride)
                rb.useGravity = false;
        }

        // ActionSequenceSO 실행 (CapsuleController 코루틴 위임)
        if (node.actionSequence)
        {
            // AttackCaster에 현재 노드의 시퀀스 정보 전달
            foreach (var action in node.actionSequence.actions)
            {
                if (token.IsInterrupted) break;

                // 현재 실행 중인 액션 추적 (테스트플레이어콘트롤러가 이동 허용 여부를 여기서 읽음)
                currentExecutingAction = action;

                // 액션 시작 시각 및 예상 지속 시간 계산
                currentActionStartTime = Time.time;
                float duration = 0.5f; // 기본 폴백 시간
                if (action.playAnimation && capsuleController && capsuleController.animController)
                {
                    duration = capsuleController.animController.GetAnimationLength(action.animationName);
                    if (duration <= 0f) duration = 0.5f;
                }
                else
                {
                    duration = action.timeLimit;
                }
                currentActionDuration = duration;

                // 이동 제어 캐싱 갱신 (애니메이션/액션 코루틴이 일찍 끝나도 대기 시간 동안 이동 속성을 유지하기 위함)
                cachedAllowsMove = action.allowMoveWhileAttacking;
                cachedForwardOnly = action.forwardMoveOnly;
                cachedAttackMoveSpeed = action.attackMoveSpeed;

                cachedStartMoveSpeed = action.startMoveSpeed;
                cachedAutoMoveSpeed = action.autoMoveSpeed;
                cachedBrakeOnOppositeInput = action.brakeOnOppositeInput;
                cachedOppositeBrakeSpeed = action.oppositeBrakeSpeed;
                cachedAccelerateOnForwardInput = action.accelerateOnForwardInput;
                cachedForwardAccelerationSpeed = action.forwardAccelerationSpeed;

                cachedCastDamageOnStart = action.castDamageOnStart;
                cachedUseJumpInAttack = action.useJumpInAttack;
                cachedAttackJumpForce = action.attackJumpForce;
                cachedUseLerpMovement = action.useLerpMovement;
                cachedEnableDynamicMovement = action.enableDynamicMovement;

                ActionState state = CreateActionState(action, token);
                if (state == null) continue;

                // 공격 액션이면 AttackCaster 준비
                if (action.actionType == ActionType.VariableAttack ||
                    action.actionType == ActionType.FixedAttack)
                {
                    attackCaster.SetAttackData(action, false, Vector3.zero);
                }

                yield return StartCoroutine(state.Execute());
            }
        }

        currentExecutingAction = null;

        if (token.IsInterrupted)
        {
            ResetComboState();
            yield break;
        }

        // ── 공중 중력 복구 ──
        if (!isGrounded && useGravityOverride)
            rb.useGravity = true;

        // ── 런처 이벤트 발행 ──
        if (node.isLauncher)
            CombatEventBus.Instance.RaiseGroggy(transform); // 런처 성공 훅

        // ── 에어 피니셔 이벤트 발행 ──
        if (node.isAirFinisher)
            CombatEventBus.Instance.RaiseCounter(transform); // 에어 피니셔 훅

        // ── 다음 콤보 결정 ──
        float waitStartTime = Time.time;
        float maxWait = node.inputWindowEnd;

        while (Time.time - waitStartTime < maxWait - node.inputWindowStart)
        {
            if (token.IsInterrupted) break;

            float elapsed = Time.time - attackStartTime;

            if (inputBuffered && node.IsInputWindowOpen(elapsed))
            {
                inputBuffered = false;
                ComboNodeSO nextNode = FindValidNode(node.children);

                if (nextNode)
                {
                    // 콤보 연장: 현재 코루틴은 끝내고 새 노드 시작
                    ResetComboStateKeepActive();
                    StartComboNode(nextNode);
                    yield break;
                }
            }

            yield return null;
        }

        // 입력 윈도우 초과 → 콤보 리셋
        ResetComboState();
    }

    /// <summary>
    /// 현재 지상/공중 상태에서 실행 가능한 첫 번째 노드를 반환합니다.
    /// </summary>
    private ComboNodeSO FindValidNode(List<ComboNodeSO> nodes)
    {
        if (nodes == null) return null;
        foreach (var node in nodes)
        {
            if (!node) continue;
            if (IsNodeAllowed(node)) return node;
        }
        return null;
    }

    private bool IsNodeAllowed(ComboNodeSO node)
    {
        switch (node.allowedState)
        {
            case ComboGroundState.Ground: return isGrounded;
            case ComboGroundState.Air:    return !isGrounded;
            case ComboGroundState.Both:   return true;
            default:                      return false;
        }
    }

    private ActionState CreateActionState(ActionData action, InterruptToken token)
    {
        switch (action.actionType)
        {
            case ActionType.Move:
            case ActionType.FixedAttack:
            case ActionType.VariableAttack: return new AttackState(capsuleController, action, token);
            case ActionType.Wait:           return new WaitState(capsuleController, action, token);
            case ActionType.RangedAttack:   return new RangedAttackState(capsuleController, action, token);
            default: return null;
        }
    }

    private void HandleHitConfirmed()
    {
        // 히트스톱 연출 (CombatEventBus 구독자가 처리)
        // 여기서는 히트 확인 로그만 기록
        Debug.Log($"[PlayerAttackController] 타격 확인: {activeNode?.displayName}");
    }

    private void ResetComboState()
    {
        isAttacking = false;
        activeNode = null;
        inputBuffered = false;
        comboToken = null;
        currentExecutingAction = null;

        // 이동 제어 캐싱 초기화
        cachedAllowsMove = false;
        cachedForwardOnly = true;
        cachedAttackMoveSpeed = 0f;

        cachedStartMoveSpeed = 0f;
        cachedAutoMoveSpeed = 0f;
        cachedBrakeOnOppositeInput = false;
        cachedOppositeBrakeSpeed = 0f;
        cachedAccelerateOnForwardInput = false;
        cachedForwardAccelerationSpeed = 0f;

        cachedCastDamageOnStart = true;
        cachedUseJumpInAttack = false;
        cachedAttackJumpForce = 0f;
        cachedUseLerpMovement = false;
        cachedEnableDynamicMovement = false;

        currentActionStartTime = 0f;
        currentActionDuration = 0f;

        if (useGravityOverride)
            rb.useGravity = true;

        // 콤보 리셋 시 애니메이션을 Idle로 명시적 복구
        if (capsuleController && capsuleController.animController)
        {
            capsuleController.animController.Play("Idle");
        }
    }

    private void ResetComboStateKeepActive()
    {
        // 콤보 연장 시 isAttacking = true 유지, activeNode는 다음 노드로 교체됨
        inputBuffered = false;
        comboToken = null;
    }

    // ═══════════════════════════════════════════════════════════
    // 디버그
    // ═══════════════════════════════════════════════════════════

    private void OnDrawGizmosSelected()
    {
        if (!groundCheck) return;
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
