using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace DreamLayerJam
{
    /// <summary>
    /// The lobby (Enter the Gungeon "Breach" style): the title sits over the live scene with the hero
    /// idling; a click pans and zooms the camera onto the hero; then walk to a relic and touch it to
    /// enter its fight. Coming back from a fight skips the title and puts the hero next to that relic.
    /// </summary>
    public class LobbyDirector : MonoBehaviour
    {
        public enum Phase { Title, ZoomIn, Explore, Entering, Story }

        public LobbyHero hero;
        public Camera cam;
        public LevelEntrance[] entrances;

        [Header("Lobby bounds (camera)")]
        public float minX = -16f;
        public float maxX = 16f;

        [Header("Camera")]
        public Vector2 titleCenter = new Vector2(0f, 0.6f);
        public float titleSize = 6.5f;
        public float playSize = 4.5f;
        [Tooltip("Where the ground sits on screen while exploring, as a fraction of the screen height from the bottom.")]
        [Range(0.05f, 0.5f)] public float playGroundFraction = 0.25f;
        public float zoomInSeconds = 1.6f;
        public float followSharpness = 6f;

        [Header("Entering a fight")]
        [Tooltip("Standing at a relic this long enters it (walking past never does). W/E enters at once.")]
        public float touchDwellSeconds = 0.6f;
        public float enterSeconds = 0.9f;
        public float enterSize = 0.9f;

        [Header("Story card (the relic's story over black before its fight; any key / click skips)")]
        public float storyBaseSeconds = 1.5f;
        public float storySecondsPerWord = 0.3f;

        [Header("Title")]
        public string gameTitle = "Only Boss Fights";
        public string gameSubtitle = "The Fallen Knight";

        // Title input as actions (any mouse / keyboard / gamepad), not Mouse.current, which only
        // tracks the most recently used device.
        InputAction playAction;
        InputAction resetAction;

        Phase phase;
        float phaseTime;
        float fade;
        bool loading; // the fight scene is loading: keep the screen black until it replaces this one
        float dwell;
        LevelEntrance dwellTarget;
        bool messageShown;
        LevelEntrance entering;
        Vector3 fromPos;
        float fromSize;
        GUIStyle titleStyle, subtitleStyle, textStyle, smallStyle, storyStyle, relicStyle, capStyle, hintStyle;

        public Phase CurrentPhase => phase;
        public LevelEntrance NearestEntrance { get; private set; }
        public string Message { get; private set; }
        public float MessageTime { get; private set; } = float.NegativeInfinity;

        void Awake()
        {
            playAction = new InputAction("Play", InputActionType.Button);
            playAction.AddBinding("<Mouse>/leftButton");
            playAction.AddBinding("<Mouse>/rightButton");
            playAction.AddBinding("<Keyboard>/anyKey");
            playAction.AddBinding("<Gamepad>/start");
            playAction.AddBinding("<Gamepad>/buttonSouth");
            resetAction = new InputAction("ResetProgress", InputActionType.Button, "<Keyboard>/backspace");
        }

        void OnEnable()
        {
            playAction.Enable();
            resetAction.Enable();
        }

        void OnDisable()
        {
            playAction.Disable();
            resetAction.Disable();
        }

        void OnDestroy()
        {
            playAction.Dispose();
            resetAction.Dispose();
        }

        void Start()
        {
            GameAudio.PlayMusic(MusicTrack.Lobby);
            if (GameProgress.TitleShown)
            {
                // Back from a fight: stand just left of the relic used, outside its touch range.
                var from = Find(GameProgress.LastEntranceId);
                if (from != null) hero.PlaceAt(from.transform.position.x - (from.touchRadius + 1.2f), -1);
                hero.inputEnabled = true;
                SetPhase(Phase.Explore);
                Snap(PlayTarget(), playSize);
                fade = 1f;
            }
            else
            {
                hero.inputEnabled = false;
                SetPhase(Phase.Title);
                SnapTitle();
            }
        }

        void Update()
        {
            if (loading) return;
            float dt = Time.unscaledDeltaTime;
            phaseTime += dt;
            if (phase != Phase.Entering && phase != Phase.Story && fade > 0f) fade = Mathf.Max(0f, fade - dt / 0.6f);

            switch (phase)
            {
                case Phase.Title:
                    SnapTitle(); // follows window resizes (fullscreen)
                    if (resetAction.WasPressedThisFrame())
                    {
                        if (GameProgress.AnyCleared || GameProgress.AnyLearned) { GameProgress.ResetAll(); ShowMessage("Progress reset"); }
                    }
                    else if (playAction.WasPressedThisFrame())
                    {
                        GameAudio.Play(Sfx.UiConfirm);
                        fromPos = cam.transform.position;
                        fromSize = cam.orthographicSize;
                        GameProgress.TitleShown = true;
                        SetPhase(Phase.ZoomIn);
                    }
                    break;

                case Phase.ZoomIn:
                {
                    float t = Mathf.Clamp01(phaseTime / zoomInSeconds);
                    float e = t * t * (3f - 2f * t); // ease in-out
                    cam.transform.position = Vector3.Lerp(fromPos, PlayTarget(), e);
                    cam.orthographicSize = Mathf.Lerp(fromSize, playSize, e);
                    if (t >= 1f)
                    {
                        hero.inputEnabled = true;
                        SetPhase(Phase.Explore);
                    }
                    break;
                }

                case Phase.Explore:
                    float k = 1f - Mathf.Exp(-followSharpness * dt);
                    cam.transform.position = Vector3.Lerp(cam.transform.position, PlayTarget(), k);
                    CheckEntrances();
                    break;

                case Phase.Entering:
                {
                    float t = Mathf.Clamp01(phaseTime / enterSeconds);
                    float e = t * t; // accelerate into the relic
                    var target = entering.VisualCenter;
                    target.z = -10f;
                    cam.transform.position = Vector3.Lerp(fromPos, target, e);
                    cam.orthographicSize = Mathf.Lerp(fromSize, enterSize, e);
                    fade = t;
                    if (t >= 1f)
                    {
                        if (string.IsNullOrWhiteSpace(entering.story)) LoadFight();
                        else SetPhase(Phase.Story);
                    }
                    break;
                }

                case Phase.Story:
                {
                    // Skipping jumps to the fade-out rather than cutting.
                    float duration = StorySeconds;
                    if (phaseTime > 0.5f && phaseTime < duration - StoryFadeOut && playAction.WasPressedThisFrame())
                        phaseTime = duration - StoryFadeOut;
                    if (phaseTime >= duration) LoadFight();
                    break;
                }
            }
        }

        const float StoryFadeIn = 0.5f;
        const float StoryFadeOut = 0.4f;

        /// <summary>How long the story card stays up: long enough to read it.</summary>
        float StorySeconds
        {
            get
            {
                int words = entering.story.Split(new[] { ' ', '\n' }, System.StringSplitOptions.RemoveEmptyEntries).Length;
                return storyBaseSeconds + words * storySecondsPerWord;
            }
        }

        void LoadFight()
        {
            GameProgress.LastEntranceId = entering.fightId;
            // Not `enabled = false`: that also stopped OnGUI's black fade a frame before the fight scene appeared, so the
            // camera's close-up of the relic flashed on screen (Shawn: "when I enter room 3 something flashed").
            loading = true;
            SceneManager.LoadScene(entering.sceneName);
        }

        /// <summary>
        /// All relics sit on one line, so walking past one must not enter it: the hero has to stop at a
        /// relic (it charges up for touchDwellSeconds) or press W / E while touching it.
        /// </summary>
        void CheckEntrances()
        {
            bool interact = hero.ConsumeInteract();

            // The relic being touched (closest, if two overlap).
            NearestEntrance = null;
            float best = float.PositiveInfinity;
            foreach (var e in entrances)
            {
                if (!Touching(e)) continue;
                float d = Mathf.Abs(hero.transform.position.x - e.transform.position.x);
                if (d < best) { best = d; NearestEntrance = e; }
            }

            if (NearestEntrance != dwellTarget)
            {
                if (dwellTarget != null) dwellTarget.Charge = 0f;
                dwellTarget = NearestEntrance;
                dwell = 0f;
                messageShown = false;
            }
            if (dwellTarget == null) return;

            // Charge while standing still; walking drains it quickly.
            float dt = Time.unscaledDeltaTime;
            dwell = hero.IsWalking ? Mathf.Max(0f, dwell - dt * 3f) : dwell + dt;
            bool trigger = interact || dwell >= touchDwellSeconds;

            if (dwellTarget.CanEnter)
            {
                // The relic's charge-up sound starts with the charge (once per charge).
                if (dwell > 0f && dwellTarget.Charge <= 0f) GameAudio.Play(Sfx.RelicCharge);
                dwellTarget.Charge = Mathf.Clamp01(dwell / touchDwellSeconds);
                if (trigger) StartEnter(dwellTarget);
            }
            else if (trigger && (!messageShown || interact))
            {
                messageShown = true;
                GameAudio.Play(Sfx.RelicSealed);
                ShowMessage(dwellTarget.State == LevelEntrance.EntranceState.Sealed ? dwellTarget.sealedMessage : "Not built yet");
            }
        }

        void StartEnter(LevelEntrance e)
        {
            entering = e;
            GameAudio.Play(Sfx.RelicEnter);
            hero.inputEnabled = false;
            fromPos = cam.transform.position;
            fromSize = cam.orthographicSize;
            SetPhase(Phase.Entering);
        }

        // ------------------------------------------------------------------ helpers

        bool Touching(LevelEntrance e) =>
            e != null && Mathf.Abs(hero.transform.position.x - e.transform.position.x) < e.touchRadius;

        LevelEntrance Find(string id)
        {
            foreach (var e in entrances) if (e != null && e.fightId == id) return e;
            return null;
        }

        Vector3 PlayTarget()
        {
            float halfWidth = playSize * cam.aspect;
            float x = maxX - minX <= halfWidth * 2f
                ? (minX + maxX) * 0.5f
                : Mathf.Clamp(hero.transform.position.x, minX + halfWidth, maxX - halfWidth);
            return new Vector3(x, hero.groundY + playSize * (1f - 2f * playGroundFraction), -10f);
        }

        /// <summary>
        /// The title shot, narrowed when the screen is wider than the painting allows (a wide fullscreen window, or the
        /// Editor's Game view): it keeps its bottom edge and shrinks until the sides stay inside minX..maxX.
        /// </summary>
        void SnapTitle()
        {
            float size = Mathf.Min(titleSize, (maxX - minX) * 0.5f / Mathf.Max(0.01f, cam.aspect));
            Snap(new Vector3(titleCenter.x, titleCenter.y - titleSize + size, -10f), size);
        }

        void Snap(Vector3 position, float size)
        {
            cam.transform.position = position;
            cam.orthographicSize = size;
        }

        void SetPhase(Phase next)
        {
            phase = next;
            phaseTime = 0f;
        }

        void ShowMessage(string text)
        {
            Message = text;
            MessageTime = Time.unscaledTime;
        }

        // ------------------------------------------------------------------ UI (IMGUI, styled by UiKit)

        static readonly (string, string)[] ExploreControls = { ("A D", "move"), ("W E", "enter a relic (or stand still at it)") };
        static readonly (string, string)[] ResetControls = { ("Backspace", "reset progress") };

        void OnGUI()
        {
            if (loading)
            {
                UiKit.Fill(new Rect(0f, 0f, Screen.width, Screen.height), Color.black);
                return;
            }
            float s = Mathf.Max(0.5f, Mathf.Min(Screen.height / 540f, Screen.width / 960f));
            EnsureStyles(s);

            // Title over the live scene, on a soft dark band; fades out as the camera moves in.
            float titleAlpha = phase == Phase.Title ? 1f : phase == Phase.ZoomIn ? 1f - Mathf.Clamp01(phaseTime / (zoomInSeconds * 0.5f)) : 0f;
            if (titleAlpha > 0f)
            {
                float ty = Screen.height * 0.16f;
                bool sub = !string.IsNullOrEmpty(gameSubtitle);
                UiKit.Band(ty + (sub ? 70f : 48f) * s, (sub ? 230f : 170f) * s, titleAlpha * 0.6f, s, false);
                UiKit.Text(new Rect(0f, ty, Screen.width, 74f * s), gameTitle, titleStyle, new Color(1f, 1f, 1f, titleAlpha), s, 2.5f);
                float below = 80f * s;
                if (sub)
                {
                    UiKit.Text(new Rect(0f, ty + 66f * s, Screen.width, 34f * s), gameSubtitle, subtitleStyle, new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, titleAlpha), s, 2f);
                    below = 116f * s;
                }
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 3f);
                UiKit.Rule(new Rect((Screen.width - 260f * s) * 0.5f, ty + below - 2f * s, 260f * s, Mathf.Max(1f, s)), titleAlpha * 0.6f);
                UiKit.Text(new Rect(0f, ty + below + 4f * s, Screen.width, 28f * s), "Click to play", textStyle, new Color(1f, 1f, 1f, titleAlpha * pulse), s);
                if (GameProgress.AnyCleared)
                    UiKit.KeyHints(Screen.width * 0.5f, Screen.height - 30f * s, ResetControls, capStyle, hintStyle, titleAlpha * 0.7f, s);
            }

            // Relic names (+ state) under each relic once playing.
            if (phase == Phase.Explore || phase == Phase.Entering)
            {
                foreach (var e in entrances)
                {
                    if (e == null) continue;
                    string state = e.State switch
                    {
                        LevelEntrance.EntranceState.Open => "stand at it to enter",
                        LevelEntrance.EntranceState.Cleared => "cleared  -  stand at it to fight again",
                        LevelEntrance.EntranceState.Sealed => "sealed",
                        _ => "not built yet",
                    };
                    var sp = cam.WorldToScreenPoint(new Vector3(e.transform.position.x, hero.groundY - 0.35f, 0f));
                    float lx = sp.x - 160f * s, ly = Screen.height - sp.y;
                    bool near = e == NearestEntrance;
                    var nameColor = e.State == LevelEntrance.EntranceState.Sealed ? new Color(0.75f, 0.75f, 0.8f) : UiKit.Gold;
                    UiKit.Text(new Rect(lx, ly, 320f * s, 20f * s), e.title, relicStyle, nameColor, s);
                    UiKit.Text(new Rect(lx, ly + 18f * s, 320f * s, 16f * s), state, smallStyle, new Color(1f, 1f, 1f, near ? 0.95f : 0.7f), s, 1f);
                    if (e.State == LevelEntrance.EntranceState.Cleared)
                        UiKit.Diamond(new Vector2(sp.x, ly - 7f * s), 6f * s, UiKit.Gold, new Color(0f, 0f, 0f, 0.85f), s); // a cleared mark
                }
                UiKit.KeyHints(Screen.width * 0.5f, Screen.height - 26f * s, ExploreControls, capStyle, hintStyle, 0.85f, s);
            }

            // Short messages above the hero (e.g. a sealed relic).
            float age = Time.unscaledTime - MessageTime;
            if (!string.IsNullOrEmpty(Message) && age < 1.8f)
            {
                Vector3 anchor = phase == Phase.Title ? new Vector3(titleCenter.x, titleCenter.y - 2f, 0f) : hero.transform.position + new Vector3(0f, 2.6f, 0f);
                var sp = cam.WorldToScreenPoint(anchor);
                UiKit.Text(new Rect(sp.x - 220f * s, Screen.height - sp.y - 14f * s, 440f * s, 28f * s), Message, textStyle,
                           new Color(1f, 1f, 1f, 1f - Mathf.Clamp01((age - 1.3f) / 0.5f)), s, 2f);
            }

            // Fade (entering a fight / arriving back).
            if (fade > 0f) UiKit.Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, fade));

            // Story card over the black: the relic's name, then his story so far.
            if (phase == Phase.Story && entering != null)
            {
                float duration = StorySeconds;
                float a = Mathf.Clamp01(phaseTime / StoryFadeIn) * Mathf.Clamp01((duration - phaseTime) / StoryFadeOut);
                float w = Mathf.Min(Screen.width - 32f * s, 680f * s);
                float x = (Screen.width - w) * 0.5f, y = Screen.height * 0.36f;
                UiKit.Text(new Rect(x, y - 44f * s, w, 24f * s), entering.title.ToUpperInvariant(), relicStyle, new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, a), s);
                UiKit.Rule(new Rect((Screen.width - 240f * s) * 0.5f, y - 16f * s, 240f * s, Mathf.Max(1f, s)), a * 0.7f);
                UiKit.Text(new Rect(x, y, w, 140f * s), entering.story, storyStyle, new Color(1f, 1f, 1f, a), s);
                UiKit.Text(new Rect(0f, Screen.height - 36f * s, Screen.width, 22f * s), "click to continue", smallStyle,
                           new Color(1f, 1f, 1f, a * 0.5f * Mathf.Clamp01((phaseTime - 1f) / 0.5f)), s, 1f);
            }
        }

        void EnsureStyles(float s)
        {
            int size = Mathf.RoundToInt(16f * s);
            // Rebuild if any style is missing too (a script reload while playing keeps the old ones, not new ones).
            if (textStyle != null && subtitleStyle != null && storyStyle != null && capStyle != null && relicStyle != null && textStyle.fontSize == size) return;
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = TextAnchor.MiddleCenter, richText = true, padding = new RectOffset(0, 0, 0, 0) };
            textStyle.normal.textColor = Color.white;
            titleStyle = new GUIStyle(textStyle) { fontSize = Mathf.RoundToInt(56f * s), fontStyle = FontStyle.Bold };
            subtitleStyle = new GUIStyle(textStyle) { fontSize = Mathf.RoundToInt(26f * s), fontStyle = FontStyle.Italic };
            relicStyle = new GUIStyle(textStyle) { fontSize = Mathf.RoundToInt(15f * s), fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
            if (UiKit.DisplayFont != null) titleStyle.font = subtitleStyle.font = relicStyle.font = UiKit.DisplayFont;
            smallStyle = new GUIStyle(textStyle) { fontSize = Mathf.RoundToInt(12f * s), alignment = TextAnchor.UpperCenter };
            storyStyle = new GUIStyle(textStyle) { fontSize = Mathf.RoundToInt(22f * s), alignment = TextAnchor.UpperCenter, wordWrap = true };
            capStyle = new GUIStyle(textStyle) { fontSize = Mathf.RoundToInt(11f * s), fontStyle = FontStyle.Bold };
            hintStyle = new GUIStyle(textStyle) { fontSize = Mathf.RoundToInt(12f * s), alignment = TextAnchor.MiddleLeft, wordWrap = false };
        }
    }
}
