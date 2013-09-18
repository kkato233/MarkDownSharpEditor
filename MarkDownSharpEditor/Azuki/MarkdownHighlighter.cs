//=========================================================
using System;
using System.Collections.Generic;
using MarkDownSharpEditor;
using Sgry.Azuki;
using System.Linq;

namespace MarkDownSharpEditor.Azuki
{
    using CC = CharClass;
    using System.Text.RegularExpressions;
    using Sgry.Azuki.Highlighter;

	/// <summary>
	/// A highlighter to highlight LaTeX.
	/// </summary>
    class MarkdownHighlighter : IHighlighter
	{
        public MarkdownHighlighter(Sgry.Azuki.Document doc) 
        {
            _MarkdownSyntaxKeywordAarray = MarkdownSyntaxKeyword.CreateKeywordList();

            int begin = 0;
            int end = doc.Length;
            Highlight(doc, ref begin, ref end);
        }

        public void UpdateKeywords(ICollection<MarkdownSyntaxKeyword> keywords)
        {
            List<MarkdownSyntaxKeyword> list = new List<MarkdownSyntaxKeyword>(keywords);
            _MarkdownSyntaxKeywordAarray = list;
        }

        public class ColorSchemeExtention
        {
            public byte Key;
            public System.Drawing.Color ForeColor;
            public System.Drawing.Color BackColor;
            public MarkdownSyntaxKeyword Syntax;
        }

        int markDownSchemeBegin = 128;

        public List<ColorSchemeExtention> GetColorSchemExtentions()
        {
            List<ColorSchemeExtention> ans = new List<ColorSchemeExtention>();
            int n = markDownSchemeBegin;
            foreach (var item in _MarkdownSyntaxKeywordAarray)
            {
                ColorSchemeExtention scheme = new ColorSchemeExtention()
                {
                    Key = (byte)n,
                    ForeColor = item.ForeColor,
                    BackColor = item.BackColor,
                    Syntax = item,
                };

                ans.Add(scheme);
                n++;
            }

            return ans;
        }

        List<MarkdownSyntaxKeyword> _MarkdownSyntaxKeywordAarray;
 
        public void Highlight(Document doc, ref int dirtyBegin, ref int dirtyEnd)
        {
            if (dirtyBegin < 0 || doc.Length < dirtyBegin)
                throw new ArgumentOutOfRangeException("dirtyBegin");
            if (dirtyEnd < 0 || doc.Length < dirtyEnd)
                throw new ArgumentOutOfRangeException("dirtyEnd");

            int minPos = dirtyBegin;
            int maxPos = dirtyEnd;

            string text = doc.GetTextInRange(0, doc.Length);

            int n = markDownSchemeBegin;
            var schemaDic = GetColorSchemExtentions().ToDictionary(r => r.Syntax);

            for (int i = 0; i < doc.Length; i++)
            {
                doc.SetCharClass(i, CC.Normal);
            }

            foreach (MarkdownSyntaxKeyword mk in _MarkdownSyntaxKeywordAarray)
            {
                ColorSchemeExtention colorScheme = null;
                schemaDic.TryGetValue(mk, out colorScheme);
#if true
                MatchCollection col = mk.Regex.Matches(text, 0);
                if (col.Count > 0)
                {
                    foreach (Match m in col)
                    {
                        int selectionStartIndex = m.Groups[0].Index;
                        int selectionLength = m.Groups[0].Length;
                        int selectEndPos = selectionStartIndex + selectionLength;

                        for (int i = 0; i < selectionLength; i++)
                        {
                            doc.SetCharClass(i + selectionStartIndex, (CC) n);
                        }

                        if (InSet(dirtyBegin, dirtyEnd, selectionStartIndex, selectEndPos))
                        {
                            if (selectionStartIndex < minPos)
                            {
                                minPos = selectionStartIndex;
                            }
                            if (selectEndPos > maxPos)
                            {
                                maxPos = selectEndPos;
                            }

                        }
                    }
                }

                dirtyBegin = minPos;
                dirtyEnd = maxPos;
#else
                var m = mk.Regex.Match(text);
                while (m.Success)
                {
                    int selectionStartIndex = m.Index;
                    int selectionLength = m.Length;
                    int selectEndPos = selectionStartIndex + selectionLength;

                    for (int i = 0; i < selectionLength; i++)
                    {
                        doc.SetCharClass(i + selectionStartIndex, (CC)n);
                    }

                    m = m.NextMatch();
                }
                
            dirtyBegin = 0;
            dirtyEnd = doc.Length;
#endif
                n++;
            }

            return;
        }

        /// <summary>
        /// この2つの数字に交わりがあるか調査する
        /// </summary>
        /// <param name="dirtyBegin"></param>
        /// <param name="dirtyEnd"></param>
        /// <param name="selectionStartIndex"></param>
        /// <param name="selectEndPos"></param>
        /// <returns></returns>
        private bool InSet(int dirtyBegin, int dirtyEnd, int selectionStartIndex, int selectEndPos)
        {
#if DEBUG
            if (dirtyEnd < dirtyBegin) throw new InvalidOperationException("");
            if (selectEndPos < selectionStartIndex) throw new InvalidOperationException("");
#endif
            if (selectEndPos < dirtyBegin) return false;
            if (selectEndPos < dirtyBegin) return false;

            return true;
        }

        public ColorScheme MarkDownColorScheme
        {
            get
            {
                if (_scheme == null)
                {
                    ColorScheme sc = new ColorScheme(ColorScheme.Default);
                    foreach (var item in GetColorSchemExtentions())
                    {
                        sc.SetColor((CharClass)item.Key, item.ForeColor, item.BackColor);
                    }
                    // テキストの色
                    sc.SetColor(CC.Normal, System.Drawing.Color.Black, System.Drawing.Color.White);
                    _scheme = sc;
                }

                return _scheme;
            }
        }
        ColorScheme _scheme;

        public bool CanUseHook
        {
            get { return false; }
        }

        public void Highlight(Document doc)
        {
            throw new NotImplementedException();
        }

        public HighlightHook HookProc
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }
    }
}
