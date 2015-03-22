using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MarkdownDeep
{
    /// <summary>
    /// 描画位置情報
    /// </summary>
    public class RenderPosition
    {
        public BlockType BlockType;
        public TokenType TokenType;
        public int Start;
        public int Len;
    }
}
