// System.Web.dll への参照が必要。
//
// 2009.10.10 属性の終了判定の間違い修正
// 2009.10.13 タグの中にある " の取り扱いを多少改善 
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using System.Xml.XPath;

namespace HtmlTools
{
    public class AttributeParser
    {
        /// <summary>
        /// HTML タグを解析して 属性を抽出する。
        /// </summary>
        /// <param name="htmlTagString"></param>
        /// <returns></returns>
        public List<AttributeItem> ParseHtmlTag(string htmlTagString)
        {
            List<AttributeItem> ans = new List<AttributeItem>();
            // <img src=test.gif > を解析して <img {space} , attr:src=gif , {space} > に分解する。
            // <div> は <div > に分解する
            using (StringReader sr = new StringReader(htmlTagString))
            {
                AttributeTokenizer t = new AttributeTokenizer(sr);

                while (t.Read())
                {
                    ans.Add(t.Current);
                }
                // Tokenの先頭と 末尾は NullAttributeItem に変換する。
                if (ans.Count >= 2)
                {
                    AttributeItem top = ans.First();
                    AttributeItem last = ans.Last();

                    AttributeItem nullTop = new NullAttributeItem(top.RawString);
                    AttributeItem nullLast = new NullAttributeItem(last.RawString);

                    ans.Remove(top);
                    ans.Remove(last);
                    ans.Insert(0, nullTop);
                    ans.Add(nullLast);
                }
            }
            return ans;
        }
    }

    public class AttributeTokenizer
    {
        TextReader _stream;

        int ch = -1;
        int ch_next = -1;

        bool stream_eof = false;

        readonly int EOF = -1;

        public AttributeTokenizer(TextReader stream)
        {
            _stream = stream;
            stream_eof = false;
            read_ch();
            read_ch();
        }

        private void read_ch()
        {
            ch = ch_next;
            if (stream_eof)
            {
                ch_next = -1;
            }
            else
            {
                ch_next = _stream.Read();
                if (ch_next < 0)
                {
                    stream_eof = true;
                }
            }
        }

        AttributeItem currentItem = null;

        /// <summary>
        /// 属性を読む
        /// 空白：空白の終了まで
        /// 文字：属性＝属性の値 
        /// 属性の値："で始まれば " まで それ以外は空白または 終了タグ が出現するまで
        /// </summary>
        /// <returns></returns>
        private AttributeItem read_token()
        {
            if (ch == EOF)
            {
                currentItem = null;
                return null;
            }
            else if (char.IsWhiteSpace((char)ch))
            {
                // 空白をすべて取り込む
                StringBuilder raw = new StringBuilder();
                raw.Append((char)ch);
                read_ch();
                while (ch != EOF)
                {
                    if (char.IsWhiteSpace((char)ch))
                    {
                        raw.Append((char)ch);
                    }
                    else
                    {
                        break;
                    }

                    read_ch();
                }
                AttributeItem attr = new AttributeItem(raw.ToString());
                currentItem = attr;
                return attr;
            }
            else if (ch == '>')
            {
                // 終了タグ
                read_ch();
                AttributeItem attr = new AttributeItem(">");
                currentItem = attr;
                return attr;
            }
            else
            {
                // = または 空白 または > が出現するまで attribute名
                StringBuilder sbAttrName = new StringBuilder();
                StringBuilder raw = new StringBuilder();
                while (ch != EOF)
                {
                    if (ch == '=')
                    {
                        break;
                    }
                    else if (char.IsWhiteSpace((char)ch))
                    {
                        break;
                    }
                    else if (ch == '>')
                    {
                        break;
                    }
                    sbAttrName.Append((char)ch);
                    raw.Append((char)ch);
                    read_ch();
                }

                StringBuilder sbValue = new StringBuilder();
                if (ch == '=')
                {
                    // 属性の値を取得
                    raw.Append((char)ch);
                    read_ch();
                    if (ch == '"' || ch == '\'')
                    {
                        char find_char = (char)ch;
                        sbValue.Append((char)ch);
                        raw.Append((char)ch);
                        read_ch();
                        // TODO: 文字列のエスケープ処理の対応が抜けている。
                        // 終了の " または ' が見つかるまで取り込み
                        while (ch != EOF)
                        {
                            // 2009.10.10 属性の終了判定の間違い修正 Start
                            //if (char.IsWhiteSpace((char)ch) || ch == '>' || ch == '<')
                            //{
                            //    break;
                            //}
                            //else 
                            // 2009.10.10 End
                            if (ch == find_char)
                            {
                                sbValue.Append((char)ch);
                                raw.Append((char)ch);
                                read_ch();
                                break;
                            }
                            sbValue.Append((char)ch);
                            raw.Append((char)ch);
                            read_ch();
                        }
                    }
                    else
                    {
                        // 空白が見つかるまで取り込み
                        while (ch != EOF)
                        {
                            if (char.IsWhiteSpace((char)ch) || ch == '>' ) // > が来たら終了
                            {
                                break;
                            }
                            sbValue.Append((char)ch);
                            raw.Append((char)ch);
                            read_ch();
                        }
                    }
                }
                AttributeItem attr = new AttributeItem(sbAttrName.ToString(), sbValue.ToString(), raw.ToString());
                currentItem = attr;
                return attr;
            }
        }

