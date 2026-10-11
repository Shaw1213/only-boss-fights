using UnityEngine;
using UnityEngine.InputSystem;

namespace DreamLayerJam
{
    /// <summary>
    /// QA hooks for checking a browser build, off unless the page address carries "qa=1" (players never see them):
    ///   - every relic opens (the Knight and the King count as cleared), so the later fights can be tested in a browser;
    ///   - F9 in a fight drops the boss to 1 HP, so the finale and the ending can be reached quickly.
    /// </summary>
    public class WebTestHooks : MonoBehaviour
    {
        public static bool Enabled => Application.absoluteURL.Contains("qa=1");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            if (!Enabled) return;
            GameProgress.MarkCleared("boss01");
            GameProgress.MarkCleared("boss02");
            var go = new GameObject("QA hooks");
            DontDestroyOnLoad(go);
            go.AddComponent<WebTestHooks>();
            Debug.Log("[QA] hooks on: every relic open, F9 = boss to 1 HP");
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.f9Key.wasPressedThisFrame) return;
            var boss = FindAnyObjectByType<BossController>();
            if (boss == null) return;
            var health = boss.GetComponent<Health>();
            if (health != null && health.Current > 1) health.TakeDamage(health.Current - 1);
            Debug.Log("[QA] boss to 1 HP");
        }
    }
}
