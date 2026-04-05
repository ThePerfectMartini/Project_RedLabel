using System; // Action 이벤트를 사용하기 위해 추가
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler
{
    private Player _player;
    
    public Vector2 MoveInput { get; private set; }
    public bool RunTriggered { get; private set; }

    public event Action OnAttackEvent;
    private PlayerInputAction _inputActions;

    private float _lastTapTime;
    private float _tapThreshold = 0.25f;
    private int _lastTapDirection = 0;

    public PlayerInputHandler(Player player)
    {
        _player = player;
        _inputActions = new PlayerInputAction();

        _inputActions.Player.Move.performed += ctx => MoveInput = ctx.ReadValue<Vector2>();
        _inputActions.Player.Move.canceled += ctx => 
        {
            MoveInput = Vector2.zero;
            RunTriggered = false; 
        };
        _inputActions.Player.Move.started += CheckRunInput;
        
        _inputActions.Player.Attack.started += TriggerAttackEvent;
    }

    public void Enable()
    {
        _inputActions.Enable();
    }

    public void Disable()
    {
        _inputActions.Disable();
    }
    
    private void TriggerAttackEvent(InputAction.CallbackContext ctx)
    {
        // 구독자가 있을 경우에만 실행 (? 연산자)
        OnAttackEvent?.Invoke();
        RunTriggered = false;
    }
    private void CheckRunInput(InputAction.CallbackContext ctx)
    {
        Vector2 input = ctx.ReadValue<Vector2>();

        if (Mathf.Abs(input.y) > 0.1f) return;

        int currentDirection = (int)Mathf.Sign(input.x);

        if (Time.time - _lastTapTime < _tapThreshold && currentDirection == _lastTapDirection)
        {
            RunTriggered = true;
        }
        else
        {
            _lastTapTime = Time.time;
            _lastTapDirection = currentDirection;
        }
    }
}