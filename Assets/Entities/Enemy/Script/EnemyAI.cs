using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    private Enemy enemy;
    
    [Header("추격 타겟")]
    public Transform target; 

    [Header("거리 및 공격 설정")]
    public float detectionRadius = 10f; 
    public float attackRadius = 2f;   
    
    [Tooltip("공격 애니메이션이 끝난 후 추가로 대기할 시간")]
    public float extraCooldown = 0.5f; 
    
    private float attackCooldown; // 애니메이션 길이 + extraCooldown (자동 계산됨)
    private float lastAttackTime = -9999f;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
    }

    private void Start()
    {
        if (!target)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) target = playerObj.transform;
        }
        
        float attackAnimLength = enemy.animationController.GetAnimationLength("EnemyAttack");
        attackCooldown = attackAnimLength + extraCooldown;
    }

    private void Update()
    {
        if (!target) return;

        var currentState = enemy.StateMachine.CurrentState;
        
        // 공격 중이거나 피격 중일 때는 AI 판단 정지
        if (currentState == enemy.StateMachine.HitState || 
            currentState == enemy.StateMachine.AttackState) 
            return;

        Vector3 myPos = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 targetPos = new Vector3(target.position.x, 0, target.position.z);
        float distance = Vector3.Distance(myPos, targetPos);

        // 1순위: 공격 범위 내
        if (distance <= attackRadius)
        {
            // 현재 시간이 (마지막 공격 시간 + 쿨타임)을 지났는지 확인
            if (Time.time >= lastAttackTime + attackCooldown)
            {
                enemy.StateMachine.ChangeState(enemy.StateMachine.AttackState);
                lastAttackTime = Time.time; // 공격을 실행했으므로 시간 갱신
            }
            else
            {
                // 쿨타임이 아직 안 돌았다면, 공격하지 않고 대기(Idle) 상태 유지
                if (currentState != enemy.StateMachine.IdleState)
                    enemy.StateMachine.ChangeState(enemy.StateMachine.IdleState);
                
                Vector3 directionToTarget = Get8WayDirectionToTarget();
                enemy.Movement.FaceTarget(directionToTarget);
            }
        }
        // 2순위: 감지 범위 내 -> 추격(Move)
        else if (distance <= detectionRadius)
        {
            if (currentState != enemy.StateMachine.MoveState)
                enemy.StateMachine.ChangeState(enemy.StateMachine.MoveState);
        }
        // 3순위: 범위 밖 -> 대기(Idle)
        else
        {
            if (currentState != enemy.StateMachine.IdleState)
                enemy.StateMachine.ChangeState(enemy.StateMachine.IdleState);
        }
    }

    public Vector3 Get8WayDirectionToTarget()
    {
        if (!target) return Vector3.zero;

        Vector3 direction = target.position - transform.position;
        direction.y = 0; 
        direction.Normalize();

        float snapX = Mathf.Round(direction.x);
        float snapZ = Mathf.Round(direction.z);

        return new Vector3(snapX, 0f, snapZ).normalized;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRadius);
    }
}