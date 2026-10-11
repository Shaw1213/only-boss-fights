using UnityEngine;

namespace DreamLayerJam
{
    /// <summary>The hero in the lobby: walk left/right, face the walking direction, interact.</summary>
    [RequireComponent(typeof(HeroInput))]
    public class LobbyHero : MonoBehaviour
    {
        public HeroConfig config;
        public Transform body;
        [Tooltip("The hero's DreamLayer art (the same PoseSet as the fight). Empty = the greybox box.")]
        public PoseSet poses;
        [Tooltip("Size in the lobby relative to the fight: the lobby painting's architecture is drawn bigger than the arena's.")]
        public float artScale = 1f;
        [Tooltip("Walk speed as a fraction of the fight's move speed.")]
        public float walkSpeedFactor = 0.8f;
        [Tooltip("Off during the title screen and while entering a fight.")]
        public bool inputEnabled = true;

        [Header("Contact shadow")]
        [Tooltip("A soft ellipse under the feet, so he stands on the painted floor instead of floating in front of it.")]
        public bool contactShadow = true;
        [Tooltip("Width in units at fight size (x artScale in the lobby).")]
        public float shadowWidth = 0.9f;
        public float shadowHeight = 0.2f;
        [Range(0f, 1f)] public float shadowOpacity = 0.75f;

        [Header("Bounds")]
        public float minX = -15.5f;
        public float maxX = 15.5f;
        public float groundY = -3f;

        HeroInput input;
        float velocityX;
        int facing = 1;
        float cycle;
        float moveCycleTime;
        bool wasWalking;
        Vector3 bodyBaseScale;
        Vector3 bodyBasePos;
        SpriteRenderer bodyRenderer;
        PoseCrossfade crossfade;
        ContactShadow shadow;
        float shadowSpread = 1f;
        float stepDistance; // ground walked since the last footstep sound

        bool ArtMode => poses != null && poses.IsUsable && bodyRenderer != null;

        public int Facing => facing;
        public bool IsWalking => Mathf.Abs(velocityX) > 0.2f;

        void Awake()
        {
            input = GetComponent<HeroInput>();
            if (body != null)
            {
                bodyBaseScale = body.localScale;
                bodyBasePos = body.localPosition;
                bodyRenderer = body.GetComponent<SpriteRenderer>();
            }
            if (ArtMode)
            {
                bodyRenderer.sprite = poses.idle;
                bodyRenderer.color = Color.white; // the greybox box was tinted
                body.localPosition = Vector3.zero;
                crossfade = PoseCrossfade.Create(body.parent, bodyRenderer.sortingOrder + 1);
            }
            if (contactShadow && bodyRenderer != null)
                shadow = ContactShadow.Attach(transform, bodyRenderer.sortingOrder - 1, shadowWidth * artScale, shadowHeight * artScale,
                                              shadowOpacity, new Color(0.02f, 0.03f, 0.07f));
        }

        public bool ConsumeInteract() => inputEnabled && input.ConsumeInteract(config.inputBufferSeconds);

        public void PlaceAt(float x, int faceDir)
        {
            transform.position = new Vector3(Mathf.Clamp(x, minX, maxX), groundY, 0f);
            velocityX = 0f;
            facing = faceDir >= 0 ? 1 : -1;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            float move = inputEnabled ? input.Move : 0f;
            if (Mathf.Abs(move) > 0.2f) facing = move > 0f ? 1 : -1;
            float target = move * config.moveSpeed * walkSpeedFactor;
            float rate = Mathf.Abs(target) > 0.01f ? config.acceleration : config.deceleration;
            velocityX = Mathf.MoveTowards(velocityX, target, rate * dt);

            var p = transform.position;
            p.x = Mathf.Clamp(p.x + velocityX * dt, minX, maxX);
            p.y = groundY;
            transform.position = p;

            if (shadow != null)
            {
                // Feet apart while walking: the shadow widens a little.
                shadowSpread = Mathf.MoveTowards(shadowSpread, IsWalking ? 1.2f : 1f, dt * 2f);
                shadow.SetSpread(shadowSpread);
            }
            if (body == null) return;
            if (ArtMode)
            {
                UpdateArt(dt);
                return;
            }
            // Greybox animation: walk bob + lean while walking, a slow breath while idle.
            cycle += dt * (IsWalking ? 12f : 2f);
            float bob = IsWalking ? Mathf.Abs(Mathf.Sin(cycle)) * 0.08f : 0f;
            float breathe = IsWalking ? 0f : Mathf.Sin(cycle) * 0.015f;
            body.localPosition = bodyBasePos + new Vector3(IsWalking ? 0.05f * facing : 0f, bob, 0f);
            body.localScale = Vector3.Scale(bodyBaseScale, new Vector3(1f - breathe, 1f + breathe, 1f));
        }

        /// <summary>The fight's art: idle pose, walk cycle timed to ground speed, idle ↔ walk cross-fade, breathing.</summary>
        void UpdateArt(float dt)
        {
            bool walking = IsWalking && poses.move != null && poses.move.IsValid;
            if (walking && !wasWalking)
            {
                moveCycleTime = 0f; // start every walk on the ready stance
                stepDistance = 0.5f * poses.StepLength * artScale; // the first footfall comes half a step in
            }
            wasWalking = walking;
            // One footstep per step of ground covered (the walk is foot-locked, so this matches the feet).
            if (walking && (stepDistance += Mathf.Abs(velocityX) * dt) >= poses.StepLength * artScale)
            {
                stepDistance = 0f;
                GameAudio.Play(Sfx.Footstep);
            }
            // A bigger hero covers more ground per step, so the cycle plays slower for the same speed (no foot sliding).
            if (poses.moveCycleSpeed > 0f) moveCycleTime += Mathf.Abs(velocityX) * dt / (poses.moveCycleSpeed * artScale);

            var sprite = walking ? poses.move.Sample(moveCycleTime) : poses.idle;
            if (crossfade != null && sprite != bodyRenderer.sprite && (sprite == poses.idle || bodyRenderer.sprite == poses.idle))
                crossfade.Begin(bodyRenderer);
            bodyRenderer.sprite = sprite;
            bodyRenderer.flipX = facing < 0; // art faces right

            float scale = poses.Scale * artScale * (walking ? poses.move.scale : 1f);
            float breathe = walking ? 0f : poses.idleBreathAmount * Mathf.Sin(Time.time * poses.idleBreathRate);
            body.localScale = new Vector3(scale, scale * (1f + breathe), 1f);
        }
    }
}
