using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Test 폴더 기반 플레이어 입력 + 이동 컨트롤러.
/// New Input System을 사용해 이동 / 점프 / 공격 입력을 처리하고
/// PlayerAttackController 및 CapsuleController에 위임합니다.
///
/// 부착 필수 컴포넌트:
///  - CapsuleController (이동, 점프)
///  - PlayerAttackController (공격, 콤보)
///  - Rigidbody
///  - Collider
/// </summary>
[RequireComponent(typeof(CapsuleController))]
[RequireComponent(typeof(PlayerAttackController))]
public class TestPlayerController : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════
    // 인스펙터 설정
    // ═══════════════════════════════════════════════════════════

    [Header("이동")]
    [Tooltip("일반 이동 속도 (초당 유닛)")]
    public float moveSpeed = 6f;

    [Tooltip("달리기 속도 (초당 유닛). 좌우 방향 더블탭으로 발동")]
    public float runSpeed = 10f;

    [Tooltip("달리기 전환 더블탭 판정 시간 (초)")]
    public float runDoubleTapThreshold = 0.25f;

    [Header("점프")]
    [Tooltip("점프 초기 속도 (ForceMode.VelocityChange)")]
    public float jumpForce = 7f;

    [Tooltip("최대 점프 횟수 (1 = 더블점프 없음, 2 = 더블점프 허용)")]
    [Range(1, 2)]
    public int maxJumpCount = 1;

    [Header("지상 감지")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.25f;
    public LayerMask groundLayer;

    // ═══════════════════════════════════════════════════════════
    // 내부 상태
    // ═══════════════════════════════════════════════════════════

    private CapsuleController capsuleController;
    private PlayerAttackController attackController;
    private ParryController  parryController;
    private DodgeController  dodgeController;
    private Rigidbody rb;
    private AnimationController animController;

    // ── 입력 ──
    private PlayerInputAction inputActions;
    private Vector2 moveInput;

    // ── 달리기 더블탭 감지 ──
    private float lastTapTime;
    private int lastTapDirection;
    private bool isRunning;

    // ── 점프 ──
    private int remainingJumps;
    private bool isGrounded;

    // ── 입력 잠금 ──
    /// <summary>피격 경직 중 true. TestHittableComponent.LockInput()이 설정합니다.</summary>
    private bool isStunned;
    private Coroutine stunCoroutine;

    // ── 공격 물리 마찰 우회용 가상 속도 ──
    private Vector3 activeAttackVelocity;
    private bool wasAttackActiveLastFrame;
    private bool isSlidingAfterAction;

    // ═══════════════════════════════════════════════════════════
    // 초기화
    // ═══════════════════════════════════════════════════════════

    private void Awake()
    {
        capsuleController = GetComponent<CapsuleController>();
        attackController  = GetComponent<PlayerAttackController>();
        parryController   = GetComponent<ParryController>();
        dodgeController   = GetComponent<DodgeController>();
        rb = GetComponent<Rigidbody>();
        animController    = GetComponentInChildren<AnimationController>();

        // Input Actions 생성 및 바인딩
        inputActions = new PlayerInputAction();

        inputActions.Player.Move.performed += OnMovePerformed;
        inputActions.Player.Move.canceled  += OnMoveCanceled;
        inputActions.Player.Move.started   += OnMoveStarted;

        inputActions.Player.Jump.started   += OnJumpStarted;
        inputActions.Player.Parry.started  += OnParryStarted;
        inputActions.Player.Dodge.started  += OnDodgeStarted;

        // ── 콤보 공격 입력 바인딩 ──
        inputActions.Player.AttackX.started += OnAttackXStarted;
        inputActions.Player.AttackZ.started += OnAttackZStarted;
        inputActions.Player.AttackQ.started += OnAttackQStarted;
        inputActions.Player.AttackW.started += OnAttackWStarted;
        inputActions.Player.AttackE.started += OnAttackEStarted;
        inputActions.Player.AttackR.started += OnAttackRStarted;
    }

    private void OnEnable()  => inputActions.Enable();
    private void OnDisable() => inputActions.Disable();

    private void OnDestroy()
    {
        inputActions.Player.Move.performed -= OnMovePerformed;
        inputActions.Player.Move.canceled  -= OnMoveCanceled;
        inputActions.Player.Move.started   -= OnMoveStarted;
        inputActions.Player.Jump.started   -= OnJumpStarted;
        inputActions.Player.Parry.started  -= OnParryStarted;
        inputActions.Player.Dodge.started  -= OnDodgeStarted;

        // ── 콤보 공격 입력 바인딩 해제 ──
        inputActions.Player.AttackX.started -= OnAttackXStarted;
        inputActions.Player.AttackZ.started -= OnAttackZStarted;
        inputActions.Player.AttackQ.started -= OnAttackQStarted;
        inputActions.Player.AttackW.started -= OnAttackWStarted;
        inputActions.Player.AttackE.started -= OnAttackEStarted;
        inputActions.Player.AttackR.started -= OnAttackRStarted;

        inputActions.Dispose();
    }

    // ═══════════════════════════════════════════════════════════
    // 입력 콜백 (New Input System)
    // ═══════════════════════════════════════════════════════════

    private void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        moveInput = ctx.ReadValue<Vector2>();
    }

    private void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        moveInput = Vector2.zero;
        isRunning = false;
    }

    private void OnMoveStarted(InputAction.CallbackContext ctx)
    {
        Vector2 input = ctx.ReadValue<Vector2>();

        // Y 입력 위주면 달리기 감지 생략 (상하 이동)
        if (Mathf.Abs(input.y) > Mathf.Abs(input.x)) return;

        int currentDir = input.x > 0f ? 1 : -1;

        if (Time.time - lastTapTime < runDoubleTapThreshold && currentDir == lastTapDirection)
        {
            isRunning = true;
        }
        else
        {
            lastTapTime      = Time.time;
            lastTapDirection = currentDir;
            isRunning        = false;
        }
    }

    private void OnJumpStarted(InputAction.CallbackContext ctx)
    {
        if (remainingJumps > 0)
        {
            // 점프 전 Y속도 초기화 후 점프력 인가 (더블 점프 일관성)
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            capsuleController.Jump(jumpForce);
            remainingJumps--;
        }
    }



    private void OnParryStarted(InputAction.CallbackContext ctx)
    {
        parryController?.RequestParry();
    }

    private void OnDodgeStarted(InputAction.CallbackContext ctx)
    {
        dodgeController?.RequestDodge(moveInput);
    }

    // ── 공격 입력 콜백 ──
    private void OnAttackXStarted(InputAction.CallbackContext ctx)
    {
        Debug.Log("[TestPlayerController] OnAttackXStarted: Keyboard X키 입력 이벤트 수신");
        attackController.RequestAttack(ComboInputType.X);
    }
    private void OnAttackZStarted(InputAction.CallbackContext ctx)
    {
        Debug.Log("[TestPlayerController] OnAttackZStarted: Keyboard Z키 입력 이벤트 수신");
        attackController.RequestAttack(ComboInputType.Z);
    }
    private void OnAttackQStarted(InputAction.CallbackContext ctx) => attackController.RequestAttack(ComboInputType.Q);
    private void OnAttackWStarted(InputAction.CallbackContext ctx) => attackController.RequestAttack(ComboInputType.W);
    private void OnAttackEStarted(InputAction.CallbackContext ctx) => attackController.RequestAttack(ComboInputType.E);
    private void OnAttackRStarted(InputAction.CallbackContext ctx) => attackController.RequestAttack(ComboInputType.R);

    // ═══════════════════════════════════════════════════════════
    // 매 프레임 처리
    // ═══════════════════════════════════════════════════════════

    private void FixedUpdate()
    {
        UpdateGrounded();
        ApplyMovement();
    }

    private void UpdateGrounded()
    {
        bool wasGrounded = isGrounded;
        isGrounded = groundCheck
            ? Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundLayer)
            : false;

        // 착지 시 점프/회피 횟수 복구
        if (!wasGrounded && isGrounded)
        {
            remainingJumps = maxJumpCount;
            dodgeController?.OnLanded();
        }
    }

    private void ApplyMovement()
    {
        // ── ⓪ 수동 이동(코루틴에 의한 강제 위치 이동)이 활성화되어 있다면 플레이어 물리 속도 제어를 우회 ──
        if (capsuleController.IsManualMoving) return;

        // ── ① 경직 중: 이동 완전 차단 (넉백 속도는 TestHittableComponent 코루틴이 관리) ──
        if (isStunned) return;

        // ── ② 공격 중 처리 (자동 이동, 조작 차단/가속/제동 등 물리 연산 포함) ──
        if (attackController.IsAttacking)
        {
            ApplyAttackMovement();
            return;
        }
        else
        {
            if (wasAttackActiveLastFrame)
            {
                if (attackController.CurrentAllowSlideAfterAction)
                {
                    isSlidingAfterAction = true;
                }
                wasAttackActiveLastFrame = false;
            }
        }

        // ── ③ 일반 이동 ──
        if (moveInput.sqrMagnitude < 0.01f)
        {
            if (isSlidingAfterAction)
            {
                Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                if (flatVel.magnitude < 0.15f)
                {
                    isSlidingAfterAction = false;
                    rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
                }
            }
            else
            {
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            }
            SetAnimatorSpeed(0f);
            return;
        }

        isSlidingAfterAction = false;

        float speed = isRunning ? runSpeed : moveSpeed;
        Vector3 dir = new Vector3(moveInput.x, 0f, moveInput.y);
        Vector3 velocity = dir.normalized * speed;
        rb.linearVelocity = new Vector3(velocity.x, rb.linearVelocity.y, velocity.z);

        SetAnimatorSpeed(new Vector2(rb.linearVelocity.x, rb.linearVelocity.z).magnitude);

        if (Mathf.Abs(moveInput.x) > 0.1f)
        {
            float yRot = moveInput.x > 0f ? 180f : 0f;
            transform.rotation = Quaternion.Euler(0f, yRot, 0f);
        }
    }

    /// <summary>
    /// 공격 중 이동 허용 시 제한된 속도로 이동합니다.
    /// forwardMoveOnly == true면 현재 바라보는 방향의 입력만 수용합니다.
    /// </summary>
    /// <summary>
    /// 공격 중 이동/물리 제어.
    /// 자동 이동 및 사용자의 동적 입력(제동/가속/이동 고정)을 반영합니다.
    /// </summary>
    private void ApplyAttackMovement()
    {
        // 1. 플레이어가 바라보는 방향 판단: Y 오일러 180°이면 +X(오른쪽), 0°이면 -X(왼쪽)
        float yEuler = transform.eulerAngles.y;
        float facingSignX = Mathf.Abs(Mathf.DeltaAngle(yEuler, 180f)) < 90f ? 1f : -1f;

        // ⓪ 첫 프레임 진입 시 가상 속도를 초기 속도로 대입하여 즉시 출발 처리
        if (!wasAttackActiveLastFrame)
        {
            float initialSpeed = attackController.CurrentUseStartEase ? attackController.CurrentStartMoveSpeed : attackController.CurrentAutoMoveSpeed;
            activeAttackVelocity = new Vector3(initialSpeed * facingSignX, 0f, 0f);
            wasAttackActiveLastFrame = true;
        }

        // 2. 입력의 방향성 분석
        bool hasInput = moveInput.sqrMagnitude > 0.01f;
        float inputDirectionX = moveInput.x;

        // 순방향 입력 여부 (바라보는 방향과 입력 방향의 일치 여부)
        bool isForwardInput = hasInput && (inputDirectionX * facingSignX > 0.1f);
        // 역방향 입력 여부
        bool isOppositeInput = hasInput && (inputDirectionX * facingSignX < -0.1f);

        // 3. 속도 계산
        float targetSpeedX = 0f;
        float targetSpeedZ = 0f;

        // 동적 이동 제어(자동 이동/돌진, 조작 고정 등) 허용 상태 체크
        bool enableDynamic = attackController.CurrentEnableDynamicMovement;
        
        float autoSpeed = 0f;
        bool isInEaseRange = false; // 현재 프레임이 이징이 켜져있고 가감속 적용 구간인지 감지하는 플래그

        if (enableDynamic)
        {
            float startEaseDuration = Mathf.Max(0f, attackController.CurrentStartEaseDuration);
            float elapsed = attackController.CurrentActionElapsedTime;

            if (attackController.CurrentUseStartEase && startEaseDuration > 0.001f && elapsed < startEaseDuration)
            {
                float t = Mathf.Clamp01(elapsed / startEaseDuration);
                float tVal = CapsuleController.ApplyEaseCurve(t, attackController.CurrentStartEaseType, attackController.CurrentStartEaseExponent);
                autoSpeed = Mathf.Lerp(attackController.CurrentStartMoveSpeed, attackController.CurrentAutoMoveSpeed, tVal);
                isInEaseRange = true;
            }
            else
            {
                autoSpeed = attackController.CurrentAutoMoveSpeed;
            }
        }
        
        // 기본은 자동 이동 속도에서 출발
        float currentSpeed = autoSpeed;

        if (enableDynamic)
        {
            // 반대 방향 입력 시 제동 (입력된 제동 수치만큼 속도를 감속, 0 이하로 내려가지 않도록 차단)
            if (attackController.CurrentBrakeOnOppositeInput && isOppositeInput)
            {
                currentSpeed = Mathf.Max(0f, currentSpeed - attackController.CurrentOppositeBrakeSpeed);
            }
            // 순방향 입력 시 가속
            else if (attackController.CurrentAccelerateOnForwardInput && isForwardInput)
            {
                currentSpeed += attackController.CurrentForwardAccelerationSpeed;
            }
        }

        targetSpeedX = currentSpeed * facingSignX;

        // 조작 가능 공격 상태(allowMoveWhileAttacking == true)인 경우
        if (attackController.CurrentAllowsMove)
        {
            float manualSpeed = attackController.CurrentAttackMoveSpeed;

            if (attackController.CurrentForwardOnly)
            {
                // 바라보는 앞 방향으로만 조작 가능한 경우
                if (isForwardInput)
                {
                    targetSpeedX = facingSignX * manualSpeed;
                }
                else if (!isOppositeInput && autoSpeed == 0f)
                {
                    targetSpeedX = 0f;
                }
            }
            else
            {
                // 전 방향 조작 가능한 경우
                if (hasInput)
                {
                    Vector3 manualDir = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
                    if (autoSpeed == 0f)
                    {
                        targetSpeedX = manualDir.x * manualSpeed;
                    }
                    targetSpeedZ = manualDir.z * manualSpeed;
                }
            }
        }

        // 공격 중 가감속 보간 적용 (지면 마찰력을 우회하기 위해 activeAttackVelocity 가상 필드 사용)
        float finalSpeedX = targetSpeedX;
        float finalSpeedZ = targetSpeedZ;

        if (isInEaseRange)
        {
            // 반응성 15f 상수로 가상 속도를 타겟 속도에 고정 보간
            float lerpT = 15f * Time.fixedDeltaTime;
            activeAttackVelocity.x = Mathf.Lerp(activeAttackVelocity.x, targetSpeedX, lerpT);
            activeAttackVelocity.z = Mathf.Lerp(activeAttackVelocity.z, targetSpeedZ, lerpT);
            finalSpeedX = activeAttackVelocity.x;
            finalSpeedZ = activeAttackVelocity.z;
        }
        else
        {
            // 이징 미사용 구간이거나 체크박스가 꺼졌다면 즉시 물리 속도를 타겟 속도에 대입하여 즉시 정지/즉시 속도 동기화
            activeAttackVelocity.x = targetSpeedX;
            activeAttackVelocity.z = targetSpeedZ;
            finalSpeedX = targetSpeedX;
            finalSpeedZ = targetSpeedZ;
        }

        // Y축 중력 속도는 그대로 유지하면서 속도 설정
        rb.linearVelocity = new Vector3(finalSpeedX, rb.linearVelocity.y, finalSpeedZ);

        SetAnimatorSpeed(new Vector2(rb.linearVelocity.x, rb.linearVelocity.z).magnitude);
    }

    /// <summary>Animator speed 파라미터를 안전하게 갱신합니다.</summary>
    private void SetAnimatorSpeed(float speed)
    {
        if (animController && animController.animator)
            animController.animator.SetFloat("speed", speed);
    }

    // ═══════════════════════════════════════════════════════════
    // 피격 입력 잠금 (TestHittableComponent에서 호출)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 피격 경직 발생 시 TestHittableComponent에서 호출합니다.
    /// 콤보를 취소하고 duration 동안 이동 입력을 잠급니다.
    /// </summary>
    public void LockInput(float duration)
    {
        attackController.CancelCombo();
        if (stunCoroutine != null) StopCoroutine(stunCoroutine);
        stunCoroutine = StartCoroutine(StunRoutine(duration));
    }

    private System.Collections.IEnumerator StunRoutine(float duration)
    {
        isStunned = true;
        yield return new WaitForSeconds(duration);
        isStunned = false;
        stunCoroutine = null;
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
