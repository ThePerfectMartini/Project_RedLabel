using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewActionSequence", menuName = "Action Sequence")]
public class ActionSequenceSO : ScriptableObject
{
    [Header("■ 시퀀스 반복 설정")]
    public bool isInfiniteLoop;
    public int repeatCount = 1;

    [SerializeReference]
    public List<ActionBase> actions = new List<ActionBase>();

    private void OnValidate()
    {
        if (actions == null) return;
        
        foreach (var action in actions)
        {
            if (action == null) continue;

            if (action is MoveAction move)
            {
                if (move.speed <= 0f) move.speed = 0.01f;
                if (move.startSpeed < 0f) move.startSpeed = 0f;
                if (move.acceleration <= 0f) move.acceleration = 0.01f;
                if (move.timeLimit < 0f) move.timeLimit = 0f;
                if (move.playAnimation && string.IsNullOrEmpty(move.animationName)) move.animationName = "Walk";
            }
            else if (action is WaitAction wait)
            {
                if (wait.playAnimation && string.IsNullOrEmpty(wait.animationName)) wait.animationName = "Idle";
            }
            else if (action is RangedAttackAction ranged)
            {
                if (ranged.projectileCount < 1) ranged.projectileCount = 1;
                if (ranged.projectileInterval < 0f) ranged.projectileInterval = 0f;
                if (ranged.playAnimation && string.IsNullOrEmpty(ranged.animationName)) ranged.animationName = "Shoot";
            }
            else if (action is BaseAttackAction attack)
            {
                if (attack.playAnimation && string.IsNullOrEmpty(attack.animationName)) attack.animationName = "Attack";
            }
        }
    }
}