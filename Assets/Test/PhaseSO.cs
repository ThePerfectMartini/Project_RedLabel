using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 하나의 페이즈 안에서 여러 액션 시퀀스를 가중치 기반으로 선택하고,
/// 콤보 연쇄까지 지원하는 ScriptableObject
/// </summary>
[CreateAssetMenu(fileName = "NewPhase", menuName = "Actions/페이즈")]
public class PhaseSO : ScriptableObject
{
    [Header("페이즈 전역 설정")]
    [Tooltip("이 페이즈를 무한 반복할지 여부")]
    public bool isInfiniteLoop = true;
    [Tooltip("유한 반복 시 반복 횟수")]
    public int repeatCount = 3;

    [Tooltip("직전 선택 시퀀스에 적용할 가중치 패널티 비율 (0 = 완전 차단, 1 = 패널티 없음)")]
    [Range(0f, 1f)]
    public float repeatPenalty = 0.2f;

    [Tooltip("거리 계산에 사용할 타겟 태그 (보통 Player)")]
    public string targetTag = "Player";

    public List<PhaseEntry> entries = new List<PhaseEntry>();
}

// ═══════════════════════════════════════════════════════════════
// 거리 가중치 관련 열거형
// ═══════════════════════════════════════════════════════════════

/// <summary>거리에 따른 가중치 변화 방향</summary>
public enum DistanceWeightMode
{
    /// <summary>거리와 무관하게 항상 동일한 가중치</summary>
    Constant,
    /// <summary>가까울수록 가중치가 높음 (근접 공격용)</summary>
    CloseRange,
    /// <summary>멀수록 가중치가 높음 (원거리 공격용)</summary>
    FarRange,
    /// <summary>Z축이 정렬되었을 때 X축이 가까울수록 높음 (X축 돌진 근거리용)</summary>
    XAxis_Close,
    /// <summary>Z축이 정렬되었을 때 X축이 멀수록 높음 (X축 돌진 원거리용)</summary>
    XAxis_Far,
    /// <summary>X축이 정렬되었을 때 Z축이 가까울수록 높음 (Z축 기습 근거리용)</summary>
    ZAxis_Close,
    /// <summary>X축이 정렬되었을 때 Z축이 멀수록 높음 (Z축 기습 원거리용)</summary>
    ZAxis_Far,
}

/// <summary>min ~ max 사이의 보간 형태</summary>
public enum InterpolationShape
{
    /// <summary>직선 (일정한 기울기)</summary>
    Linear,
    /// <summary>완만하게 시작 → 급격히 변화 (가속형)</summary>
    EaseIn,
    /// <summary>급격히 시작 → 완만하게 마무리 (감속형)</summary>
    EaseOut,
    /// <summary>양쪽 완만, 중간 급격 (S자 곡선)</summary>
    EaseInOut
}

[System.Serializable]
public class PhaseEntry
{
    [Tooltip("실행할 액션 시퀀스")]
    public ActionSequenceSO actionSequence;

    [Tooltip("기본 가중치 (높을수록 선택 확률 증가)")]
    public float baseWeight = 1f;

    [Tooltip("씬 뷰에서 이 시퀀스의 공격 범위 기즈모를 표시")]
    public bool showGizmos = false;

    [Tooltip("씬 뷰에서 이 엔트리의 거리(Min/Max) 활성화 범위를 기즈모로 표시")]
    public bool showRangeGizmo = false;

    [Header("거리 가중치 설정")]
    [Tooltip("거리에 따른 가중치 변화 방향")]
    public DistanceWeightMode distanceMode = DistanceWeightMode.Constant;

    [Tooltip("보간 형태 (직선, 곡선 등)")]
    public InterpolationShape interpolation = InterpolationShape.Linear;

    [Tooltip("효과 시작 거리")]
    public float minDistance = 0f;

    [Tooltip("효과 종료 거리")]
    public float maxDistance = 10f;

    [Tooltip("정렬 허용 오차: 정렬 축 방향의 차이가 이 값 이하일 때만 가중치 작동 (축 정렬 모드 전용)")]
    public float alignThreshold = 0.5f;
    [Tooltip("정면 전용 판정: 플레이어가 이 캐릭터의 정면 방향에 있을 때만 가중치 작동 (축 정렬 모드 전용)")]
    public bool frontFacingOnly = false;

    [Header("콤보 설정")]
    [Tooltip("이 시퀀스가 콤보의 시작인지 여부 (타격 성공 시 후속 콤보 연쇄)")]
    public bool isComboStarter;

    [Tooltip("콤보 후속 시퀀스 목록 (타격 성공 시 순차 실행)")]
    public List<ActionSequenceSO> comboFollowUps = new List<ActionSequenceSO>();

    [Tooltip("콤보 후속 시퀀스별 기즈모 표시 토글 (콤보후속과 1:1 대응)")]
    public List<bool> comboFollowUpGizmos = new List<bool>();

    /// <summary>
    /// 현재 상대 위치에 대한 가중치 곱수를 반환.
    /// 축 정렬 모드일 경우 정렬 조건 미충족 시 0을 반환합니다.
    /// </summary>
    /// <param name="relativePos">몬스터 기준 플레이어의 상대 위치 (Y=0)</param>
    /// <param name="monsterForward">몬스터의 정면 방향 벡터</param>
    public float EvaluateDistanceMultiplier(Vector3 relativePos, Vector3 monsterForward)
    {
        if (distanceMode == DistanceWeightMode.Constant)
            return 1f;

        if (minDistance >= maxDistance)
            return 1f;

        // 단순 거리 모드
        if (distanceMode == DistanceWeightMode.CloseRange || distanceMode == DistanceWeightMode.FarRange)
        {
            float distance = relativePos.magnitude;
            float t = Mathf.Clamp01((distance - minDistance) / (maxDistance - minDistance));
            float shaped = ApplyShape(t);
            return distanceMode == DistanceWeightMode.CloseRange ? 1f - shaped : shaped;
        }

        // 축 정렬 모드
        float alignAxisDist, measureAxisDist;

        if (distanceMode == DistanceWeightMode.XAxis_Close || distanceMode == DistanceWeightMode.XAxis_Far)
        {
            alignAxisDist   = Mathf.Abs(relativePos.z);
            measureAxisDist = Mathf.Abs(relativePos.x);
        }
        else
        {
            alignAxisDist   = Mathf.Abs(relativePos.x);
            measureAxisDist = Mathf.Abs(relativePos.z);
        }

        if (alignAxisDist > alignThreshold) return 0f;
        if (frontFacingOnly && Vector3.Dot(relativePos, monsterForward) <= 0f) return 0f;

        float tAxis     = Mathf.Clamp01((measureAxisDist - minDistance) / (maxDistance - minDistance));
        float shapedAxis = ApplyShape(tAxis);

        bool isClose = distanceMode == DistanceWeightMode.XAxis_Close || distanceMode == DistanceWeightMode.ZAxis_Close;
        return isClose ? 1f - shapedAxis : shapedAxis;
    }

    private float ApplyShape(float t)
    {
        switch (interpolation)
        {
            case InterpolationShape.Linear:    return t;
            case InterpolationShape.EaseIn:    return t * t;
            case InterpolationShape.EaseOut:   return 1f - (1f - t) * (1f - t);
            case InterpolationShape.EaseInOut:
                return t < 0.5f ? 2f * t * t : 1f - 2f * (1f - t) * (1f - t);
            default: return t;
        }
    }
}
