using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DreamLayerJam
{
    /// <summary>
    /// Fight HUD (IMGUI): hero health, boss health and stagger meter, the boss's name card, the result banner,
    /// the first-time coach's prompts (FirstTimeCoach), and (editor / development builds only) a debug readout plus
    /// keys: F1 hitboxes, F2 the readout, F3 cycles the hero's held element (testing Harvest), F4 switches art and
    /// greybox boxes.
    /// </summary>
    public class DebugHUD : MonoBehaviour
    {
        public HeroController hero;
        public BossController boss;
        public FightFlow flow;
        public bool showDebugText = false;

        [Header("Name card (entering from the lobby; skipped on a retry)")]
        public string nameCardSubtitle = "First form";
        public float nameCardSeconds = 2.8f;

        [Tooltip("The control hints at the bottom dim after this many seconds of a fight.")]
        public float controlsDimAfterSeconds = 8f;

        GUIStyle small;
        GUIStyle right;
        GUIStyle centered;
        GUIStyle big;
        GUIStyle callout;
        GUIStyle bossName;
        GUIStyle cap;
        GUIStyle hint;
        GUIStyle story;
        readonly StringBuilder sb = new StringBuilder();
        FirstTimeCoach coach;

        // Damage just taken: the boss bar's pale ghost drains after a short hold; lost hero pips flash.
        float bossGhost = 1f;
        float bossHitTime;
        int lastBossHealth = -1;
        int lastHeroHealth = -1;
        int heroLostFrom;
        float heroHitTime = float.NegativeInfinity;

        static readonly (string, string)[] FightControls =
        {
            ("A D", "move"), ("Space", "dash"), ("LMB J", "attack, hold to charge"), ("RMB K", "parry"), ("R", "restart"), ("Esc", "lobby"),
        };

        void Update()
        {
            TrackDamage();
            if (coach == null && hero != null && boss != null && flow != null) coach = new FirstTimeCoach(hero, boss, flow, nameCardSeconds);
            coach?.Tick();
#if UNITY_EDITOR || DEBUG
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f1Key.wasPressedThisFrame) CombatDebug.ShowHitboxes = !CombatDebug.ShowHitboxes;
            if (kb.f2Key.wasPressedThisFrame) showDebugText = !showDebugText;
            if (kb.f3Key.wasPressedThisFrame && hero != null) hero.DebugCycleElement();
            if (kb.f4Key.wasPressedThisFrame && hero != null && boss != null)
            {
                // Art <-> greybox (before/after captures, and debugging hitboxes against plain boxes).
                bool art = !(hero.ArtMode || boss.ArtMode);
                hero.SetArtMode(art);
                boss.SetArtMode(art);
            }
#endif
        }

        void TrackDamage()
        {
            if (boss == null || hero == null || boss.Health == null || hero.Health == null) return;
            int b = boss.Health.Current, h = hero.Health.Current;
            if (lastBossHealth < 0) { lastBossHealth = b; bossGhost = boss.Health.Normalized; }
            if (b < lastBossHealth) bossHitTime = Time.unscaledTime;
            if (b > lastBossHealth) bossGhost = boss.Health.Normalized; // a restart refills it
            lastBossHealth = b;
            if (Time.unscaledTime - bossHitTime > 0.45f)
                bossGhost = Mathf.MoveTowards(bossGhost, boss.Health.Normalized, 0.6f * Time.unscaledDeltaTime);

            if (lastHeroHealth < 0) lastHeroHealth = h;
            if (h < lastHeroHealth) { heroLostFrom = lastHeroHealth; heroHitTime = Time.unscaledTime; }
            lastHeroHealth = h;
        }

        void OnGUI()
        {
            if (hero == null || boss == null || hero.Health == null || boss.Health == null) return;
            // Scale against a 960x540 reference, fitting whichever side is tighter, so nothing overlaps
            // in a non-16:9 Game view.
            float s = Mathf.Max(0.5f, Mathf.Min(Screen.height / 540f, Screen.width / 960f));
            EnsureStyles(s);
            var heroHealth = hero.Health;
            var bossHealth = boss.Health;

            // Hero health: diamond pips (top-left); the ones just lost flash white and fade.
            float x = 26f * s, y = 26f * s, pip = 12f * s, step = 23f * s;
            float lostAge = Time.unscaledTime - heroHitTime;
            for (int i = 0; i < heroHealth.Max; i++)
            {
                var c = new Vector2(x + i * step, y);
                if (i < heroHealth.Current) UiKit.Diamond(c, pip, hero.baseColor, new Color(0f, 0f, 0f, 0.85f), s);
                else
                {
                    UiKit.Diamond(c, pip, new Color(0.12f, 0.13f, 0.17f, 0.85f), new Color(0f, 0f, 0f, 0.7f), s);
                    if (i < heroLostFrom && lostAge < 0.6f)
                        UiKit.Diamond(c, pip * (1f + lostAge), new Color(1f, 1f, 1f, 1f - lostAge / 0.6f), new Color(0f, 0f, 0f, 0f), s);
                }
            }
            float row = y + 16f * s;
            if (hero.HeldElement != Element.None)
            {
                // Harvested element: what the next charged heavy (wave) or riposte will carry.
                UiKit.Diamond(new Vector2(x, row + 10f * s), 9f * s, ElementColors.Of(hero.HeldElement), new Color(0f, 0f, 0f, 0.85f), s);
                UiKit.Text(new Rect(x + 14f * s, row, 320f * s, 20f * s), $"{hero.HeldElement.ToString().ToUpperInvariant()}  -  heavy or riposte", small, Color.white, s);
                row += 20f * s;
            }
            // Boss spells on the hero.
            string spell = hero.IsBurning ? "BURNING  -  dash to put it out" : hero.IsRooted ? "ROOTED  -  can't move: parry" : hero.IsChilled ? "CHILLED  -  slowed" : null;
            if (spell != null)
            {
                var spellColor = hero.IsBurning ? ElementColors.Fire : hero.IsRooted ? ElementColors.Grip : ElementColors.Ice;
                UiKit.Diamond(new Vector2(x, row + 10f * s), 9f * s, spellColor, new Color(0f, 0f, 0f, 0.85f), s);
                UiKit.Text(new Rect(x + 14f * s, row, 320f * s, 20f * s), spell, small, Color.white, s);
                row += 20f * s;
            }
            if (hero.FlashCounterReady) UiKit.Text(new Rect(x - 6f * s, row, 320f * s, 20f * s), "FLASH COUNTER!  -  dash", small, UiKit.Gold, s);
            else if (hero.RiposteReady) UiKit.Text(new Rect(x - 6f * s, row, 320f * s, 20f * s), "RIPOSTE!", small, UiKit.Gold, s);

            // Boss: name over a framed health bar (top-center), the stagger meter under it.
            float w = 460f * s, bx = (Screen.width - w) * 0.5f, by = 8f * s;
            UiKit.Text(new Rect(bx, by, w, 20f * s), boss.config.displayName.ToUpperInvariant(), bossName, UiKit.Gold, s);
            UiKit.Text(new Rect(bx, by + 2f * s, w, 18f * s), boss.Phase == 1 ? "Phase 1" : "Phase 2", right, new Color(1f, 1f, 1f, 0.7f), s, 1f);
            var hpRect = new Rect(bx, by + 24f * s, w, 11f * s);
            UiKit.Bar(hpRect, bossHealth.Normalized, bossGhost, boss.Phase == 1 ? boss.phase1Color : boss.phase2Color, s);
            float mark = boss.Phase == 1 ? boss.config.phase2AtHealth : boss.config.finaleAtHealth > 0f && !boss.FinaleUsed ? boss.config.finaleAtHealth : -1f;
            if (mark > 0f)
            {
                // Where the next phase (or the last stand) begins.
                float mx = bx + w * mark;
                UiKit.Fill(new Rect(mx - s, hpRect.y, 2f * s, hpRect.height), new Color(1f, 1f, 1f, 0.85f));
                UiKit.Diamond(new Vector2(mx, hpRect.y - 3f * s), 6f * s, UiKit.Gold, new Color(0f, 0f, 0f, 0.85f), s);
            }
            UiKit.Bar(new Rect(bx + w * 0.15f, hpRect.yMax + 7f * s, w * 0.7f, 4f * s), boss.StaggerNormalized, 0f, boss.staggerColor, s, 0.9f);
            string status = (boss.IsBurning ? "BURNING   " : "") + (boss.IsFrozen ? "FROZEN   " : "") + (boss.IsRooted ? "ROOTED" : "");
            if (status.Length > 0)
            {
                var sc = boss.IsBurning ? ElementColors.Fire : boss.IsFrozen ? ElementColors.Ice : ElementColors.Wood;
                UiKit.Text(new Rect(bx, hpRect.yMax + 13f * s, w, 18f * s), status.Trim(), centered, Color.Lerp(sc, Color.white, 0.35f), s);
            }

            // Callout above the hero ("PARRY!", "CLASH!", "EARLY", "LATE", "RED = DASH"), fading out
            float calloutAge = Time.unscaledTime - hero.CalloutTime;
            var cam = Camera.main;
            if (!string.IsNullOrEmpty(hero.Callout) && calloutAge < 0.8f && cam != null)
            {
                Vector3 sp = cam.WorldToScreenPoint(hero.transform.position + new Vector3(0f, hero.config.hurtboxSize.y + 0.9f, 0f));
                float a = 1f - Mathf.Clamp01((calloutAge - 0.5f) / 0.3f);
                float pop = 1f + 0.25f * (1f - Mathf.Clamp01(calloutAge / 0.12f)); // lands a little big, then settles
                var m = GUI.matrix;
                GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), new Vector2(sp.x, Screen.height - sp.y));
                UiKit.Text(new Rect(sp.x - 150f * s, Screen.height - sp.y - 18f * s, 300f * s, 36f * s), hero.Callout, callout, new Color(1f, 1f, 1f, a), s, 2f);
                GUI.matrix = m;
            }

            // The finale's last stand: the knight holds him.
            if (boss.IsHeld && !string.IsNullOrEmpty(boss.config.finaleText))
            {
                var c = UiKit.Gold;
                c.a = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f);
                UiKit.Text(new Rect(0f, Screen.height * 0.22f, Screen.width, 40f * s), boss.config.finaleText, callout, c, s, 2f);
            }

            // Name card: the boss's name on a dark band as the fight opens.
            if (flow != null && !FightFlow.IsRetry && flow.Current == FightFlow.Result.Fighting && flow.FightTime < nameCardSeconds)
            {
                float t = flow.FightTime;
                float a = Mathf.Clamp01(t / 0.35f) * Mathf.Clamp01((nameCardSeconds - t) / 0.6f);
                float cy = Screen.height * 0.4f;
                UiKit.Band(cy, 150f * s, a, s, false);
                float line = 380f * s * Mathf.SmoothStep(0.4f, 1f, Mathf.Clamp01(t / 0.6f));
                UiKit.Rule(new Rect((Screen.width - line) * 0.5f, cy + 22f * s, line, Mathf.Max(1f, 1.5f * s)), a);
                UiKit.Text(new Rect(0f, cy - 40f * s, Screen.width, 60f * s), boss.config.displayName.ToUpperInvariant(), big, new Color(1f, 1f, 1f, a), s, 2f);
                UiKit.Text(new Rect(0f, cy + 27f * s, Screen.width, 24f * s), nameCardSubtitle, centered, new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, a), s);
            }

            // First time a mechanic comes up: the coach's prompt (and its teaching pause).
            coach?.Draw(s);

            // The story after a win (and the ending after the final one): black falls over the arena, then the lines one by one.
            if (flow != null && flow.ShowsEpilogue)
            {
                DrawEpilogue(s);
                return;
            }

            // Result banner
            bool over = flow != null && flow.Current != FightFlow.Result.Fighting;
            if (over)
            {
                bool won = flow.Current == FightFlow.Result.Victory;
                float a = Mathf.Clamp01(flow.ResultAge / 0.35f);
                float ty = Screen.height * 0.42f;
                UiKit.Band(ty, 190f * s, a, s);
                var titleColor = won ? UiKit.Gold : new Color(1f, 0.45f, 0.42f);
                titleColor.a = a;
                UiKit.Text(new Rect(0f, ty - 54f * s, Screen.width, 62f * s), won ? "VICTORY" : "DEFEATED", big, titleColor, s, 2f);
                UiKit.Text(new Rect(0f, ty + 8f * s, Screen.width, 22f * s), $"Time {flow.FightTime:0.0} s     Parries {hero.ParryCount}", centered,
                           new Color(1f, 1f, 1f, a * 0.9f), s);
                var next = won
                    ? new[] { ("J", "or click: back to the lobby"), ("R", "fight again") }
                    : new[] { ("J R", "or click: retry"), ("Esc", "lobby") };
                UiKit.KeyHints(Screen.width * 0.5f, ty + 38f * s, next, cap, hint, a, s);
            }

