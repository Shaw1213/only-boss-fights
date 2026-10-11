using UnityEngine;

namespace DreamLayerJam
{
    /// <summary>One boss move: one or more attack steps played back to back.</summary>
    [CreateAssetMenu(menuName = "DreamLayer Jam/Boss Move", fileName = "NewBossMove")]
    public class BossMove : ScriptableObject
    {
        [Tooltip("Steps play back to back. A step's recovery plus the next step's startup sets the rhythm between hits; the last step's recovery is the hero's punish window.")]
        public AttackData[] steps;

        [Header("Spacing")]
        [Tooltip("Distance to the hero the boss walks to before starting this move.")]
        public float preferredRange = 2.5f;
        [Min(0f)] public float rangeTolerance = 0.8f;

        [Header("Selection")]
        [Min(0f)] public float weight = 1f;
        [Tooltip("Parrying every step of this move staggers the boss instantly.")]
        public bool fullParryStaggers;
        [Tooltip("Later steps keep the first step's facing (a line attack marches on even if the hero dashes through it).")]
        public bool lockFacing;

        public float TotalTime
        {
            get
            {
                float t = 0f;
                if (steps != null)
                    foreach (var step in steps)
                        if (step != null) t += step.TotalTime;
                return t;
            }
        }
    }
}
