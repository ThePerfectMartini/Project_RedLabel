using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    // 입력 값을 외부(PlayerController)에서 읽어갈 변수들
    public Vector2 MoveInput { get; private set; }
    public bool AttackTriggered { get; private set; }
    public bool JumpTriggered { get; private set; }
    public bool ParryTriggered { get; private set; }
    
    public bool runTriggered { get; private set; }

    // 내부 변수
    private PlayerInputActions _inputActions;

    // 대쉬(더블 탭) 감지용 변수
    private float _lastTapTime;
    private float _tapThreshold = 0.25f; // 이 시간 안에 두 번 눌러야 대쉬 인정
    private int _lastTapDirection = 0;   // -1: 좌, 1: 우, 0: 없음

    private void OnEnable()
    {
        // 1. Input Action 인스턴스 생성 및 활성화
        if (_inputActions == null)
        {
            _inputActions = new PlayerInputActions();

            // 2. 이벤트 구독 (람다식 활용)

            // [이동]
            // performed: 키를 누르고 있는 동안 지속적으로 호출 (값 갱신)
            _inputActions.Player.Move.performed += ctx => MoveInput = ctx.ReadValue<Vector2>();
            // canceled: 키를 떼면 (0,0)으로 초기화
            _inputActions.Player.Move.canceled += ctx => MoveInput = Vector2.zero;

            // [대쉬 감지 로직] started: 키를 누르는 '순간' 한 번만 호출
            _inputActions.Player.Move.started += CheckRunInput;

            // [점프]
            _inputActions.Player.Jump.started += ctx =>
            {
                JumpTriggered = true;
            };

            // [공격]
            _inputActions.Player.Attack.started += ctx =>
            {
                AttackTriggered = true;
            };

            // [패링]
            _inputActions.Player.Parry.started += ctx =>
            {
                ParryTriggered = true;
            };
        }

        _inputActions.Enable();
    }

    private void OnDisable()
    {
        _inputActions.Disable();
    }

    // 더블 탭(대쉬) 감지 알고리즘
    private void CheckRunInput(InputAction.CallbackContext ctx)
    {
        Vector2 input = ctx.ReadValue<Vector2>();

        // Y축(위아래) 입력은 대쉬로 치지 않음 (필요하면 제거 가능)
        if (Mathf.Abs(input.y) > 0.1f) return;

        // 현재 입력 방향 (-1 or 1)
        int currentDirection = (int)Mathf.Sign(input.x);

        // 로직: 이전 탭 시간과 현재 시간 차이가 짧고 && 방향이 같다면 -> 대쉬
        if (Time.time - _lastTapTime < _tapThreshold && currentDirection == _lastTapDirection)
        {
            runTriggered = true;
        }
        else
        {
            // 대쉬 실패(첫 입력) 시 데이터 갱신
            _lastTapTime = Time.time;
            _lastTapDirection = currentDirection;
        }
    }

    // ---[소비(Consume) 메서드]---
    // StateMachine이 입력을 감지하고 행동을 시작하면, 이 함수를 호출해서 
    // "입력 처리 완료" 상태로 만듭니다. (안 그러면 계속 공격 상태가 됨)

    public void UseAttackInput() => AttackTriggered = false;
    public void UseJumpInput() => JumpTriggered = false;
    public void UseParryInput() => ParryTriggered = false;
    public void UseRunInput() => runTriggered = false;
}