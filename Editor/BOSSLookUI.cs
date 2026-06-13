using System;
using UnityEditor;
using UnityEngine;

namespace DarataBOSS.BOSSLookPreset.Editor
{
    /// <summary>
    /// Shared IMGUI styling so every module reads the same: titled "cards"
    /// with an accent bar, role-colored buttons, and compact labeled rows.
    /// Keeping this in one place is what makes the wizard feel consistent.
    /// </summary>
    public static class BOSSLookUI
    {
        // Role palette (buttons + accents).
        public static readonly Color Generate = new Color(0.55f, 0.85f, 0.6f);
        public static readonly Color Auto = new Color(0.48f, 0.74f, 1f);
        public static readonly Color Bake = new Color(1f, 0.7f, 0.35f);
        public static readonly Color Finalize = new Color(0.78f, 0.62f, 1f);
        public static readonly Color Danger = new Color(1f, 0.5f, 0.5f);
        public static readonly Color Done = new Color(0.55f, 0.9f, 0.55f);
        public static readonly Color Accent = new Color(0.45f, 0.78f, 0.95f);

        private static GUIStyle _cardStyle;
        private static GUIStyle _cardTitle;
        private static GUIStyle _pill;

        private static GUIStyle CardStyle
        {
            get
            {
                if (_cardStyle == null)
                {
                    _cardStyle = new GUIStyle(EditorStyles.helpBox)
                    {
                        padding = new RectOffset(10, 10, 8, 10),
                        margin = new RectOffset(2, 2, 4, 4),
                    };
                }
                return _cardStyle;
            }
        }

        private static GUIStyle CardTitle
        {
            get
            {
                if (_cardTitle == null)
                {
                    _cardTitle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 12,
                    };
                }
                return _cardTitle;
            }
        }

        private static GUIStyle Pill
        {
            get
            {
                if (_pill == null)
                {
                    _pill = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Bold,
                    };
                }
                return _pill;
            }
        }

        /// <summary>Opens a titled card. Dispose the returned scope to close it.
        /// Draws an accent bar + title, with an optional ✓ done badge.</summary>
        public static IDisposable Card(string title, Color accent, bool? done = null)
        {
            var scope = new EditorGUILayout.VerticalScope(CardStyle);
            using (new EditorGUILayout.HorizontalScope())
            {
                var bar = GUILayoutUtility.GetRect(3f, 16f, GUILayout.Width(3f));
                EditorGUI.DrawRect(bar, accent);
                GUILayout.Space(4f);
                EditorGUILayout.LabelField(title, CardTitle);
                if (done.HasValue)
                {
                    GUILayout.FlexibleSpace();
                    var prev = GUI.color;
                    GUI.color = done.Value ? Done : new Color(1f, 1f, 1f, 0.35f);
                    GUILayout.Label(done.Value ? "✓ 完了" : "未設定", Pill, GUILayout.Width(48f));
                    GUI.color = prev;
                }
            }
            GUILayout.Space(4f);
            return scope;
        }

        public static bool Button(string label, Color tint, float height = 22f)
        {
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = tint;
            bool pressed = GUILayout.Button(label, GUILayout.Height(height));
            GUI.backgroundColor = prev;
            return pressed;
        }

        public static bool MiniButton(string label, Color tint, float width)
        {
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = tint;
            bool pressed = GUILayout.Button(label, GUILayout.Width(width));
            GUI.backgroundColor = prev;
            return pressed;
        }

        /// <summary>A help line in a quieter tone than HelpBox, for hints under
        /// a control without stealing visual weight.</summary>
        public static void Hint(string text)
        {
            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.6f);
            EditorGUILayout.LabelField(text, EditorStyles.wordWrappedMiniLabel);
            GUI.color = prev;
        }

        public static void SectionSpace()
        {
            GUILayout.Space(6f);
        }
    }
}