        private bool is_space_char(int ch)
        {
            if (ch == ' ' || ch == '\t' || char.IsControl((char)ch))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool Read()
        {
            read_token();
            if (currentItem == null)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public AttributeItem Current
        {
            get { return currentItem; }
        }

    }

    public class HtmlParser
    {
        // HTML ファイルを解析して DOM クラスを作成する。
        /// <summary>
        /// ファイルが UTF8だという前提で動作するのでそれ以外のエンコードの場合
        /// 正常に動作しない可能性がある。
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public DOM Parse(string file)
        {
            DOM dom = new DOM();
            using (StreamReader sr = new StreamReader(file))
            {
                Tokenizer p = new Tokenizer(sr);
                while (p.Read())
                {
                    p.Current._dom = dom;
                    dom.TokenList.Add(p.Current);
                }
            }
            return dom;
        }

        public DOM ParseHtmlString(string htmlString)
        {
            DOM dom = new DOM();
            using (StringReader sr = new StringReader(htmlString))
            {
                Tokenizer p = new Tokenizer(sr);
                while (p.Read())
                {
                    p.Current._dom = dom;
                    dom.TokenList.Add(p.Current);
                }
            }
            return dom;
        }
    }

    /*BNF として解析する */
    /* 変であっても 処理できるようにする */
    /* 字句解析 */
    public class Tokenizer
    {
        TextReader _stream;

        int ch = -1;
        int ch_next = -1;
        int ch_next_next = -1;
        int ch_next_next_next = -1;

        bool stream_eof = false;

        readonly int EOF = -1;

        public Tokenizer(TextReader stream)
        {
            _stream = stream;
            stream_eof = false;
            read_ch();
            read_ch();
            read_ch();
            read_ch();
        }

        private void read_ch()
        {
            ch = ch_next;
            ch_next = ch_next_next;
            ch_next_next = ch_next_next_next;
            if (stream_eof)
            {
                ch_next_next_next = -1;
            }
            else
            {
                ch_next_next_next = _stream.Read();
                if (ch_next_next_next < 0)
                {
                    stream_eof = true;
                }
            }
        }

        Token currentToken = null;

        private Token read_token()
        {
            if (ch == EOF)
            {
                currentToken = null;
                return null;
            }
            else if (ch == '<' && ch_next == '!' && ch_next_next == '-' && ch_next_next_next == '-')
            {
                // コメントの終了を探す
                // IEの仕様は <!--> もコメントとして判断するので・・それと同様の動きをする。

                StringBuilder sb = new StringBuilder();
                sb.Append((char)ch);
                read_ch();
                while (ch != EOF)
                {
                    if (ch == '-' && ch_next == '-' && ch_next_next == '>')
                    {
                        sb.Append((char)ch);
                        read_ch();
                        sb.Append((char)ch);
                        read_ch();
                        sb.Append((char)ch);
                        read_ch();
                        break;
                    }
                    else
                    {
                        sb.Append((char)ch);
                    }

                    read_ch();
                }
                Token token = new Token();
                token.TokenType = TokenTypeEnum.Comments;
                token.RawString = sb.ToString();
                currentToken = token;
                return token;

            }
            else if (ch == '<' && (!is_space_char(ch_next)))
            {
                // タグ
                StringBuilder sb = new StringBuilder();
                sb.Append((char)ch);
                read_ch();

                bool tagEndFlag = false;
                // タグの開始
                while (ch != EOF)
                {
                    sb.Append((char)ch);

                    if (ch == '"' || ch == '\'')
                    {
                        char find_char = (char)ch;
                        read_ch();
                        while (ch != EOF)
                        {
                            if (ch == find_char)
                            {
                                sb.Append((char)ch);
                                break;
                            }
                            sb.Append((char)ch);
                            read_ch();
                        }
                    }
                    else if (ch == '>')
                    {
                        // タグの終了
                        read_ch();
                        tagEndFlag = true;
                        break;
                    }
                    read_ch();
                }
                string rawStr = sb.ToString();

                Token token = new Token();
                if (tagEndFlag == false)
                {
                    // タグだと思ったが途中で終了
                    token.TokenType = TokenTypeEnum.Literal;
                    token.RawString = rawStr;
                }
                else if (rawStr.Length > 2)
                {
                    // タグ
                    token.TokenType = TokenTypeEnum.HtmlTag;
                }
                else
                {
                    // それ以外の文字
                    token.TokenType = TokenTypeEnum.Literal;
                }
                token.RawString = rawStr;
                currentToken = token;
                return token;
            }
            else
            {
                // タグの可能性のある文字 < が来るまでは Literal として登録
                StringBuilder sb = new StringBuilder();
                sb.Append((char)ch);
                read_ch();

                while (ch != EOF)
                {
                    if (ch != '<')
                    {
                        sb.Append((char)ch);
                        read_ch();
                    }
                    else
                    {
                        break;
                    }
                }
                Token token = new Token();
                token.TokenType = TokenTypeEnum.Literal;
                token.RawString = sb.ToString();
                currentToken = token;
                return token;
            }
        }

        private bool is_space_char(int ch)
        {
            if (ch == ' ' || ch == '\t' || char.IsControl((char)ch))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool Read()
        {
            read_token();
            if (currentToken == null)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public Token Current
        {
            get { return currentToken; }
        }
    }

    /*
<! があった場合 > までを特殊トークンとする。
<!--   --> が見つかるまでコメント
<!DOCTYPE
     * * * */

    public enum TokenTypeEnum
    {
        /// <summary>
        /// 未定義
        /// </summary>
        Undef,
        /// <summary>
        /// 文字
        /// </summary>
        Literal,
        /// <summary>
        /// タグ要素
        /// </summary>
        HtmlTag,
        /// <summary>
        /// コメント
        /// </summary>
        Comments,
    }

    public class Token
    {
        public TokenTypeEnum TokenType;

        public override string ToString()
        {
            if (RawString != null) return RawString;

            // 分解した AttributeItem を文字列に変換して結合
            StringBuilder sb = new StringBuilder();
            if (firstNullAttribute != null) sb.Append(firstNullAttribute.ToString());
            foreach (var attr in _attrList)
            {
                sb.Append(attr.ToString());
            }
            if (lastNullAttribute != null) sb.Append(lastNullAttribute.ToString());

            return sb.ToString();
        }

        public string RawString;

        /// <summary>
        /// タグ名が一致するか判定。
        /// タグ解析前であれば、
        /// タグ解析を行わずに 文字列として一致するか？を判定して
        /// 処理速度を早くする。
        /// </summary>
        /// <param name="tagName"></param>
        /// <returns></returns>
        public bool EqualsTag(string tagName)
        {
            if (TokenType != TokenTypeEnum.HtmlTag)
            {
                return false;
            }

            if (_tagName != null)
            {
                return _tagName.Equals(tagName, StringComparison.InvariantCultureIgnoreCase);
            }
            // タグ名解析を行っていない場合
            string checkTag = "<" + tagName;
            if (RawString.Length > checkTag.Length + 1 &&
                string.Equals(checkTag, RawString.Substring(0, checkTag.Length), StringComparison.OrdinalIgnoreCase))
            {
                char tagNextChar = RawString[tagName.Length + 1]; // タグの終了の文字
#if DEBUG
                // 上記コードでは以下のようなテストを実施するつもり
                string t2 = "<test>";
                string tTagName = "test";
                char tNextChar = t2[1 + tTagName.Length];
                System.Diagnostics.Debug.Assert(tNextChar == '>');
#endif
                if (tagNextChar == '>' || char.IsWhiteSpace(tagNextChar))
                {
                    _tagName = tagName.ToLower();
                    return true;
                }
            }

            return false;
        }


        /// <summary>
        /// 属性名を指定してその値を文字列として取得する。
        /// 属性が指定されていない場合には null を返す。
        /// Attribute名は大文字小文字を区別しない。
        /// </summary>
        /// <param name="attributeName"></param>
        /// <returns></returns>
        public string GetAttribute(string attributeName)
        {
            return GetAttributes(attributeName);
        }
        /// <summary>
        /// 属性名を指定してその値を文字列として取得する。
        /// 属性が指定されていない場合には null を返す。
        /// Attribute名は大文字小文字を区別しない。
        /// </summary>
        /// <param name="attributeName"></param>
        /// <returns></returns>
        public string GetAttributes(string attributeName)
        {
            string keyAttributeName = attributeName.ToLower();
            if (_attribute == null)
            {
                ParseAttribute();
            }

            if (_attribute.ContainsKey(attributeName))
            {
                string attr = _attribute[keyAttributeName];

                // TODO: 今回は文字列抽出のタイミングで 属性の値の変換を行っているが
                //       トラブルとなる可能性もあるので注意。
                //       Valueの中間に " が含まれていた場合のエスケープ処理が問題
                if (attr.Length >= 2 && ( attr[0] == '"'  || attr[0] == '\'') && attr[attr.Length-1] == attr[0])
                {
                    return System.Web.HttpUtility.HtmlDecode(attr.Substring(1, attr.Length - 2));
                }
                else
                {
                    return System.Web.HttpUtility.HtmlDecode(attr);
                }
            }
            else
            {
                return null; // 見つからない
            }
        }

        /// <summary>
        /// 指定の属性を削除する
        /// </summary>
        /// <param name="attributeName"></param>
        public void RemoveAttribute(string attributeName)
        {
            // null を指定する事で 削除できる。
            SetAttributes(attributeName, null);
        }

        AttributeItem firstNullAttribute;
        AttributeItem lastNullAttribute;

        /// <summary>
        /// RawString から 属性を生成する。
        /// </summary>
        private void ParseAttribute()
        {
            // 属性を細かく検査する。
            _attrList = ParseHtmlAttribute(RawString, out firstNullAttribute, out lastNullAttribute);
            _attribute = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            RawString = null;
            foreach (var attr in _attrList)
            {
                if (!string.IsNullOrEmpty(attr.Name))
                {
                    _attribute[attr.Name.ToLower()] = attr.Value;
                }
            }
        }

        /// <summary>
        /// HTML タグを解析して属性の一覧を作成する。
        /// </summary>
        /// <param name="RawString"></param>
        /// <returns></returns>
        private List<AttributeItem> ParseHtmlAttribute(string RawString, out AttributeItem tagAttr, out AttributeItem endTagAttr)
        {
            // <img src=test.gif > を解析して <img , {space} , attr:src=gif , {space} , > に分解する。
            AttributeParser p = new AttributeParser();
            List<AttributeItem> ans = p.ParseHtmlTag(RawString);

            // タグ部分を抽出して タグ名を取得する。
            if (ans.Count >= 2)
            {
                AttributeItem tag = ans.FirstOrDefault();
                AttributeItem endTag = ans.LastOrDefault();

                string rawTagStr = "";
                if (tag != null)
                {
                    rawTagStr = tag.RawString;
                }
                if (rawTagStr.Length > 1 && rawTagStr[0] == '<')
                {
                    // タグの開始
                    string strTag = rawTagStr.Substring(1).Trim().ToLower();
                    _tagName = strTag;
                }
                ans.Remove(tag);
                ans.Remove(endTag);
                tagAttr = tag;
                endTagAttr = endTag;
            }
            else
            {
                tagAttr = null;
                endTagAttr = null;
            }

            return ans;
        }

        /// <summary>
        /// Token のタグ名を返す
        /// </summary>
        public string TagName
        {
            get
            {
                if (TokenType != TokenTypeEnum.HtmlTag) return null;

                if (_attrList == null)
                {
                    ParseAttribute();
                }
                return _tagName;
            }
        }

        Dictionary<string, string> _attribute;
        List<AttributeItem> _attrList;

        /// <summary>
        /// タグ名は小文字で記録しておくこと
        /// </summary>
        string _tagName;

        /// <summary>
        /// 属性名を設定する。
        /// 設定する値が null の場合は該当の属性は『削除』する
        /// </summary>
        /// <param name="attributeName"></param>
        /// <param name="attributeValue"></param>
        public void SetAttributes(string attributeName, string attributeValue)
        {
            string keyAttributeName = attributeName.ToLower();
            if (_attrList == null)
            {
                ParseAttribute();
            }
            if (attributeValue == null)
            {
                // 削除項目の検索
                List<AttributeItem> delItemList = new List<AttributeItem>();
                foreach (var attr in _attrList)
                {
                    if (string.Equals(attr.Name, keyAttributeName,StringComparison.OrdinalIgnoreCase))
                    {
                        delItemList.Add(attr);
                    }
                }
                // 削除の実施
                foreach (AttributeItem delItem in delItemList)
                {
                    _attrList.Remove(delItem);
                }
                _attribute.Remove(keyAttributeName);
            }
            else
            {
                AttributeItem item = _attrList.FirstOrDefault(at => at.Name == keyAttributeName);
                if (item == null)
                {
                    // 新規追加
                    if (_attrList.Count > 0)
                    {
                        AttributeItem spaceItem = new NullAttributeItem(" ");
                        _attrList.Add(spaceItem);
                    }
                    item = new AttributeItem();
                    item.Name = keyAttributeName;
                    _attrList.Add(item);
                }
                item.Value = attributeValue;
            }
        }

        /// <summary>
        /// 内部のHTML を解析する
        /// </summary>
        public string InnerHtml
        {
            get
            {
                var list = GetInnerTokens();

                StringBuilder sb = new StringBuilder();
                foreach (Token t in list)
                {
                    sb.Append(t.ToString());
                }
                return sb.ToString();
            }
            set
            {
                List<Token> newTokenList = new List<Token>();

                if (string.IsNullOrEmpty(value) == false)
                {
                    newTokenList = ParseInnerHtmlToToken(value);
                }
                int thisPos = _dom.TokenList.IndexOf(this);
                
                // 今までのToken を取り除く
                var list = GetInnerTokens();
                foreach (var oldToken in list)
                {
                    _dom.TokenList.Remove(oldToken);
                }
                // 取り除いた場所に新しいTokenを入れる
                if (thisPos >= _dom.TokenList.Count - 1)
                {
                    _dom.TokenList.AddRange(newTokenList);
                }
                else
                {
                    _dom.TokenList.InsertRange(thisPos + 1, newTokenList);
                }
            }
        }

        /// <summary>
        /// 内部にあるToken一覧を取得
        /// </summary>
        /// <returns></returns>
        private List<Token> GetInnerTokens()
        {
            if (this._dom == null)
            {
                throw new InvalidOperationException("DOMから切り離された Token の InnerHtml は解析できません。");
            }

            List<Token> innerToken = new List<Token>();

            int pos = this._dom.TokenList.IndexOf(this);
            if (pos >= 0)
            {
                // 終了タグを発見する
                string tagName = this.TagName;
                if (tagName != null && tagName.StartsWith("/") == false)
                {
                    string findEndTag = "/" + tagName;
                    int startPos = pos + 1;
                    int findEndTagPos = startPos;
                    int nestLevel = 0;
                    for(int i=pos + 1; i < _dom.TokenList.Count;i++) {
                        var t = _dom.TokenList[i];

                        if (t.TagName == null)
                        {
                            continue;
                        }
                        else if (string.Equals(tagName, t.TagName, StringComparison.OrdinalIgnoreCase))
                        {
                            // 階層を上げる
                            nestLevel++;
                        }
                        else if (string.Equals(findEndTag, t.TagName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (nestLevel == 0)
                            {
                                // 目的のタグ発見
                                findEndTagPos = i;
                                break;
                            }
                            else
                            {
                                // 階層を下げる
                                nestLevel--;
                            }
                        }
                    }

                    if (findEndTagPos > startPos)
                    {
                        // この範囲のタグが 内部のタグ
                        for (int i = startPos; i < findEndTagPos; i++)
                        {
                            innerToken.Add(_dom.TokenList[i]);
                        }
                    }
                }
            }
            return innerToken;
        }

        private List<Token> ParseInnerHtmlToToken(string innerHtml)
        {
            List<Token> ans = new List<Token>();
            using (StringReader sr = new StringReader(innerHtml))
            {
                Tokenizer p = new Tokenizer(sr);
                while (p.Read())
                {
                    ans.Add(p.Current);
                }
            }
            return ans;
        }

        /// <summary>
        /// このToken を保持しているDOMへの参照
        /// </summary>
        internal DOM _dom;
    }

    /// <summary>
    /// 名前も値も null の Attribute を作成。
    /// RawString には 空白の情報が含まれている。
    /// </summary>
    public class NullAttributeItem : AttributeItem
    {
        public NullAttributeItem(string rawString)
        {
            base._RawString = rawString;
        }

        /// <summary>
        /// 
        /// </summary>
        public override string RawString
        {
            get
            {
                return base._RawString;
            }
        }

        public override string ToString()
        {
            return base._RawString;
        }
    }

    public class AttributeItem
    {
        public AttributeItem()
        {
        }
        /// <summary>
        /// アートリビュート名と値を指定してインスタンスを作成
        /// </summary>
        /// <param name="attributeName"></param>
        /// <param name="initialValue"></param>
        public AttributeItem(string attributeName, string initialValue)
        {
            _RawString = null;
            _Name = attributeName;
            _Value = initialValue;
        }

        /// <summary>
        /// アートリビュート名と値を指定してインスタンスを作成。
        /// 内容に変更が無ければオリジナルの文字列と同じ値を返す事ができるように
        /// RawString を事前に設定することができる。
        /// </summary>
        /// <param name="attributeName"></param>
        /// <param name="initialValue"></param>
        public AttributeItem(string attributeName, string initialValue, string paramRawString)
        {
            _RawString = paramRawString;
            _Name = attributeName;
            _Value = initialValue;
        }
        /// <summary>
        /// </summary>
        /// <param name="fromRawString"></param>
        public AttributeItem(string fromRawString)
        {
            _RawString = fromRawString;
            // 属性の文字列を解析する
            // alt=test 等になっているはず。
            int eqPos = fromRawString.IndexOf('=');
            if (eqPos >= 1)
            {
                _Name = fromRawString.Substring(0, eqPos);
                _Value = fromRawString.Substring(eqPos + 1);
            }
            else
            {
                // 空白文字が含まれていない事が前提。上位クラスで事前に処理済み？
                _Name = fromRawString.Trim();
                _Value = null;
            }
        }
        /// <summary>
        /// 属性名は小文字に正規化されている。
        /// </summary>
        public string Name
        {
            get { return _Name; }
            set { _Name = value; _RawString = null; }
        }
        string _Name;

        public string Value
        {
            get { return _Value; }
            set
            {
                _Value = value; _RawString = null;
            }
        }
        string _Value;
        /// <summary>
        /// オリジナルの文字列
        /// </summary>
        public virtual string RawString
        {
            get
            {
                // RawString に変更が無い場合はそのまま返す
                if (_RawString != null) return _RawString;
                // 属性に値が設定されていない場合にはその属性を返す
                if (_Value == null) return _Name;

                // 属性="値" という形にして返す。

                string dispValue;
                if (_Value.Length >= 2 && _Value[0] == '"' && _Value[_Value.Length - 1] == '"')
                {
                    // 事前に "" が設定されているのでそのまま利用する。
                    dispValue = _Value;
                }
                if (_Value.Length >= 2 && _Value[0] == '\'' && _Value[_Value.Length - 1] == '\'')
                {
                    // 事前に ' が設定されているのでそのまま利用する。
                    dispValue = _Value;
                }
                else
                {
                    // " を &quot; に変換する。
                    dispValue = "\"" + _Value.Replace("\"", "&quot;") + "\"";
                }
                return _Name + "=" + dispValue;
            }
        }
        protected string _RawString;

        public override string ToString()
        {
            return RawString;
        }
    }


    public class DOM
    {
        public List<Token> TokenList = new List<Token>();

        /// <summary>
        /// HTML文字列に変換する。
        /// </summary>
        /// <returns></returns>
        public string ToHtmlString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (Token t in TokenList)
            {
                sb.Append(t.ToString());
            }
            return sb.ToString();
        }

        public IEnumerable<Token> GetElementsByTagName(string tagName)
        {
            List<Token> ans = new List<Token>();
            foreach (Token token in TokenList)
            {
                if (token.EqualsTag(tagName))
                {
                    ans.Add(token);
                }
            }

            return ans;
        }

        /// <summary>
        /// タグを指定して その 子要素も含めて削除する
        /// </summary>
        /// <param name="tag"></param>
        public void RemoveTokenAndChild(Token tag)
        {
            int pos = this.TokenList.IndexOf(tag);
            if (pos >= 0)
            {
                string endTagName = "/" + tag.TagName;
                // 次の /frame を探す
                var tagEnd = this.TokenList.Skip(pos)
                    .Where(r => string.Equals(r.TagName,endTagName,StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault();

                if (tagEnd != null)
                {
                    int endPos = this.TokenList.IndexOf(tagEnd);
                    if (endPos > pos)
                    {
                        List<Token> deleteList = new List<Token>();
                        for (int i = pos; i <= endPos; i++)
                        {
                            deleteList.Add(this.TokenList[i]);
                        }
                        // 実際に削除
                        foreach (var token in deleteList)
                        {
                            token._dom = null;
                            this.TokenList.Remove(token);
                        }
                    }
                }
            }
        }
    }
}
