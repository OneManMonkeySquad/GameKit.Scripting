using UnityEngine;
using UnityEditor;
using GameKit.Scripting.Runtime;
using GameKit.Scripting.Internal;
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace GameKit.Scripting
{
    public class CodeEditorWindow : EditorWindow
    {
        private AttachedScriptAuthoring targetScript;
        private string tempString;
        private Vector2 scroll;
        private GUIStyle codeStyle;
        private GUIStyle gutterStyle;
        private GUIStyle statusStyle;
        private bool wordWrap = false;
        private bool allowForceSave = false;
        private bool _suppressTextAreaTab;
        // focus + duplicate-tab suppression
        private bool _swallowTabUp;
        private bool _tabActive;              // we've already handled a Tab; swallow the next KeyDown
        private const string TextControlName = "CodeTextArea";
        private string IndentString => "    ";

        // Parse cache / debounce
        private bool parseDirty = true;
        private double nextParseTime = 0d;
        private const double ParseDebounce = 0.50d; // seconds
        private ParserResult parseResult;

        // Error highlight color
        private static readonly Color ErrorTint = new Color(1f, 0f, 0f, 0.18f);

        // --- Highlight cache ---
        private string lastHighlightedSrc;
        private string highlightedRichText;

        // Colors (tweak to taste)
        private static readonly string ColKeyword = "80A0FF";
        private static readonly string ColString = "98C379";
        private static readonly string ColNumber = "D19A66";
        private static readonly string ColComment = "6A9955";
        private static readonly string ColType = "4EC9B0"; // e.g. Ast, ParserResult, etc.

        // Simple keyword sets
        private static readonly string[] CSharpKeywords = new[]
        {
            "func", "if", "else", "branch", "sync", "null"
        };

        // Optional: project/type-ish identifiers you want highlighted like types
        private static readonly string[] TypeLike = new[]
        {
            "Ast","ParserResult","ParserException","Script","AttachedScriptAuthoring","CodeEditorWindow"
        };

        // Regexes (compiled for speed)
        private static readonly Regex RxString = new Regex(@"""([^""\\]|\\.)*""", RegexOptions.Compiled);
        private static readonly Regex RxComment = new Regex(@"//.*?$|/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.Multiline);
        private static readonly Regex RxNumber = new Regex(@"\b\d+(\.\d+)?\b", RegexOptions.Compiled);
        private static Regex RxKeywords;  // built once from CSharpKeywords
        private static Regex RxTypes;     // built once from TypeLike

        // line height cache
        private float cachedLineHeight = -1f;

        // Transparent 1x1 texture for overlay backgrounds
        private static Texture2D _clearTex;
        private static Texture2D ClearTex
        {
            get
            {
                if (_clearTex == null)
                {
                    _clearTex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                    {
                        name = "CodeEditor.ClearTex",
                        hideFlags = HideFlags.HideAndDontSave,
                        filterMode = FilterMode.Point,
                        wrapMode = TextureWrapMode.Clamp
                    };
                    _clearTex.SetPixel(0, 0, new Color(0, 0, 0, 0));
                    _clearTex.Apply();
                }
                return _clearTex;
            }
        }

        // Enter handling (duplicate suppression + focus retention)
        private bool _enterActive;
        private bool _swallowEnterUp;

        // Open
        public static void OpenWindow(AttachedScriptAuthoring target)
        {
            var window = GetWindow<CodeEditorWindow>("Code Editor");
            window.targetScript = target;
            window.tempString = target != null ? target.Code : string.Empty;
            window.InitStyles();
            window.parseDirty = true;
            window.Repaint();
            window.Show();
        }

        private void OnEnable()
        {
            InitStyles();
            BuildHighlightRegexesIfNeeded();
            EditorApplication.update += DebouncedParseTick;
        }

        private void OnDisable()
        {
            EditorApplication.update -= DebouncedParseTick;
        }

        private void InitStyles()
        {
            // Code area (monospace + adjustable wrap)
            if (codeStyle == null)
            {
                codeStyle = new GUIStyle(EditorStyles.textArea)
                {
                    richText = false,
                    wordWrap = wordWrap,
                    font = EditorStyles.textArea.font ?? TryGetMonoFont(),
                    fontSize = Mathf.Max(11, EditorStyles.textArea.fontSize)
                };
            }

            // Gutter (line numbers)
            if (gutterStyle == null)
            {
                gutterStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.UpperRight,
                    font = codeStyle.font,
                    fontSize = codeStyle.fontSize
                };
                gutterStyle.normal.textColor = EditorGUIUtility.isProSkin
                    ? new Color(0.7f, 0.7f, 0.7f)
                    : new Color(0.35f, 0.35f, 0.35f);
            }

            if (statusStyle == null)
            {
                statusStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleRight
                };
            }
        }

        private void BuildHighlightRegexesIfNeeded()
        {
            if (RxKeywords == null)
            {
                var kw = string.Join("|", CSharpKeywords.Select(Regex.Escape));
                RxKeywords = new Regex(@"\b(" + kw + @")\b", RegexOptions.Compiled);
            }
            if (RxTypes == null)
            {
                var ty = string.Join("|", TypeLike.Select(Regex.Escape));
                RxTypes = new Regex(@"\b(" + ty + @")\b", RegexOptions.Compiled);
            }
        }

        private static Font TryGetMonoFont()
        {
            try
            {
#if UNITY_EDITOR_WIN
                return Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Courier New" }, 12);
#elif UNITY_EDITOR_OSX
                return Font.CreateDynamicFontFromOSFont(new[] { "Menlo", "Monaco", "Courier" }, 12);
#else
                return Font.CreateDynamicFontFromOSFont(new[] { "DejaVu Sans Mono", "Liberation Mono", "Courier New" }, 12);
#endif
            }
            catch { return EditorStyles.textArea.font; }
        }

        private void DebouncedParseTick()
        {
            if (!parseDirty) return;
            if (EditorApplication.timeSinceStartup >= nextParseTime)
            {
                parseDirty = false;
                parseResult = Script.Parse(tempString ?? string.Empty, "<code>");
                // ensure list exists so UI can iterate safely
                parseResult.Errors ??= new List<ParserException>();
                Repaint();
            }
        }

        private void MarkDirtyForParse()
        {
            parseDirty = true;
            nextParseTime = EditorApplication.timeSinceStartup + ParseDebounce;
        }

        private void HandleShortcuts(Event e)
        {
            if (e.type != EventType.KeyDown) return;

            // Save (Ctrl/Cmd+S)
            if ((e.control || e.command) && e.keyCode == KeyCode.S)
            {
                TrySave();
                e.Use();
            }

            // Revert (Ctrl/Cmd+R)
            if ((e.control || e.command) && e.keyCode == KeyCode.R)
            {
                tempString = targetScript != null ? targetScript.Code : tempString;
                lastHighlightedSrc = null;
                MarkDirtyForParse();
                e.Use();
            }
        }

        private void HandleIndentation(Event e)
        {
            // Only act on KeyDown; Unity sends two KeyDowns for Tab (keyCode + character)
            if (e.type != EventType.KeyDown) return;

            bool isTabKey = (e.keyCode == KeyCode.Tab) || (e.character == '\t');
            if (!isTabKey) return;

            // If we already handled a Tab this event cycle, just swallow this duplicate
            if (_tabActive)
            {
                e.Use();
                return;
            }

            if (GUI.GetNameOfFocusedControl() != TextControlName) return;

            var te = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
            if (te == null) return;

            string src = tempString ?? string.Empty;
            int selStart = Mathf.Min(te.cursorIndex, te.selectIndex);
            int selEnd = Mathf.Max(te.cursorIndex, te.selectIndex);

            if (!e.shift) // TAB → indent
            {
                if (selStart == selEnd)
                {
                    tempString = src.Insert(selStart, IndentString);
                    te.cursorIndex = te.selectIndex = selStart + IndentString.Length;
                }
                else
                {
                    int blockStart = FindLineStart(src, selStart);
                    string block = src.Substring(blockStart, selEnd - blockStart);
                    string indented = IndentBlock(block, IndentString);
                    tempString = src.Substring(0, blockStart) + indented + src.Substring(selEnd);
                    te.cursorIndex = blockStart;
                    te.selectIndex = blockStart + indented.Length;
                }
            }
            else // SHIFT+TAB → outdent
            {
                if (selStart == selEnd)
                {
                    int lineStart = FindLineStart(src, selStart);
                    int removed = 0;
                    if (lineStart < src.Length)
                    {
                        if (src[lineStart] == '\t') { tempString = src.Remove(lineStart, 1); removed = 1; }
                        else
                        {
                            int spaces = 0;
                            for (int i = 0; i < 4 && lineStart + i < src.Length && src[lineStart + i] == ' '; i++) spaces++;
                            if (spaces > 0) { tempString = src.Remove(lineStart, spaces); removed = spaces; }
                        }
                    }
                    te.cursorIndex = te.selectIndex = Mathf.Max(selStart - removed, lineStart);
                }
                else
                {
                    int blockStart = FindLineStart(src, selStart);
                    int removed;
                    string outdented = OutdentBlock(src.Substring(blockStart, selEnd - blockStart), out removed);
                    tempString = src.Substring(0, blockStart) + outdented + src.Substring(selEnd);
                    te.cursorIndex = blockStart;
                    te.selectIndex = blockStart + outdented.Length;
                }
            }

            lastHighlightedSrc = null;
            MarkDirtyForParse();

            // mark that we've handled Tab; swallow the *next* KeyDown and the KeyUp
            _tabActive = true;
            _swallowTabUp = true;

            // keep focus here
            GUI.FocusControl(TextControlName);

            // fully consume this event so TextArea won't see it
            e.Use();
        }

        private void HandleAutoIndentEnter(Event e)
        {
            if (e.type != EventType.KeyDown) return;

            bool isEnter =
                e.keyCode == KeyCode.Return ||
                e.keyCode == KeyCode.KeypadEnter ||
                e.character == '\n' ||
                e.character == '\r';

            if (!isEnter) return;

            // Unity often fires two KeyDowns for Enter (keyCode + character).
            // If we already handled one this frame, swallow the duplicate.
            if (_enterActive) { e.Use(); return; }

            if (GUI.GetNameOfFocusedControl() != TextControlName) return;

            var te = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
            if (te == null) return;

            string s = tempString ?? string.Empty;

            // Selection/caret
            int selStart = Mathf.Min(te.cursorIndex, te.selectIndex);
            int selEnd = Mathf.Max(te.cursorIndex, te.selectIndex);

            // Find start of the *current* line (the one we’re splitting)
            int lineStart = FindLineStart(s, selStart);

            // Extract the line’s leading indentation (exact tabs/spaces)
            int i = lineStart;
            var indent = new System.Text.StringBuilder(16);
            while (i < s.Length)
            {
                char ch = s[i];
                if (ch == ' ' || ch == '\t') { indent.Append(ch); i++; }
                else break;
            }
            string indentStr = indent.ToString();

            // Replace selection with newline + same indent
            string before = s.Substring(0, selStart);
            string after = s.Substring(selEnd);
            tempString = before + "\n" + indentStr + after;

            // Move caret after the inserted indent
            int newCaret = before.Length + 1 + indentStr.Length;
            te.cursorIndex = te.selectIndex = newCaret;

            lastHighlightedSrc = null;
            MarkDirtyForParse();

            // Prevent TextArea & IMGUI from also handling this Enter
            _enterActive = true;
            _swallowEnterUp = true;
            GUI.FocusControl(TextControlName);
            e.Use();
        }

        private void OnGUI()
        {
            InitStyles();
            var e = Event.current;

            // If we handled Enter already, swallow the duplicate KeyDown Unity sends
            if (_enterActive && e.type == EventType.KeyDown &&
                (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.character == '\n' || e.character == '\r'))
            {
                e.Use();
                // Return early so nothing else processes this duplicate
                return;
            }

            // Eat the KeyUp so focus doesn't advance to next control
            if (_swallowEnterUp && e.type == EventType.KeyUp &&
                (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
            {
                e.Use();
                _swallowEnterUp = false;
                _enterActive = false;
                GUI.FocusControl(TextControlName);
                Repaint();
            }

            // If we handled a Tab already, swallow the *next* Tab KeyDown (Unity sends two)
            if (_tabActive && e.type == EventType.KeyDown && e.keyCode == KeyCode.Tab)
            {
                e.Use();
                // keep _tabActive until we observe the KeyUp below
                return; // nothing else in this OnGUI pass should react to this duplicate
            }

            // Eat the KeyUp so focus doesn't advance
            if (_swallowTabUp && e.type == EventType.KeyUp && e.keyCode == KeyCode.Tab)
            {
                e.Use();
                _swallowTabUp = false;
                _tabActive = false; // done with this Tab cycle
                GUI.FocusControl(TextControlName);
                Repaint();
            }

            HandleShortcuts(e);
            HandleIndentation(e);
            HandleAutoIndentEnter(e);

            if (targetScript == null)
            {
                EditorGUILayout.HelpBox("No target selected.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                // Word wrap toggle
                var ww = GUILayout.Toggle(wordWrap, "Word Wrap", EditorStyles.toolbarButton);
                if (ww != wordWrap)
                {
                    wordWrap = ww;
                    codeStyle.wordWrap = wordWrap;
                    cachedLineHeight = -1f;
                }

                using (new EditorGUI.DisabledScope(IsFailed() && !allowForceSave))
                {
                    if (GUILayout.Button("Save", EditorStyles.toolbarButton))
                        TrySave();
                }

                if (GUILayout.Button("Revert", EditorStyles.toolbarButton))
                {
                    tempString = targetScript.Code;
                    lastHighlightedSrc = null;
                    MarkDirtyForParse();
                }

                // Force save toggle
                GUILayout.FlexibleSpace();
                allowForceSave = GUILayout.Toggle(allowForceSave, "Force Save", EditorStyles.toolbarButton);
            }

            // --- compute first, draw conditionally on Repaint ---
            bool repaint = Event.current.type == EventType.Repaint;

            // Main editor area
            Rect fullRect = GUILayoutUtility.GetRect(0, 100000, 0, 100000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            string src = tempString ?? string.Empty;
            string[] lines = src.Split('\n');
            int lineCount = Mathf.Max(1, lines.Length);

            // Gutter width
            int gutterDigits = Mathf.FloorToInt(Mathf.Log10(Mathf.Max(1, lineCount))) + 1;
            float digitWidth = gutterStyle.CalcSize(new GUIContent(new string('0', gutterDigits))).x;
            float gutterWidth = Mathf.Ceil(digitWidth) + 12f;

            // Backgrounds
            if (repaint)
                EditorGUI.DrawRect(fullRect, EditorGUIUtility.isProSkin ? new Color(0.15f, 0.15f, 0.15f) : new Color(0.95f, 0.95f, 0.95f));

            // Split rects
            Rect gutterRect = new Rect(fullRect.x, fullRect.y, gutterWidth, fullRect.height);
            Rect codeRect = new Rect(fullRect.x + gutterWidth, fullRect.y, fullRect.width - gutterWidth, fullRect.height);

            // Gutter background
            if (repaint)
                EditorGUI.DrawRect(gutterRect, EditorGUIUtility.isProSkin ? new Color(0.12f, 0.12f, 0.12f) : new Color(0.90f, 0.90f, 0.90f));

            // Determine content height
            float lineH = GetLineHeight(codeStyle);
            var padded = new GUIStyle(codeStyle) { padding = new RectOffset(6, 6, 4, 4) };
            float textWidth = Mathf.Max(1f, codeRect.width - 16f - padded.padding.horizontal);
            float contentHeight = !codeStyle.wordWrap
                ? Mathf.Max(fullRect.height, lineCount * lineH + 8f)
                : Mathf.Max(fullRect.height, Mathf.CeilToInt(padded.CalcHeight(new GUIContent(src), textWidth)) + 8f);

            // Scroll view
            scroll = GUI.BeginScrollView(fullRect, scroll, new Rect(0, 0, fullRect.width - 16, contentHeight));
            {
                Rect contentGutterRect = new Rect(0, 0, gutterWidth, contentHeight);
                Rect contentCodeRect = new Rect(gutterWidth, 0, fullRect.width - gutterWidth - 16, contentHeight);

                // 1) Gutter: line numbers
                using (new GUI.GroupScope(contentGutterRect))
                {
                    float y = 2f;
                    for (int i = 0; i < lineCount; i++)
                    {
                        GUI.Label(new Rect(0, y, contentGutterRect.width - 4, lineH), (i + 1).ToString(), gutterStyle);
                        y += lineH;
                    }
                }

                // 2) Current line highlight
                if (repaint)
                {
                    var te = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
                    if (te != null)
                    {
                        GetLineCol(src, te.cursorIndex, out int cl, out _);
                        float y = cl * lineH;
                        var curLineRect = new Rect(contentCodeRect.x, y, contentCodeRect.width, lineH);
                        EditorGUI.DrawRect(curLineRect, EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.05f) : new Color(0f, 0f, 0f, 0.05f));
                    }
                }

                // 3) Error highlight bars
                if (repaint && HasErrors())
                {
                    foreach (var err in parseResult.Errors)
                    {
                        int l = Mathf.Clamp(err.SourceLoc.Line - 1, 0, lineCount - 1);
                        float y = l * lineH;
                        Rect highlight = new Rect(contentCodeRect.x, y, contentCodeRect.width, lineH);
                        EditorGUI.DrawRect(highlight, ErrorTint);
                    }
                }

                // 4) Syntax highlighted text (GUIStyle.Draw must be Repaint-only)
                if (repaint)
                {
                    EnsureHighlightCache();
                    var drawStyle = new GUIStyle(codeStyle)
                    {
                        richText = true,
                        padding = padded.padding,
                        clipping = TextClipping.Clip,
                        alignment = TextAnchor.UpperLeft
                    };

                    // Use a bright base for Pro skin, dark for Personal
                    Color baseColor = EditorGUIUtility.isProSkin
                        ? new Color(0.88f, 0.88f, 0.88f, 1f)
                        : new Color(0.12f, 0.12f, 0.12f, 1f);

                    var prevContentColor = GUI.contentColor;
                    GUI.contentColor = baseColor;

                    drawStyle.Draw(contentCodeRect, new GUIContent(highlightedRichText ?? (tempString ?? string.Empty)), false, false, false, false);

                    GUI.contentColor = prevContentColor;
                }

                // 5) Editable transparent overlay: no background, invisible text
                var editStyle = new GUIStyle(codeStyle)
                {
                    richText = false,
                    padding = padded.padding,
                    clipping = TextClipping.Clip,
                    alignment = TextAnchor.UpperLeft
                };
                MakeOverlayTransparent(editStyle);
                MakeTextInvisible(editStyle);

                // If we just handled a Tab, make sure the TextArea doesn’t also insert it
                if (_suppressTextAreaTab && Event.current.type == EventType.KeyDown)
                {
                    // Neutralize character/key so the TextArea won’t insert anything
                    Event.current.character = '\0';
                    Event.current.keyCode = KeyCode.None;
                    // Do not Use() here; we still want caret/selection logic to run
                    _suppressTextAreaTab = false;
                }

                EditorGUI.BeginChangeCheck();
                GUI.SetNextControlName(TextControlName);
                tempString = GUI.TextArea(contentCodeRect, src, editStyle);
                if (EditorGUI.EndChangeCheck())
                {
                    lastHighlightedSrc = null; // invalidate cache
                    MarkDirtyForParse();
                }
            }
            GUI.EndScrollView();

            // Parse messages
            GUILayout.Space(6);
            if (parseDirty)
            {
                EditorGUILayout.HelpBox("Parsing…", MessageType.Info);
            }
            else if (IsFailed() && HasErrors())
            {
                foreach (var err in parseResult.Errors)
                    EditorGUILayout.HelpBox($"{err.SourceLoc.Line}: {err.Message}", MessageType.Error);
            }
            else
            {
                EditorGUILayout.HelpBox("Code compiles fine.", MessageType.Info);
            }

            // Status bar (cursor, chars)
            DrawStatusBar();
        }

        private void TrySave()
        {
            if (targetScript == null) return;

            if (IsFailed() && !allowForceSave)
            {
                ShowNotification(new GUIContent("Fix errors or enable Force Save."));
                return;
            }

            Undo.RecordObject(targetScript, "Edit Script Code");
            targetScript.Code = tempString ?? string.Empty;
            EditorUtility.SetDirty(targetScript);
            ShowNotification(new GUIContent("Saved"));
        }

        private void DrawStatusBar()
        {
            var te = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
            int cursor = te != null ? te.cursorIndex : 0;
            GetLineCol(tempString ?? string.Empty, cursor, out int line, out int col);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label($"Ln {line + 1}, Col {col + 1}", statusStyle, GUILayout.ExpandWidth(false));
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{(tempString?.Length ?? 0)} chars", statusStyle, GUILayout.ExpandWidth(false));
            }
        }

        private static void GetLineCol(string text, int index, out int line, out int col)
        {
            line = 0; col = 0;
            index = Mathf.Clamp(index, 0, string.IsNullOrEmpty(text) ? 0 : text.Length);

            for (int i = 0; i < index && i < text.Length; i++)
            {
                if (text[i] == '\n') { line++; col = 0; }
                else col++;
            }
        }

        private static string HtmlEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        private string BuildHighlightedRichText(string src)
        {
            // Order matters: comments first, then strings, types/keywords, numbers.
            var safe = HtmlEscape(src);

            // Spans list
            var spans = new List<(int start, int length, string color)>();

            void AddMatches(Regex rx, string color)
            {
                foreach (Match m in rx.Matches(safe))
                    spans.Add((m.Index, m.Length, color));
            }

            // Find spans
            AddMatches(RxComment, ColComment);
            AddMatches(RxString, ColString);

            // Guard: don't color within already marked spans
            bool IsCovered(int i) => spans.Any(s => i >= s.start && i < s.start + s.length);

            // Keywords
            foreach (Match m in RxKeywords.Matches(safe))
                if (!IsCovered(m.Index)) spans.Add((m.Index, m.Length, ColKeyword));

            // Type-like names
            foreach (Match m in RxTypes.Matches(safe))
                if (!IsCovered(m.Index)) spans.Add((m.Index, m.Length, ColType));

            // Numbers
            foreach (Match m in RxNumber.Matches(safe))
                if (!IsCovered(m.Index)) spans.Add((m.Index, m.Length, ColNumber));

            // Merge and emit with color tags
            if (spans.Count == 0) return safe;

            spans.Sort((a, b) => a.start.CompareTo(b.start));

            var sb = new StringBuilder(safe.Length + 64);
            int cursor = 0;
            foreach (var sp in spans)
            {
                if (sp.start > cursor) sb.Append(safe, cursor, sp.start - cursor);
                sb.Append("<color=#").Append(sp.color).Append('>');
                sb.Append(safe, sp.start, sp.length);
                sb.Append("</color>");
                cursor = sp.start + sp.length;
            }
            if (cursor < safe.Length) sb.Append(safe, cursor, safe.Length - cursor);
            return sb.ToString();
        }

        private void EnsureHighlightCache()
        {
            BuildHighlightRegexesIfNeeded();
            if (!string.Equals(lastHighlightedSrc, tempString, StringComparison.Ordinal))
            {
                highlightedRichText = BuildHighlightedRichText(tempString ?? string.Empty);
                lastHighlightedSrc = tempString;
            }
        }

        // ---- Indentation helpers ----
        private static int FindLineStart(string s, int index)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            index = Mathf.Clamp(index, 0, s.Length);
            int nl = s.LastIndexOf('\n', Mathf.Max(0, index - 1));
            return nl < 0 ? 0 : nl + 1;
        }

        private static string IndentBlock(string block, string indent)
        {
            if (string.IsNullOrEmpty(block)) return indent;
            var sb = new System.Text.StringBuilder(block.Length + 32);
            sb.Append(indent);
            for (int i = 0; i < block.Length; i++)
            {
                char ch = block[i];
                sb.Append(ch);
                if (ch == '\n' && i < block.Length - 1) sb.Append(indent);
            }
            return sb.ToString();
        }

        private string OutdentOnce(string line)
        {
            if (string.IsNullOrEmpty(line)) return line;
            if (line[0] == '\t') return line.Substring(1);

            // remove up to indentSize leading spaces
            int toRemove = 0;
            for (int i = 0; i < IndentString.Length && i < line.Length && line[i] == ' '; i++) toRemove++;
            return toRemove > 0 ? line.Substring(toRemove) : line;
        }

        private string OutdentBlock(string block, out int removedTotal)
        {
            removedTotal = 0;
            if (string.IsNullOrEmpty(block)) return block;

            var sr = new System.IO.StringReader(block);
            var sb = new System.Text.StringBuilder(block.Length);
            string line;
            bool first = true;

            while ((line = sr.ReadLine()) != null)
            {
                string after = OutdentOnce(line);
                removedTotal += line.Length - after.Length;
                if (!first) sb.Append('\n'); else first = false;
                sb.Append(after);
            }
            return sb.ToString();
        }

        // ---- Parse state helpers ----
        private float GetLineHeight(GUIStyle style)
        {
            if (cachedLineHeight > 0f) return cachedLineHeight;
            var h = style.lineHeight;
            if (h <= 0f) h = style.CalcSize(new GUIContent("W")).y + 2f;
            cachedLineHeight = Mathf.Max(12f, h);
            return cachedLineHeight;
        }

        private bool HasParseResult() =>
            !parseDirty && (parseResult.Ast != null || (parseResult.Errors != null));

        private bool HasErrors() =>
            HasParseResult() && (parseResult.Errors?.Count ?? 0) > 0;

        private bool IsFailed() =>
            HasParseResult() && (parseResult.Ast == null || HasErrors());

        // ---- Overlay style helpers ----
        private static void MakeOverlayTransparent(GUIStyle s)
        {
            // Force transparent backgrounds in all states
            s.normal.background = ClearTex;
            s.hover.background = ClearTex;
            s.active.background = ClearTex;
            s.focused.background = ClearTex;
            s.onNormal.background = ClearTex;
            s.onHover.background = ClearTex;
            s.onActive.background = ClearTex;
            s.onFocused.background = ClearTex;
            s.border = new RectOffset();
        }

        private static void MakeTextInvisible(GUIStyle s)
        {
            // Hide overlay text but keep caret/selection
            Color t = s.normal.textColor; t.a = 0f;
            s.normal.textColor = t;
            s.focused.textColor = t;
            s.active.textColor = t;
            s.hover.textColor = t;
            s.onNormal.textColor = t;
            s.onFocused.textColor = t;
            s.onActive.textColor = t;
            s.onHover.textColor = t;
        }
    }
}
