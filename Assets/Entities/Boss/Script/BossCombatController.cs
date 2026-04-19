using UnityEngine;
using System.Collections; // 코루틴 사용을 위해 필요

public class BossCombatController : MonoBehaviour
{
    [Header("공격 판정 설정 (3D)")]
    [Tooltip("공격 판정 박스의 크기 (X, Y, Z)")]
    [SerializeField] private Vector3 attackBoxSize = new Vector3(2f, 2f, 3f); 
    
    [Tooltip("캐릭터 중심을 기준으로 한 공격 판정의 발생 위치 오프셋 (X, Y, Z)")]
    [SerializeField] private Vector3 attackOffset = new Vector3(1.5f, 0f, 0f); 
    
    [SerializeField] private LayerMask BossLayer;

    [Header("공격 능력치")]
    [SerializeField] private float damage = 10f;
    
    // 신규 추가: 공격의 물리력 설정
    [Tooltip("뒤로 밀어내는 힘")]
    [SerializeField] private float knockbackPower = 5f; 
    [Tooltip("위로 띄우는 힘")]
    [SerializeField] private float launchPower = 0f;

    [Header("디버그 설정")]
    [SerializeField] private bool showGizmos = true; // 기즈모 표시 여부
    [SerializeField] private float gizmoFlashDuration = 0.1f; // 초록색으로 유지될 시간(초)

    // --- 내부 변수 ---
    private bool _isHitCheckActive = false; // 현재 공격 판정 프레임인지 여부
    private Coroutine _gizmoFlashCoroutine;  // 실행 중인 코루틴 저장용

    // 공격 상태(AttackState)에서 애니메이션 이벤트가 발생할 때 이 함수를 호출
    public void PerformHitCheck()
    {
        // 1. 코루틴을 통해 기즈모 색상 변경 플래그 활성화
        if (_gizmoFlashCoroutine != null) StopCoroutine(_gizmoFlashCoroutine);
        _gizmoFlashCoroutine = StartCoroutine(FlashGizmoCoroutine());

        // 2. 실제 물리 타격 연산 수행
        Vector3 boxCenter = transform.TransformPoint(attackOffset);
        Collider[] hitEntities = Physics.OverlapBox(
            boxCenter,
            attackBoxSize / 2f,
            transform.rotation,
            BossLayer
        );

        foreach (Collider collider in hitEntities)
        {
            // TODO: 적의 데미지 처리 로직 연결
            Entity targetEntity = collider.GetComponent<Entity>();
            
            if (targetEntity != null)
            {
                // 1. 적이 날아갈 방향 계산 
                // 보통 플레이어가 바라보는 방향(forward/right) 혹은 때린 위치에서 적 위치를 향하는 방향을 씁니다.
                // 여기서는 캐릭터가 바라보는 방향(X축 기준이라면 transform.right)을 기준으로 합니다.
                // ※ 이전 대화에서 X축 기준을 선호하셨으므로 transform.right를 기본 밀림 방향으로 둡니다.
                Vector3 pushDirection = transform.right * knockbackPower;
                
                // 2. 위로 띄우는 힘 추가
                Vector3 upwardForce = Vector3.up * launchPower;

                // 3. HitData 꾸러미 포장
                HitData attackData = new HitData
                {
                    damage = this.damage,
                    knockbackForce = pushDirection + upwardForce
                };

                // 4. 적에게 전송!
                targetEntity.TakeDamage(attackData);
            }
        }
    }

    // --- 기즈모 색상을 잠시 초록색으로 바꾸는 코루틴 ---
    private IEnumerator FlashGizmoCoroutine()
    {
        _isHitCheckActive = true;
        yield return new WaitForSeconds(gizmoFlashDuration);
        _isHitCheckActive = false;
    }

    // --- 에디터에서 공격 범위와 오프셋 위치를 시각적으로 확인하기 위한 기즈모 ---
    private void OnDrawGizmos()
    {
        if (!showGizmos) return; // 디버그용 끄기 기능

        // --- 색상 결정 로직 ---
        if (_isHitCheckActive)
        {
            Gizmos.color = Color.green; // 공격 순간엔 초록색!
        }
        else
        {
            // 평소엔 빨간색, 하지만 'Selected'가 아니더라도 보이도록 투명도 조절
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f); 
        }
        // ---------------------

        Vector3 boxCenter = transform.TransformPoint(attackOffset);
        
        Matrix4x4 originalMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(boxCenter, transform.rotation, Vector3.one);
        
        // 꽉 찬 박스(DrawCube)와 테두리(DrawWireCube)를 같이 그려서 더 잘 보이게 함
        Gizmos.DrawWireCube(Vector3.zero, attackBoxSize);
        
        // 공격 순간엔 속까지 꽉 찬 초록색 박스를 그림
        if (_isHitCheckActive)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f); // 약간 투명한 초록색 속
            Gizmos.DrawCube(Vector3.zero, attackBoxSize);
        }

        Gizmos.matrix = originalMatrix;
    }
}