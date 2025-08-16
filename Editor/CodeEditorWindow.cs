using UnityEngine;
using UnityEditor;
using GameKit.Scripting.Runtime;
using GameKit.Scripting.Internal;
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Reflection;

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
        private static readonly string ColFunctions = "FFA500"; // e.g. Ast, ParserResult, etc.

        // Simple keyword sets
        private static readonly string[] CSharpKeywords = new[]
        {
            "func", "if", "else", "branch", "sync", "null"
        };

        // Regexes (compiled for speed)
        private static readonly Regex RxString = new Regex(@"""([^""\\]|\\.)*""", RegexOptions.Compiled);
        private static readonly Regex RxComment = new Regex(@"//.*?$|/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.Multiline);
        private static readonly Regex RxNumber = new Regex(@"\b\d+(\.\d+)?\b", RegexOptions.Compiled);
        private static Regex RxKeywords;  // built once from CSharpKeywords
        private static Regex RxFunctions;     // built once from TypeLike

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

        // Wiggle settings
        private const float WiggleAmplitude = 0.7f;   // px up/down
        private const float WiggleWavelength = 3;   // px per wave
        private const float WiggleThickness = 2.0f;   // line thickness
        private const float UnderlineYOffset = -1;   // pixels above the bottom of the line
        static Color underlineColor = new Color(1f, 0, 0, 1f);

        // A style used only for measuring text widths (no wrap, no padding)
        private GUIStyle measureStyle;

        private int fontSize = 15;

        // ---- Completion data/state ----
        private static List<string> _globalCompletions = new List<string> { };
        private List<string> completionFiltered = new List<string>();
        private bool completionOpen;
        private int completionSel = 0;
        private string completionPrefix = "";
        private int completionTokenStartIndex = 0;
        private Rect completionPopupRect;
        private Vector2 completionScroll;

        // UI sizing
        private const int CompletionMaxVisible = 10;
        private const float CompletionItemHeight = 18f;
        private const float CompletionWidthMin = 180f;
        private const float CompletionWidthMax = 420f;

        // When completion is open, Tab/Enter should commit (not indent/newline)
        private bool CompletionActive => completionOpen && completionFiltered.Count > 0;

        private int _lastCaretForCompletion = -1;

        // --- Completion sidebar (right pane) ---
        private bool showCompletionSidebar = true;
        private float sidebarWidth = 260f;
        private const float SidebarMin = 160f;
        private const float SidebarMax = 520f;
        private bool resizingSidebar;

        private Vector2 completionSidebarScroll;
        private string completionSidebarFilter = ""; // optional filter box

        // Sidebar selection + details
        private string sidebarSelectedName;
        private Vector2 sidebarDetailsScroll;
        private Dictionary<string, MethodInfo> completionInfo = new Dictionary<string, MethodInfo>(StringComparer.OrdinalIgnoreCase);

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

            if (_globalCompletions.Count == 0)
            {
                Dictionary<string, MethodInfo> methods = new();
                Script.GatherScriptableFunctions(methods);

                _globalCompletions = methods.Select(m => m.Key).ToList();
                UpdateCompletionFilter();

                completionInfo = new Dictionary<string, MethodInfo>(methods, StringComparer.OrdinalIgnoreCase);
            }
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
                codeStyle.fontSize = fontSize;
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
                gutterStyle.fontSize = fontSize;
            }

            if (statusStyle == null)
            {
                statusStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleRight
                };
            }

            if (measureStyle == null)
            {
                measureStyle = new GUIStyle(codeStyle)
                {
                    richText = false,
                    wordWrap = false,
                    padding = new RectOffset(),
                    clipping = TextClipping.Overflow
                };
                measureStyle.fontSize = fontSize;
            }
        }

        private void BuildHighlightRegexesIfNeeded()
        {
            if (RxKeywords == null)
            {
                var kw = string.Join("|", CSharpKeywords.Select(Regex.Escape));
                RxKeywords = new Regex(@"\b(" + kw + @")\b", RegexOptions.Compiled);
            }
            if (RxFunctions == null)
            {
                Dictionary<string, MethodInfo> methods = new();
                Script.GatherScriptableFunctions(methods);

                var ty = string.Join("|", methods.Select(m => m.Key).Select(Regex.Escape));
                RxFunctions = new Regex(@"\b(" + ty + @")\b", RegexOptions.Compiled);
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

            // Manual completion trigger
            if ((e.control || e.command) && e.keyCode == KeyCode.Space)
            {
                UpdateCompletionFilter();
                completionOpen = completionFiltered.Count > 0;
                if (completionOpen) GUI.FocusControl(TextControlName);
                e.Use();
            }
        }

        private void HandleIndentation(Event e)
        {
            // Only handle real Tab keydowns; never touch Layout/Repaint
            if (e.type != EventType.KeyDown) return;

            bool isTab = (e.keyCode == KeyCode.Tab) || (e.character == '\t');

            // If completion is open, Tab commits it (still only on KeyDown)
            if (CompletionActive && isTab)
            {
                CommitCompletion(completionFiltered[completionSel], addParens: false);
                GUI.FocusControl(TextControlName);
                e.Use();
                return;
            }

            if (!isTab) return;

            // If we already handled a Tab this cycle, swallow duplicate KeyDown
            if (_tabActive) { e.Use(); return; }
            if (GUI.GetNameOfFocusedControl() != TextControlName) return;

            var te = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
            if (te == null) return;

            string src = tempString ?? string.Empty;
            int selStart = Mathf.Min(te.cursorIndex, te.selectIndex);
            int selEnd = Mathf.Max(te.cursorIndex, te.selectIndex);

            if (!e.shift) // indent
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
            else // outdent
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
                            for (int i = 0; i < IndentString.Length && lineStart + i < src.Length && src[lineStart + i] == ' '; i++) spaces++;
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

            _tabActive = true;      // swallow next Tab KeyDown
            _swallowTabUp = true;   // swallow Tab KeyUp to keep focus
            GUI.FocusControl(TextControlName);
            e.Use();                // consume this KeyDown
        }

        private void HandleAutoIndentEnter(Event e)
        {
            // Only handle real Enter keydowns; never touch Layout/Repaint
            if (e.type != EventType.KeyDown) return;

            bool isEnter =
                e.keyCode == KeyCode.Return ||
                e.keyCode == KeyCode.KeypadEnter ||
                e.character == '\n' || e.character == '\r';

            // If completion is open, Enter commits it (still only on KeyDown)
            if (CompletionActive && isEnter)
            {
                CommitCompletion(completionFiltered[completionSel], addParens: false);
                GUI.FocusControl(TextControlName);
                e.Use();
                return;
            }

            if (!isEnter) return;

            if (_enterActive) { e.Use(); return; }
            if (GUI.GetNameOfFocusedControl() != TextControlName) return;

            var te = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
            if (te == null) return;

            string s = tempString ?? string.Empty;
            int selStart = Mathf.Min(te.cursorIndex, te.selectIndex);
            int selEnd = Mathf.Max(te.cursorIndex, te.selectIndex);

            // previous line indent
            int lineStart = FindLineStart(s, selStart);
            int i = lineStart;
            var indent = new StringBuilder(16);
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t')) { indent.Append(s[i]); i++; }
            string indentStr = indent.ToString();

            // insert newline + indent
            string before = s.Substring(0, selStart);
            string after = s.Substring(selEnd);
            tempString = before + "\n" + indentStr + after;

            int newCaret = before.Length + 1 + indentStr.Length;
            te.cursorIndex = te.selectIndex = newCaret;

            lastHighlightedSrc = null;
            MarkDirtyForParse();

            _enterActive = true;
            _swallowEnterUp = true;
            GUI.FocusControl(TextControlName);
            e.Use(); // consume this KeyDown
        }

        private void HandleCompletionKeys(Event e)
        {
            if (!CompletionActive) return;

            // Navigation
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.UpArrow)
                {
                    completionSel = (completionSel - 1 + completionFiltered.Count) % completionFiltered.Count;
                    EnsureCompletionVisible();
                    e.Use(); return;
                }
                if (e.keyCode == KeyCode.DownArrow)
                {
                    completionSel = (completionSel + 1) % completionFiltered.Count;
                    EnsureCompletionVisible();
                    e.Use(); return;
                }

                // Commit: Enter / Tab
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Tab)
                {
                    CommitCompletion(completionFiltered[completionSel], addParens: false);
                    GUI.FocusControl(TextControlName);
                    e.Use(); return;
                }

                // Commit with parentheses if '(' typed
                if (e.character == '(')
                {
                    CommitCompletion(completionFiltered[completionSel], addParens: true);
                    GUI.FocusControl(TextControlName);
                    e.Use(); return;
                }

                // Dismiss
                if (e.keyCode == KeyCode.Escape)
                {
                    completionOpen = false;
                    e.Use(); return;
                }
            }
        }

        private void OnGUI()
        {
            InitStyles();
            var e = Event.current;

            HandleCompletionKeys(e);

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

            // Toolbar
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

                // Force save toggle
                allowForceSave = GUILayout.Toggle(allowForceSave, "Force Save", EditorStyles.toolbarButton);

                // Toggle sidebar visibility
                showCompletionSidebar = GUILayout.Toggle(showCompletionSidebar, "Completions", EditorStyles.toolbarButton);

                GUILayout.FlexibleSpace();

                // Save
                using (new EditorGUI.DisabledScope(IsFailed() && !allowForceSave))
                {
                    if (GUILayout.Button("Save", EditorStyles.toolbarButton))
                        TrySave();
                }

                // Revert
                if (GUILayout.Button("Revert", EditorStyles.toolbarButton))
                {
                    tempString = targetScript.Code;
                    lastHighlightedSrc = null;
                    MarkDirtyForParse();
                }
            }

            // --- compute first, draw conditionally on Repaint ---
            bool repaint = Event.current.type == EventType.Repaint;

            // Main editor area
            Rect fullRect = GUILayoutUtility.GetRect(0, 100000, 0, 100000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));


            // If sidebar is visible, reserve space on the right
            Rect mainRect = showCompletionSidebar
                ? new Rect(fullRect.x, fullRect.y, fullRect.width - sidebarWidth, fullRect.height)
                : fullRect;

            Rect sidebarRect = showCompletionSidebar
                ? new Rect(fullRect.xMax - sidebarWidth, fullRect.y, sidebarWidth, fullRect.height)
                : Rect.zero;


            // Gutter width
            string src = tempString ?? string.Empty;
            string[] lines = src.Split('\n');
            int lineCount = Mathf.Max(1, lines.Length);

            int gutterDigits = Mathf.FloorToInt(Mathf.Log10(Mathf.Max(1, lineCount))) + 1;
            float digitWidth = gutterStyle.CalcSize(new GUIContent(new string('0', gutterDigits))).x;
            float gutterWidth = Mathf.Ceil(digitWidth) + 12f;

            // Split rects for the main editor area (inside mainRect)
            Rect gutterRect = new Rect(mainRect.x, mainRect.y, gutterWidth, mainRect.height);
            Rect codeRect = new Rect(mainRect.x + gutterWidth, mainRect.y, mainRect.width - gutterWidth, mainRect.height);

            // Backgrounds
            if (repaint)
            {
                EditorGUI.DrawRect(mainRect, EditorGUIUtility.isProSkin ? new Color(0.15f, 0.15f, 0.15f) : new Color(0.95f, 0.95f, 0.95f));
                EditorGUI.DrawRect(gutterRect, EditorGUIUtility.isProSkin ? new Color(0.12f, 0.12f, 0.12f) : new Color(0.90f, 0.90f, 0.90f));
            }

            // Determine content height
            float lineH = GetLineHeight(codeStyle);
            var padded = new GUIStyle(codeStyle) { padding = new RectOffset(6, 6, 4, 4) };
            float textWidth = Mathf.Max(1f, codeRect.width - 16f - padded.padding.horizontal);
            float contentHeight = !codeStyle.wordWrap
                ? Mathf.Max(fullRect.height, lineCount * lineH + 8f)
                : Mathf.Max(fullRect.height, Mathf.CeilToInt(padded.CalcHeight(new GUIContent(src), textWidth)) + 8f);

            // Scroll view
            scroll = GUI.BeginScrollView(mainRect, scroll, new Rect(0, 0, mainRect.width - 16, contentHeight));
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

                    // --- NEW: wiggly underlines for exact error spans (works best with wordWrap=false) ---
                    if (HasErrors())
                    {
                        foreach (var err in parseResult.Errors)
                        {
                            int line1 = err.SourceLoc.Line;
                            int col1 = GetErrColumn(err.SourceLoc);                 // 1-based col (0 if unknown)
                            int len = GetErrLength(err.SourceLoc);                 // 0 if unknown

                            int li0 = Mathf.Clamp(line1 - 1, 0, lines.Length - 1);

                            if (col1 <= 0)
                            {
                                int start = 0;
                                while (start < lines[li0].Length && lines[li0][start] == ' ')
                                {
                                    ++start;
                                }

                                // No column info: underline the whole line
                                DrawErrorUnderline(contentCodeRect, lineH, lines, li0, start + 1, lines[li0].Length, underlineColor);
                            }
                            else
                            {
                                DrawErrorUnderline(contentCodeRect, lineH, lines, li0, col1, len, underlineColor);
                            }
                        }
                    }
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
                    UpdateCompletionFilter();
                }

                // Also refresh completion when the caret moves (arrows, mouse click)
                var teAfter = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
                int caretNow = teAfter != null ? teAfter.cursorIndex : -1;
                if (caretNow != _lastCaretForCompletion)
                {
                    _lastCaretForCompletion = caretNow;
                    UpdateCompletionFilter();
                }

                if (Event.current.type == EventType.Repaint && CompletionActive)
                {
                    // recompute popup rect at caret
                    completionPopupRect = GetCaretPopupRect(contentCodeRect, lineH, lines);
                    DrawCompletionPopup(completionPopupRect);
                }
            }
            GUI.EndScrollView();

            // ----- Right completion sidebar -----
            if (showCompletionSidebar)
            {
                OnGUICompletionSidebar(repaint, fullRect, sidebarRect);
            }

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

        private void OnGUICompletionSidebar(bool repaint, Rect fullRect, Rect sidebarRect)
        {
            // Resize handle (between main and sidebar)
            var splitterRect = new Rect(sidebarRect.x - 3f, sidebarRect.y, 6f, sidebarRect.height);
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);

            // Handle drag (don’t Use() layout events)
            if (Event.current.type == EventType.MouseDown && splitterRect.Contains(Event.current.mousePosition))
            {
                resizingSidebar = true;
                Event.current.Use();
            }
            if (resizingSidebar && Event.current.type == EventType.MouseDrag)
            {
                float newWidth = Mathf.Clamp(fullRect.xMax - Event.current.mousePosition.x, SidebarMin, SidebarMax);
                if (!Mathf.Approximately(newWidth, sidebarWidth))
                {
                    sidebarWidth = newWidth;
                    Repaint();
                }
                Event.current.Use();
            }
            if (Event.current.type == EventType.MouseUp)
            {
                resizingSidebar = false;
            }

            // Background + border
            if (repaint)
            {
                EditorGUI.DrawRect(sidebarRect, EditorGUIUtility.isProSkin ? new Color(0.10f, 0.10f, 0.10f, 1f) : new Color(0.98f, 0.98f, 0.98f, 1f));
                Handles.BeginGUI();
                Handles.color = EditorGUIUtility.isProSkin ? new Color(0.2f, 0.2f, 0.2f, 1f) : new Color(0.7f, 0.7f, 0.7f, 1f);
                Handles.DrawLine(new Vector3(sidebarRect.x, sidebarRect.y), new Vector3(sidebarRect.x, sidebarRect.yMax));
                Handles.EndGUI();
            }

            // Header + filter bar
            var filterRect = new Rect(sidebarRect.x + 6f, sidebarRect.y + 2, sidebarRect.width - 12f, 18f);
            completionSidebarFilter = EditorGUI.TextField(filterRect, completionSidebarFilter);

            // List area
            float listTop = filterRect.yMax + 4f;
            float detailsHeight = Mathf.Clamp(sidebarRect.height * 0.35f, 120f, 260f);
            var listRect = new Rect(sidebarRect.x, listTop, sidebarRect.width, sidebarRect.yMax - listTop - detailsHeight);
            var items = string.IsNullOrEmpty(completionSidebarFilter)
                ? _globalCompletions
                : _globalCompletions.Where(w => w.IndexOf(completionSidebarFilter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            // Scrollable list
            var viewRect = new Rect(0, 0, listRect.width - 16f, items.Count * 20f);
            completionSidebarScroll = GUI.BeginScrollView(listRect, completionSidebarScroll, viewRect);

            for (int i = 0; i < items.Count; i++)
            {
                var row = new Rect(0, i * 20f, viewRect.width, 20f);
                bool isSelected = string.Equals(items[i], sidebarSelectedName, StringComparison.Ordinal);

                if (repaint && row.Contains(Event.current.mousePosition - new Vector2(listRect.x, listRect.y)))
                    EditorGUI.DrawRect(new Rect(row.x, row.y, row.width, row.height),
                        EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.06f) : new Color(0f, 0f, 0f, 0.06f));

                if (isSelected)
                    EditorGUI.DrawRect(new Rect(row.x, row.y, row.width, row.height),
                        EditorGUIUtility.isProSkin ? new Color(0.25f, 0.35f, 0.6f, 0.30f) : new Color(0.2f, 0.4f, 0.8f, 0.25f));

                // Invisible button for hit test (works with scroll)
                if (GUI.Button(row, GUIContent.none, GUIStyle.none))
                {
                    sidebarSelectedName = items[i];
                    if (Event.current.clickCount >= 2)
                    {
                        GUI.FocusControl(TextControlName);
                        CommitCompletion(items[i], addParens: Event.current.shift);
                    }
                    Repaint();
                }

                GUI.Label(new Rect(row.x + 6f, row.y, row.width - 12f, row.height), items[i]);
            }

            GUI.EndScrollView();

            // Details panel at the bottom
            var detailsRect = new Rect(sidebarRect.x, sidebarRect.yMax - detailsHeight, sidebarRect.width, detailsHeight);

            // Adjust the list height so it doesn't overlap the details panel
            // (If you didn't already, set listRect height to: sidebarRect.yMax - listTop - detailsHeight)

            if (repaint)
            {
                EditorGUI.DrawRect(detailsRect, EditorGUIUtility.isProSkin ? new Color(0.12f, 0.12f, 0.12f, 1f) : new Color(1f, 1f, 1f, 1f));
                Handles.BeginGUI();
                Handles.color = EditorGUIUtility.isProSkin ? new Color(0.2f, 0.2f, 0.2f, 1f) : new Color(0.7f, 0.7f, 0.7f, 1f);
                Handles.DrawLine(new Vector2(detailsRect.x, detailsRect.y), new Vector2(detailsRect.xMax, detailsRect.y));
                Handles.EndGUI();
            }

            var pad = 6f;
            var line = new Rect(detailsRect.x + pad, detailsRect.y + pad, detailsRect.width - 2 * pad, 18f);
            if (!string.IsNullOrEmpty(sidebarSelectedName) && completionInfo.TryGetValue(sidebarSelectedName, out var mi))
            {
                // Title
                var title = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleLeft };
                GUI.Label(line, sidebarSelectedName, title);
                line.y += 20f;

                // Signature
                var sig = "FormatSignature(mi)";
                GUI.Label(line, sig, EditorStyles.miniLabel);
                line.y += 18f;

                // Declaring type + static
                string meta = $"{(mi.IsStatic ? "static " : "")}declared in {mi.DeclaringType?.FullName ?? "?"}";
                GUI.Label(line, meta, EditorStyles.miniLabel);
                line.y += 18f;

                // Notes (description/obsolete/tooltip) in scroll
                var notes = "GetMethodNotes(mi)";
                var notesRect = new Rect(detailsRect.x + pad, line.y, detailsRect.width - 2 * pad, detailsRect.yMax - line.y - 28f);
                var viewW = notesRect.width - 16f;
                var content = new GUIContent(string.IsNullOrEmpty(notes) ? "No description available." : notes);
                var neededH = Mathf.Max(20f, EditorStyles.wordWrappedLabel.CalcHeight(content, viewW));
                var viewRect2 = new Rect(0, 0, viewW, neededH);

                sidebarDetailsScroll = GUI.BeginScrollView(notesRect, sidebarDetailsScroll, viewRect2);
                GUI.Label(new Rect(0, 0, viewRect2.width, viewRect2.height), content, EditorStyles.wordWrappedLabel);
                GUI.EndScrollView();

                // Buttons (Insert / Insert())
                var btnY = detailsRect.yMax - 24f;
                if (GUI.Button(new Rect(detailsRect.x + pad, btnY, 80f, 20f), "Insert"))
                {
                    GUI.FocusControl(TextControlName);
                    CommitCompletion(sidebarSelectedName, addParens: false);
                }
                if (GUI.Button(new Rect(detailsRect.x + pad + 86f, btnY, 90f, 20f), "Insert()"))
                {
                    GUI.FocusControl(TextControlName);
                    CommitCompletion(sidebarSelectedName, addParens: true);
                }
            }
            else
            {
                GUI.Label(line, "Select a function to see details.", EditorStyles.miniLabel);
            }
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
            foreach (Match m in RxFunctions.Matches(safe))
                if (!IsCovered(m.Index)) spans.Add((m.Index, m.Length, ColFunctions));

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

        private bool HasErrors() => (parseResult.Errors?.Count ?? 0) > 0;

        private bool IsFailed() => parseResult.Ast == null || HasErrors();

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

        private static int GetErrColumn(object srcLoc)  // 1-based; 0/neg if unknown
        {
            dynamic l = srcLoc;
            try { return Mathf.Max(0, (int)l.Column); } catch { return 0; }
        }

        private static int GetErrLength(object srcLoc)  // 0 if unknown
        {
            dynamic l = srcLoc;
            try
            {
                int len = 0;
                try { len = (int)l.Length; } catch { /* ignore */ }
                if (len > 0) return len;
                int col = 0, end = 0;
                try { col = (int)l.Column; } catch { }
                try { end = (int)l.EndColumn; } catch { }
                if (col > 0 && end > col) return end - col;
                return 0;
            }
            catch { return 0; }
        }

        // Fallback: guess token length until whitespace
        private static int GuessTokenLength(string line, int startIdx0)
        {
            int i = Mathf.Clamp(startIdx0, 0, line.Length);
            while (i < line.Length && !char.IsWhiteSpace(line[i])) i++;
            return i - startIdx0;
        }

        // Measure X offset inside a line at a 1-based column
        private float GetXAtColumn(string line, int col1Based)
        {
            int idx0 = Mathf.Clamp(col1Based - 1, 0, line?.Length ?? 0);
            if (string.IsNullOrEmpty(line) || idx0 == 0) return 0f;
            // measure substring width with a non-wrapping style
            var before = line.Substring(0, idx0);
            return measureStyle.CalcSize(new GUIContent(before)).x;
        }

        // Draw a polyline wiggle from x0..x1 at baseline y
        private void DrawWiggly(float x0, float x1, float y, Color color)
        {
            if (x1 <= x0) return;

            // Retina-safe thickness
            float t = WiggleThickness * EditorGUIUtility.pixelsPerPoint;
            float amp = WiggleAmplitude;
            float wave = Mathf.Max(2f, WiggleWavelength);

            // Create points
            int steps = Mathf.Max(2, Mathf.CeilToInt((x1 - x0) / (wave * 0.5f)));
            var pts = new Vector3[steps + 1];
            float dx = (x1 - x0) / steps;

            for (int i = 0; i <= steps; i++)
            {
                float x = x0 + dx * i;
                // zigzag (cheaper than sine): alternate ±amp every half-wave
                float phase = (x - x0) / wave;
                float ay = ((int)Mathf.Floor(phase + 0.5f) % 2 == 0) ? amp : -amp;
                pts[i] = new Vector3(x, y + ay, 0);
            }

            Handles.BeginGUI();
            var prevCol = Handles.color;
            Handles.color = color;
#if UNITY_2022_1_OR_NEWER
            Handles.DrawAAPolyLine(t, pts);
#else
    Handles.DrawAAPolyLine(pts); // older versions ignore thickness
#endif
            Handles.color = prevCol;
            Handles.EndGUI();
        }

        // Convenience: draw a squiggle for a single error on a single (unwrapped) line
        private void DrawErrorUnderline(Rect contentCodeRect, float lineH, string[] lines, int lineIdx0, int col1, int len, Color color)
        {
            lineIdx0 = Mathf.Clamp(lineIdx0, 0, lines.Length - 1);
            string line = lines[lineIdx0];

            float leftX = contentCodeRect.x + 6f; // left padding to match your drawStyle padding
            float baseY = contentCodeRect.y + (lineIdx0 + 1) * lineH - UnderlineYOffset;

            float segX0 = leftX + GetXAtColumn(line, Mathf.Max(1, col1));
            float segX1;
            if (len > 0)
            {
                int endCol = Mathf.Max(1, col1 + len);
                float width = measureStyle.CalcSize(new GUIContent(line.Substring(Mathf.Max(0, col1 - 1), Mathf.Min(len, line.Length - (col1 - 1))))).x;
                segX1 = segX0 + width;
            }
            else
            {
                // underline until token end if no length info
                int start0 = Mathf.Max(0, col1 - 1);
                int guessed = GuessTokenLength(line, start0);
                float width = measureStyle.CalcSize(new GUIContent(line.Substring(start0, Mathf.Min(guessed, line.Length - start0)))).x;
                segX1 = segX0 + width;
            }

            // fallback if nothing measurable
            if (Mathf.Approximately(segX0, segX1))
                segX1 = leftX + measureStyle.CalcSize(new GUIContent(line)).x;

            DrawWiggly(segX0, segX1, baseY, color);
        }

        private static bool IsWordChar(char ch) => char.IsLetterOrDigit(ch) || ch == '_';

        private static int FindTokenStart(string s, int index)
        {
            index = Mathf.Clamp(index, 0, s?.Length ?? 0);
            int i = index;
            while (i > 0 && IsWordChar(s[i - 1])) i--;
            return i;
        }

        private TextEditor GetTE() =>
            (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);

        private void UpdateCompletionFilter()
        {
            var te = GetTE();
            if (te == null) { completionOpen = false; return; }

            string src = tempString ?? string.Empty;
            int caret = Mathf.Clamp(te.cursorIndex, 0, src.Length);

            completionTokenStartIndex = FindTokenStart(src, caret);
            int len = caret - completionTokenStartIndex;
            completionPrefix = (len > 0) ? src.Substring(completionTokenStartIndex, len) : string.Empty;

            if (string.IsNullOrEmpty(completionPrefix))
            {
                completionOpen = false;
                completionFiltered.Clear();
                return;
            }

            completionFiltered = _globalCompletions
                .Where(w => w.StartsWith(completionPrefix, StringComparison.OrdinalIgnoreCase))
                .Take(200)
                .ToList();

            completionSel = 0;
            completionOpen = completionFiltered.Count > 0;
        }

        private Rect GetCaretPopupRect(Rect contentCodeRect, float lineH, string[] lines)
        {
            var te = GetTE();
            int caret = te != null ? te.cursorIndex : 0;

            // line/column
            GetLineCol(tempString ?? string.Empty, caret, out int line, out int col);
            string lineText = (line >= 0 && line < lines.Length) ? lines[line] : string.Empty;

            // measure X up to 'col'
            int span = Mathf.Min(col, lineText.Length);
            float xOff = measureStyle.CalcSize(new GUIContent(span > 0 ? lineText.Substring(0, span) : "")).x;

            // popup size
            float itemCount = Mathf.Min(CompletionMaxVisible, Mathf.Max(1, completionFiltered.Count));
            float height = itemCount * CompletionItemHeight + 4f;
            float width = Mathf.Clamp(
                completionFiltered.Count > 0 ? measureStyle.CalcSize(new GUIContent(completionFiltered[0])).x + 60f : 200f,
                CompletionWidthMin, CompletionWidthMax);

            // position right under caret
            float x = contentCodeRect.x + 6f + xOff;
            float y = contentCodeRect.y + line * lineH + lineH - 2f;

            // ensure within the code rect horizontally
            if (x + width > contentCodeRect.x + contentCodeRect.width) x = contentCodeRect.x + contentCodeRect.width - width;
            if (x < contentCodeRect.x) x = contentCodeRect.x;

            return new Rect(x, y, width, height);
        }

        private void CommitCompletion(string word, bool addParens)
        {
            var te = GetTE();
            string src = tempString ?? string.Empty;
            int caret = te != null ? te.cursorIndex : 0;

            int start = Mathf.Clamp(completionTokenStartIndex, 0, src.Length);
            int end = Mathf.Clamp(caret, 0, src.Length);

            string insert = word + (addParens ? "()" : "");
            tempString = src.Substring(0, start) + insert + src.Substring(end);

            int newCaret = start + word.Length + (addParens ? 1 : 0); // inside parens if added
            if (te != null) te.cursorIndex = te.selectIndex = newCaret;

            lastHighlightedSrc = null;
            MarkDirtyForParse();

            completionOpen = false;
        }

        private void DrawCompletionPopup(Rect popupRect)
        {
            // background
            EditorGUI.DrawRect(popupRect, EditorGUIUtility.isProSkin ? new Color(0.09f, 0.09f, 0.09f, 0.98f) : new Color(1f, 1f, 1f, 0.98f));
            var border = new Rect(popupRect.x, popupRect.y, popupRect.width, popupRect.height);
            Handles.BeginGUI();
            Handles.color = EditorGUIUtility.isProSkin ? new Color(0.2f, 0.2f, 0.2f, 1f) : new Color(0.7f, 0.7f, 0.7f, 1f);
            Handles.DrawSolidRectangleWithOutline(border, Color.clear, Handles.color);
            Handles.EndGUI();

            // list
            var listRect = new Rect(popupRect.x + 2f, popupRect.y + 2f, popupRect.width - 4f, popupRect.height - 4f);
            var viewRect = new Rect(0, 0, listRect.width - 16f, completionFiltered.Count * CompletionItemHeight);
            completionScroll = GUI.BeginScrollView(listRect, completionScroll, viewRect);

            for (int i = 0; i < completionFiltered.Count; i++)
            {
                var r = new Rect(0, i * CompletionItemHeight, viewRect.width, CompletionItemHeight);
                bool sel = (i == completionSel);

                if (sel)
                    EditorGUI.DrawRect(r, EditorGUIUtility.isProSkin ? new Color(0.25f, 0.35f, 0.5f, 0.35f) : new Color(0.2f, 0.4f, 0.8f, 0.25f));

                string item = completionFiltered[i];
                var gc = new GUIContent(item);
                var style = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft };
                style.normal.textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black;

                GUI.Label(new Rect(r.x + 6f, r.y, r.width - 6f, r.height), gc, style);

                // mouse commit
                if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
                {
                    completionSel = i;
                    CommitCompletion(item, addParens: false);
                    GUI.FocusControl(TextControlName);
                    Event.current.Use();
                    break;
                }
            }

            GUI.EndScrollView();
        }

        private void EnsureCompletionVisible()
        {
            float y = completionSel * CompletionItemHeight;
            if (y < completionScroll.y) completionScroll.y = y;
            if (y + CompletionItemHeight > completionScroll.y + (CompletionMaxVisible * CompletionItemHeight))
                completionScroll.y = y + CompletionItemHeight - (CompletionMaxVisible * CompletionItemHeight);
        }
    }
}