#if UNITY_EDITOR || DEBUG
            // Debug readout (bottom-left)
            if (showDebugText)
            {
                sb.Clear();
                sb.Append("Hero  ").Append(hero.CurrentState);
                if (hero.CurrentAttack != null) sb.Append("  ").Append(hero.CurrentAttack.name).Append("  ").Append(hero.CurrentPhase);
                sb.Append("  f").Append(Mathf.FloorToInt(hero.StateTime * Frames.PerSecond)).Append('\n');

                sb.Append("Boss  ").Append(boss.CurrentState);
                if (boss.CurrentMove != null)
                    sb.Append("  ").Append(boss.CurrentMove.name)
                      .Append("  step ").Append(boss.StepIndex + 1).Append('/').Append(boss.CurrentMove.steps.Length)
                      .Append("  ").Append(boss.CurrentPhase);
                sb.Append("  f").Append(Mathf.FloorToInt(boss.StateTime * Frames.PerSecond)).Append('\n');

                sb.Append($"Stagger {boss.Stagger:0}/{boss.config.staggerMax:0}   Parries {hero.ParryCount}   Time {(flow != null ? flow.FightTime : 0f):0.0}s\n");
                sb.Append($"F1 hitboxes: {(CombatDebug.ShowHitboxes ? "on" : "off")}   F2 debug text   F3 element: {hero.HeldElement}   F4 art/greybox");
                UiKit.Text(new Rect(16f * s, Screen.height - 120f * s, 760f * s, 92f * s), sb.ToString(), small, Color.white, s, 1f);
            }
