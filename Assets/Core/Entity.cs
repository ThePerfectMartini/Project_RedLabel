using UnityEngine;

// 추상 클래스로 선언하여 Entity 자체를 직접 생성하지 못하게 합니다.
public abstract class Entity : MonoBehaviour
{
    [Header("Base Stats")]
    public float maxHealth = 100f;
    protected float currentHealth;

    [Header("Core Components")]
    protected Rigidbody Rb;
    protected Animator Animator;

    // 가상 메서드(virtual)로 선언하여 자식 클래스에서 재정의할 수 있게 합니다.
    protected virtual void Awake()
    {
        Rb = GetComponent<Rigidbody>();
        Animator = GetComponentInChildren<Animator>();
    }

    protected virtual void Start()
    {
        currentHealth = maxHealth;
    }

    // 공통 피격 로직
    public virtual void TakeDamage(HitData hitData)
    {
        currentHealth -= hitData.damage;
        
        // 피격 애니메이션이나 이펙트 호출 로직 추가 가능
        // anim.SetTrigger("Hit");
        
        // 2. 물리적인 넉백(밀림/띄움) 적용
        if (Rb != null)
        {
            // 밀리기 전에 기존에 가지고 있던 속도를 초기화해야 힘이 일정하게 들어갑니다.
            Rb.linearVelocity = Vector3.zero; 
            
            // 순간적인 물리력(Impulse)을 가하여 날려버립니다.
            Rb.AddForce(hitData.knockbackForce, ForceMode.Impulse);
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    // 공통 사망 로직
    protected virtual void Die()
    {
        // 기본 사망 처리 (오브젝트 파괴 또는 풀링 반환)
        Destroy(gameObject);
    }
}