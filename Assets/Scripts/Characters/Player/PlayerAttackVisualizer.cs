using UnityEngine;
using System.Collections;

// 이 컴포넌트는 PlayerAttack이 있어야만 작동함
[RequireComponent(typeof(PlayerAttack))]
public class PlayerAttackVisualizer : MonoBehaviour
{
    private PlayerAttack _playerAttack;
    private bool _isVisualizingAttack = false; // 현재 공격 중인지 확인용

    private void Awake()
    {
        _playerAttack = GetComponent<PlayerAttack>();
    }

    private void OnEnable()
    {
        // 컨트롤러의 공격 이벤트 구독 (연결)
        if (_playerAttack != null)
        {
            _playerAttack.OnAttackPerformed += HandleAttackVisual;
        }
    }

    private void OnDisable()
    {
        // 구독 해제 (메모리 관리)
        if (_playerAttack != null)
        {
            _playerAttack.OnAttackPerformed -= HandleAttackVisual;
        }
    }

    // 공격 신호를 받으면 실행되는 함수
    private void HandleAttackVisual()
    {
        // 기존 코루틴이 있다면 멈추고 새로 시작
        StopAllCoroutines();
        StartCoroutine(FlashGizmoRoutine());
    }

    // 0.2초 동안 빨간불을 켜주는 코루틴
    private IEnumerator FlashGizmoRoutine()
    {
        _isVisualizingAttack = true;
        yield return new WaitForSeconds(0.1f); // 0.2초 대기
        _isVisualizingAttack = false;
    }

    // 에디터 씬 뷰에 박스를 그리는 함수
    private void OnDrawGizmos()
    {
        // 컨트롤러나 데이터가 없으면 그리지 않음
        if (_playerAttack == null) _playerAttack = GetComponent<PlayerAttack>();
        if (_playerAttack == null || _playerAttack.currentAttack == null || _playerAttack.attackPoint == null) return;

        // 색상 결정: 공격 중이면 빨강, 평소엔 연한 초록
        if (_isVisualizingAttack)
        {
            Gizmos.color = new Color(1, 0, 0, 0.7f); // 빨간색 (공격!)
        }
        else
        {
            Gizmos.color = new Color(0, 1, 0, 0.2f); // 초록색 (대기)
        }

        // 박스의 위치와 회전 행렬 계산
        Matrix4x4 rotationMatrix = Matrix4x4.TRS(
            _playerAttack.attackPoint.TransformPoint(_playerAttack.currentAttack.hitboxOffset),
            _playerAttack.attackPoint.rotation,
            Vector3.one // Scale은 이미 TransformPoint 계산에 포함되었으므로 여기선 1
        );

        Gizmos.matrix = rotationMatrix;

        // 실제 그리기
        Gizmos.DrawCube(Vector3.zero, _playerAttack.currentAttack.hitboxSize);
        Gizmos.DrawWireCube(Vector3.zero, _playerAttack.currentAttack.hitboxSize);
    }
}