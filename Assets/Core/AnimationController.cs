using System;
using System.Collections;
using UnityEngine;

public class AnimationController : MonoBehaviour
{
    public event Action OnAnimEnded;
    public event Action OnAttackHitCheck;
    public Animator animator;
    private AttackCaster attackCaster;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    [Header("테스트 설정")]
    [Tooltip("실제 애니메이션 클립이 없는 임시 큐브 등에서 코드로 타격 이벤트를 시뮬레이션할지 여부")]
    public bool useMockSimulation = false;
    [Tooltip("공격 시작 후 타격 판정(OnAttackImpact)이 발생할 때까지의 시간")]
    public float mockHitDelay = 0.15f;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        attackCaster = GetComponentInParent<AttackCaster>();
    }

    public void Play(string animationName)
    {
        if (animator && animator.runtimeAnimatorController)
        {
            if (animator.HasState(0, Animator.StringToHash(animationName)))
            {
                animator.Play(animationName);
            }
            else
            {
                Debug.LogWarning($"[{gameObject.name}] 애니메이터 컨트롤러에 '{animationName}' 상태가 없습니다!");
            }
        }

        if (useMockSimulation)
        {
            StartCoroutine(SimulateAttackImpactRoutine());
        }
    }

    private IEnumerator SimulateAttackImpactRoutine()
    {
        yield return new WaitForSeconds(mockHitDelay);
        OnAttackImpact();
        
        // 애니메이션이 대략 0.5초 정도 지속된다고 가정하고 종료 이벤트 호출 (콤보 연결용)
        yield return new WaitForSeconds(0.35f);
        AnimationEnded();
    }
    
    public void PlaySetTime(string animationName, float time)
    {
        if (animator && animator.runtimeAnimatorController)
        {
            if (animator.HasState(0, Animator.StringToHash(animationName)))
            {
                animator.Play(animationName, 0, time);
            }
            else
            {
                Debug.LogWarning($"[{gameObject.name}] 애니메이터 컨트롤러에 '{animationName}' 상태가 없습니다!");
            }
        }
    }
    

    public void AnimationEnded()
    {
        OnAnimEnded?.Invoke();
    }

    public void OnAttackTriggered()
    {
        OnAttackHitCheck?.Invoke();
    }
    
    public void OnAttackImpact()
    {
        if (attackCaster != null)
        {
            attackCaster.CastDamage();
        }
    }
    
    
    public float GetAnimationLength(string clipName)
    {
        if (!animator || !animator.runtimeAnimatorController) return 0f;
        
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip.name == clipName)
            {
                return clip.length;
            }
        }
        
        Debug.LogWarning($"[{gameObject.name}] '{clipName}' 애니메이션 클립을 찾을 수 없습니다!");
        return 1f; // 못 찾았을 경우의 기본값(1초)
    }
}
