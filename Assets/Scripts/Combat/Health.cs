using System;
using UnityEngine;

namespace DreamLayerJam
{
    public class Health : MonoBehaviour
    {
        [SerializeField, Min(1)] int max = 5;

        public int Max => max;
        public int Current { get; private set; }
        public bool IsDead => Current <= 0;
        public float Normalized => max > 0 ? (float)Current / max : 0f;

        public event Action<int> Damaged;
        public event Action Died;

        /// <summary>Called by the owning controller in Awake with the value from its config.</summary>
        public void Init(int maxHealth)
        {
            max = Mathf.Max(1, maxHealth);
            Current = max;
        }

        public void Heal(int amount)
        {
            if (IsDead || amount <= 0) return;
            Current = Mathf.Min(max, Current + amount);
        }

        public void TakeDamage(int amount)
        {
            if (IsDead || amount <= 0) return;
            Current = Mathf.Max(0, Current - amount);
            Damaged?.Invoke(amount);
            if (Current == 0) Died?.Invoke();
        }
    }
}
