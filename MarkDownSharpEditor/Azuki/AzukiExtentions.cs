using Sgry.Azuki;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MarkDownSharpEditor.Azuki
{
    static class AzukiExtentions
    {
        /*
            public static System.Drawing.Point GetPositionFromCharIndex(this Sgry.Azuki.WinForms.AzukiControl ctrl,int charPos)
            {
                return new System.Drawing.Point(0, 0);
            }
         * */
    }

    /// <summary>
    /// RichText を Azuki に置き換えたときに処理の違いを吸収するためのクラス
    /// </summary>
    public class AzukeRTF 
    {
        public bool IMEWorkaroundEnabled;
        ColorScheme sc;

        public AzukeRTF(Sgry.Azuki.WinForms.AzukiControl azuki)
        {
            ctrl = azuki;
            azuki.TextChanged += azuki_TextChanged;
            azuki.Resize += azuki_Resize;

            sc = new ColorScheme(ColorScheme.Default);
        }

        void azuki_Resize(object sender, EventArgs e)
        {
            ctrl.ViewWidth = ctrl.Width - 22;
        }

        public EventHandler TextChanged;

        void azuki_TextChanged(object sender, EventArgs e)
        {
            if (TextChanged != null)
            {
                TextChanged(sender, e);
            }
        }
        Sgry.Azuki.WinForms.AzukiControl ctrl;

        public int Width
        {
            get
            {
                return ctrl.Width;
            }
            set
            {
                ctrl.Width = value;
            }
        }

        public bool AllowDrop
        {
            get
            {
                return ctrl.AllowDrop;
            }
            set
            {
                ctrl.AllowDrop = value;
            }
        }

        public System.Drawing.Font Font
        {
            get
            {
                return ctrl.Font;
            }
            set
            {
                ctrl.Font = value;
            }
        }

        public bool Modified
        {
            get
            {
                return ctrl.Document.IsDirty;
            }
            set
            {
                if (value == false)
                {
                    ctrl.Document.IsDirty = value;
                }
                else
                {
                    // IsDirty はプログラムで true に設定できないので無視
                }
            }
        }

        public System.Drawing.Point AutoScrollOffset { get; set; }

        public int SelectionStart
        {
            get
            {
                int begin;
                int end;
                ctrl.Document.GetSelection(out begin, out end);
                return begin;
            }
            set
            {
                ctrl.Document.SetSelection(value, value);
            }
        }

        public int SelectionLength
        {
            get
            {
                int begin;
                int end;
                ctrl.Document.GetSelection(out begin, out end);
                return end - begin;
            }
            set
            {
                int start = SelectionStart;
                int end = start + value;
                ctrl.Document.SetSelection(start, end);
            }
        }

        internal void Select(int selectStart, int selectLength)
        {
            if (SelectionStart > ctrl.Document.Length)
            {
                SelectionStart = ctrl.Document.Length;
            }
            int selectEnd = selectStart + selectLength;
            if (selectEnd > ctrl.Document.Length)
            {
                selectEnd = ctrl.Document.Length;
            }
            ctrl.Document.SetSelection(selectStart, selectEnd);
        }

        public string Text
        {
            get
            {
                return ctrl.Text;
            }
            set
            {
                ctrl.Text = value;
            }
        }

        public int VerticalPosition {
            get
            {
                return ctrl.View.GetVirPosFromIndex(ctrl.Document.Length).Y;
            }
            set
            {
                int y = value;
                int index = ctrl.View.GetIndexFromVirPos(new System.Drawing.Point(0, y));
                ctrl.Document.SetSelection(index, index);
                ctrl.ScrollToCaret();
            }
        }

        public System.Drawing.Color ForeColor
        {
            get
            {
                return ctrl.ForeColor;
            }
            set
            {
                ctrl.ForeColor = value;
            }
        }

        public System.Drawing.Color BackColor
        {
            get
            {
                return ctrl.BackColor;
            }
            set
            {
                ctrl.BackColor = value;
            }
        }

        public System.Drawing.Color SelectionBackColor { get; set; }


        internal void EndUpdate()
        {
            // 
        }

        internal void BeginUpdate()
        {
            // 
        }

        internal System.Drawing.Point GetPositionFromCharIndex(int p)
        {
            return ctrl.GetPositionFromIndex(p);
        }

        internal System.Drawing.Point PointToScreen(System.Drawing.Point pt)
        {
            return ctrl.PointToScreen(pt);
        }

        public string Rtf
        {
            get
            {
                return ctrl.Document.Text;
            }
            set
            {
                ctrl.Document.Text = value;
            }
        }

        internal void ScrollToCaret()
        {
            ctrl.ScrollToCaret();
        }

        internal int GetFirstCharIndexOfCurrentLine()
        {
            int begin;
            int end;
            ctrl.Document.GetSelection(out begin,out end);
            //int lineIndex;
            //int columnIndex;
            //ctrl.GetLineColumnIndexFromCharIndex(begin, out lineIndex, out columnIndex);

            return ctrl.GetLineHeadIndexFromCharIndex(begin);
        }

        internal void Focus()
        {
            ctrl.Focus();
        }

        public System.Drawing.Color SelectionColor
        {
            get;
            set;
        }

        public bool Focused
        {
            get
            {
                return ctrl.Focused;
            }
        }

        internal void Copy()
        {
            this.ctrl.Copy();
        }

        internal void Paste(System.Windows.Forms.DataFormats.Format fmt)
        {
            this.ctrl.Paste();
        }

        public string SelectedText
        {
            get
            {
                return ctrl.GetSelectedText();
            }
            set
            {
                ctrl.Document.Replace(value);
            }
        }

        internal void Cut()
        {
            ctrl.Cut();
        }

        internal void SelectAll()
        {
            ctrl.SelectAll();
        }

        internal void Clear()
        {
            ctrl.Text = "";
        }

        internal int GetLineFromCharIndex(int p)
        {
            return ctrl.GetLineIndexFromCharIndex(p);
        }

    }
}
