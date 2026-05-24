using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [임시 디버그용] PhaseRunner의 가중치 계산 과정과 AttackCaster의 타격 판정을
/// 게임 화면(OnGUI)에 실시간으로 표시하는 모니터.
/// 테스트가 끝나면 이 컴포넌트를 제거하면 됩니다.
/// </summary>
public class PhaseDebugMonitor : MonoBehaviour
{
    [Header("참조 (자동 탐색됨)")]
    [SerializeField] private PhaseSO phase;

    [Tooltip("타겟 태그 (보통 Player)")]
    [SerializeField] private string targetTag = "Player";

    // ── 타격 판정 로그 ──
    private AttackCaster attackCaster;
    private PhaseRunner phaseRunner;
    
    private readonly List<LogEntry> eventLog = new List<LogEntry>();
    private const int maxLogEntries = 20;

    // ── 선택 이력 ──
    private readonly List<PhaseSelectionInfo> selectionHistory = new List<PhaseSelectionInfo>();
    private const int maxSelectionHistory = 8;

    // ── GUI 스타일 ──
    private GUIStyle boxStyle;
    private GUIStyle headerStyle;
    private GUIStyle normalStyle;
    private GUIStyle hitStyle;
    private GUIStyle missStyle;
    private GUIStyle selectStyle;
    private GUIStyle barBgStyle;
    private GUIStyle barFillStyle;
    private bool stylesInitialized;

    // ── 스크롤 ──
    private Vector2 scrollPos;

    private enum LogType { Hit, Select, Info }

    private struct LogEntry
    {
        public float time;
        public string message;
        public LogType type;
    }

    private void Awake()
    {
        attackCaster = GetComponent<AttackCaster>();
        phaseRunner = GetComponent<PhaseRunner>();
    }

    private void OnEnable()
    {
        if (attackCaster)
            attackCaster.OnHitConfirmed += OnHitConfirmed;
        if (phaseRunner)
            phaseRunner.OnSequenceSelected += OnSequenceSelected;
    }

    private void OnDisable()
    {
        if (attackCaster)
            attackCaster.OnHitConfirmed -= OnHitConfirmed;
        if (phaseRunner)
            phaseRunner.OnSequenceSelected -= OnSequenceSelected;
    }

    private void OnHitConfirmed()
    {
        AddLog("★ 타격 성공! (OnHitConfirmed 발행됨)", LogType.Hit);
    }

    private void OnSequenceSelected(PhaseSelectionInfo info)
    {
        // 선택 이력 저장
        selectionHistory.Insert(0, info);
        if (selectionHistory.Count > maxSelectionHistory)
            selectionHistory.RemoveAt(selectionHistory.Count - 1);

        // 로그에도 추가
        string prevName = info.lastIndex >= 0 && info.lastIndex < info.entryNames.Length
            ? info.entryNames[info.lastIndex]
            : "없음";

        float prob = info.probabilities[info.selectedIndex] * 100f;
        AddLog($"▶ 선택: [{info.selectedName}] (확률 {prob:F1}%, 거리 {info.distance:F1}, 직전: {prevName})", LogType.Select);
    }

    private void AddLog(string msg, LogType type)
    {
        eventLog.Insert(0, new LogEntry
        {
            time = Time.time,
            message = msg,
            type = type
        });
        if (eventLog.Count > maxLogEntries)
            eventLog.RemoveAt(eventLog.Count - 1);
    }

