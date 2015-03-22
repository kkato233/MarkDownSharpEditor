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
    using MarkdownDeep;
    using System.Drawing;
    using System.Text;
    using System.Diagnostics;

	/// <summary>
	/// A highlighter to highlight LaTeX.
	/// </summary>
    class MarkdownHighlighterEx : IHighlighter
	{
        public MarkdownHighlighterEx(Sgry.Azuki.WinForms.AzukiControl ctrl)
        {
            int begin = 0;
            int end = ctrl.Document.Length;
            Highlight(ctrl.Document, ref begin, ref end);
            _ctrl = ctrl;
        }
        Sgry.Azuki.WinForms.AzukiControl _ctrl;

        int markDownSchemeBegin = 128;

        public void Highlight(Document doc, ref int dirtyBegin, ref int dirtyEnd)
        {
            if (dirtyBegin < 0 || doc.Length < dirtyBegin)
                throw new ArgumentOutOfRangeException("dirtyBegin");
            if (dirtyEnd < 0 || doc.Length < dirtyEnd)
                throw new ArgumentOutOfRangeException("dirtyEnd");

            int minPos = dirtyBegin;
            int maxPos = dirtyEnd;

            MarkdownDeep.Markdown m_Markdown = new MarkdownDeep.Markdown();
            m_Markdown.ExtraMode = MarkDownSharpEditor.AppSettings.Instance.fMarkdownExtraMode;
            m_Markdown.SafeMode = false;

            string text = doc.GetTextInRange(0, doc.Length);
            byte[] cars = new byte[text.Length];
            int n = markDownSchemeBegin;

            var schemaDic = this.GetColorSchemExtentions()
                .Where(r => r.BlockType != BlockType.Blank)
                .ToDictionary(r => r.BlockType);

            var schemaTokenDic = this.GetColorSchemExtentions()
                .Where(r => r.TokenType != TokenType.Text)
                .ToDictionary(r => r.TokenType);

            var blocks = m_Markdown.ProcessBlocks(text);

            for (int i = 0; i < doc.Length; i++)
            {
                cars[i] = (byte)CC.Normal;
            }
            int formatCount = 0;

            foreach (var items in blocks)
            {
                int begin = items.LineStart;
                int end = Math.Min(text.Length, items.LineStart + items.LineLength);
                ColorSchemeExtention bScheme;
                byte bType = 0;

                bool skipContents = false;

                if (items.BlockType == BlockType.html)
                {
                    Sgry.Azuki.Highlighter.Highlighters.Xml.Highlight(doc, ref begin, ref end);
                    skipContents = true;
                }
#if false
                else if (items.BlockType == BlockType.codeblock)
                {
                    int visibleStart = this._ctrl.GetIndexFromPosition(new Point(0, 0));
                    int visibleEnd = this._ctrl.GetIndexFromPosition(new Point(this._ctrl.Height, this._ctrl.Width));

                    if (InSet(visibleStart, visibleEnd, begin, end) && formatCount < 5)
                    {
                        Sgry.Azuki.Highlighter.Highlighters.CSharp.Highlight(doc, ref begin, ref end);
                        skipContents = true;
                        formatCount++;
                    }
                }
#endif
                if (skipContents == false && schemaDic.TryGetValue(items.BlockType, out bScheme))
                {
                    bType = (byte)bScheme.Key;
                    for (int i = begin; i < end; i++)
                    {
                        cars[i] = bType;
                    }

                    if (bScheme.BlockType >= BlockType.h1 && bScheme.BlockType <= BlockType.h6)
                    {
                        // H1 H6 �܂ł� �R���e���c�`�悵�Ȃ�
                        skipContents = true;
                    }
                }

                // Span�̗v�f��`��
                if (skipContents == false)
                {
                    StringBuilder sb = new StringBuilder();

                    items.InitRenderPositionList();

                    // Span �̕`��
                    items.Render(m_Markdown, sb);

                    var list = items.GetRenderPositionList();

                    // �J�n�ƏI�����������
                    foreach (var t in list)
                    {
                        int contBegin = Math.Max(t.Start,begin);
                        int contEnd = Math.Min(text.Length, t.Start + t.Len);
                        if (end > 0)
                        {
                            contEnd = Math.Min(contEnd, end);
                        }

                        //contBegin = t.Start;
                        //contEnd = Math.Min(text.Length, t.Start + t.Len);

                        if (schemaTokenDic.TryGetValue(t.TokenType, out bScheme))
                        {
                            if (bScheme.FindTokenType != null)
                            {
                                // �I���ʒu�̕␳
                                var endT = findNextToken(list, t, bScheme.FindTokenType.Value);
                                if (endT != null)
                                {
                                    contEnd = Math.Min(text.Length, endT.Start + endT.Len);
                                }
                            }
                            else if (bScheme.TokenType == TokenType.code_span)
                            {
                                // �O���1�����Ђ��L����
                                contBegin--;
                                contEnd ++;
                            }

                            bType = (byte)bScheme.Key;
                            for (int i = contBegin; i < contEnd; i++)
                            {
                                cars[i] = bType;
                            }
                        }
                        else if (schemaDic.TryGetValue(t.BlockType, out bScheme))
                        {
                            bType = (byte)bScheme.Key;
                            for (int i = contBegin; i < contEnd; i++)
                            {
                                cars[i] = bType;
                            }
                        }
                    }
                }
            }

            dirtyBegin = 0;
            dirtyEnd = text.Length - 1;

            for (int i = 0; i < text.Length;i++)
            {
                doc.SetCharClass(i, (CC)cars[i]);
            }

            return;
        }

        /// <summary>
        /// ����2�̐����Ɍ���肪���邩��������
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
            if (selectEndPos < dirtyBegin)
            {
                return false;
            }
            else if (dirtyEnd < selectionStartIndex)
            {
                return false;
            }
            return true;
        }

        public class ColorSchemeExtention
        {
            public ColorSchemeExtention()
            {

            }

            public ColorSchemeExtention(string regEx,Color forColor,Color backColor)
            {
                this.ForeColor = forColor;
                this.BackColor = backColor;
            }
            public ColorSchemeExtention(MarkdownDeep.BlockType block, Color forColor, Color backColor)
            {
                this.BlockType = block;
                this.ForeColor = forColor;
                this.BackColor = backColor;
            }
            public ColorSchemeExtention(MarkdownDeep.TokenType token, Color forColor, Color backColor)
            {
                this.TokenType = token;
                this.ForeColor = forColor;
                this.BackColor = backColor;
            }
            public ColorSchemeExtention(MarkdownDeep.TokenType token, MarkdownDeep.TokenType findToken, Color forColor, Color backColor)
            {
                this.TokenType = token;
                this.FindTokenType = findToken;
                this.ForeColor = forColor;
                this.BackColor = backColor;
            }
            public byte Key;
            public MarkdownDeep.BlockType BlockType;
            public MarkdownDeep.TokenType TokenType;
            public MarkdownDeep.TokenType ? FindTokenType;
            public Color ForeColor;
            public Color BackColor;
        }

        private const string mail_regex = @"<(?:(?:(?:(?:[a-zA-Z0-9_!#\$\%&'*+/=?\^`{}~|\-]+)(?:\.(?:[a-zA-Z0-9_!#\$\%&'*+/=?\^`{}~|\-]+))*)|(?:""(?:\\[^\r\n]|[^\\""])*"")))\@(?:(?:(?:(?:[a-zA-Z0-9_!#\$\%&'*+/=?\^`{}~|\-]+)(?:\.(?:[a-zA-Z0-9_!#\$\%&'*+/=?\^`{}~|\-]+))*)|(?:\[(?:\\\S|[\x21-\x5a\x5e-\x7e])*\])))>";
        private const string horisontal_regex = @"^(\* ){3,}$|^\*.$|^(- ){3,}|^-{3,}$|^(_ ){3,}$|^_{3,}$";

        public List<ColorSchemeExtention> GetColorSchemExtentions()
        {
            List<ColorSchemeExtention> ans = new List<ColorSchemeExtention>();

            var obj = MarkDownSharpEditor.AppSettings.Instance;
            var list = new List<ColorSchemeExtention>
            {
				//�����u���[�N ( Line break )
				new ColorSchemeExtention(@"  $", Color.FromArgb(obj.ForeColor_LineBreak), Color.FromArgb(obj.BackColor_LineBreak)),
				//���o���P ( Header 1 )
				new ColorSchemeExtention(BlockType.h1, Color.FromArgb(obj.ForeColor_Headlines[1]), Color.FromArgb(obj.BackColor_Headlines[1])),
				//���o���Q ( Header 2 )
				new ColorSchemeExtention(BlockType.h2, Color.FromArgb(obj.ForeColor_Headlines[2]), Color.FromArgb(obj.BackColor_Headlines[2])),
				//���o���R ( Header 3 )
				new ColorSchemeExtention(BlockType.h3, Color.FromArgb(obj.ForeColor_Headlines[3]), Color.FromArgb(obj.BackColor_Headlines[3])),
				//���o���S ( Header 4 )
				new ColorSchemeExtention(BlockType.h4, Color.FromArgb(obj.ForeColor_Headlines[4]), Color.FromArgb(obj.BackColor_Headlines[4])),
				//���o���T ( Header 5 )
				new ColorSchemeExtention(BlockType.h5, Color.FromArgb(obj.ForeColor_Headlines[5]), Color.FromArgb(obj.BackColor_Headlines[5])),
				//���o���U ( Header 6 )
				new ColorSchemeExtention(BlockType.h6, Color.FromArgb(obj.ForeColor_Headlines[6]), Color.FromArgb(obj.BackColor_Headlines[6])),
				//���p ( Brockquote )
				new ColorSchemeExtention(BlockType.quote, Color.FromArgb(obj.ForeColor_Blockquotes), Color.FromArgb(obj.BackColor_Blockquotes)),
				//���X�g ( Lists )
				new ColorSchemeExtention(BlockType.ul, Color.FromArgb(obj.ForeColor_Lists), Color.FromArgb(obj.BackColor_Lists)),
				new ColorSchemeExtention(BlockType.li, Color.FromArgb(obj.ForeColor_Lists), Color.FromArgb(obj.BackColor_Lists)),
				//�R�[�h�u���b�N ( Code blocks )
				new ColorSchemeExtention(BlockType.codeblock, Color.FromArgb(obj.ForeColor_CodeBlocks), Color.FromArgb(obj.BackColor_CodeBlocks)),
				new ColorSchemeExtention(TokenType.code_span, Color.FromArgb(obj.ForeColor_CodeBlocks), Color.FromArgb(obj.BackColor_CodeBlocks)),
				//�r�� ( Horizontal )
				new ColorSchemeExtention(BlockType.table_spec, Color.FromArgb(obj.ForeColor_Horizontal), Color.FromArgb(obj.BackColor_Horizontal)),
				//�����N ( Link )
				// [an example](http://example.com/ "Title") 
				new ColorSchemeExtention(TokenType.link, Color.FromArgb(obj.ForeColor_Links), Color.FromArgb(obj.BackColor_Links)),
				//�����iem, em, strong, strong�j
				new ColorSchemeExtention(TokenType.open_strong, TokenType.close_strong, Color.FromArgb(obj.ForeColor_Emphasis), Color.FromArgb(obj.BackColor_Emphasis)),
				//�����iem, em, strong, strong�j
				new ColorSchemeExtention(TokenType.open_em, TokenType.close_em, Color.FromArgb(obj.ForeColor_Emphasis), Color.FromArgb(obj.BackColor_Emphasis)),
				//�摜 ( Image )
				new ColorSchemeExtention(TokenType.img, Color.FromArgb(obj.ForeColor_Images), Color.FromArgb(obj.BackColor_Emphasis)),
				//���������N�i���[���A�h���X��URL�j ( Auto Links )
				new ColorSchemeExtention(@"<(https?|ftp)(:\/\/[-_.!~*\'()a-zA-Z0-9;\/?:\@&=+\$,%#]+)>", Color.FromArgb(obj.ForeColor_Links), Color.FromArgb(obj.BackColor_Links)),
				new ColorSchemeExtention(mail_regex, Color.FromArgb(obj.ForeColor_Links), Color.FromArgb(obj.BackColor_Links)),
				//�R�����g�A�E�g�i�����s�܂߂��R�����g�S���j( Comment out )
				new ColorSchemeExtention(@"<!--((?:.|\n)+)-->", Color.FromArgb(obj.ForeColor_Comments), Color.FromArgb(obj.BackColor_Comments))
			};

            //-----------------------------------
            // Markdown "Extra" SyntaxHighlighter
            //-----------------------------------
            if (MarkDownSharpEditor.AppSettings.Instance.fMarkdownExtraMode == true)
            {
                //HTML�u���b�N����Markdown�L�@�iMarkdown Inside HTML Blocks�j
                list.Add(new ColorSchemeExtention("\\s*markdown\\s*=\\s*(?>([\"\'])(.*?)\\1|([^\\s>]*))()", Color.FromArgb(obj.ForeColor_MarkdownInsideHTMLBlocks), Color.FromArgb(obj.BackColor_MarkdownInsideHTMLBlocks)));
                list.Add(new ColorSchemeExtention("(</?[\\w:$]+(?:(?=[\\s\"\'/a-zA-Z0-9])(?>\".*?\"|\'.*?\'|.+?)*?)?>|<!--.*?-->|<\\?.*?\\?>|<%.*?%>|<!\\[CDATA\\[.*?\\]\\]>)", Color.FromArgb(obj.ForeColor_MarkdownInsideHTMLBlocks), Color.FromArgb(obj.BackColor_MarkdownInsideHTMLBlocks)));
                //����ȑ��� ( Special Attributes )
                list.Add(new ColorSchemeExtention("(^.+?)(?:[ ]+.+?)?[ ]*\n(=+|-+)[ ]*\n+", Color.FromArgb(obj.ForeColor_SpecialAttributes), Color.FromArgb(obj.BackColor_SpecialAttributes)));
                list.Add(new ColorSchemeExtention("^(\\#{1,6})[ ]*(.+?)[ ]*\\#*(?:[ ]+.+?)?[ ]*\n+", Color.FromArgb(obj.ForeColor_SpecialAttributes), Color.FromArgb(obj.BackColor_SpecialAttributes)));
                //�R�[�h�u���b�N��؂�iFenced Code Blocks�j
                list.Add(new ColorSchemeExtention("(?:\\n|\\A)(~{3,})[ ]*(?:\\.?([-_:a-zA-Z0-9]+)|\\{.+?\\})?[ ]*\\n((?>(?!\\1[ ]*\\n).*\\n+)+)\\1[ ]*\\n", Color.FromArgb(obj.ForeColor_FencedCodeBlocks), Color.FromArgb(obj.BackColor_FencedCodeBlocks)));
                //�\�g�� ( Tables )
                list.Add(new ColorSchemeExtention("^[ ]{0,2}[|](.+)\\n[ ]{0,2}[|]([ ]*[-:]+[-| :]*)\\n((?:[ ]*[|].*\\n)*)(?=\\n|\\Z)", Color.FromArgb(obj.ForeColor_Tables), Color.FromArgb(obj.BackColor_Tables)));
                list.Add(new ColorSchemeExtention("^[ ]{0,2}(\\S.*[|].*)\\n[ ]{0,2}([-:]+[ ]*[|][-| :]*)\\n((?:.*[|].*\\n)*)(?=\\n|\\Z)", Color.FromArgb(obj.ForeColor_Tables), Color.FromArgb(obj.BackColor_Tables)));
                //��`���X�g ( Definition Lists )
                list.Add(new ColorSchemeExtention("(?>\\A\\n?|(?<=\n\n))(?>(([ ]{0,}((?>.*\\S.*\\n)+)\\n?[ ]{0,}:[ ]+)(?s:.+?)(\\z|\\n{2,}(?=\\S)(?![ ]{0,}(?: \\S.*\\n )+?\\n?[ ]{0,}:[ ]+)(?![ ]{0,}:[ ]+))))", Color.FromArgb(obj.ForeColor_DefinitionLists), Color.FromArgb(obj.BackColor_DefinitionLists)));
                //�r�� ( Footnotes )
                list.Add(new ColorSchemeExtention("^[ ]{0,}\\[\\^(.+?)\\][ ]?:[ ]*\n?((?:.+|\n(?!\\[\\^.+?\\]:\\s)(?!\\n+[ ]{0,3}\\S))*)", Color.FromArgb(obj.ForeColor_Footnotes), Color.FromArgb(obj.BackColor_Footnotes)));
                //�ȗ��\�L ( Abbreviations )
                list.Add(new ColorSchemeExtention("^[ ]{0,}\\*\\[(.+?)\\][ ]?:(.*)", Color.FromArgb(obj.ForeColor_Abbreviations), Color.FromArgb(obj.BackColor_Abbreviations)));
                //�����\�� : �ނ���_�u���R�[�e�[�V�������͉�������
                //Emphasis : Rather than remove the syntaxHighlighter of Emphasis within the double quotes
                list.Add(new ColorSchemeExtention("\".*?\"", Color.FromArgb(obj.ForeColor_MainText), Color.FromArgb(obj.BackColor_MainText)));
                //�o�b�N�X���b�V���G�X�P�[�v ( Backslash Escapes )
                list.Add(new ColorSchemeExtention(@"\\:|\\\|", Color.FromArgb(obj.ForeColor_BackslashEscapes), Color.FromArgb(obj.BackColor_BackslashEscapes)));
            }
            int n = markDownSchemeBegin;
            foreach (var item in list)
            {
                item.Key = (byte) n;
                n++;
            }

            return list;
        }

        private RenderPosition findNextToken(List<RenderPosition> list, RenderPosition t, TokenType tokenType)
        {
            int startPos = list.IndexOf(t);
            var item = list.Skip(startPos + 1).Where(r => r.TokenType == tokenType).FirstOrDefault();
            return item;
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
                    // �e�L�X�g�̐F
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
