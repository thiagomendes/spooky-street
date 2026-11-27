using System;
using System.Collections.Generic;
using SpookyStreet.Controls;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Nonverbal;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SpookyStreet.UI
{
    /// <summary>
    /// Gamepad and keyboard for one screen (S004 F1, D25): a <see cref="UiFocusGraph"/> says where focus goes, the
    /// screen's own <see cref="UiNavigateActions"/> say what was pressed, and a <see cref="UiFocus"/> outline rings the
    /// focused element. Submit presses the focused element exactly as a tap would, so touch stays first-class and every
    /// scheme runs the same code; Back always does what the graph's <see cref="UiFocusGraph.Exit"/> element does unless
    /// <see cref="BackOverride"/> first takes it (let go of a held item). Elements that are hidden or not interactable
    /// are skipped. The outline shows while the player uses a gamepad or keyboard and hides on touch.
    /// Usage: build the screen's buttons, then
    /// <code>
    /// var graph = new UiFocusGraph("settings").AddRow(0, "close").AddRow(1, "music", "voices");
    /// graph.Exit = "close";
    /// var nav = UiFocusScreen.Attach(root, graph, overlay);
    /// nav.Bind("close", closeButton); nav.Bind("music", musicButton); …
    /// </code>
    /// The map is on while the root is active; prompts follow the scheme through <see cref="AddPrompt"/>.
    /// Screens stack (S004 F2): only the screen enabled last (<see cref="IsTop"/>) reads input and shows its outline, so
    /// a card opened over another one (settings over the pause card, the gate over the title) never shares a press with
    /// the screen below. When the top screen closes, the one below takes input back with its focus where it was; no
    /// screen reads input on the frame the stack changed, so the press that closed a card never acts on the next one.
    /// </summary>
    public sealed class UiFocusScreen : MonoBehaviour
    {
        private sealed class Element
        {
            public RectTransform Rect;
            public Selectable Selectable;
            public Action Submit;
        }

        private static readonly List<UiFocusScreen> Stack = new List<UiFocusScreen>();
        private static int s_stackChangedFrame = -1;

        private readonly Dictionary<string, Element> _elements = new Dictionary<string, Element>(StringComparer.Ordinal);
        private readonly List<(ControlGlyphView View, ControlPrompt Prompt)> _prompts = new List<(ControlGlyphView, ControlPrompt)>();

        private UiNavigateActions _actions;
        private Transform _overlay;
        private bool _hooked;

        public UiFocusGraph Graph { get; private set; }

        /// <summary>The focused element's id (null when nothing can be focused).</summary>
        public string Focus { get; private set; }

        public UiFocus Outline { get; private set; }

        public UiNavigateActions Actions => _actions;

        /// <summary>True for the screen enabled last of those enabled now: it alone reads input and shows its outline.</summary>
        public bool IsTop => _hooked && Stack.Count > 0 && Stack[Stack.Count - 1] == this;

        /// <summary>The screen that reads input now (null when no screen is up).</summary>
        public static UiFocusScreen Top => Stack.Count > 0 ? Stack[Stack.Count - 1] : null;

        /// <summary>Runs before Back exits; return true to keep the screen open (it handled Back itself).</summary>
        public Func<bool> BackOverride { get; set; }

        /// <summary>
        /// Runs before a d-pad step moves focus; return true when the screen used the step itself (a slider moved, a
        /// sheet scrolled), so focus stays where it is.
        /// </summary>
        public Func<UiDirection, bool> DirectionOverride { get; set; }

        /// <summary>Page left and right (null: the bumpers do nothing on this screen).</summary>
        public Action PageLeft { get; set; }

        public Action PageRight { get; set; }

        /// <summary>Raised when focus moves to another element.</summary>
        public event Action<string> FocusChanged;

        /// <summary>
        /// Adds navigation to the screen whose root is <paramref name="root"/>; the outline goes on
        /// <paramref name="overlay"/> (the root when null), which should draw above the elements.
        /// </summary>
        public static UiFocusScreen Attach(GameObject root, UiFocusGraph graph, Transform overlay = null)
        {
            var screen = root.AddComponent<UiFocusScreen>();
            screen.Graph = graph ?? throw new ArgumentNullException(nameof(graph));
            screen._overlay = overlay != null ? overlay : root.transform;
            screen._actions = GameInput.CreateUiNavigate();
            if (root.activeInHierarchy)
            {
                screen.OnEnable();
            }

            return screen;
        }

        /// <summary>A button (or toggle, slider…): Submit clicks it the way a tap does.</summary>
        public void Bind(string id, Selectable selectable)
        {
            Action submit = null;
            if (selectable is Button button)
            {
                submit = () => button.onClick.Invoke();
            }
            else if (selectable is Toggle toggle)
            {
                submit = () => toggle.isOn = !toggle.isOn;
            }

            _elements[Known(id)] = new Element
            {
                Rect = selectable.transform as RectTransform,
                Selectable = selectable,
                Submit = submit,
            };
        }

        /// <summary>Any element: Submit runs <paramref name="submit"/>.</summary>
        public void Bind(string id, RectTransform rect, Action submit)
        {
            _elements[Known(id)] = new Element { Rect = rect, Submit = submit };
        }

        /// <summary>
        /// A glyph for <paramref name="prompt"/> (Submit, Back, PageLeft, PageRight, Rewatch) that follows the control
        /// scheme: a key cap, a lit gamepad button, or nothing on touch (the on-screen button is the prompt there).
        /// </summary>
        public ControlGlyphView AddPrompt(ControlPrompt prompt, Transform parent, Vector2 anchor, Vector2 position, float size)
        {
            var view = ControlGlyphView.Create(parent, "Prompt" + prompt, anchor, position, size);
            view.Show(ControlGlyphs.For(ControlSchemeTracker.Current, prompt));
            _prompts.Add((view, prompt));
            return view;
        }

        /// <summary>Moves focus to <paramref name="id"/> (ignored when it cannot hold focus).</summary>
        public void FocusOn(string id)
        {
            SyncEnabled();
            if (Graph.IsEnabled(id))
            {
                SetFocus(id);
            }
        }

        /// <summary>One d-pad step (tests call this like a press).</summary>
        public void Navigate(UiDirection direction)
        {
            SyncEnabled();
            if (DirectionOverride != null && DirectionOverride(direction))
            {
                return;
            }

            SetFocus(Graph.Move(Focus, direction));
        }

        /// <summary>Presses the focused element.</summary>
        public void Submit()
        {
            SyncEnabled();
            SetFocus(Graph.Resolve(Focus));
            if (Focus != null && _elements.TryGetValue(Focus, out var element))
            {
                element.Submit?.Invoke();
            }
        }

        /// <summary>Back: the override first, otherwise the exit element's own action.</summary>
        public void Back()
        {
            if (BackOverride != null && BackOverride())
            {
                return;
            }

            if (Graph.Exit != null && _elements.TryGetValue(Graph.Exit, out var exit))
            {
                exit.Submit?.Invoke();
            }
        }

        private string Known(string id)
        {
            if (!Graph.Contains(id))
            {
                throw new ArgumentException($"{Graph.ScreenId}: '{id}' is not in the focus graph", nameof(id));
            }

            return id;
        }

        private void SetFocus(string id)
        {
            if (id == Focus)
            {
                return;
            }

            Focus = id;
            FocusChanged?.Invoke(id);
        }

        /// <summary>Hidden or non-interactable elements cannot hold focus.</summary>
        private void SyncEnabled()
        {
            foreach (var pair in _elements)
            {
                var e = pair.Value;
                bool usable = e.Rect != null && e.Rect.gameObject.activeInHierarchy &&
                              (e.Selectable == null || e.Selectable.IsInteractable());
                if (Graph.IsEnabled(pair.Key) != usable)
                {
                    Graph.SetEnabled(pair.Key, usable);
                }
            }
        }

        private void OnEnable()
        {
            if (_actions == null || _hooked)
            {
                return;
            }

            _hooked = true;
            Stack.Remove(this);
            Stack.Add(this);
            s_stackChangedFrame = Time.frameCount;
            _actions.Enable();
            ControlSchemeTracker.Changed += OnSchemeChanged;
            SyncEnabled();
            Focus = Graph.Start();
            // This screen drives focus itself; no uGUI selection, so the default UI module never double-moves.
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            OnSchemeChanged(ControlSchemeTracker.Current);
        }

        private void OnDisable()
        {
            if (!_hooked)
            {
                return;
            }

            _hooked = false;
            if (Stack.Remove(this))
            {
                s_stackChangedFrame = Time.frameCount;
            }

            _actions?.Disable();
            ControlSchemeTracker.Changed -= OnSchemeChanged;
            if (Outline != null)
            {
                Outline.Hide();
            }
        }

        private void OnSchemeChanged(ControlScheme scheme)
        {
            foreach (var (view, prompt) in _prompts)
            {
                if (view != null)
                {
                    view.Show(ControlGlyphs.For(scheme, prompt));
                }
            }
        }

        private void Update()
        {
            // Only the top screen reads input, and none on the frame a screen opened or closed (that press was theirs).
            if (!IsTop || s_stackChangedFrame == Time.frameCount)
            {
                return;
            }

            var direction = _actions.DirectionPressed();
            if (direction.HasValue)
            {
                Navigate(direction.Value);
            }

            if (_actions.Pressed(_actions.Submit))
            {
                Submit();
            }
            else if (_actions.Pressed(_actions.Back))
            {
                Back();
            }
            else if (_actions.Pressed(_actions.PageLeft))
            {
                PageLeft?.Invoke();
            }
            else if (_actions.Pressed(_actions.PageRight))
            {
                PageRight?.Invoke();
            }
        }

        private void LateUpdate()
        {
            if (!_hooked)
            {
                return;
            }

            SyncEnabled();
            SetFocus(Graph.Resolve(Focus));
            RectTransform target = null;
            if (IsTop && ControlSchemeTracker.Current != ControlScheme.Touch && Focus != null &&
                _elements.TryGetValue(Focus, out var element))
            {
                target = element.Rect;
            }

            if (target == null)
            {
                if (Outline != null && Outline.Showing)
                {
                    Outline.Hide();
                }

                return;
            }

            if (Outline == null)
            {
                Outline = UiFocus.Create(_overlay);
            }

            if (Outline.Target != target)
            {
                Outline.Show(target);
            }

            if (Outline.transform.GetSiblingIndex() != Outline.transform.parent.childCount - 1)
            {
                Outline.transform.SetAsLastSibling();
            }
        }

        private void OnDestroy()
        {
            OnDisable();
            _actions?.Dispose();
            _actions = null;
        }
    }
}
