using System.Collections.Generic;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════
// 열거형
// ═══════════════════════════════════════════════════════════════

/// <summary>콤보 노드의 지상/공중 실행 조건 열거형.</summary>
public enum ComboGroundState
{
    /// <summary>지상에서만 실행 가능</summary>
    Ground,
    /// <summary>공중에서만 실행 가능</summary>
    Air,
    /// <summary>지상, 공중 모두 실행 가능</summary>
    Both,
}

// ═══════════════════════════════════════════════════════════════
// ComboStepPhysics — 콤보 단계별 이동·물리 제어 설정
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// 콤보 공격 단계 실행 중의 이동 및 물리 제어를 정의하는 직렬화 가능 클래스.
/// ComboStep에 내장되어 단계(1타·2타·3타...)마다 다른 이동 특성을 부여합니다.
///
/// 이 설정은 플레이어 전용입니다. 적 AI(PhaseRunner)는 이 필드를 사용하지 않습니다.
/// </summary>
[System.Serializable]
public class ComboStepPhysics
{
    [Header("수동 조작 이동")]
    [Tooltip("공격 동작 중에도 이동 입력을 허용합니다.")]
    public bool allowMoveWhileAttacking = false;

    [Tooltip("true면 현재 바라보는 방향(앞)으로 전진 입력만 수용합니다.")]
    public bool forwardMoveOnly = true;

    [Tooltip("공격 중 수동 조작 이동 속도.")]
    public float attackMoveSpeed = 2f;

    [Header("동적 이동 제어 (자동 돌진 / 제동)")]
    [Tooltip("자동 이동 및 관성 물리를 활성화합니다.")]
    public bool enableDynamicMovement = false;

    [Tooltip("입력 없이 자동으로 이동하는 속도. 양수=전진, 음수=후진.")]
    public float autoMoveSpeed = 0f;

    [Tooltip("출발 시 초기 속도. 가감속 이징 사용 시에만 유효합니다.")]
    public float startMoveSpeed = 0f;

    [Tooltip("반대 방향 입력 시 감속합니다.")]
    public bool brakeOnOppositeInput = false;

    [Tooltip("반대 방향 입력 시 감속 속도 값.")]
    public float oppositeBrakeSpeed = 2f;

    [Tooltip("순방향 입력 시 추가 가속합니다.")]
    public bool accelerateOnForwardInput = false;

    [Tooltip("순방향 입력 시 추가되는 속도 값.")]
    public float forwardAccelerationSpeed = 2f;

    [Header("출발 가감속 이징")]
    [Tooltip("출발 속도부터 목표 속도까지 이징 곡선으로 가속합니다.")]
    public bool useStartEase;

    [Tooltip("이징 곡선 타입.")]
    public EaseType startEaseType = EaseType.EaseOut;

    [Range(1f, 5f)]
    [Tooltip("이징 곡선 지수 강도. 1.0이 직선, 커질수록 급격한 곡선.")]
    public float startEaseExponent = 2f;

    [Tooltip("출발 속도에서 목표 속도까지 도달하는 시간(초).")]
    public float startEaseDuration = 0.2f;

    [Header("기타")]
    [Tooltip("액션 종료 후 자연스럽게 미끄러지도록 허용합니다.")]
    public bool allowSlideAfterAction = false;
}

// ═══════════════════════════════════════════════════════════════
// ComboStep — 단일 공격 단계
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// 단일 공격 단계를 정의하는 직렬화 가능 클래스.
/// </summary>
[System.Serializable]
public class ComboStep
{
    [Tooltip("에디터 및 인스펙터에서 구분하기 위한 표시 이름")]
    public string displayName = "공격";

    [Tooltip("씬 뷰에서 이 단계의 공격 범위 기즈모 표시 여부")]
    public bool showGizmos = false;

    [Tooltip("씬 뷰에서 이 단계의 입력 윈도우 타이밍에 따라 기즈모 색상을 실시간으로 변경하여 표시할지 여부")]
    public bool showWindowGizmos = false;

    [Tooltip("이 단계에서 실행할 ActionSequenceSO (이동 + 공격 + 대기 액션 포함)")]
    public ActionSequenceSO actionSequence;

    [Tooltip("이 공격 시작 후 다음 입력을 받기 시작하는 시점 (초). 이 시간 이전의 빠른 연타는 무시됩니다.")]
    [Range(0f, 2f)]
    public float inputWindowStart = 0.2f;

    [Tooltip("이 공격 단계 실행 시 런처(공중 띄우기) 이벤트도 발행할지 여부")]
    public bool isLauncher = false;

    [Tooltip("이 공격 단계가 에어 피니셔인 경우. true면 히트 시 슬램 연출이 발동됩니다.")]
    public bool isAirFinisher = false;

    [Tooltip("이 콤보 단계 실행 중의 이동·물리 특성을 정의합니다. (플레이어 전용)")]
    public ComboStepPhysics physics = new ComboStepPhysics();

    /// <summary>주어진 경과 시간이 다음 콤보 입력 윈도우 안에 있는지 확인합니다.</summary>
    public bool IsInputWindowOpen(float elapsed)
        => elapsed >= inputWindowStart;
}

// ═══════════════════════════════════════════════════════════════
// ComboNodeSO — 콤보 ScriptableObject
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// 콤보 데이터를 관리하는 ScriptableObject.
/// </summary>
[CreateAssetMenu(fileName = "NewComboNode", menuName = "Actions/콤보 노드")]
public class ComboNodeSO : ScriptableObject
{
    [Header("콤보 공통 설정")]
    [Tooltip("콤보 이름")]
    public string comboName = "기본 공격 콤보";

    [Tooltip("이 콤보 세트 전체의 발동 조건 (지상/공중/모두)")]
    public ComboGroundState allowedState = ComboGroundState.Ground;

    [Header("콤보 순서 리스트")]
    [Tooltip("콤보의 연타 순서대로 액션시퀀스와 입력을 조절하는 리스트")]
    public List<ComboStep> comboSteps = new List<ComboStep>();
}
