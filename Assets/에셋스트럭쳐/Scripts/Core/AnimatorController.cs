using UnityEngine;

public class AnimatorController : MonoBehaviour
{
    // 자식 클래스(PlayerAnimationController 등)에서도 쓸 수 있게 protected로 선언
    protected Entity _entity;
    protected Animator _animator;
    
    // Initialize 함수 대신 생성자를 사용합니다.
    protected virtual void Awake()
    {
        _entity = GetComponent<Entity>();
        _animator = GetComponent<Animator>();
    }    
    public virtual void PlayAnimation(int animHash)
    {
        _animator.Play(animHash);
    }

    public virtual bool IsAnimationFinished(int animHash, float finishTime = 1.0f)
    {
        AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        if (!_animator.IsInTransition(0) && stateInfo.shortNameHash == animHash)
        {
            return stateInfo.normalizedTime >= finishTime;
        }
        return false;
    }
}