#endif

            // Controls (bottom): bright as the fight starts, then dimmed; the result banner shows its own.
            if (!over)
            {
                float t = flow != null ? flow.FightTime : 0f;
                float alpha = Mathf.Lerp(0.9f, 0.4f, Mathf.Clamp01((t - controlsDimAfterSeconds) / 1.5f));
                UiKit.KeyHints(Screen.width * 0.5f, Screen.height - 24f * s, FightControls, cap, hint, alpha, s);
            }
        }

        void DrawEpilogue(float s)
        {
            float age = flow.ResultAge, delay = flow.epilogueDelay;
            var art = flow.epilogueArt;
            float dark = Mathf.Clamp01((age - delay + 1.2f) / 1.2f);
            UiKit.Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, (art != null ? 1f : 0.88f) * dark));

            float progress = flow.EpilogueProgress;
            var lines = flow.epilogue;
            float a = flow.EpilogueDone ? Mathf.Clamp01((progress - lines.Length) / 0.4f) : 0f;
            float w = Mathf.Min(Screen.width - 32f * s, 760f * s), x = (Screen.width - w) * 0.5f;
            if (art != null) DrawEpilogueArt(art, age - delay, progress, lines, a, x, w, s);
            else
            {
                // Over black: the lines build up one by one, the closing line under them.
                float lineH = 38f * s, y = Screen.height * 0.42f - lines.Length * lineH * 0.5f;
                for (int i = 0; i < lines.Length; i++)
                    UiKit.Text(new Rect(x, y + i * lineH, w, lineH), lines[i], story, new Color(1f, 1f, 1f, Mathf.Clamp01((progress - i) / 0.4f)), s);
                if (flow.EpilogueDone && !string.IsNullOrEmpty(flow.epilogueClosing))
                {
                    float cy = y + lines.Length * lineH + 30f * s;
                    UiKit.Rule(new Rect((Screen.width - 300f * s) * 0.5f, cy - 8f * s, 300f * s, Mathf.Max(1f, s)), a * 0.7f);
                    UiKit.Text(new Rect(0f, cy, Screen.width, 36f * s), flow.epilogueClosing, callout, new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, a), s, 2f);
                }
            }

            if (flow.EpilogueDone)
            {
                float statsY = Screen.height - (art != null ? 50f : 64f) * s, keysY = Screen.height - (art != null ? 24f : 38f) * s;
                UiKit.Text(new Rect(0f, statsY, Screen.width, 20f * s), $"Time {flow.FightTime:0.0} s     Parries {hero.ParryCount}", centered,
                           new Color(1f, 1f, 1f, a * 0.6f), s, 1f);
                UiKit.KeyHints(Screen.width * 0.5f, keysY, new[] { ("J", "or click: back to the lobby"), ("R", "fight again") }, cap, hint, a * 0.85f, s);
            }
        }

        /// <summary>
        /// The ending with its illustration: it fades in over the black and slowly pushes in toward the knight (60% down
        /// the picture); the lines play one at a time as subtitles on a dark band at the bottom, then the closing line in
        /// the same place, so nothing covers him.
        /// </summary>
        void DrawEpilogueArt(Sprite art, float t, float progress, string[] lines, float closingAlpha, float x, float w, float s)
        {
            var tex = art.texture;
            float artA = Mathf.Clamp01(t / 1.8f);
            float zoom = 1f + 0.07f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 22f));
            float aspect = art.rect.width / art.rect.height;
            float h = Mathf.Max(Screen.height, Screen.width / aspect) * zoom, wArt = h * aspect;
            var r = new Rect((Screen.width - wArt) * 0.5f, (Screen.height - h) * 0.6f, wArt, h); // cover the screen, focus on the knight
            var uv = new Rect(art.rect.x / tex.width, art.rect.y / tex.height, art.rect.width / tex.width, art.rect.height / tex.height);
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, artA);
            GUI.DrawTextureWithTexCoords(r, tex, uv);
            GUI.color = old;

            // The subtitle band: the bottom quarter (the floor in front of him), with the stats and keys under the line.
            UiKit.Band(Screen.height * 0.88f, Screen.height * 0.26f, 0.85f * artA, s, false);
            float sy = Screen.height * 0.835f - 19f * s;
            int i = Mathf.Min(lines.Length - 1, Mathf.FloorToInt(progress));
            if (i >= 0 && !flow.EpilogueDone)
            {
                // One line at a time: each fades in, holds, and fades out before the next.
                float inA = Mathf.Clamp01((progress - i) / 0.25f), outA = Mathf.Clamp01((i + 1 - progress) / 0.15f);
                UiKit.Text(new Rect(x, sy, w, 38f * s), lines[i], story, new Color(1f, 1f, 1f, inA * outA * artA), s);
            }
            else if (flow.EpilogueDone && !string.IsNullOrEmpty(flow.epilogueClosing))
                UiKit.Text(new Rect(0f, sy - 2f * s, Screen.width, 36f * s), flow.epilogueClosing, callout,
                           new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, closingAlpha), s, 2f);
        }

        void EnsureStyles(float s)
        {
            int size = Mathf.RoundToInt(14f * s);
            // Rebuild if any style is missing too (a script reload while playing keeps the old ones, not new ones).
            if (small != null && story != null && cap != null && small.fontSize == size) return;
            small = new GUIStyle(GUI.skin.label) { fontSize = size, padding = new RectOffset(0, 0, 0, 0) };
            small.normal.textColor = Color.white;
            right = new GUIStyle(small) { alignment = TextAnchor.MiddleRight, wordWrap = false, fontSize = Mathf.RoundToInt(12f * s) };
            centered = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(16f * s) };
            big = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(46f * s), fontStyle = FontStyle.Bold };
            callout = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(22f * s), fontStyle = FontStyle.Bold };
            bossName = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(15f * s), fontStyle = FontStyle.Bold };
            if (UiKit.DisplayFont != null) big.font = bossName.font = UiKit.DisplayFont;
            cap = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(11f * s), fontStyle = FontStyle.Bold };
            hint = new GUIStyle(small) { alignment = TextAnchor.MiddleLeft, fontSize = Mathf.RoundToInt(12f * s), wordWrap = false };
            story = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(19f * s), wordWrap = true };
        }
    }
}
