using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 콤보 트리의 단일 노드를 정의하는 ScriptableObject.
/// 각 노드는 한 번의 공격 동작을 표현하며, children 리스트를 통해
/// 다음 연결 가능한 공격(콤보 연장)을 트리 구조로 정의합니다.
///
/// 트리 구조 예시:
///   루트(없음)
///   ├─ GroundAttack1 ← 지상 공격 시작점
///   │   ├─ GroundAttack2 (Ground)
///   │   │   └─ GroundAttack3 (Ground)  ← 지상 3타
///   │   └─ LaunchAttack (Ground) ← 런처 (공중으로 띄움)
///   │       └─ AirAttack1 (Air)
///   │           └─ AirAttack2 (Air)
///   │               └─ AirFinisher (Air) ← 에어 피니셔
///   └─ AirAttack1 ← 공중에서 시작 가능
/// </summary>
[CreateAssetMenu(fileName = "NewComboNode", menuName = "Test/Combat/Combo Node")]
public class ComboNodeSO : ScriptableObject
{
    // ── 기본 정보 ──────────────────────────────────────────────
    [Header("기본 정보")]
    [Tooltip("에디터에서 구분하기 위한 표시 이름 (코드에서 사용 안 함)")]
    public string displayName = "공격";

    // ── 실행 데이터 ────────────────────────────────────────────
    [Header("실행 데이터")]
    [Tooltip("이 노드가 실행할 ActionSequenceSO (이동 + 공격 + 대기 액션 포함)")]
    public ActionSequenceSO actionSequence;

    // ── 상태 조건 ──────────────────────────────────────────────
    [Header("실행 조건")]
    [Tooltip("지상 전용 / 공중 전용 / 양쪽 모두 가능")]
    public ComboGroundState allowedState = ComboGroundState.Ground;

    // ── 타이밍 ─────────────────────────────────────────────────
    [Header("콤보 타이밍")]
    [Tooltip("이 공격 시작 후 다음 입력을 받기 시작하는 시점 (초). 너무 빠르면 의도치 않은 콤보 연장이 발생합니다.")]
    [Range(0f, 2f)]
    public float inputWindowStart = 0.2f;

    [Tooltip("이 공격 시작 후 다음 입력을 마감하는 시점 (초). 이 시간이 지나면 콤보가 리셋됩니다.")]
    [Range(0f, 3f)]
    public float inputWindowEnd = 0.8f;

    // ── 콤보 연결 ──────────────────────────────────────────────
    [Header("다음 콤보")]
    [Tooltip("이 노드에서 연결 가능한 다음 공격 목록. 순서대로 입력 시 첫 번째 유효 노드가 선택됩니다.")]
    public List<ComboNodeSO> children = new List<ComboNodeSO>();

    // ── 연출 트리거 ─────────────────────────────────────────────
    [Header("연출 트리거")]
    [Tooltip("이 노드 실행 시 CombatEventBus.RaiseHitDealt 외에 런처 이벤트도 발행할지 여부")]
    public bool isLauncher = false;

    [Tooltip("이 노드가 에어 피니셔인 경우. true면 히트 시 슬램 연출이 발동됩니다.")]
    public bool isAirFinisher = false;

    // ── 입력 유효성 ────────────────────────────────────────────
    /// <summary>주어진 경과 시간이 입력 윈도우 안에 있는지 확인합니다.</summary>
    public bool IsInputWindowOpen(float elapsed)
        => elapsed >= inputWindowStart && elapsed <= inputWindowEnd;
}

/// <summary>
/// 콤보 노드의 지상/공중 실행 조건 열거형.
/// </summary>
public enum ComboGroundState
{
    /// <summary>지상에서만 실행 가능</summary>
    Ground,
    /// <summary>공중에서만 실행 가능</summary>
    Air,
    /// <summary>지상, 공중 모두 실행 가능</summary>
    Both,
}
