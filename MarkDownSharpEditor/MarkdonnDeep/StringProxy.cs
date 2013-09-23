using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MarkdownDeep
{
    /// <summary>
    /// 現在の文字列の指定の場所が オリジナル文字列のどの位置に該当するのか？という情報を保持するためのクラス
    /// </summary>
    public class StringProxy
    {
        public StringBuilder baseString;

        public bool IsLiteral
        {
            get
            {
                return _literal;
            }
        }
        protected bool _literal = false;

        public StringProxy()
        {
            segments = new List<StringSegment>();
        }

        /// <summary>
        /// もとになる文字列を指定したコンストラクタ
        /// </summary>
        /// <param name="s"></param>
        public StringProxy(string s)
        {
            baseString = new StringBuilder(s);
            segments.Add(new StringSegment()
            {
                start = 0,
                length = s.Length,
                sb = baseString,
            });
        }

        /// <summary>
        /// もとになる文字列を指定したコンストラクタ
        /// </summary>
        /// <param name="s"></param>
        public StringProxy(string s,bool Literal)
        {
            baseString = new StringBuilder(s);
            segments.Add(new StringSegment()
            {
                start = 0,
                length = s.Length,
                sb = baseString,
                isLiteral = Literal,
            });

            this._literal = Literal;
        }

        /// <summary>
        /// 文字の範囲を指定した部分文字列の作成
        /// </summary>
        /// <param name="sb"></param>
        /// <param name="paramSegment"></param>
        public StringProxy(StringBuilder sb, List<StringSegment> paramSegment)
        {
            this.baseString = sb;
            this.segments = paramSegment;
        }

        /// <summary>
        /// 指定の部分文字列で生成される文字列
        /// </summary>
        public string str
        {
            get
            {
                if (_str != null) return _str;

#if false
                System.Text.StringBuilder sb = new StringBuilder();
                foreach (var item in segments)
                {
                    for (int i = 0; i < item.length; i++)
                    {
                        char c = item.sb[item.start + i];
                        sb.Append(c);
                    }
                }
                _str = sb.ToString();

#else
                StringBuilder sb2 = new StringBuilder();
                int maxLen = segments.Max(r => r.length);
                char[] buffer = new char[maxLen+ 1];

                foreach (var item in segments)
                {
                    item.sb.CopyTo(item.start,buffer,0,item.length);
                    sb2.Append(buffer, 0, item.length);
                }
                _str = sb2.ToString();

                // System.Diagnostics.Debug.Assert(_str2 == _str);
#endif

                return _str;
            }
        }

        public char this[int index]
        {
            get
            {
                if (_prev_index == index) return _prev_char;
                char c;
                int pos = index;
#if false
                if (_this != null && _this.TryGetValue(index,out c)) {
                    return c;
                }
                _this = _this ?? new Dictionary<int, char>();
#endif
                foreach (var item in segments)
                {
                    if (item.length > pos)
                    {
                        c = item.sb[item.start + pos];
#if false
                        _this[index] = c;
#endif
                        _prev_char = c;
                        _prev_index = index;
                        return c;
                    }

                    pos = pos - item.length;
                }
                c = '\0';
#if false
                _this[index] = c;
#endif
                _prev_char = c;
                _prev_index = index;

                return c;
            }
        }
        int _prev_index = -1;
        char _prev_char = '\0';

        Dictionary<int, char> _this;

        string _str;

        public int Length
        {
            get
            {
                if (_len >= 0) return _len;

                _len = segments.Sum(r => r.length);

                return _len;
            }
        }
        int _len = -1;

        /// <summary>
        /// 文字列の部分文字列を取得する
        /// </summary>
        /// <param name="start"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public StringProxy Substring(int start, int length)
        {
            List<StringSegment> segment = SubStringSegment(start, length);
            return new StringProxy(baseString, segment);
        }

        public void Append(StringProxy proxy, int start, int length)
        {
            StringProxy sub = proxy.Substring(start, length);
            Append(sub);
        }

        /// <summary>
        /// 2つの文字列を連結する
        /// </summary>
        /// <param name="addItem"></param>
        public virtual void Append(StringProxy addItem)
        {
            // 基本となる StringBuilder が同じものであることが前提条件
            if (this.baseString == null)
            {
                this.baseString = addItem.baseString;
            }
#if false
            else if (addItem.baseString == LnString)
            {
                // チェック不要
            }
            else if (this.baseString != addItem.baseString)
            {
                throw new InvalidOperationException("基準となる文字列が違う場合は連結できません。");
            }
#endif
            // 状態クリア
            InitStr();
            
            // セグメントの連結させる
            this.segments.AddRange(addItem.segments);
        }

        private void InitStr()
        {
            _len = -1;
            _str = null;
            _this = new Dictionary<int, char>();
            _prev_index = -1;
        }

        private List<StringSegment> SubStringSegment(int start, int length)
        {
            List<StringSegment> ans = new List<StringSegment>();

            // 最初のセグメントを探す
            int i = 0;
            int skip = start;
            while (skip > segments[i].length)
            {
                skip = skip - segments[i].length;
                i++;
            }
            StringSegment item = new StringSegment();
            item.sb = this.baseString;
            item.start = segments[i].start + skip;
            item.isLiteral = this._literal;
            int ll = segments[i].length - skip;
            while(ll < length) {
                item.length = ll;
                length = length - ll;
                ans.Add(item);
                item = new StringSegment();
                item.isLiteral = this._literal;
                item.sb = this.baseString;
                i++;
                item.start = segments[i].start;
                ll = segments[i].length;
            }
            item.length = length;
            ans.Add(item);

            return ans;
        }

        public int ?LocalPosToGlobalPos(int pos)
        {
            foreach (var item in segments)
            {
                for (int i = 0; i < item.length; i++)
                {
                    pos--;
                    if (pos <= 0 && item.isLiteral == false)
                    {
                        return item.start + i;
                    }
                }
            }

            return null;
        }

        protected List<StringSegment> segments = new List<StringSegment>();

        static StringBuilder LnString = new StringBuilder("\n");
        static StringBuilder EmptyString = new StringBuilder("");

        /// <summary>
        /// 改行だけのセグメント
        /// </summary>
        public static StringProxy Ln = new StringProxyLn(LnString);

        /// <summary>
        /// 空文字列
        /// </summary>
        public static StringProxy Empty = new StringProxyLn(EmptyString);

        public override string ToString()
        {
            return base.ToString();
        }

        /// <summary>
        /// トリミングした新しい文字列を作成する
        /// </summary>
        /// <returns></returns>
        public StringProxy Trim()
        {
            // TrimStart
            int skip = 0;
            while (skip < this.Length)
            {
                char c = this[skip];
                if (char.IsWhiteSpace(c))
                {
                    skip++;
                }
                else
                {
                    break;
                }
            }
            // TrimEnd
            int len = this.Length - skip;
            while (len > 0)
            {
                char c = this[skip + len];
                if (char.IsWhiteSpace(c))
                {
                    len = len - 1;
                } else {
                    break;
                }
            }

            return this.Substring(skip, len);
        }
    }

    internal class StringProxyLn : StringProxy
    {
        /// <summary>
        /// 文字の範囲を指定した部分文字列の作成
        /// </summary>
        /// <param name="sb"></param>
        /// <param name="paramSegment"></param>
        public StringProxyLn(StringBuilder sb, List<StringSegment> paramSegment)
            :base(sb,paramSegment)
        {
        }

        public StringProxyLn(StringBuilder sb)
        {
            this.baseString = sb;
            this._literal = true;
            segments.Add(new StringSegment()
            {
                sb = this.baseString,
                start = 0,
                length = sb.Length,
                isLiteral = true,
            });
        }

        public override void Append(StringProxy addItem)
        {
            throw new InvalidOperationException("改行文字だけのProxy に何も追加できません。");
        }
    }

    public class StringSegment
    {
        public int start;
        public int length;
        public StringBuilder sb;
        public bool isLiteral;
    }
}
