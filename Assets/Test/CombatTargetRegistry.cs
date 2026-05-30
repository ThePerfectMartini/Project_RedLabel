using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GameObject.FindWithTag 를 완전히 대체하는 경량 전역 타겟 레지스트리.
/// 플레이어·적 등이 OnEnable/OnDisable 시 자동 등록·해제됩니다.
///
/// 사용법:
///   등록 : CombatTargetRegistry.Register("Player", transform);
///   해제 : CombatTargetRegistry.Unregister("Player", transform);
///   조회 : CombatTargetRegistry.GetFirst("Player");
///   최근접: CombatTargetRegistry.GetNearest("Player", from);
/// </summary>
public static class CombatTargetRegistry
{
    private static readonly Dictionary<string, List<Transform>> registry = new Dictionary<string, List<Transform>>();

    // ═══════════════════════════════════════════════════════════
    // 등록 / 해제
    // ═══════════════════════════════════════════════════════════

    public static void Register(string tag, Transform target)
    {
        if (string.IsNullOrEmpty(tag) || target == null) return;

        if (!registry.ContainsKey(tag))
            registry[tag] = new List<Transform>();

        if (!registry[tag].Contains(target))
            registry[tag].Add(target);
    }

    public static void Unregister(string tag, Transform target)
    {
        if (string.IsNullOrEmpty(tag) || target == null) return;

        if (registry.TryGetValue(tag, out var list))
            list.Remove(target);
    }

    // ═══════════════════════════════════════════════════════════
    // 조회
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 태그에 등록된 첫 번째 살아 있는 Transform을 반환합니다.
    /// 없으면 null.
    /// </summary>
    public static Transform GetFirst(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return null;
        if (!registry.TryGetValue(tag, out var list)) return null;

        // 파괴된 오브젝트 자동 정리
        list.RemoveAll(t => !t);
        return list.Count > 0 ? list[0] : null;
    }

    /// <summary>
    /// 태그에 등록된 Transform 중 from 위치에서 가장 가까운 것을 반환합니다.
    /// 없으면 null.
    /// </summary>
    public static Transform GetNearest(string tag, Vector3 from)
    {
        if (string.IsNullOrEmpty(tag)) return null;
        if (!registry.TryGetValue(tag, out var list)) return null;

        list.RemoveAll(t => !t);

        Transform nearest = null;
        float minSqrDist = float.MaxValue;
        foreach (var t in list)
        {
            float sqrDist = (t.position - from).sqrMagnitude;
            if (sqrDist < minSqrDist)
            {
                minSqrDist = sqrDist;
                nearest = t;
            }
        }
        return nearest;
    }

    /// <summary>
    /// 태그에 등록된 모든 살아 있는 Transform 목록을 반환합니다.
    /// </summary>
    public static IReadOnlyList<Transform> GetAll(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return System.Array.Empty<Transform>();
        if (!registry.TryGetValue(tag, out var list)) return System.Array.Empty<Transform>();

        list.RemoveAll(t => !t);
        return list;
    }

    /// <summary>
    /// 씬 전환 등에서 레지스트리 전체를 초기화합니다.
    /// </summary>
    public static void Clear() => registry.Clear();
}
