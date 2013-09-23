// 
//   MarkdownDeep - http://www.toptensoftware.com/markdowndeep
//	 Copyright (C) 2010-2011 Topten Software
// 
//   Licensed under the Apache License, Version 2.0 (the "License"); you may not use this product except in 
//   compliance with the License. You may obtain a copy of the License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software distributed under the License is 
//   distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
//   See the License for the specific language governing permissions and limitations under the License.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MarkdownDeep
{
	// Some block types are only used during block parsing, some
	// are only used during rendering and some are used during both
	public enum BlockType
	{
		Blank,			// blank line (parse only)
		h1,				// headings (render and parse)
		h2, 
		h3, 
		h4, 
		h5, 
		h6,
		post_h1,		// setext heading lines (parse only)
		post_h2,
		quote,			// block quote (render and parse)
		ol_li,			// list item in an ordered list	(render and parse)
		ul_li,			// list item in an unordered list (render and parse)
		p,				// paragraph (or plain line during parse)
		indent,			// an indented line (parse only)
		hr,				// horizontal rule (render and parse)
		user_break,		// user break
		html,			// html content (render and parse)
		unsafe_html,	// unsafe html that should be encoded
		span,			// an undecorated span of text (used for simple list items 
						//			where content is not wrapped in paragraph tags
		codeblock,		// a code block (render only)
		li,				// a list item (render only)
		ol,				// ordered list (render only)
		ul,				// unordered list (render only)
		HtmlTag,		// Data=(HtmlTag), children = content
		Composite,		// Just a list of child blocks
		table_spec,		// A table row specifier eg:  |---: | ---|	`data` = TableSpec reference
		dd,				// definition (render and parse)	`data` = bool true if blank line before
		dt,				// render only
		dl,				// render only
		footnote,		// footnote definition  eg: [^id]   `data` holds the footnote id
		p_footnote,		// paragraph with footnote return link append.  Return link string is in `data`.
	}

    public class ContentStartEndInfo
    {
        public string buf;
        public int contentStart;
        public int contentLen;

        public void Trim()
        {
            if (buf == null) return;
#if DEBUG
            string beforData = this.Content;
#endif
            // TrimStart
            while (contentLen > 0)
            {
                char c = buf[contentStart];
                if (char.IsWhiteSpace(c))
                {
                    contentStart++;
                    contentLen--;
                }
            }
            // TrimEnd
            while (contentLen > 0)
            {
                char c = buf[contentStart + contentLen - 1];
                if (char.IsWhiteSpace(c))
                {
                    contentLen--;
                }
            }
#if DEBUG
            string afterData = this.Content;
            System.Diagnostics.Debug.Assert(afterData == beforData.Trim());
#endif
        }
#if DEBUG
        public string Content
        {
            get
            {
                if (buf == null) return null;
                if (contentStart < 0) return buf;
                return buf.Substring(contentStart, contentLen);
            }
        }
#endif
    }

	public class Block
	{
		internal Block()
		{

		}

		internal Block(BlockType type)
		{
			blockType = type;
		}

        /// <summary>
        /// Content.Split('\n') の代用品として利用する
        /// </summary>
        /// <returns></returns>
        public List<ContentStartEndInfo> ContentSplitLines()
        {
            List<ContentStartEndInfo> ans = new List<ContentStartEndInfo>();

            if (blockType == BlockType.codeblock)
            {
                foreach (var line in children)
                {
                    ans.Add(new ContentStartEndInfo()
                    {
                       buf = line.buf,
                       contentStart = line.contentStart,
                       contentLen = line.contentLen,
                    });
                }
#if DEBUG
                CheckContentSplit(ans);
#endif
                return ans;
            }
            
            if (buf == null)
            {
                return null;
            }
            else
            {
                int startPos = Math.Max(0, contentStart);
                int endPos = startPos + contentLen;
                int findPos = buf.IndexOf('\n', startPos);
                while (findPos >= 0 && findPos < endPos )
                {
                    ans.Add(new ContentStartEndInfo()
                    {
                        buf = buf,
                        contentStart = contentStart,
                        contentLen = findPos - startPos,
                    });

                    startPos = findPos + 1;
                    findPos = buf.IndexOf('\n', startPos);
                }

                // 残りの部分を登録する
                if (startPos < endPos)
                {
                    ans.Add(new ContentStartEndInfo()
                    {
                        buf = buf,
                        contentStart = startPos,
                        contentLen = contentStart + contentLen - startPos,
                    });
                }
#if DEBUG
                CheckContentSplit(ans);
#endif
                return ans;
            }
        }

#if DEBUG
        private void CheckContentSplit(List<ContentStartEndInfo> cList)
        {
            List<string> list = new List<string>();
            foreach (var l in Content.Split('\n'))
            {
                list.Add(l);
            }
            List<string> list2 = new List<string>();

            foreach (var c in cList)
            {
                list2.Add(c.Content);
            }

            System.Diagnostics.Debug.Assert(list.Count == list2.Count);
            System.Diagnostics.Debug.Assert(string.Join("-", list) ==
                string.Join("-", list2));
        }
#endif

		public string Content
		{
			get
			{
				switch (blockType)
				{
					case BlockType.codeblock:
						StringBuilder s = new StringBuilder();
						foreach (var line in children)
						{
							s.Append(line.Content);
							s.Append('\n');
						}
						return s.ToString();
				}


				if (buf==null)
					return null;
				else
					return contentStart == -1 ? buf : buf.Substring(contentStart, contentLen);
			}
		}

		public int LineStart
		{
			get
			{
				return lineStart == 0 ? contentStart : lineStart;
			}
		}

		internal void RenderChildren(Markdown m, StringBuilder b)
		{
			foreach (var block in children)
			{
                block.ParentBlock = this;
				block.Render(m, b);
			}
		}

		internal void RenderChildrenPlain(Markdown m, StringBuilder b)
		{
			foreach (var block in children)
			{
				block.RenderPlain(m, b);
			}
		}

		internal string ResolveHeaderID(Markdown m)
		{
			// Already resolved?
			if (this.data!=null)
				return (string)this.data;

			// Approach 1 - PHP Markdown Extra style header id
			int end=contentEnd;
			string id = Utils.StripHtmlID(buf, contentStart, ref end);
			if (id != null)
			{
				contentEnd = end;
			}
			else
			{
				// Approach 2 - pandoc style header id
				id = m.MakeUniqueHeaderID(buf, contentStart, contentLen);
			}

			this.data = id;
			return id;
		}

		internal void Render(Markdown m, StringBuilder b)
		{
			switch (blockType)
			{
				case BlockType.Blank:
					return;

				case BlockType.p:
                    {
                        int offset = 0;
                        if (contentLen == 0 && this.ParentBlock != null)
                        {
                            offset = this.ParentBlock.contentStart;
                        }
                        else if (this.ParentBlock != null && this.ParentBlock.buf == null && this.ParentBlock.ParentBlock != null)
                        {
                            offset = this.ParentBlock.contentStart;
                        }

                        // TODO: この buf に proxy を渡して オリジナルの文字列の位置を子要素に伝えるように改造する
                        m.SpanFormatter.FormatParagraph(b, buf, contentStart, contentLen, offset, m.RenderPos);
                    }
					break;

				case BlockType.span:
                    if (m.RenderPos)
                    {
                        b.Append("<span ");
                        b.Append(" data-pos='" + globalContentStart.ToString() + "'>");
                        b.Append("</span>");
                    }
					m.SpanFormatter.Format(b, buf, contentStart, contentLen);
					b.Append("\n");
					break;

				case BlockType.h1:
				case BlockType.h2:
				case BlockType.h3:
				case BlockType.h4:
				case BlockType.h5:
				case BlockType.h6:
					if (m.ExtraMode && !m.SafeMode)
					{
						b.Append("<" + blockType.ToString());
                        if (m.RenderPos)
                        {
                            b.Append(" data-pos='" + globalContentStart.ToString() + "'");
                        }
						string id = ResolveHeaderID(m);
						if (!String.IsNullOrEmpty(id))
						{
							b.Append(" id=\"");
							b.Append(id);
							b.Append("\">");
						}
						else
						{
							b.Append(">");
						}
					}
					else
					{
						b.Append("<" + blockType.ToString() + ">");
					}
					m.SpanFormatter.Format(b, buf, contentStart, contentLen);
					b.Append("</" + blockType.ToString() + ">\n");
					break;

				case BlockType.hr:
                    b.Append("<hr ");
                    if (m.RenderPos)
                    {
                        b.Append(" data-pos='" + globalContentStart.ToString() + "'");
                    }
                    b.Append("/>\n");
					return;

				case BlockType.user_break:
					return;

				case BlockType.ol_li:
				case BlockType.ul_li:
                    b.Append("<li");
                    if (m.RenderPos)
                    {
                        b.Append(" data-pos='" + globalContentStart.ToString() + "'");
                    }
                    b.Append(">");
					m.SpanFormatter.Format(b, buf, contentStart, contentLen);
					b.Append("</li>\n");
					break;

				case BlockType.dd:
					b.Append("<dd>");
					if (children != null)
					{
						b.Append("\n");
						RenderChildren(m, b);
					}
					else
						m.SpanFormatter.Format(b, buf, contentStart, contentLen);
					b.Append("</dd>\n");
					break;

				case BlockType.dt:
				{
					if (children == null)
					{
                            //foreach (var l in Content.Split('\n'))
                            foreach(var c in ContentSplitLines())
						{
							b.Append("<dt>");
                                //m.SpanFormatter.Format(b, l.Trim());
                                m.SpanFormatter.Format(b, c.buf, c.contentStart, c.contentLen);
							b.Append("</dt>\n");
						}
					}
					else
					{
						b.Append("<dt>\n");
						RenderChildren(m, b);
						b.Append("</dt>\n");
					}
					break;
				}

				case BlockType.dl:
                    b.Append("<dl");
                    if (m.RenderPos)
                    {
                        b.Append(" data-pos='" + globalContentStart.ToString() + "'");
                    }
                    b.Append(">\n");
					RenderChildren(m, b);
					b.Append("</dl>\n");
					return;

				case BlockType.html:
                    if (m.RenderPos)
                    {
                        b.Append("<span ");
                        b.Append(" data-pos='" + globalContentStart.ToString() + "'>");
                        b.Append("</span>");
                    }
					b.Append(buf, contentStart, contentLen);
					return;

				case BlockType.unsafe_html:
                    if (m.RenderPos)
                    {
                        b.Append("<span ");
                        b.Append(" data-pos='" + globalContentStart.ToString() + "'>");
                        b.Append("</span>");
                    }
					m.HtmlEncode(b, buf, contentStart, contentLen);
					return;

				case BlockType.codeblock:
					if (m.FormatCodeBlock != null)
					{
                        if (m.RenderPos)
                        {
                            b.Append("<span ");
                            b.Append(" data-pos='" + globalContentStart.ToString() + "'>");
                            b.Append("</span>");
                        }
						var sb = new StringBuilder();
						foreach (var line in children)
						{
							m.HtmlEncodeAndConvertTabsToSpaces(sb, line.buf, line.contentStart, line.contentLen);
							sb.Append("\n");
						}
						b.Append(m.FormatCodeBlock(m, sb.ToString()));
					}
					else
					{
                        b.Append("<pre");
                        if (m.RenderPos)
                        {
                            b.Append(" data-pos='" + globalContentStart.ToString() + "'");
                        }
                        b.Append("><code>");
						foreach (var line in children)
						{
							m.HtmlEncodeAndConvertTabsToSpaces(b, line.buf, line.contentStart, line.contentLen);
							b.Append("\n");
						}
						b.Append("</code></pre>\n\n");
					}
					return;

				case BlockType.quote:
                    b.Append("<blockquote");
                    if (m.RenderPos)
                    {
                        b.Append(" data-pos='" + globalContentStart.ToString() + "'");
                    }
                    b.Append(">\n");
					RenderChildren(m, b);
					b.Append("</blockquote>\n");
					return;

				case BlockType.li:
                    b.Append("<li");
                    if (m.RenderPos && this.proxy != null)
                    {
                        b.Append(" data-pos='" + globalContentStart.ToString() + "'");
                    }
                    b.Append(">\n");
					RenderChildren(m, b);
					b.Append("</li>\n");
					return;

				case BlockType.ol:
                    b.Append("<ol");
                    if (m.RenderPos)
                    {
                        b.Append(" data-pos='" + globalContentStart.ToString() + "'");
                    }
                    b.Append(">\n");
					RenderChildren(m, b);
					b.Append("</ol>\n");
					return;

				case BlockType.ul:
                    b.Append("<ul");
                    if (m.RenderPos)
                    {
                    }
                    b.Append(">\n");
					RenderChildren(m, b);
					b.Append("</ul>\n");
					return;

				case BlockType.HtmlTag:
                    if (m.RenderPos)
                    {
                        b.Append("<span ");
                        b.Append(" data-pos='" + globalContentStart.ToString() + "'>");
                        b.Append("</span>");
                    }

					var tag = (HtmlTag)data;

					// Prepare special tags
					var name=tag.name.ToLowerInvariant();
					if (name == "a")
					{
						m.OnPrepareLink(tag);
					}
					else if (name == "img")
					{
						m.OnPrepareImage(tag, m.RenderingTitledImage);
					}

					tag.RenderOpening(b);
					b.Append("\n");
					RenderChildren(m, b);
					tag.RenderClosing(b);
					b.Append("\n");
					return;

				case BlockType.Composite:
				case BlockType.footnote:
					RenderChildren(m, b);
					return;

				case BlockType.table_spec:
                    if (m.RenderPos)
                    {
                        b.Append("<span ");
                        b.Append(" data-pos='" + globalContentStart.ToString() + "'>");
                        b.Append("</span>");
                    }
					((TableSpec)data).Render(m, b);
					break;

				case BlockType.p_footnote:
                    b.Append("<p");
                    if (m.RenderPos)
                    {
                        b.Append(" data-pos='" + globalContentStart.ToString() + "'");
                    }
                    b.Append(">");
					if (contentLen > 0)
					{
						m.SpanFormatter.Format(b, buf, contentStart, contentLen);
						b.Append("&nbsp;");
					}
					b.Append((string)data);
					b.Append("</p>\n");
					break;

				default:
                    b.Append("<" + blockType.ToString());
                    if (m.RenderPos)
                    {
                        b.Append(" data-pos='" + globalContentStart.ToString() + "'");
                    }
                    b.Append(">");
					m.SpanFormatter.Format(b, buf, contentStart, contentLen);
					b.Append("</" + blockType.ToString() + ">\n");
					break;
			}
		}

		internal void RenderPlain(Markdown m, StringBuilder b)
		{
			switch (blockType)
			{
				case BlockType.Blank:
					return;

				case BlockType.p:
				case BlockType.span:
					m.SpanFormatter.FormatPlain(b, buf, contentStart, contentLen);
					b.Append(" ");
					break;

				case BlockType.h1:
				case BlockType.h2:
				case BlockType.h3:
				case BlockType.h4:
				case BlockType.h5:
				case BlockType.h6:
					m.SpanFormatter.FormatPlain(b, buf, contentStart, contentLen);
					b.Append(" - ");
					break;


				case BlockType.ol_li:
				case BlockType.ul_li:
					b.Append("* ");
					m.SpanFormatter.FormatPlain(b, buf, contentStart, contentLen);
					b.Append(" ");
					break;

				case BlockType.dd:
					if (children != null)
					{
						b.Append("\n");
						RenderChildrenPlain(m, b);
					}
					else
						m.SpanFormatter.FormatPlain(b, buf, contentStart, contentLen);
					break;

				case BlockType.dt:
					{
						if (children == null)
						{
							//foreach (var l in Content.Split('\n'))
                            foreach(var c in ContentSplitLines())
							{
								//var str = l.Trim();
                                c.Trim();
								//m.SpanFormatter.FormatPlain(b, str, 0, str.Length);
                                m.SpanFormatter.Format(b, c.buf, c.contentStart, c.contentLen);
							}
						}
						else
						{
							RenderChildrenPlain(m, b);
						}
						break;
					}

				case BlockType.dl:
					RenderChildrenPlain(m, b);
					return;

				case BlockType.codeblock:
					foreach (var line in children)
					{
						b.Append(line.buf, line.contentStart, line.contentLen);
						b.Append(" ");
					}
					return;

				case BlockType.quote:
				case BlockType.li:
				case BlockType.ol:
				case BlockType.ul:
				case BlockType.HtmlTag:
					RenderChildrenPlain(m, b);
					return;
			}
		}

		public void RevertToPlain()
		{
			blockType = BlockType.p;
			contentStart = lineStart;
			contentLen = lineLen;
		}

		public int contentEnd
		{
			get
			{
				return contentStart + contentLen;
			}
			set
			{
				contentLen = value - contentStart;
			}
		}

		// Count the leading spaces on a block
		// Used by list item evaluation to determine indent levels
		// irrespective of indent line type.
		public int leadingSpaces
		{
			get
			{
				int count = 0;
				for (int i = lineStart; i < lineStart + lineLen; i++)
				{
					if (buf[i] == ' ')
					{
						count++;
					}
					else
					{
						break;
					}
				}
				return count;
			}
		}

		public override string ToString()
		{
			string c = Content;
			return blockType.ToString() + " - " + (c==null ? "<null>" : c);
		}

		public Block CopyFrom(Block other)
		{
			blockType = other.blockType;
			// buf = other.buf;
            proxy = other.proxy;
			contentStart = other.contentStart;
			contentLen = other.contentLen;
			lineStart = other.lineStart;
			lineLen = other.lineLen;
			return this;
		}

		public BlockType blockType;

        internal StringProxy proxy;
        internal string buf
        {
            get
            {
                if (proxy == null) return null;
                return proxy.str;
            }
        }
        public int globalContentStart
        {
            get
            {
                if (proxy != null)
                {
                    return proxy.LocalPosToGlobalPos(contentStart);
                }
                else
                {
                    return contentStart;
                }
            }
        }
		internal int contentStart;
		internal int contentLen;
		internal int lineStart;
		internal int lineLen;
		internal object data;			// content depends on block type

        public int contentStartInPage
        {
            get
            {
                int len = contentStart;
                Block b = this;

                if (b.ParentBlock != null)
                {
                    len = len + b.ParentBlock.contentStart;
                }
                return len;

                HashSet<Block> check = new HashSet<Block>();
                check.Add(this);

                while (b.ParentBlock != null)
                {
                    len += b.contentStart;
                    b = b.ParentBlock;
                    if (check.Contains(b))
                    {
                        // 循環参照を抜ける
                        break;
                    }
                    check.Add(b);
                }

                return len;
            }
        }
        internal List<Block> children
        {
            get
            {
                if (_children == null) return null;

                // parent 設定の確認
                foreach(Block b in _children) {
                    if (b.ParentBlock != null) {
                        b.ParentBlock = this;
                    }
	}

                return _children;
            }
            set
            {
                _children = value;
            }
        }
        List<Block> _children;

        /// <summary>
        /// 親のブロック
        /// </summary>
        public Block ParentBlock
        {
            get
            {
                if (_parent != null && _parent.IsAlive)
                {
                    Block b = _parent.Target as Block;
                    return b;
                }
                else
                {
                    return null;
                }
            }
            set {
                _parent = new WeakReference(value);
            }
        }
        WeakReference _parent;
	}

}
