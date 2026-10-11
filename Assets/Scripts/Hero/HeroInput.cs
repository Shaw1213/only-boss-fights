using UnityEngine;
using UnityEngine.InputSystem;

namespace DreamLayerJam
{
    /// <summary>
    /// Hero input defined in code (keyboard + gamepad) with timestamped presses for input buffering.
    /// Runs before the controller so this frame's presses are recorded first. Presses are timed on <see cref="Clock"/>,
    /// which stops during hit-stop freezes: a press made during a freeze is still fresh when the game moves again
    /// (pressing dash the moment a clash flashes must not expire inside the clash's own freeze).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class HeroInput : MonoBehaviour
    {
        InputAction move;
        InputAction dash;
        InputAction attack;
        InputAction parry;
        InputAction interact;

        float interactPressedAt = float.NegativeInfinity;
        float dashPressedAt = float.NegativeInfinity;
        float parryPressedAt = float.NegativeInfinity;
        float attackPressedAt = float.NegativeInfinity;
        float attackHoldStart = float.NegativeInfinity;
        bool attackHeld;

        /// <summary>-1 (left) to 1 (right).</summary>
        public float Move => move.ReadValue<float>();
        /// <summary>Input time: real seconds, not counting hit-stop freezes (time scale 0).</summary>
        public float Clock { get; private set; }
        /// <summary>The attack button has been held continuously since its last press.</summary>
        public bool AttackHeld => attackHeld;
        public float AttackHoldDuration => attackHeld ? Time.unscaledTime - attackHoldStart : 0f;

        void Awake()
        {
            move = new InputAction("Move", InputActionType.Value, expectedControlType: "Axis");
            move.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
            move.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick/x");
            move.AddBinding("<Gamepad>/dpad/x");

            dash = new InputAction("Dash", InputActionType.Button);
            dash.AddBinding("<Keyboard>/space");
            dash.AddBinding("<Keyboard>/leftShift");
            dash.AddBinding("<Keyboard>/l");
            dash.AddBinding("<Gamepad>/buttonSouth");

            attack = new InputAction("Attack", InputActionType.Button);
            attack.AddBinding("<Mouse>/leftButton");
            attack.AddBinding("<Keyboard>/j");
            attack.AddBinding("<Gamepad>/buttonWest");

            parry = new InputAction("Parry", InputActionType.Button);
            parry.AddBinding("<Mouse>/rightButton");
            parry.AddBinding("<Keyboard>/k");
            parry.AddBinding("<Gamepad>/buttonEast");
            parry.AddBinding("<Gamepad>/rightShoulder");

            // Gallery: step into a painting.
            interact = new InputAction("Interact", InputActionType.Button);
            interact.AddBinding("<Keyboard>/w");
            interact.AddBinding("<Keyboard>/e");
            interact.AddBinding("<Keyboard>/upArrow");
            interact.AddBinding("<Gamepad>/buttonNorth");
        }

        void OnEnable()
        {
            move.Enable();
            dash.Enable();
            attack.Enable();
            parry.Enable();
            interact.Enable();
        }

        void OnDisable()
        {
            move.Disable();
            dash.Disable();
            attack.Disable();
            parry.Disable();
            interact.Disable();
            attackHeld = false;
        }

        void OnDestroy()
        {
            move.Dispose();
            dash.Dispose();
            attack.Dispose();
            parry.Dispose();
            interact.Dispose();
        }

        void Update()
        {
            if (Time.timeScale > 0f) Clock += Time.unscaledDeltaTime;
            float now = Clock;
            if (interact.WasPressedThisFrame()) interactPressedAt = now;
            if (dash.WasPressedThisFrame()) dashPressedAt = now;
            if (parry.WasPressedThisFrame()) parryPressedAt = now;
            if (attack.WasPressedThisFrame())
            {
                attackPressedAt = now;
                attackHoldStart = Time.unscaledTime; // holding is real time
                attackHeld = true;
            }
            if (attackHeld && !attack.IsPressed()) attackHeld = false;
        }

        public bool ConsumeInteract(float buffer) => Consume(ref interactPressedAt, buffer);
        public bool ConsumeDash(float buffer) => Consume(ref dashPressedAt, buffer);
        public bool ConsumeParry(float buffer) => Consume(ref parryPressedAt, buffer);
        public bool ConsumeAttack(float buffer) => Consume(ref attackPressedAt, buffer);

        // A press still in the buffer, left there for the controller (the first-time coach's pause waits for one).
        public bool HasDash(float buffer) => Clock - dashPressedAt <= buffer;
        public bool HasParry(float buffer) => Clock - parryPressedAt <= buffer;
        public bool HasAttack(float buffer) => Clock - attackPressedAt <= buffer;

        /// <summary>Consumes an attack press made at or after <paramref name="sinceClock"/> (on <see cref="Clock"/>; combo chaining).</summary>
        public bool ConsumeAttackSince(float sinceClock)
        {
            if (attackPressedAt < sinceClock) return false;
            attackPressedAt = float.NegativeInfinity;
            return true;
        }

        public void ClearBuffer()
        {
            interactPressedAt = float.NegativeInfinity;
            dashPressedAt = float.NegativeInfinity;
            parryPressedAt = float.NegativeInfinity;
            attackPressedAt = float.NegativeInfinity;
        }

        bool Consume(ref float pressedAt, float buffer)
        {
            if (Clock - pressedAt > buffer) return false;
            pressedAt = float.NegativeInfinity;
            return true;
        }
    }
}
