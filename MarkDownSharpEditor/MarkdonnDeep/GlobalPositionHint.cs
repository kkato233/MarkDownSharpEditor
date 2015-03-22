using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MarkdownDeep
{
    /// <summary>
    /// Block や Token では オリジナルのソースコードの位置が
    /// 明確に記録されていないので対応表を作成する
    /// </summary>
    public class GlobalPositionHint
    {
        public GlobalPositionHint(string originalStr)
        {
            if (originalStr == null)
            {
                originalStr = "";
            }

            this._s = new StringBuilder(originalStr);
            this._globalStr = originalStr;
            globalPos = new List<int>();
            for (int i = 0; i < originalStr.Length; i++)
            {
                globalPos.Add(i);
            }
        }
        public GlobalPositionHint(string globalStr, List<int> globalPos)
        {
            this._s = new StringBuilder(globalStr);
            this._globalStr = globalStr;
            this.globalPos = globalPos.ToList();
        }

        /// <summary>
        /// 指定の位置のGlobalPos を取得する
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        public int GetGlobalPosAt(int pos)
        {
            if (pos < 0 || pos >= globalPos.Count) return -1;

            return globalPos[pos];
        }
        public string GlobalStr
        {
            get {
                return _globalStr;
            }
        }
        StringBuilder _s;
        string _globalStr;
        List<int> globalPos = new List<int>();
    }
    public class GlobalPositionHintBuilder
    {
        public void AppendSpace()
        {
            pos.Add(-1);
        }
        public void Append(GlobalPositionHint hint, int startPos, int len)
        {
            if (baseString == null && hint != null && hint.GlobalStr != null)
            {
                baseString = hint.GlobalStr;
            }
            if (hint != null && hint.GlobalStr == baseString)
            {
                for (int i = 0; i < len; i++)
                {
                    pos.Add(hint.GetGlobalPosAt(startPos + i));
                }
            }
            else
            {
                for (int i = 0; i < len; i++)
                {
                    pos.Add(-1);
                }
            }
        }
        string baseString;
        List<int> pos = new List<int>();

        public GlobalPositionHint Build()
        {
            GlobalPositionHint ans = new GlobalPositionHint(baseString,pos);
            return ans;
        }
    }
}