    private void InitStyles()
    {
        if (stylesInitialized) return;
        stylesInitialized = true;

        boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.background = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.85f));
        boxStyle.padding = new RectOffset(10, 10, 8, 8);

        headerStyle = new GUIStyle(GUI.skin.label);
        headerStyle.fontStyle = FontStyle.Bold;
        headerStyle.fontSize = 14;
        headerStyle.normal.textColor = new Color(1f, 0.9f, 0.3f);

        normalStyle = new GUIStyle(GUI.skin.label);
        normalStyle.fontSize = 12;
        normalStyle.normal.textColor = Color.white;
        normalStyle.richText = true;

        hitStyle = new GUIStyle(GUI.skin.label);
        hitStyle.fontSize = 11;
        hitStyle.normal.textColor = new Color(0.3f, 1f, 0.3f);
        hitStyle.richText = true;

        missStyle = new GUIStyle(GUI.skin.label);
        missStyle.fontSize = 11;
        missStyle.normal.textColor = new Color(1f, 0.4f, 0.4f);
        missStyle.richText = true;

        selectStyle = new GUIStyle(GUI.skin.label);
        selectStyle.fontSize = 11;
        selectStyle.normal.textColor = new Color(0.5f, 0.8f, 1f);
        selectStyle.richText = true;

        barBgStyle = new GUIStyle();
        barBgStyle.normal.background = MakeTex(2, 2, new Color(0.2f, 0.2f, 0.2f, 1f));

        barFillStyle = new GUIStyle();
        barFillStyle.normal.background = MakeTex(2, 2, new Color(0.2f, 0.7f, 1f, 1f));
    }

    private void OnGUI()
    {
        InitStyles();

        if (!phase) return;

        float panelWidth = 440f;
        float panelX = Screen.width - panelWidth - 15f;
        float panelY = 15f;

        GUILayout.BeginArea(new Rect(panelX, panelY, panelWidth, Screen.height - 30f), boxStyle);
        scrollPos = GUILayout.BeginScrollView(scrollPos);

        // ════════════════════════════════════════
        // 섹션 1: 실시간 가중치 모니터
        // ════════════════════════════════════════
        GUILayout.Label("📊 실시간 가중치 (현재 시점 시뮬레이션)", headerStyle);
        GUILayout.Space(4);

        Vector3 relPos = GetRelativePositionToPlayer();
        float distance = relPos.magnitude;
        float xDist = Mathf.Abs(relPos.x);
        float zDist = Mathf.Abs(relPos.z);
        GUILayout.Label(
            $"플레이어 거리: <color=#00DDFF>{distance:F2}</color>" +
            $"  |  X: <color=#FFD080>{xDist:F2}</color>  Z: <color=#80FFCC>{zDist:F2}</color>",
            normalStyle);
        GUILayout.Space(4);

        float totalWeight = 0f;
        float[] finalWeights = new float[phase.entries.Count];
        int lastIdx = phaseRunner ? phaseRunner.LastSelectedIndex : -1;

        for (int i = 0; i < phase.entries.Count; i++)
        {
            PhaseEntry entry = phase.entries[i];
            if (!entry.actionSequence) continue;

            float distMultiplier = entry.EvaluateDistanceMultiplier(relPos, transform.forward);
            float w = entry.baseWeight * distMultiplier;

            // 연속 선택 패널티 반영
            if (i == lastIdx)
                w *= phase.repeatPenalty;

            w = Mathf.Max(0f, w);
            finalWeights[i] = w;
            totalWeight += w;
        }

        for (int i = 0; i < phase.entries.Count; i++)
        {
            PhaseEntry entry = phase.entries[i];
            if (!entry.actionSequence) continue;

            float distMultiplier = entry.EvaluateDistanceMultiplier(relPos, transform.forward);
            string seqName = entry.actionSequence.name;

            string modeStr;
            switch (entry.distanceMode)
            {
                case DistanceWeightMode.CloseRange:  modeStr = "근거리↑";  break;
                case DistanceWeightMode.FarRange:    modeStr = "원거리↑";  break;
                case DistanceWeightMode.XAxis_Close: modeStr = "X돌진↓"; break;
                case DistanceWeightMode.XAxis_Far:   modeStr = "X돌진↑"; break;
                case DistanceWeightMode.ZAxis_Close: modeStr = "Z기습↓"; break;
                case DistanceWeightMode.ZAxis_Far:   modeStr = "Z기습↑"; break;
                default:                             modeStr = "동일";    break;
            }

            float probability = totalWeight > 0 ? finalWeights[i] / totalWeight : 0f;

            bool isPenalized = (i == lastIdx);
            string penaltyTag = isPenalized
                ? $" <color=#FF6666>× 패널티({phase.repeatPenalty:F2})</color>"
                : "";
            string weightColor = isPenalized ? "#FF9999" : "#FFFFFF";

            GUILayout.Label(
                $"  <b>#{i} {seqName}</b> [{modeStr}]  " +
                $"기본:{entry.baseWeight:F1} × 거리:{distMultiplier:F2}{penaltyTag} = <color={weightColor}>{finalWeights[i]:F2}</color>" +
                $"  → <color=#00FF88>{probability * 100f:F0}%</color>", normalStyle);

            DrawProgressBar(probability, $"선택 확률: {probability * 100f:F1}%");
            GUILayout.Space(2);
        }

        GUILayout.Space(6);
        DrawGUILine();
        GUILayout.Space(4);

        // ════════════════════════════════════════
        // 섹션 2: 패턴 선택 이력 (핵심!)
        // ════════════════════════════════════════
        GUILayout.Label("🎲 패턴 선택 이력 (실제 선택된 기록)", headerStyle);
        GUILayout.Space(4);

        if (selectionHistory.Count == 0)
        {
            GUILayout.Label("  (아직 패턴이 선택되지 않음 — 시퀀스 실행 중일 수 있음)", normalStyle);
        }
        else
        {
            for (int h = 0; h < selectionHistory.Count; h++)
            {
                PhaseSelectionInfo info = selectionHistory[h];
                float elapsed = Time.time - info.time;

                string marker = h == 0 ? " ◀ 현재" : "";

                // 헤더: 선택된 패턴명 + 경과 시간
                GUILayout.Label(
                    $"  <color=#FFD700>[{elapsed:F1}초 전]{marker}</color> " +
                    $"<b><color=#00CCFF>{info.selectedName}</color></b> 선택됨  " +
                    $"(거리: {info.distance:F1})", selectStyle);

                // 각 엔트리의 가중치/확률 스냅샷
                for (int e = 0; e < info.entryNames.Length; e++)
                {
                    bool isSelected = e == info.selectedIndex;
                    bool wasPenalized = e == info.lastIndex;

                    string arrow = isSelected ? " ◀ 선택!" : "";
                    string penalty = wasPenalized ? " (패널티)" : "";
                    string color = isSelected ? "#00FF88" : "#AAAAAA";

                    GUILayout.Label(
                        $"    <color={color}>#{e} {info.entryNames[e]}: " +
                        $"가중치 {info.weights[e]:F3} ({info.probabilities[e] * 100f:F1}%)" +
                        $"{penalty}{arrow}</color>", normalStyle);
                }

                GUILayout.Space(3);
            }
        }

        GUILayout.Space(6);
        DrawGUILine();
        GUILayout.Space(4);

        // ════════════════════════════════════════
        // 섹션 3: 이벤트 통합 로그
        // ════════════════════════════════════════
        GUILayout.Label("📋 이벤트 로그", headerStyle);
        GUILayout.Space(4);

        if (eventLog.Count == 0)
        {
            GUILayout.Label("  (로그 없음)", normalStyle);
        }
        else
        {
            foreach (var log in eventLog)
            {
                float elapsed = Time.time - log.time;
                string timeStr = $"[{elapsed:F1}초 전]";
                GUIStyle style;
                switch (log.type)
                {
                    case LogType.Hit: style = hitStyle; break;
                    case LogType.Select: style = selectStyle; break;
                    default: style = normalStyle; break;
                }
                GUILayout.Label($"  {timeStr} {log.message}", style);
            }
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    // ── 유틸 ──

    private Vector3 GetRelativePositionToPlayer()
    {
        string tag = string.IsNullOrEmpty(targetTag) ? phase.targetTag : targetTag;
        if (string.IsNullOrEmpty(tag)) return Vector3.zero;

        GameObject playerObj = GameObject.FindWithTag(tag);
        if (!playerObj) return Vector3.zero;

        Vector3 diff = playerObj.transform.position - transform.position;
        diff.y = 0f;
        return diff;
    }

    private void DrawProgressBar(float ratio, string label)
    {
        Rect barRect = GUILayoutUtility.GetRect(0, 18, GUILayout.ExpandWidth(true));
        barRect.x += 20f;
        barRect.width -= 40f;

        GUI.Box(barRect, GUIContent.none, barBgStyle);

        Rect fillRect = barRect;
        fillRect.width *= Mathf.Clamp01(ratio);

        Color barColor;
        if (ratio > 0.5f) barColor = new Color(0.2f, 0.8f, 0.3f, 1f);
        else if (ratio > 0.2f) barColor = new Color(0.9f, 0.7f, 0.1f, 1f);
        else barColor = new Color(0.9f, 0.3f, 0.2f, 1f);

        Color prevColor = GUI.color;
        GUI.color = barColor;
        GUI.Box(fillRect, GUIContent.none, barFillStyle);
        GUI.color = prevColor;

        GUIStyle barLabel = new GUIStyle(GUI.skin.label);
        barLabel.fontSize = 10;
        barLabel.alignment = TextAnchor.MiddleCenter;
        barLabel.normal.textColor = Color.white;
        barLabel.fontStyle = FontStyle.Bold;
        GUI.Label(barRect, label, barLabel);
    }

    private void DrawGUILine()
    {
        Rect r = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
        Texture2D tex = MakeTex(1, 1, new Color(0.5f, 0.5f, 0.5f, 0.6f));
        GUI.DrawTexture(r, tex);
    }

    private static Texture2D MakeTex(int w, int h, Color col)
    {
        Color[] pix = new Color[w * h];
        for (int i = 0; i < pix.Length; i++) pix[i] = col;
        Texture2D result = new Texture2D(w, h);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
}
