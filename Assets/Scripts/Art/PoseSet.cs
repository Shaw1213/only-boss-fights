using System;
using UnityEngine;

namespace DreamLayerJam
{
    /// <summary>A sprite animation with per-frame durations (e.g. a DreamLayer sprite sheet's frame_durations_ms).</summary>
    [Serializable]
    public class Flipbook
    {
        public Sprite[] frames;
        [Tooltip("Seconds per frame. Missing or zero entries use 0.1 s.")]
        public float[] frameSeconds;
        public bool loop = true;
        [Tooltip("Size relative to the idle sprite's pixel scale. Sprite sheets come back at their own resolution (e.g. 256 px cells), so measure and set this once.")]
        public float scale = 1f;
        [Tooltip("Footfalls in one loop of a walk (footstep sounds land every 1/N of the ground a loop covers).")]
        public int footfallsPerLoop = 2;

        public bool IsValid => frames != null && frames.Length > 0;

        public float Duration
        {
            get
            {
                float t = 0f;
                if (frames != null)
                    for (int i = 0; i < frames.Length; i++) t += FrameTime(i);
                return t;
            }
        }

        float FrameTime(int i) => frameSeconds != null && i < frameSeconds.Length && frameSeconds[i] > 0f ? frameSeconds[i] : 0.1f;

        /// <summary>The frame showing at <paramref name="time"/> seconds (negative time plays a loop backwards).</summary>
        public Sprite Sample(float time)
        {
            if (!IsValid) return null;
            float total = Duration;
            float t = loop ? Mathf.Repeat(time, total) : Mathf.Clamp(time, 0f, total - 0.0001f);
            for (int i = 0; i < frames.Length; i++)
            {
                t -= FrameTime(i);
                if (t < 0f) return frames[i];
            }
            return frames[frames.Length - 1];
        }
    }

    /// <summary>
    /// A character's state poses (DreamLayer art). Attack poses live on each <see cref="AttackData"/>.
    /// Sprites face right with the pivot at the feet; every pose shares one scale, so normalize poses to
    /// one pixel scale before import (Tools/normalize_pose.py).
    /// </summary>
    [CreateAssetMenu(menuName = "DreamLayer Jam/Pose Set", fileName = "PoseSet")]
    public class PoseSet : ScriptableObject
    {
        [Tooltip("Height of the idle sprite in world units. Sets the scale for every pose of this character.")]
        public float idleWorldHeight = 2.2f;
        public Sprite idle;
        [Tooltip("Walk / run cycle (hero moving, boss approaching).")]
        public Flipbook move;
        [Tooltip("Ground speed (units/s) at which the move cycle plays at its authored rate.")]
        public float moveCycleSpeed = 6f;
        [Tooltip("Idle breathing: vertical stretch from the feet (0.012 = 1.2%).")]
        public float idleBreathAmount = 0.012f;
        [Tooltip("Idle breathing speed in radians per second (2.4 = one breath every 2.6 s).")]
        public float idleBreathRate = 2.4f;
        public Sprite dash;
        public Sprite charge;
        public Sprite parry;
        public Sprite hurt;
        public Sprite staggered;
        public Sprite dead;
        [Tooltip("Boss only: shown during the phase-2 transformation.")]
        public Sprite phaseChange;

        public bool IsUsable => idle != null;

        /// <summary>Ground covered between footfalls (units, at size 1): the loop's ground per footfall.</summary>
        public float StepLength => move != null && move.IsValid && moveCycleSpeed > 0f
            ? moveCycleSpeed * move.Duration / Mathf.Max(1, move.footfallsPerLoop)
            : 1.2f;
        public float Scale => idle != null && idle.bounds.size.y > 0f ? idleWorldHeight / idle.bounds.size.y : 1f;

        /// <summary>The first non-null sprite (pose fallbacks).</summary>
        public static Sprite First(params Sprite[] options)
        {
            foreach (var s in options)
                if (s != null) return s;
            return null;
        }
    }
}
