using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Nexoterra.World
{
    [DefaultExecutionOrder(-1000)]
    public sealed class MobileTouchControls : MonoBehaviour
    {
        enum Control { Ui, Move, Look, Shoot, Aim, Dodge, Interact, Companion, Map, Pause }

        static MobileTouchControls current;
        readonly Dictionary<int, Control> fingers = new Dictionary<int, Control>();
        NexoterraThirdPerson player;
        SlugInventory inventory;
        PlayerEmotes emotes;
        Vector2 moveOrigin, moveValue, lookDelta, uiPosition, uiDragDelta, uiTapPosition;
        bool aimHeld, shootPressed, dodgePressed, interactPressed, companionPressed, mapPressed, pausePressed;
        bool uiPointerHeld, uiTapPending, uiTapConsumed;
        int uiTapFrame = -1;

        public static bool IsEnabled => Application.isMobilePlatform && current;
        public static Vector2 Move => IsEnabled ? current.moveValue : Vector2.zero;
        public static Vector2 LookDelta => IsEnabled ? current.lookDelta : Vector2.zero;
        public static bool Aim => IsEnabled && current.aimHeld;
        public static bool Sprint => IsEnabled && current.moveValue.sqrMagnitude >= .60f;
        public static bool ShootPressed => IsEnabled && current.shootPressed;
        public static bool DodgePressed => IsEnabled && current.dodgePressed;
        public static bool InteractPressed => IsEnabled && current.interactPressed;
        public static bool CompanionPressed => IsEnabled && current.companionPressed;
        public static bool MapPressed => IsEnabled && current.mapPressed;
        public static bool PausePressed => IsEnabled && current.pausePressed;
        public static bool HasTouchActivity
        {
            get
            {
                if(!IsEnabled||Touchscreen.current==null)return false;
                foreach(var touch in Touchscreen.current.touches)if(touch.press.isPressed)return true;
                return false;
            }
        }
        public static bool HasGameplayActivity => IsEnabled && (current.moveValue.sqrMagnitude > .01f || current.lookDelta.sqrMagnitude > .1f || current.aimHeld || current.shootPressed || current.dodgePressed || current.interactPressed || current.companionPressed || current.mapPressed || current.pausePressed);
        public static Vector2 UiDragDelta => IsEnabled ? current.uiDragDelta : Vector2.zero;

        static Texture2D disc;
        static Texture2D blasterIcon, bootIcon;
        static GUIStyle buttonLabel, hintLabel;
        static readonly Color Teal = new Color(.22f, .9f, .9f, .78f);
        static readonly Color Orange = new Color(1f, .58f, .16f, .78f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void CreateForMobile()
        {
            if (!Application.isMobilePlatform || current) return;
            var root = new GameObject("Nexoterra Touch Controls");
            DontDestroyOnLoad(root);
            root.AddComponent<MobileTouchControls>();
        }

        void Awake()
        {
            if (current && current != this) { Destroy(gameObject); return; }
            current = this;
            DontDestroyOnLoad(gameObject);
            RefreshPlayer();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (current == this) current = null;
            if (disc) Destroy(disc);
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RefreshPlayer();

        void RefreshPlayer()
        {
            player = FindAnyObjectByType<NexoterraThirdPerson>();
            inventory = player ? player.GetComponent<SlugInventory>() : null;
            emotes = player ? player.GetComponent<PlayerEmotes>() : null;
        }

        bool WorldControlsVisible => player && !GameSession.Blocked && !(inventory && inventory.IsOpen) && !(emotes && emotes.IsOpen) && !NexoInput.UsingGamepad;

        void Update()
        {
            if (!IsEnabled) return;
            if (uiTapFrame != Time.frameCount) { uiTapPending = false; uiTapConsumed = false; }
            lookDelta = uiDragDelta = Vector2.zero;
            shootPressed = dodgePressed = interactPressed = companionPressed = mapPressed = pausePressed = false;
            aimHeld = false;

            var screen = Touchscreen.current;
            if (screen == null)
            {
                fingers.Clear(); moveValue = Vector2.zero; uiPointerHeld = false;
                return;
            }

            foreach (var touch in screen.touches)
            {
                int id = touch.touchId.ReadValue();
                var phase = touch.phase.ReadValue();
                Vector2 position = touch.position.ReadValue();
                bool began = phase == UnityEngine.InputSystem.TouchPhase.Began;
                if (began && !fingers.ContainsKey(id))
                {
                    var role = Classify(position);
                    fingers[id] = role;
                    if (role == Control.Move) moveOrigin = position;
                    if (role == Control.Ui) { uiPosition = position; uiPointerHeld = true; }
                    if (role == Control.Shoot) shootPressed = true;
                    if (role == Control.Dodge) dodgePressed = true;
                    if (role == Control.Interact) interactPressed = true;
                    if (role == Control.Companion) companionPressed = true;
                    if (role == Control.Map) mapPressed = true;
                    if (role == Control.Pause) pausePressed = true;
                }

                if (!fingers.TryGetValue(id, out var control)) continue;
                if (control == Control.Move)
                    moveValue = Vector2.ClampMagnitude((position - moveOrigin) / (76f * UiScale), 1f);
                else if (control == Control.Look && phase != UnityEngine.InputSystem.TouchPhase.Ended && phase != UnityEngine.InputSystem.TouchPhase.Canceled)
                    lookDelta += touch.delta.ReadValue();
                else if (control == Control.Aim) aimHeld = phase != UnityEngine.InputSystem.TouchPhase.Ended && phase != UnityEngine.InputSystem.TouchPhase.Canceled;
                else if (control == Control.Ui)
                {
                    uiPosition = position;
                    if (phase != UnityEngine.InputSystem.TouchPhase.Began && phase != UnityEngine.InputSystem.TouchPhase.Ended && phase != UnityEngine.InputSystem.TouchPhase.Canceled)
                        uiDragDelta += touch.delta.ReadValue();
                    if (phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                    {
                        uiPointerHeld = false;
                        if (phase == UnityEngine.InputSystem.TouchPhase.Ended && Vector2.Distance(position, touch.startPosition.ReadValue()) <= 24f * UiScale)
                        {
                            uiTapPosition = position;
                            uiTapPending = true;
                            uiTapConsumed = false;
                            uiTapFrame = Time.frameCount;
                        }
                    }
                }

                if (phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                {
                    fingers.Remove(id);
                    if (control == Control.Move) moveValue = Vector2.zero;
                }
            }
        }

        Control Classify(Vector2 point)
        {
            if (!WorldControlsVisible) return Control.Ui;
            if (ButtonRect(Control.Map).Contains(point)) return Control.Map;
            if (ButtonRect(Control.Pause).Contains(point)) return Control.Pause;
            if (ButtonRect(Control.Shoot).Contains(point)) return Control.Shoot;
            if (ButtonRect(Control.Aim).Contains(point)) return Control.Aim;
            if (ButtonRect(Control.Dodge).Contains(point)) return Control.Dodge;
            if (ButtonRect(Control.Interact).Contains(point)) return Control.Interact;
            if (ButtonRect(Control.Companion).Contains(point)) return Control.Companion;

            var safe = Screen.safeArea;
            var stick = new Vector2(safe.xMin + safe.width * .15f, safe.yMin + safe.height * .27f);
            if (!fingers.ContainsValue(Control.Move) && point.x < safe.xMin + safe.width * .38f && point.y < safe.yMin + safe.height * .52f && Vector2.Distance(point, stick) < safe.width * .24f)
                return Control.Move;
            if (point.x > safe.xMin + safe.width * .45f && point.y > safe.yMin + safe.height * .2f)
                return Control.Look;
            return Control.Ui;
        }

        static float UiScale => Mathf.Clamp(Mathf.Min(Screen.safeArea.width / 1280f, Screen.safeArea.height / 720f), .72f, 1.3f);

        static Rect ButtonRect(Control action)
        {
            var safe = Screen.safeArea;
            float s = UiScale, d = (action == Control.Map || action == Control.Pause ? 52f : action == Control.Shoot || action == Control.Dodge ? 76f : 68f) * s;
            Vector2 center = action switch
            {
                Control.Shoot => new Vector2(safe.xMax - safe.width * .075f, safe.yMin + safe.height * .30f),
                Control.Interact => new Vector2(safe.xMax - safe.width * .18f, safe.yMin + safe.height * .30f),
                Control.Aim => new Vector2(safe.xMax - safe.width * .18f, safe.yMin + safe.height * .46f),
                Control.Dodge => new Vector2(safe.xMax - safe.width * .075f, safe.yMin + safe.height * .46f),
                Control.Companion => new Vector2(safe.xMax - safe.width * .285f, safe.yMin + safe.height * .30f),
                Control.Map => new Vector2(safe.xMin + safe.width * .44f, safe.yMax - 36f * s),
                Control.Pause => new Vector2(safe.xMin + safe.width * .56f, safe.yMax - 36f * s),
                _ => Vector2.zero
            };
            return new Rect(center.x - d * .5f, center.y - d * .5f, d, d);
        }

        static Vector2 ToGui(Vector2 screenPoint) => GUIUtility.ScreenToGUIPoint(new Vector2(screenPoint.x, Screen.height - screenPoint.y));

        public static bool TryConsumeTap(Rect guiRect) => TryConsumeTap(guiRect, out _);
        public static bool TryGetPointer(out Vector2 guiPoint)
        {
            guiPoint = default;
            if (!IsEnabled || !current.uiPointerHeld) return false;
            guiPoint = ToGui(current.uiPosition);
            return true;
        }
        public static bool TryConsumeTap(Rect guiRect, out Vector2 guiPoint)
        {
            guiPoint = default;
            if (!IsEnabled || !current.uiTapPending || current.uiTapConsumed || current.uiTapFrame != Time.frameCount) return false;
            guiPoint = ToGui(current.uiTapPosition);
            if (!guiRect.Contains(guiPoint)) return false;
            current.uiTapConsumed = true;
            return true;
        }

        static Texture2D Disc
        {
            get
            {
                if (disc) return disc;
                const int size = 96;
                disc = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Mobile control disc" };
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(size * .5f, size * .5f)) / (size * .5f);
                    float alpha = 1f - Mathf.SmoothStep(.92f, 1f, distance);
                    disc.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
                disc.Apply();
                return disc;
            }
        }

        void OnGUI()
        {
            if (!IsEnabled || !WorldControlsVisible) return;
            var previous = GUI.matrix;
            int previousDepth = GUI.depth;
            Color previousColor = GUI.color;
            GUI.matrix = Matrix4x4.identity;
            GUI.depth = -50;
            EnsureStyles();
            DrawStick();
            DrawButton(Control.Companion, "COMP", companionPressed);
            DrawButton(Control.Interact, "USAR", interactPressed);
            DrawButton(Control.Shoot, "DISPARAR", shootPressed);
            DrawButton(Control.Aim, "APUNTAR", aimHeld);
            DrawButton(Control.Dodge, "ESQUIVA", dodgePressed);
            DrawButton(Control.Map, "MAPA", mapPressed);
            DrawButton(Control.Pause, "Ⅱ", pausePressed);
            var safe = Screen.safeArea;
            Vector2 stick = new Vector2(safe.xMin + safe.width * .15f, safe.yMin + safe.height * .27f);
            GUI.color = new Color(.75f, .94f, .98f, .55f);
            GUI.Label(new Rect(stick.x - 85 * UiScale, Screen.height - stick.y - 91 * UiScale, 210 * UiScale, 18 * UiScale), "CAMINA · EMPUJA PARA CORRER", hintLabel);
            GUI.Label(new Rect(safe.xMin + safe.width * .5f, Screen.height - safe.yMin - safe.height * .67f - 20 * UiScale, safe.width * .3f, 18 * UiScale), "DESLIZA PARA GIRAR LA CÁMARA", hintLabel);
            GUI.color = previousColor;
            GUI.depth = previousDepth;
            GUI.matrix = previous;
        }

        void DrawStick()
        {
            var safe = Screen.safeArea;
            float size = 152f * UiScale;
            Vector2 center = new Vector2(safe.xMin + safe.width * .15f, safe.yMin + safe.height * .27f);
            DrawDisc(center, size, new Color(.015f, .06f, .08f, .44f), new Color(.25f, .85f, .9f, .35f));
            Vector2 thumb = center + moveValue * (47f * UiScale);
            DrawDisc(thumb, 56f * UiScale, new Color(.08f, .24f, .28f, .68f), moveValue.sqrMagnitude > .01f ? Teal : new Color(.5f, .8f, .84f, .48f));
        }

        static void DrawDisc(Vector2 center, float size, Color fill, Color rim)
        {
            var rect = new Rect(center.x - size * .5f, Screen.height - center.y - size * .5f, size, size);
            GUI.color = rim; GUI.DrawTexture(rect, Disc, ScaleMode.StretchToFill, true);
            float inset = size * .055f;
            GUI.color = fill; GUI.DrawTexture(new Rect(rect.x + inset, rect.y + inset, rect.width - inset * 2, rect.height - inset * 2), Disc, ScaleMode.StretchToFill, true);
        }

        void DrawButton(Control action, string title, bool active)
        {
            var r = ButtonRect(action);
            Vector2 center = new Vector2(r.center.x, r.center.y);
            DrawDisc(center, r.width, active ? new Color(.04f, .38f, .3f, .88f) : new Color(.015f, .065f, .08f, .68f), active ? new Color(.35f, 1f, .62f, .95f) : action == Control.Shoot ? Orange : Teal);
            Texture2D icon = action == Control.Shoot ? blasterIcon : action == Control.Dodge ? bootIcon : null;
            if (icon)
            {
                float scale = UiScale;
                float iconSize = r.width * .54f;
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(center.x - iconSize * .5f, r.y + 4f * scale, iconSize, iconSize), icon, ScaleMode.ScaleToFit, true);
                buttonLabel.fontSize = Mathf.RoundToInt(8.5f * scale);
                GUI.Label(new Rect(r.x - 2f * scale, r.yMax - 20f * scale, r.width + 4f * scale, 15f * scale), title, buttonLabel);
                return;
            }
            buttonLabel.fontSize = Mathf.RoundToInt((action == Control.Pause ? 25f : 10.5f) * UiScale);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x - r.width * .2f, r.y, r.width * 1.4f, r.height), title, buttonLabel);
        }

        static void EnsureStyles()
        {
            if (buttonLabel != null) return;
            blasterIcon = Resources.Load<Texture2D>("Nexoterra/UI/Kit/Icon_Blaster");
            bootIcon = Resources.Load<Texture2D>("Nexoterra/UI/Kit/Icon_Boot");
            buttonLabel = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true, clipping = TextClipping.Clip, padding = new RectOffset(2, 2, 1, 1) };
            buttonLabel.normal.textColor = Color.white;
            hintLabel = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontSize = 11, fontStyle = FontStyle.Bold };
            hintLabel.normal.textColor = new Color(.75f, .94f, .98f, .65f);
        }
    }
}
