using System;
using UnityEngine;

public class AnimationController : MonoBehaviour
{
    public event Action OnAnimEnded;
    public event Action OnAttackHitCheck;
    public Animator animator;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public void Play(string animationName)
    {
        animator.Play(animationName);
    }
    
    public void PlaySetTime(string animationName, float time)
    {
        animator.Play("Hit", 0, time);
    }
    

    public void AnimationEnded()
    {
        OnAnimEnded?.Invoke();
    }

    public void OnAttackTriggered()
    {
        OnAttackHitCheck?.Invoke();
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
