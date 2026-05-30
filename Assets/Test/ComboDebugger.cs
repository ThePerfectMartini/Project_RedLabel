using UnityEngine;
using UnityEngine.InputSystem; // New Input System 네임스페이스 추가

/// <summary>
/// 플레이어 캐릭터의 콤보 및 공격 컨트롤러 상태를 진단하고
/// 강제로 입력을 주입해볼 수 있는 디버그 테스트 컴포넌트.
/// </summary>
public class ComboDebugger : MonoBehaviour
{
    [Header("진단 대상")]
    [Tooltip("플레이어 공격 컨트롤러. 미지정 시 이 오브젝트에서 탐색합니다.")]
    public PlayerAttackController attackController;
    [Tooltip("이동을 처리하는 캡슐 컨트롤러. 미지정 시 이 오브젝트에서 탐색합니다.")]
    public CapsuleController capsuleController;

    private Rigidbody rb;
    private bool isGroundedLocal;

    private void Awake()
    {
        if (!attackController)
            attackController = GetComponent<PlayerAttackController>();
        if (!capsuleController)
            capsuleController = GetComponent<CapsuleController>();

        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (!attackController) return;

        // ── 1. 키보드 다이렉트 디버그 입력 (New Input System API 직접 검사) ──
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            // 숫자 1 또는 키패드 1 누를 시 X 공격 강제 트리거
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
            {
                Debug.Log("[ComboDebugger] New Input System 키보드 1 입력 감지 -> X 공격 요청");
                attackController.RequestAttack(ComboInputType.X);
            }

            // 숫자 2 또는 키패드 2 누를 시 Z 공격 강제 트리거
            if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
            {
                Debug.Log("[ComboDebugger] New Input System 키보드 2 입력 감지 -> Z 공격 요청");
                attackController.RequestAttack(ComboInputType.Z);
            }

            // 숫자 0 또는 키패드 0 누를 시 콤보 강제 캔슬
            if (keyboard.digit0Key.wasPressedThisFrame || keyboard.numpad0Key.wasPressedThisFrame)
            {
                Debug.Log("[ComboDebugger] New Input System 키보드 0 입력 감지 -> 콤보 강제 캔슬");
                attackController.CancelCombo();
            }
        }
    }

    private void FixedUpdate()
    {
        if (attackController && attackController.groundCheck)
        {
            isGroundedLocal = Physics.CheckSphere(
                attackController.groundCheck.position,
                attackController.groundCheckRadius,
                attackController.groundLayer
            );
        }
    }

    // ── 2. 인스펙터 톱니바퀴 우측 클릭 Context Menu 주입기 ──

    [ContextMenu("★ [디버그] X 공격(기본) 강제 트리거")]
    public void TriggerXAttack()
    {
        if (attackController)
        {
            Debug.LogWarning("[ComboDebugger] ContextMenu -> X 공격 강제 요청 수행");
            attackController.RequestAttack(ComboInputType.X);
        }
    }

    [ContextMenu("★ [디버그] Z 공격(특수) 강제 트리거")]
    public void TriggerZAttack()
    {
        if (attackController)
        {
            Debug.LogWarning("[ComboDebugger] ContextMenu -> Z 공격 강제 요청 수행");
            attackController.RequestAttack(ComboInputType.Z);
        }
    }

    [ContextMenu("★ [디버그] 콤보 강제 리셋")]
    public void ForceResetCombo()
    {
        if (attackController)
        {
            Debug.LogWarning("[ComboDebugger] ContextMenu -> 콤보 강제 캔슬 수행");
            attackController.CancelCombo();
        }
    }

    // ── 3. 화면 내 실시간 상태 표기 OnGUI ──
    private void OnGUI()
    {
        if (!attackController)
        {
            GUI.Box(new Rect(10, 10, 300, 40), "Combo Debugger\n[에러] PlayerAttackController를 찾을 수 없음");
            return;
        }

        GUI.Box(new Rect(10, 10, 350, 190), "=== 플레이어 전투 시스템 디버거 ===");

        GUI.Label(new Rect(20, 35, 320, 20), $"지상 착지 상태 (IsGrounded): {(isGroundedLocal ? "<color=green>지상</color>" : "<color=yellow>공중</color>")}");
        GUI.Label(new Rect(20, 55, 320, 20), $"공격 진행 여부 (IsAttacking): {(attackController.IsAttacking ? "<color=red>공격중</color>" : "대기상태")}");
        
        string activeNodeName = "없음";
        var activeNodeField = typeof(PlayerAttackController).GetField("activeComboNode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (activeNodeField != null)
        {
            var activeNode = activeNodeField.GetValue(attackController) as ComboNodeSO;
            if (activeNode) activeNodeName = activeNode.name;
        }

        string currentStep = "N/A";
        var stepIndexField = typeof(PlayerAttackController).GetField("currentStepIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (stepIndexField != null)
        {
            int index = (int)stepIndexField.GetValue(attackController);
            currentStep = index >= 0 ? $"{index + 1}타 단계" : "없음";
        }

        GUI.Label(new Rect(20, 75, 320, 20), $"진행 중인 콤보 노드: {activeNodeName}");
        GUI.Label(new Rect(20, 95, 320, 20), $"현재 콤보 연계 단계: {currentStep}");
        
        GUI.Label(new Rect(20, 120, 320, 20), $"Z키 공격 노드: {(attackController.zCombo ? "<color=cyan>연결됨</color>" : "미할당")}");
        GUI.Label(new Rect(20, 140, 320, 20), $"X키 공격 노드: {(attackController.xCombo ? "<color=cyan>연결됨</color>" : "미할당")}");

        GUI.Label(new Rect(20, 165, 320, 20), "<b>단축키:</b> [숫자 1] -> X공격  |  [숫자 2] -> Z공격  |  [0] -> 리셋");
    }
}
