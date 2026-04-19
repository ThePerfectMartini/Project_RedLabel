using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(PlayerMovementController))]
public class Player : Entity
{
    public PlayerStateMachine StateMachine { get; private set; }
    public PlayerInputHandler InputHandler { get; private set; }
    public PlayerMovementController movement;
    public PlayerCombatController combatController;
    
    public AnimationController animationController;
    
    public float moveSpeed = 5f;
    public float runSpeed = 10f;

	[Header("UI")]
	public Image hpFillImage;//체력바 이미지 변수 추가

	// 수정?
	protected override void Awake()
    {
        base.Awake();
        StateMachine = new PlayerStateMachine(this);
        InputHandler = new PlayerInputHandler(this);
        movement = GetComponent<PlayerMovementController>();
        combatController = GetComponent<PlayerCombatController>();
        animationController = GetComponentInChildren<AnimationController>();
    }

    protected override void Start()
    {
        base.Start();
        StateMachine.Initialize(StateMachine.IdleState);

		UpdateHPBar();
	}

    private void OnEnable()
    {
        InputHandler?.Enable();
    }

    private void OnDisable()
    {
        InputHandler?.Disable();
    }

	private void Update()
	{
		
		StateMachine.CurrentState.LogicUpdate();

		if (hpFillImage != null && hpFillImage.fillAmount != targetFillAmount)
		{
			hpFillImage.fillAmount = Mathf.Lerp(hpFillImage.fillAmount, targetFillAmount, Time.deltaTime * hpLerpSpeed);
		}
	}

    private void FixedUpdate()
    {
        StateMachine.CurrentState.PhysicsUpdate();
    }
    
    
    // 부모 클래스(Entity)의 가상 메서드를 오버라이드(재정의)합니다.
    public override void TakeDamage(HitData hitData)
    {
        base.TakeDamage(hitData); // 부모의 데미지 깎기 + 넉백 물리력 적용 실행
        
        StateMachine.ChangeState(StateMachine.HitState);

		UpdateHPBar();

		Debug.Log($"[{gameObject.name}] 피격! 받은 데미지: {hitData.damage} / 남은 체력: {currentHealth}");
    }

	// 사망 시점도 확인하고 싶다면 Die 메서드 역시 오버라이드할 수 있습니다.
	protected override void Die()
    {
        Debug.Log($"[{gameObject.name}] 체력이 모두 소진되어 사망했습니다!");
        
        // 부모의 Die 로직(Destroy)을 실행하여 오브젝트를 파괴합니다.
        base.Die(); 
    }
	
    private float targetFillAmount = 1f; 
	public float hpLerpSpeed = 5f;

	// 체력바 UI를 업데이트하는 전용 함수 추가
	private void UpdateHPBar()
	{
		if (hpFillImage != null)
		{

			targetFillAmount = (float)currentHealth / (float)maxHealth;
		}
	}
}