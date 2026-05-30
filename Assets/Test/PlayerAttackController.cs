using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ComboInputType { Z, X, Q, W, E, R }

/// <summary>
/// Test 폴더 기반 플레이어 콤보 전투 컨트롤러.
///
/// 역할:
///  - ComboNodeSO 트리를 순회하며 현재 활성 노드를 관리
///  - 입력 버퍼 윈도우 내 공격 입력을 받아 콤보를 연장
///  - 지상 / 공중 상태에 따라 유효한 노드만 필터링
///  - AttackCaster + CapsuleController 를 통해 실제 액션 실행
///  - CombatEventBus 를 통해 런처, 에어 피니셔 이벤트 발행
///
/// 이동·물리 제어는 activeComboNode.comboSteps[currentStepIndex].physics 에서
/// 직접 읽으므로 캐시 필드가 필요 없습니다.
/// </summary>
[RequireComponent(typeof(CapsuleController))]
[RequireComponent(typeof(AttackCaster))]
public class PlayerAttackController : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════
    // 인스펙터 설정
    // ═══════════════════════════════════════════════════════════

    [Header("키별 콤보 매핑")]
    [Tooltip("Z 키 입력 시 발동할 콤보")]
    public ComboNodeSO zCombo;
    [Tooltip("X 키 입력 시 발동할 콤보 (기본 공격)")]
    public ComboNodeSO xCombo;
    [Tooltip("Q 키 입력 시 발동할 콤보")]
    public ComboNodeSO qCombo;
    [Tooltip("W 키 입력 시 발동할 콤보")]
    public ComboNodeSO wCombo;
    [Tooltip("E 키 입력 시 발동할 콤보")]
    public ComboNodeSO eCombo;
    [Tooltip("R 키 입력 시 발동할 콤보")]
    public ComboNodeSO rCombo;

    [Header("지상 감지")]
    public Transform groundCheck;
    public float     groundCheckRadius = 0.25f;
    public LayerMask groundLayer;

    [Header("공중 이동 옵션")]
    [Tooltip("공중 공격 중 중력을 일시 무시할지 여부 (에어 콤보 안정감 향상)")]
    public bool  useGravityOverride    = true;
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
    private AttackCaster      attackCaster;
    private Rigidbody         rb;

    // ── 콤보 상태 ──
    private ComboNodeSO    activeComboNode;    // 현재 실행 중인 콤보 노드
    private ComboInputType activeInputType;   // 현재 실행 중인 콤보의 입력 키
    private int            currentStepIndex = -1; // 현재 실행 단계 인덱스
    private bool           isAttacking;
    private bool           inputBuffered;
    private float          attackStartTime;
    private InterruptToken comboToken;

    // ── 현재 실행 중인 AttackActionData (castDamageOnStart, useJumpInAttack 읽기용) ──
    private AttackActionData currentExecutingAttack;

    // ── 지상 상태 ──
    private bool isGrounded;
    private bool wasGrounded;
    private bool isInAirCombo;

    // ── 액션 진행 시간 측정 ──
    private float currentActionStartTime;
    private float currentActionDuration;

    // ═══════════════════════════════════════════════════════════
    // ActivePhysics — 현재 콤보 단계의 이동 제어를 직접 참조
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 현재 활성 ComboStep의 ComboStepPhysics를 반환합니다.
    /// 공격 중이 아니거나 유효한 단계가 없으면 null.
    /// </summary>
    private ComboStepPhysics ActivePhysics
    {
        get
        {
            if (!isAttacking || activeComboNode == null) return null;
            if (currentStepIndex < 0 || currentStepIndex >= activeComboNode.comboSteps.Count) return null;
            return activeComboNode.comboSteps[currentStepIndex].physics;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 외부 쿼리 프로퍼티 (TestPlayerController가 읽음)
    // ═══════════════════════════════════════════════════════════

    public bool IsAttacking => isAttacking;

    /// <summary>현재 실행 중인 액션(주로 공격)을 가져옵니다. TestHittableComponent 등에서 무적/슈아 상태 조회를 위해 참조합니다.</summary>
    public ActionData CurrentExecutingAction => currentExecutingAttack;

    public bool  CurrentAllowsMove            => isAttacking && (ActivePhysics?.allowMoveWhileAttacking ?? false);
    public bool  CurrentForwardOnly           => ActivePhysics?.forwardMoveOnly ?? true;
    public float CurrentAttackMoveSpeed       => ActivePhysics?.attackMoveSpeed ?? 0f;

    public bool  CurrentEnableDynamicMovement => ActivePhysics?.enableDynamicMovement ?? false;
    public float CurrentAutoMoveSpeed         => ActivePhysics?.autoMoveSpeed ?? 0f;
    public float CurrentStartMoveSpeed        => ActivePhysics?.startMoveSpeed ?? 0f;

    public bool  CurrentBrakeOnOppositeInput      => ActivePhysics?.brakeOnOppositeInput ?? false;
    public float CurrentOppositeBrakeSpeed        => ActivePhysics?.oppositeBrakeSpeed ?? 0f;
    public bool  CurrentAccelerateOnForwardInput  => ActivePhysics?.accelerateOnForwardInput ?? false;
    public float CurrentForwardAccelerationSpeed  => ActivePhysics?.forwardAccelerationSpeed ?? 0f;

    public bool    CurrentUseStartEase      => ActivePhysics?.useStartEase ?? false;
    public EaseType CurrentStartEaseType   => ActivePhysics?.startEaseType ?? EaseType.EaseOut;
    public float   CurrentStartEaseExponent => ActivePhysics?.startEaseExponent ?? 2f;
    public float   CurrentStartEaseDuration => ActivePhysics?.startEaseDuration ?? 0.2f;

    public bool  CurrentAllowSlideAfterAction => ActivePhysics?.allowSlideAfterAction ?? false;

    // 공격 점프 제어는 AttackActionData에서 직접 읽음
    public bool  CurrentCastDamageOnStart => currentExecutingAttack?.castDamageOnStart ?? true;
    public bool  CurrentUseJumpInAttack   => currentExecutingAttack?.useJumpInAttack ?? false;
    public float CurrentAttackJumpForce   => currentExecutingAttack?.attackJumpForce ?? 5f;

    /// <summary>현재 실행 중인 액션의 경과 시간(초).</summary>
    public float CurrentActionElapsedTime => Time.time - currentActionStartTime;

    /// <summary>현재 실행 중인 액션의 정규화 시간 (0~1).</summary>
    public float CurrentActionNormalizedTime
        => currentActionDuration > 0f
            ? Mathf.Clamp01((Time.time - currentActionStartTime) / currentActionDuration)
            : 1f;

    // ═══════════════════════════════════════════════════════════
    // 초기화
    // ═══════════════════════════════════════════════════════════

    private void Awake()
    {
        capsuleController = GetComponent<CapsuleController>();
        attackCaster      = GetComponent<AttackCaster>();
        rb                = GetComponent<Rigidbody>();
    }

    private void OnEnable()  => attackCaster.OnHitConfirmed += HandleHitConfirmed;
    private void OnDisable() => attackCaster.OnHitConfirmed -= HandleHitConfirmed;

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
    public void RequestAttack(ComboInputType inputType)
    {
        if (!isAttacking)
        {
            TryStartRootAttack(inputType);
            return;
        }

        // 같은 키 입력 → 콤보 연계 대기
        if (activeComboNode != null && activeInputType == inputType)
        {
            float elapsed = Time.time - attackStartTime;
            if (currentStepIndex >= 0 && currentStepIndex < activeComboNode.comboSteps.Count)
            {
                // 윈도우 안팎 관계없이 버퍼에 등록 (윈도우 체크는 ExecuteComboStep에서 수행)
                inputBuffered = true;
            }
        }
        // 다른 키 입력 → 캔슬 연계
        else
        {
            CancelCombo();
            TryStartRootAttack(inputType);
        }
    }

    /// <summary>콤보를 강제로 리셋합니다. 피격 / 그로기 / 페이즈 전환 시 호출하세요.</summary>
    public void CancelCombo()
    {
        comboToken?.Interrupt(InterruptReason.External);
        StopAllCoroutines();
        ResetComboState();
    }

    // ═══════════════════════════════════════════════════════════
    // 내부 로직
    // ═══════════════════════════════════════════════════════════

    private void TryStartRootAttack(ComboInputType inputType)
    {
        ComboNodeSO targetNode = GetComboNodeByInput(inputType);
        if (!targetNode)
        {
            Debug.LogWarning($"[PlayerAttackController] {inputType} 입력에 매핑된 ComboNodeSO가 플레이어 인스펙터에 설정되어 있지 않습니다.");
            return;
        }
        if (targetNode.comboSteps == null || targetNode.comboSteps.Count == 0)
        {
            Debug.LogWarning($"[PlayerAttackController] {targetNode.name} 노드의 콤보 단계(Combo Steps) 리스트가 비어 있어 공격을 시작할 수 없습니다.");
            return;
        }
        if (!IsNodeAllowed(targetNode))
        {
            Debug.LogWarning($"[PlayerAttackController] {targetNode.name} 노드는 현재 상태에서 허용되지 않습니다. (노드 허용상태: {targetNode.allowedState}, 현재 캐릭터 지상여부: {isGrounded})");
            return;
        }

        Debug.Log($"[PlayerAttackController] 콤보 공격 시작: 노드={targetNode.name}, 단계=0");
        StartComboStep(targetNode, inputType, 0);
    }

    private ComboNodeSO GetComboNodeByInput(ComboInputType inputType)
    {
        return inputType switch
        {
            ComboInputType.Z => zCombo,
            ComboInputType.X => xCombo,
            ComboInputType.Q => qCombo,
            ComboInputType.W => wCombo,
            ComboInputType.E => eCombo,
            ComboInputType.R => rCombo,
            _                => null,
        };
    }

    private bool IsNodeAllowed(ComboNodeSO node)
    {
        return node.allowedState switch
        {
            ComboGroundState.Ground => isGrounded,
            ComboGroundState.Air    => !isGrounded,
            ComboGroundState.Both   => true,
            _                       => false,
        };
    }

    private void StartComboStep(ComboNodeSO node, ComboInputType inputType, int stepIndex)
    {
        activeComboNode  = node;
        activeInputType  = inputType;
        currentStepIndex = stepIndex;
        isAttacking      = true;
        inputBuffered    = false;
        attackStartTime  = Time.time;

        comboToken = new InterruptToken();
        StartCoroutine(ExecuteComboStep(node.comboSteps[stepIndex], comboToken));
    }

    private IEnumerator ExecuteComboStep(ComboStep step, InterruptToken token)
    {
        // 공중 상태 진입 처리
        if (!isGrounded)
        {
            isInAirCombo = true;
            if (useGravityOverride)
                rb.useGravity = false;
        }

        // ActionSequenceSO 실행
        if (step.actionSequence)
        {
            var parallelCoroutines = new List<Coroutine>();

            foreach (var action in step.actionSequence.actions)
            {
                if (token.IsInterrupted) break;

                // 액션 진행 시간 기록
                currentActionStartTime = Time.time;
                float duration = 0.5f;
                if (action.playAnimation && capsuleController && capsuleController.animController)
                {
                    duration = capsuleController.animController.GetAnimationLength(action.animationName);
                    if (duration <= 0f) duration = 0.5f;
                }
                else if (action is WaitActionData waitData)
                {
                    duration = waitData.timeLimit;
                }

                if (action is AttackActionData atkData && atkData.isContinuousAttack)
                    duration = Mathf.Max(duration, atkData.continuousDuration);

                currentActionDuration = duration;

                // 현재 실행 중인 공격 데이터 추적 (점프·판정 제어용)
                currentExecutingAttack = action as AttackActionData;

                ActionState state = CreateActionState(action, token);
                if (state == null) continue;

                // 공격 액션이면 AttackCaster 준비 (AttackState에서도 처리하지만 병렬 실행 시 선제 설정)
                if (action is AttackActionData atkAction && !atkAction.isFixedAttack)
                    attackCaster.SetAttackData(atkAction, false, Vector3.zero);

                if (action.executeParallel)
                    parallelCoroutines.Add(StartCoroutine(state.Execute()));
                else
                    yield return StartCoroutine(state.Execute());
            }

            foreach (var c in parallelCoroutines)
            {
                if (c != null) yield return c;
            }
        }

        currentExecutingAttack = null;

        if (token.IsInterrupted)
        {
            ResetComboState();
            yield break;
        }

        // 공중 중력 복구
        if (!isGrounded && useGravityOverride)
            rb.useGravity = true;

        // 런처 이벤트 발행
        if (step.isLauncher)
            CombatEventBus.Instance.RaiseGroggy(transform);

        // 에어 피니셔 이벤트 발행
        if (step.isAirFinisher)
            CombatEventBus.Instance.RaiseCounter(transform);

        // 다음 콤보 결정 (입력 윈도우 대기)
        float waitStartTime = Time.time;
        float maxWait = step.inputWindowEnd;

        while (Time.time - waitStartTime < maxWait - step.inputWindowStart)
        {
            if (token.IsInterrupted) break;

            float elapsed = Time.time - attackStartTime;
            if (inputBuffered && step.IsInputWindowOpen(elapsed))
            {
                inputBuffered = false;

                int nextIndex = currentStepIndex + 1;
                if (activeComboNode != null && nextIndex < activeComboNode.comboSteps.Count)
                {
                    ResetComboStateKeepActive();
                    StartComboStep(activeComboNode, activeInputType, nextIndex);
                    yield break;
                }
            }

            yield return null;
        }

        ResetComboState();
    }

    /// <summary>
    /// 타입 패턴 매칭으로 적절한 ActionState를 생성합니다.
    /// </summary>
    private ActionState CreateActionState(ActionData action, InterruptToken token)
    {
        return action switch
        {
            MoveActionData move            => new MoveState(capsuleController, move, token),
            WaitActionData wait            => new WaitState(capsuleController, wait, token),
            RangedAttackActionData ranged  => new RangedAttackState(capsuleController, ranged, token),
            AttackActionData atk           => new AttackState(capsuleController, atk, token),
            _                              => null,
        };
    }

    private void HandleHitConfirmed()
    {
        string stepName = (activeComboNode != null && currentStepIndex >= 0
                           && currentStepIndex < activeComboNode.comboSteps.Count)
            ? activeComboNode.comboSteps[currentStepIndex].displayName
            : "알 수 없음";
        Debug.Log($"[PlayerAttackController] 타격 확인: {stepName}");
    }

    // ═══════════════════════════════════════════════════════════
    // 상태 초기화
    // ═══════════════════════════════════════════════════════════

    private void ResetComboState()
    {
        isAttacking            = false;
        activeComboNode        = null;
        currentStepIndex       = -1;
        inputBuffered          = false;
        comboToken             = null;
        currentExecutingAttack = null;
        currentActionStartTime = 0f;
        currentActionDuration  = 0f;

        if (useGravityOverride)
            rb.useGravity = true;

        if (capsuleController && capsuleController.animController)
            capsuleController.animController.Play("Idle");
    }

    private void ResetComboStateKeepActive()
    {
        inputBuffered = false;
        comboToken    = null;
    }

    // ═══════════════════════════════════════════════════════════
    // 디버그
    // ═══════════════════════════════════════════════════════════

    private void OnDrawGizmos()
    {
        if (groundCheck)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        DrawComboGizmo(zCombo);
        DrawComboGizmo(xCombo);
        DrawComboGizmo(qCombo);
        DrawComboGizmo(wCombo);
        DrawComboGizmo(eCombo);
        DrawComboGizmo(rCombo);
    }

    private void DrawComboGizmo(ComboNodeSO combo)
    {
        if (combo == null || combo.comboSteps == null) return;
        
        float facingSignX = Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, 180f)) < 90f ? 1f : -1f;

        for (int i = 0; i < combo.comboSteps.Count; i++)
        {
            var step = combo.comboSteps[i];
            if ((step.showGizmos || step.showWindowGizmos) && step.actionSequence != null)
            {
                // 현재 순회 중인 단계가 실제로 플레이어가 실행 중인 단계인지 확인합니다.
                bool isCurrentStep = Application.isPlaying && 
                                     isAttacking && 
                                     activeComboNode == combo && 
                                     currentStepIndex == i;

                // 👁(기본 기즈모)가 켜져 있거나, ⏱(타이밍 기즈모)가 켜져 있으면서 현재 이 단계가 실행 중일 때만 그립니다.
                bool shouldDraw = step.showGizmos || (step.showWindowGizmos && isCurrentStep);
                if (!shouldDraw) continue;

                foreach (var action in step.actionSequence.actions)
                {
                    if (action is AttackActionData attackData)
                    {
                        // 기본 기즈모 색상은 초록색
                        Color gizmoColor = new Color(0f, 1f, 0f, 0.6f);

                        // ⏱ 타이밍 토글이 켜져있고, 현재 이 콤보 단계가 실행 중일 때 색상을 실시간으로 변환합니다.
                        if (step.showWindowGizmos && isCurrentStep)
                        {
                            float elapsed = Time.time - attackStartTime;
                            
                            if (elapsed < step.inputWindowStart)
                            {
                                // 공격 시작부터 ~ 입력 수용 시작 전 (예: 0~2초) : 빨간색 (입력 불가 대기 구간)
                                gizmoColor = new Color(1f, 0f, 0f, 0.8f);
                            }
                            else if (elapsed <= step.inputWindowEnd)
                            {
                                // 입력 수용 구간 (예: 2~5초) : 파란색 (입력 가능 구간)
                                gizmoColor = new Color(0f, 0.3f, 1f, 0.8f);
                            }
                        }

                        Gizmos.color = gizmoColor;
                        
                        Vector3 offset = new Vector3(attackData.attackOffset.x * facingSignX, attackData.attackOffset.y, attackData.attackOffset.z);
                        Vector3 center = transform.position + offset;

                        switch (attackData.attackShape)
                        {
                            case AttackShape.Sphere:
                                Gizmos.DrawWireSphere(center, attackData.attackRadius);
                                break;
                            case AttackShape.Box:
                                Gizmos.matrix = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
                                Gizmos.DrawWireCube(Vector3.zero, attackData.attackHitBoxSize);
                                Gizmos.matrix = Matrix4x4.identity;
                                break;
                            case AttackShape.Cylinder:
                                float capOffset = Mathf.Max(0f, attackData.attackHeight * 0.5f - attackData.attackRadius);
                                Vector3 capTop = center + Vector3.up * capOffset;
                                Vector3 capBot = center - Vector3.up * capOffset;
                                Gizmos.DrawWireSphere(capTop, attackData.attackRadius);
                                Gizmos.DrawWireSphere(capBot, attackData.attackRadius);
                                Gizmos.DrawLine(capTop + Vector3.right * attackData.attackRadius, capBot + Vector3.right * attackData.attackRadius);
                                Gizmos.DrawLine(capTop + Vector3.left * attackData.attackRadius, capBot + Vector3.left * attackData.attackRadius);
                                Gizmos.DrawLine(capTop + Vector3.forward * attackData.attackRadius, capBot + Vector3.forward * attackData.attackRadius);
                                Gizmos.DrawLine(capTop + Vector3.back * attackData.attackRadius, capBot + Vector3.back * attackData.attackRadius);
                                break;
                        }
                    }
                }
            }
        }
    }
}
