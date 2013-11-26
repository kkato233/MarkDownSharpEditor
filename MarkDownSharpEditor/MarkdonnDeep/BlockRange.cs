using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MarkdownDeep
{
    /// <summary>
    /// 全体の文字列の部分文字列を作成した場合
    /// その部分文字列が全体の文字列の中の位置を
    /// </summary>
    public class BlockRange
    {
        /// <summary>
        /// 基本のオフセット
        /// </summary>
        public int BaseOffset;

        public List<BlockRangeItem> Items = new List<BlockRangeItem>();

        public class BlockRangeItem
        {
            public string buf;
            public int start;
            public int len;
            public BlockType blockType;

            public string Content
            {
                get
                {
                    if (buf == null) return null;
                    return buf.Substring(start, len);
                }
            }
        }

        public List<TokenRangeItem> Tokens = new List<TokenRangeItem>();

        public class TokenRangeItem
        {
            public string buf;
            public int start;
            public int len;
            public TokenType tokenType;
            public BlockType blockType;

            public string Content
            {
                get
                {
                    if (buf == null) return null;
                    return buf.Substring(start, len);
                }
            }
        }

        /// <summary>
        /// 範囲を追加する
        /// </summary>
        /// <param name="b"></param>
        /// <param name="blockStart"></param>
        /// <param name="blockLen"></param>
        internal void Append(Block b, int blockStart, int blockLen)
        {
            b.IsUse = true; // 利用しているフラグを設定

            if (b.blockRange == null)
            {
                // 新規に追加
                this.Items.Add(new BlockRangeItem() { blockType = b.blockType, buf = b.buf, start = blockStart, len = blockLen });
            }
            else
            {
                // 間接参照して追加
                var list = b.blockRange.GetRange(blockStart, blockLen);

                // BlockType を正しく設定
                list.ForEach(r =>
                {
                    r.blockType = b.blockType;
                });

                this.Items.AddRange(list);
            }
        }
        internal void Append(string str, int blockStart, int blockLen)
        {
            BlockType t = BlockType.Blank;

            // 空白以外を追加した場合に種類を変更
            foreach (char c in str)
            {
                if (char.IsControl(c) == false || char.IsWhiteSpace(c) == false)
                {
                    t = BlockType.p;
                    break;
                }
            }
            // 新規に追加
            this.Items.Add(new BlockRangeItem() { blockType = t, buf = str, start = blockStart, len = blockLen });
        }
        /// <summary>
        /// 指定の範囲で絞り込んだBlockRange のリストを取得する
        /// </summary>
        /// <param name="blockStart"></param>
        /// <param name="blockLen"></param>
        /// <returns></returns>
        internal List<BlockRangeItem> GetRange(int blockStart, int blockLen)
        {
            int skipLen = blockStart;

            var list = this.Items.ToList();
            var item = list.FirstOrDefault();

            // 開始位置の検出
            while (item != null && skipLen > item.len)
            {
                // スキップ位置を次に進める
                list.RemoveAt(0);
                skipLen -= item.len;
                item = list.FirstOrDefault();
            }

            int readLen = blockLen;

            List<BlockRangeItem> ans = new List<BlockRangeItem>();

            // 終了位置の検出 & ans に追加
            while (item != null && readLen > 0)
            {
                int thisReadLen = Math.Min(item.len - skipLen, readLen);

                ans.Add(new BlockRangeItem()
                {
                    blockType = item.blockType,
                    buf = item.buf,
                    start = item.start + skipLen,
                    len = thisReadLen,
                });

                readLen = readLen - thisReadLen;
                skipLen = 0;
                list.RemoveAt(0);
                item = list.FirstOrDefault();
            }

            return ans;
        }

        internal int? GetIndexOf(int pos,string buf)
        {
            int skipLen = pos;
            int? lastPos = null;

            foreach (var item in this.Items)
            {
                if (skipLen < item.len)
                {
                    if (item.buf == buf)
                    {
                        return item.start + skipLen;
                    }
                    else if (item.buf == null)
                    {
                        return lastPos;
                    }
                    else
                    {
                        return null;
                    }
                }
                skipLen -= item.len;
                if (item.buf != null)
                {
                    lastPos = item.start + item.len;
                }
            }

            return lastPos;
        }

        internal int? GetEndPos(string md)
        {
            var lastItem = this.Items.LastOrDefault();
            if (lastItem != null && lastItem.buf == md)
            {
                return lastItem.start + lastItem.len;
            }

            return null;
        }
    }
}
