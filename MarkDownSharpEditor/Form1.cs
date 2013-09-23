﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Collections;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Documents;
using MarkDownSharpEditor.Properties;
using mshtml;

namespace MarkDownSharpEditor
{

	public partial class Form1 : Form
	{
		//-----------------------------------
		// WebBrowserコンポーネントプレビューの更新音（クリック音）をOFFにする
		// Click sound off when webBrowser component preview
		const int FEATURE_DISABLE_NAVIGATION_SOUNDS = 21;
		const int SET_FEATURE_ON_PROCESS = 0x00000002;
		[DllImport("urlmon.dll")]
		[PreserveSig]
		[return: MarshalAs(UnmanagedType.Error)]
		static extern int CoInternetSetFeatureEnabled(int FeatureEntry, [MarshalAs(UnmanagedType.U4)] int dwFlags, bool fEnable);

		//-----------------------------------
		//int undoCounter = 0;                                              // Undo buffer counter
		//List<string> UndoBuffer = new List<string>();
		private bool _fSearchStart = false;                                //検索を開始したか ( Start search flag )
		private int _richEditBoxInternalHeight;                            //richTextBox1の内部的な高さ ( internal height of richTextBox1 component )
		private int _WebBrowserInternalHeight;                             //webBrowser1の内部的な高さ（body）( internal height of webBrowser1 component )

		private Point _preFormPos;                                         //フォームサイズ変更前の位置を一時保存 ( Temporary form position before resizing form )
		private Size _preFormSize;                                         //フォームサイズ変更前のサイズを一時保存 ( Temporay form size before resizing form )

		private bool _fNoTitle = true;                                     //無題のまま編集中かどうか ( no name of file flag )
		private string _MarkDownTextFilePath = "";                         //編集中のMDファイルパス ( Editing .MD file path )
		private string _TemporaryHtmlFilePath = "";                        //プレビュー用のテンポラリHTMLファイルパス ( Temporary HTML file path for preview )
		private string _SelectedCssFilePath = "";                          //選択中のCSSファイルパス ( Applying CSS file path )
		private Encoding _EditingFileEncoding = Encoding.UTF8;             //編集中MDファイルの文字エンコーディング ( Character encoding of MD file editing )

		private bool _fConstraintChange = true;	                           //更新状態の抑制 ( Constraint changing flag )
		//private ICollection<MarkdownSyntaxKeyword> _MarkdownSyntaxKeywordAarray;

        private Azuki.AzukeRTF richTextBox1;
        private Azuki.MarkdownHighlighter highlighter;

		//-----------------------------------
		// コンストラクタ ( Constructor )
		//-----------------------------------
		public Form1()
		{
			InitializeComponent();

            // RichTextBox を Azuki に置き換え 
            highlighter = new Azuki.MarkdownHighlighter(this.azukiRichTextBox1.Document);
            this.azukiRichTextBox1.Highlighter = highlighter;
            this.azukiRichTextBox1.ColorScheme = highlighter.MarkDownColorScheme;
            this.azukiRichTextBox1.Document.EolCode = "\n";
            
            richTextBox1 = new Azuki.AzukeRTF(this.azukiRichTextBox1);

            this.azukiRichTextBox1.DrawsEofMark = true;
            this.azukiRichTextBox1.ViewType = Sgry.Azuki.ViewType.WrappedProportional;

			//IME Handler On/Off
			if (MarkDownSharpEditor.AppSettings.Instance.Lang == "ja")
			{
				richTextBox1.IMEWorkaroundEnabled = true;
			}
			else
			{
				richTextBox1.IMEWorkaroundEnabled = false;
			}

            this.azukiRichTextBox1.DragEnter += new System.Windows.Forms.DragEventHandler(this.richTextBox1_DragEnter);
            this.azukiRichTextBox1.DragDrop += new System.Windows.Forms.DragEventHandler(this.richTextBox1_DragDrop);

			richTextBox1.AllowDrop = true;

			//設定を読み込む ( Read options )
			//Program.csで読み込むようにした ( Load in "Program.cs" )
			//MarkDownSharpEditor.AppSettings.Instance.ReadFromXMLFile();

			//WebBrowserClickSoundOFF();
			CoInternetSetFeatureEnabled(FEATURE_DISABLE_NAVIGATION_SOUNDS, SET_FEATURE_ON_PROCESS, true);

            System.Windows.Forms.Application.Idle += Application_Idle;

		}

		//----------------------------------------------------------------------
		// フォームをロード ( Load main form )
		//----------------------------------------------------------------------
		private void Form1_Load(object sender, EventArgs e)
		{

			var obj = MarkDownSharpEditor.AppSettings.Instance;

			//-----------------------------------
			//フォーム位置・サイズ ( Form position & size )
			//-----------------------------------

			this.Location = obj.FormPos;
			this.Size = obj.FormSize;
			this.richTextBox1.Width = obj.richEditWidth;

			//ウィンドウ位置の調整（へんなところに行かないように戻す）
			// Ajust window position ( Set position into Desktop range )
			if (this.Left < 0 || this.Left > Screen.PrimaryScreen.Bounds.Width)
			{
				this.Left = 0;
			}
			if (this.Top < 0 || this.Top > Screen.PrimaryScreen.Bounds.Height)
			{
				this.Top = 0;
			}

			if (obj.FormState == 1)
			{	//最小化 ( Minimize )
				this.WindowState = FormWindowState.Minimized;
			}
			else if (obj.FormState == 2)
			{	//最大化 ( Maximize )
				this.WindowState = FormWindowState.Maximized;
			}

			//メインメニュー表示
			//View main menu
			this.menuViewToolBar.Checked = obj.fViewToolBar;
			this.toolStrip1.Visible = obj.fViewToolBar;
			this.menuViewStatusBar.Checked = obj.fViewStatusBar;
			this.statusStrip1.Visible = obj.fViewStatusBar;
			this.menuViewWidthEvenly.Checked = obj.fSplitBarWidthEvenly;

			//言語 ( Language )
			if (obj.Lang == "ja")
			{
				menuVieｗJapanese.Checked = true;
				menuViewEnglish.Checked = false;
			}
			else
			{
				menuVieｗJapanese.Checked = false;
				menuViewEnglish.Checked = true;
			}

			//ブラウザープレビューまでの間隔
			//Interval time of browser preview
			if (obj.AutoBrowserPreviewInterval > 0)
			{
				timer1.Interval = obj.AutoBrowserPreviewInterval;
			}

			//-----------------------------------
			//RichEditBox font
			FontConverter fc = new FontConverter();
			try { richTextBox1.Font = (Font)fc.ConvertFromString(obj.FontFormat); }
			catch { }
			//RichEditBox font color
			richTextBox1.ForeColor = Color.FromArgb(obj.richEditForeColor);
			//ステータスバーに表示
			//View in statusbar
			toolStripStatusLabelFontInfo.Text =
				richTextBox1.Font.Name + "," + richTextBox1.Font.Size.ToString() + "pt";

			//エディターのシンタックスハイライター設定の反映
			//Syntax highlighter of editor window is enabled 
			//_MarkdownSyntaxKeywordAarray = MarkdownSyntaxKeyword.CreateKeywordList();

			//-----------------------------------
			//選択中のエンコーディングを表示
			//View selected character encoding name
			foreach (EncodingInfo ei in Encoding.GetEncodings())
			{
				if (ei.GetEncoding().IsBrowserDisplay == true)
				{
					if (ei.CodePage == obj.CodePageNumber)
					{
						toolStripStatusLabelHtmlEncoding.Text = ei.DisplayName;
						break;
					}
				}
			}

			//-----------------------------------
			//指定されたCSSファイル名を表示
			//View selected CSS file name
			toolStripStatusLabelCssFileName.Text = Resources.toolTipCssFileName; //"CSSファイル指定なし"; ( No CSS file )

			if (obj.ArrayCssFileList.Count > 0)
			{
				string FilePath = (string)obj.ArrayCssFileList[0];
				if (File.Exists(FilePath) == true)
				{
					toolStripStatusLabelCssFileName.Text = Path.GetFileName(FilePath);
					_SelectedCssFilePath = FilePath;
				}
			}

			//-----------------------------------
			//出力するHTMLエンコーディング表示
			//View HTML charcter encoding name for output
			if (obj.HtmlEncodingOption == 0)
			{
				// 編集中（RichEditBox側）のエンコーディング
				// 基本的にはテキストファイルが読み込まれたときに表示する
				// View encoding name of editor window
				toolStripStatusLabelHtmlEncoding.Text = _EditingFileEncoding.EncodingName;
			}
			else
			{
				//エンコーディングを指定する(&C)
				//Select encoding
				Encoding enc = Encoding.GetEncoding(obj.CodePageNumber);
				toolStripStatusLabelHtmlEncoding.Text = enc.EncodingName;
			}

			//-----------------------------------
			//検索フォーム・オプション
			//Search form options
			chkOptionCase.Checked = obj.fSearchOptionIgnoreCase ? false : true;

		}

		//----------------------------------------------------------------------
		// フォームを表示
		// View Main form
		//----------------------------------------------------------------------
		private void Form1_Shown(object sender, EventArgs e)
		{
            webBrowser1.Navigate("about:blank");

			string DirPath = MarkDownSharpEditor.AppSettings.GetAppDataLocalPath();

			ArrayList FileArray = new ArrayList();

			//TODO: 「新しいウィンドウで開く」="/new"などの引数も含まれるので、
			//       その辺りの処理も将来的に入れる。

			//コマンドラインでファイルが投げ込まれてきている
			//Launch with arguments
			string[] cmds = System.Environment.GetCommandLineArgs();
			for (int i = 1; i < cmds.Count(); i++)
			{
				if (File.Exists(cmds[i]) == true)
				{
					FileArray.Add(cmds[i]);
				}
			}

			try
			{
				if (FileArray.Count > 1)
				{	//"問い合わせ"
					//"複数のファイルが読み込まれました。\n現在の設定内容でHTMLファイルへの一括変換を行いますか？
					//「いいえ」を選択するとそのまますべてのファイル開きます。"
					//"Question"
					//"More than one were read.\nDo you wish to convert all files to HTML files on this options?\n
					// if you select 'No', all files will be opend without converting."
					DialogResult ret = MessageBox.Show(Resources.MsgConvertAllFilesToHTML,
					Resources.DialogTitleQuestion, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);

					if (ret == DialogResult.Yes)
					{ //一括でHTMLファイル出力
						//Output HTML files in batch process
						BatchOutputToHtmlFiles((String[])FileArray.ToArray(typeof(string)));
						return;

					}
					else if (ret == DialogResult.Cancel)
					{	//キャンセル
						//Cancel
						return;
					}
					else
					{	//「いいえ」
						// "NO" button
						bool fOpen = false;
						foreach (string FilePath in FileArray)
						{	//最初のファイルだけ、このウィンドウだけ開く
							//First file open in this window 
							if (fOpen == false)
							{
								richTextBox1.Modified = false;
								OpenFile(FilePath);
								fOpen = true;
							}
							else
							{	//他の複数ファイルは順次新しいウィンドウで開く
								//Other files open in new windows
								System.Diagnostics.Process.Start(
									Application.ExecutablePath, string.Format("{0}", FilePath));
							}
						}
					}
				}
				else if (FileArray.Count == 1)
				{
					richTextBox1.Modified = false;
					OpenFile((string)FileArray[0]);
				}
				else
				{ //前に編集していたファイルがあればそれを開く
					//Open it if there is editing file before
					if (MarkDownSharpEditor.AppSettings.Instance.fOpenEditFileBefore == true)
					{
						if (MarkDownSharpEditor.AppSettings.Instance.ArrayHistoryEditedFiles.Count > 0)
						{
							AppHistory EditedFilePath = new AppHistory();
							EditedFilePath = (AppHistory)MarkDownSharpEditor.AppSettings.Instance.ArrayHistoryEditedFiles[0];
							if (File.Exists(EditedFilePath.md) == true)
							{
								_TemporaryHtmlFilePath = "";
								richTextBox1.Modified = false;
								OpenFile(EditedFilePath.md);
								return;
							}
						}
					}
					if (_MarkDownTextFilePath == "")
					{	//無ければ「無題」ファイル
						// "No title" if no file exists
						richTextBox1.Modified = false;
						OpenFile("");
					}
				}
			}
			finally
			{
				_fConstraintChange = false;
				//SyntaxHighlighter
				if (backgroundWorker2.IsBusy == false)
				{
					//backgroundWorker2.WorkerReportsProgress = true;
					backgroundWorker2.RunWorkerAsync(richTextBox1.Text);
				}
				richTextBox1.Modified = false;
				//フォームタイトル更新 / Refresh form caption
				FormTextChange();
			}
		}

		//----------------------------------------------------------------------
		// フォームタイトルの表示（更新）
		// Refresh form caption
		//----------------------------------------------------------------------
		private void FormTextChange()
		{
			string FileName;
			if (_fNoTitle == true)
			{
				FileName = Resources.NoFileName; //"(無題)" "(No title)"
			}
			else
			{
				//FileName = System.IO.Path.GetFileName(_MarkDownTextFilePath);
				FileName = _MarkDownTextFilePath;
			}

			if (richTextBox1.Modified == true)
			{
				FileName = FileName + Resources.FlagChanged; //"(更新)"; "(Changed)"
			}
			this.Text = FileName + " - " + Application.ProductName;
		}

		//----------------------------------------------------------------------
		// 見出しリストメニューの表示
		//----------------------------------------------------------------------
		private void mnuShowHeaderListMenu_Click(object sender, EventArgs e)
		{
			ShowHeaderListContextMenu();
		}

		//----------------------------------------------------------------------
		// 見出しリストメニューの表示
		//----------------------------------------------------------------------

		void ShowHeaderListContextMenu()
		{

			int retCode;

			//Markdown
			object[][] mkObject = {
				new object[2] { 1, @"^#[^#]*?$" },     //見出し１ (headline1)
				new object[2] { 1, @"^.*\n=+$" },      //見出し１ (headline1)
				new object[2] { 2, @"^##[^#]*?$" },    //見出し２ (headline2)
				new object[2] { 2, @"^.+\n-+$" },      //見出し２ (headline2)
				new object[2] { 3, @"^###[^#]*?$" },   //見出し３ (headline3)
				new object[2] { 4, @"^####[^#]*?$" },  //見出し４ (headline4)
				new object[2] { 5, @"^#####[^#]*?$" }, //見出し５ (headline5)
				new object[2] { 6, @"^######[^#]*?$"}  //見出し６ (headline6)
			};

			//コンテキストメニュー項目を初期化（クリア）
			//Clear context menus
			contextMenu2.Items.Clear();

			bool fModify = richTextBox1.Modified;
			_fConstraintChange = true;

			//現在のカーソル位置
			//Current cursor position
			int selectStart = this.richTextBox1.SelectionStart;
			int selectEnd = richTextBox1.SelectionLength;
			Point CurrentOffset = richTextBox1.AutoScrollOffset;

			//現在のスクロール位置
			//Current scroll position
			int CurrentScrollPos = richTextBox1.VerticalPosition;
			//描画停止
			//Stop to update
			richTextBox1.BeginUpdate();

			for (int i = 0; i < mkObject.Length; i++)
			{

				Regex r = new Regex((string)mkObject[i][1], RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);
				MatchCollection col = r.Matches(richTextBox1.Text, 0);

				if (col.Count > 0)
				{
					foreach (Match m in col)
					{
						int IndexNum = m.Groups[0].Index;
						string title = new String('　', (int)mkObject[i][0]) + richTextBox1.Text.Substring(m.Groups[0].Index, m.Groups[0].Length);

						if ((retCode = title.LastIndexOf("\n")) > -1)
						{
							title = title.Substring(0, title.LastIndexOf("\n"));
						}

						//コンテキストメニューに登録
						//Regist item to context menus
						bool fAdd = false;
						ToolStripMenuItem item = new ToolStripMenuItem();
						for (int c = 0; c < contextMenu2.Items.Count; c++)
						{
							//登録されている項目よりも前の項目のときは挿入する
							//Insert item to registed items
							if (IndexNum < (int)contextMenu2.Items[c].Tag)
							{
								item.Text = title;
								item.Tag = IndexNum;
								contextMenu2.Items.Insert(c, item);
								fAdd = true;
								break;
							}
						}
						if (fAdd == false)
						{
							item.Text = title;
							item.Tag = IndexNum;
							contextMenu2.Items.Add(item);
						}
					}
				}
			}

			//カーソル位置を戻す
			//Restore cursor position
			richTextBox1.Select(selectStart, selectEnd);
			richTextBox1.AutoScrollOffset = CurrentOffset;

			//描画再開
			//Resume to update
			richTextBox1.EndUpdate();
			richTextBox1.Modified = fModify;

			_fConstraintChange = false;

			//richTextBox1のキャレット位置
			//Curret position in richTextBox1
			Point pt = richTextBox1.GetPositionFromCharIndex(richTextBox1.SelectionStart);
			//スクリーン座標に変換
			//Convert the position to screen position
			pt = richTextBox1.PointToScreen(pt);
			//コンテキストメニューを表示
			//View context menus
			contextMenu2.Show(pt);

		}

		//----------------------------------------------------------------------
		// 見出しメニュークリックイベント
		// Click event in context menus
		//----------------------------------------------------------------------
		private void contextMenu2_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
		{

			if ((int)e.ClickedItem.Tag > 0)
			{
				richTextBox1.SelectionStart = (int)e.ClickedItem.Tag;
				richTextBox1.ScrollToCaret();
			}

		}

		//----------------------------------------------------------------------
		// HACK: RichTextBox内容変更 [ シンタックス・ハイライター表示）]
		//       Apply richTextBox1 to syntax highlight with changing
		//----------------------------------------------------------------------
		private void richTextBox1_TextChanged(object sender, EventArgs e)
		{

			if (_fConstraintChange == true)
			{
				return;
			}

			if (backgroundWorker2.IsBusy == false)
			{
				//バックグラウンドワーカーへパースを投げる / SyntaxHightlighter on BackgroundWorker
				backgroundWorker2.RunWorkerAsync(richTextBox1.Text);
			}

			//-----------------------------------
			//Formキャプション変更 / Change form caption
			FormTextChange();

			//-----------------------------------
			//ブラウザプレビュー制御 / Preview webBrowser component
			if (MarkDownSharpEditor.AppSettings.Instance.fAutoBrowserPreview == true)
			{
				timer1.Enabled = true;
			}

		}

		//----------------------------------------------------------------------
		// RichTextBox Key Press
		//----------------------------------------------------------------------
		private void richTextBox1_KeyPress(object sender, KeyPressEventArgs e)
		{
			//-----------------------------------
			// Add undo buffer
			//-----------------------------------
#if false

			//現在のUndoCounterから先を削除
			//Remove first item of undo counter
			if (undoCounter < UndoBuffer.Count)
			{
				UndoBuffer.RemoveRange(undoCounter, UndoBuffer.Count - undoCounter);
			}

			UndoBuffer.Add(richTextBox1.Rtf);
			undoCounter = UndoBuffer.Count;

			//EnterやEscapeキーでビープ音が鳴らないようにする
			//Stop to beep in Enter & Escape key
			if (e.KeyChar == (char)Keys.Enter || e.KeyChar == (char)Keys.Escape)
			{
				e.Handled = true;
			}
#endif
            timer2.Enabled = true;

		}
		
		//----------------------------------------------------------------------
		// RichTextBox VScroll event
		//----------------------------------------------------------------------
		private void richTextBox1_VScroll(object sender, EventArgs e)
		{
			if (_fConstraintChange == false)
			{
				WebBrowserMoveCursor();
			}
		}

		//----------------------------------------------------------------------
		// RichTextBox Enter event
		//----------------------------------------------------------------------
		private void richTextBox1_Enter(object sender, EventArgs e)
		{
			//ブラウザプレビュー制御 / Preview webBrowser component
			if (MarkDownSharpEditor.AppSettings.Instance.fAutoBrowserPreview == true)
			{
				timer1.Enabled = true;
			}
		}

		//----------------------------------------------------------------------
		// RichTextBox Mouse click
		//----------------------------------------------------------------------
		private void richTextBox1_MouseClick(object sender, MouseEventArgs e)
		{

			//timer1.Enabled = true;

		}

		//----------------------------------------------------------------------
		// Form Closing event
		//----------------------------------------------------------------------
		private void Form1_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (richTextBox1.Modified == true)
			{
				//"問い合わせ"
				//"編集中のファイルがあります。保存してから終了しますか？"
				//"Question"
				//"This file being edited. Do you wish to save before exiting?"
				DialogResult ret = MessageBox.Show(Resources.MsgSaveFileToEnd,
				Resources.DialogTitleQuestion, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

				if (ret == DialogResult.Yes)
				{
					if (SaveToEditingFile() == true)
					{
						_fNoTitle = false;
					}
					else
					{
						//キャンセルで抜けてきた
						//user cancel
						e.Cancel = true;
						return;
					}
				}
				else if (ret == DialogResult.Cancel)
				{
					e.Cancel = true;
					return;
				}
			}

			_fConstraintChange = true;

			//無題ファイルのまま編集しているのなら削除
			//Delete file if the file is no title
			if (_fNoTitle == true)
			{
				if (File.Exists(_MarkDownTextFilePath) == true)
				{
					try
					{
						File.Delete(_MarkDownTextFilePath);
					}
					catch
					{
					}
				}
			}

			//データバージョン
			//Data version
			System.Reflection.Assembly asmbly = System.Reflection.Assembly.GetExecutingAssembly();
			System.Version ver = asmbly.GetName().Version;
			MarkDownSharpEditor.AppSettings.Instance.Version = ver.Major * 1000 + ver.Minor * 100 + ver.Build * 10 + ver.Revision;

			//フォーム位置・サイズ ( Form position & size )
			if (this.WindowState == FormWindowState.Minimized)
			{	//最小化 ( Minimize )
				MarkDownSharpEditor.AppSettings.Instance.FormState = 1;
				//一時記憶していた位置・サイズを保存 ( Save temporary position & size value )
				MarkDownSharpEditor.AppSettings.Instance.FormPos = new Point(_preFormPos.X, _preFormPos.Y);
				MarkDownSharpEditor.AppSettings.Instance.FormSize = new Size(_preFormSize.Width, _preFormSize.Height);
			}
			else if (this.WindowState == FormWindowState.Maximized)
			{	//最大化 ( Maximize )
				MarkDownSharpEditor.AppSettings.Instance.FormState = 2;
				//一時記憶していた位置・サイズを保存 ( Save temporary position & size value )
				MarkDownSharpEditor.AppSettings.Instance.FormPos = new Point(_preFormPos.X, _preFormPos.Y);
				MarkDownSharpEditor.AppSettings.Instance.FormSize = new Size(_preFormSize.Width, _preFormSize.Height);
			}
			else
			{	//通常 ( Normal window )
				MarkDownSharpEditor.AppSettings.Instance.FormState = 0;
				MarkDownSharpEditor.AppSettings.Instance.FormPos = new Point(this.Left, this.Top);
				MarkDownSharpEditor.AppSettings.Instance.FormSize = new Size(this.Width, this.Height);
			}

			MarkDownSharpEditor.AppSettings.Instance.richEditWidth = this.richTextBox1.Width;
			FontConverter fc = new FontConverter();
			MarkDownSharpEditor.AppSettings.Instance.FontFormat = fc.ConvertToString(richTextBox1.Font);
			MarkDownSharpEditor.AppSettings.Instance.richEditForeColor = richTextBox1.ForeColor.ToArgb();

			//表示オプションなど
			//Save view options etc
			MarkDownSharpEditor.AppSettings.Instance.fViewToolBar = this.menuViewToolBar.Checked;
			MarkDownSharpEditor.AppSettings.Instance.fViewStatusBar = this.menuViewStatusBar.Checked;
			MarkDownSharpEditor.AppSettings.Instance.fSplitBarWidthEvenly = this.menuViewWidthEvenly.Checked;

			//検索オプション
			//Save search options
			MarkDownSharpEditor.AppSettings.Instance.fSearchOptionIgnoreCase = chkOptionCase.Checked ? false : true;

			if (File.Exists(_MarkDownTextFilePath) == true)
			{
				//編集中のファイルパス
				//Save editing file path
				foreach (AppHistory data in MarkDownSharpEditor.AppSettings.Instance.ArrayHistoryEditedFiles)
				{
					if (data.md == _MarkDownTextFilePath)
					{ //いったん削除して ( delete once ... )
						MarkDownSharpEditor.AppSettings.Instance.ArrayHistoryEditedFiles.Remove(data);
						break;
					}
				}
				AppHistory HistroyData = new AppHistory();
				HistroyData.md = _MarkDownTextFilePath;
				HistroyData.css = _SelectedCssFilePath;
				MarkDownSharpEditor.AppSettings.Instance.ArrayHistoryEditedFiles.Insert(0, HistroyData);	//先頭に挿入 ( Insert at the top )
			}

			//設定の保存
			//Save settings
			MarkDownSharpEditor.AppSettings.Instance.SaveToXMLFile();
			//MarkDownSharpEditor.AppSettings.Instance.SaveToJsonFile();

			timer1.Enabled = false;

			//webBrowser1.Navigate("about:blank");
			//クリック音対策
			//Constraint click sounds
			if (webBrowser1.Document != null)
			{
				webBrowser1.Document.OpenNew(true);
				webBrowser1.Document.Write("");
			}
		}

		//-----------------------------------
		// Form closed event
		//-----------------------------------
		private void Form1_FormClosed(object sender, FormClosedEventArgs e)
		{
			//テンポラリファイルの削除
			//Delete temporary files
			Delete_TemporaryHtmlFilePath();
		}

		//-----------------------------------
		// Form resize begin event
		//-----------------------------------
		private void Form1_ResizeBegin(object sender, EventArgs e)
		{
			//リサイズ前の位置・サイズを一時記憶
			_preFormPos.X = this.Left;
			_preFormPos.Y = this.Top;
			_preFormSize.Width = this.Width;
			_preFormSize.Height = this.Height;
		}

		//-----------------------------------
		// Form resize end event
		//-----------------------------------
		private void Form1_ResizeEnd(object sender, EventArgs e)
		{
			//ソースウィンドウとビューウィンドウを均等にするか
			//Equalize width of source window and view window
			if (menuViewWidthEvenly.Checked == true)
			{
				this.richTextBox1.Width =
					(splitContainer1.Width - splitContainer1.SplitterWidth) / 2;
			}
		}

		//-----------------------------------
		// richTextBox1 DragEnter event
		//-----------------------------------
		private void richTextBox1_DragEnter(object sender, System.Windows.Forms.DragEventArgs e)
        {
			if (e.Data.GetDataPresent(DataFormats.FileDrop) == true)
			{
				e.Effect = DragDropEffects.Copy;
			}
			else
			{
				e.Effect = DragDropEffects.None;
			}
        }

		//-----------------------------------
		// richTextBox1 Drag and Drop event
		//-----------------------------------
		private void richTextBox1_DragDrop(object sender, System.Windows.Forms.DragEventArgs e)
        {
			string[] FileArray = (string[])e.Data.GetData(DataFormats.FileDrop, false);
			if (FileArray.Length > 1)
			{
				if (FileArray.Length > 0)
				{
					//"問い合わせ"
					//"複数のファイルが読み込まれました。\n現在の設定内容でHTMLファイルへの一括変換を行いますか？
					//「いいえ」を選択するとそのまますべてのファイル開きます。"
					//"Question"
					//"More than one were read.\nDo you wish to convert all files to HTML files on this options?\n
					// if you select 'No', all files will be opend without converting."
					DialogResult ret = MessageBox.Show("MsgConvertAllFilesToHTML",
					Resources.DialogTitleQuestion, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);

					if (ret == DialogResult.Yes)
					{
						//一括でHTMLファイル出力
						//Output HTML files in batch process
						BatchOutputToHtmlFiles(FileArray);
						return;

					}
					else if (ret == DialogResult.Cancel)
					{
						//キャンセル
						//Cancel
						return;
					}
					else
					{	//「いいえ」
						// "No" button
						bool fOpen = false;
						foreach (string FilePath in FileArray)
						{
							//最初のファイルだけ、このウィンドウだけ開く
							//First file open in this window 
							if (fOpen == false)
							{
								OpenFile(FilePath);
								fOpen = true;
							}
							else
							{
								//他の複数ファイルは順次新しいウィンドウで開く
								//Other files open in new windows
								System.Diagnostics.Process.Start(
									Application.ExecutablePath, string.Format("{0}", FilePath));
							}
						}
					}
				}
				else
				{
					//前に編集していたファイルがあればそれを開く
					//Open it if there is editing file before
					if (MarkDownSharpEditor.AppSettings.Instance.fOpenEditFileBefore == true)
					{
						if (MarkDownSharpEditor.AppSettings.Instance.ArrayHistoryEditedFiles.Count > 0)
						{
							AppHistory EditedFilePath = new AppHistory();
							EditedFilePath = (AppHistory)MarkDownSharpEditor.AppSettings.Instance.ArrayHistoryEditedFiles[0];

							if (File.Exists(EditedFilePath.md) == true)
							{
								OpenFile(EditedFilePath.md);
							}
						}
					}
				}
				_fConstraintChange = false;
				//フォームタイトル更新
				//Refresh form caption
				FormTextChange();

				ArrayList ArrayFileList = new ArrayList();
				foreach (string FilePath in FileArray)
				{
					ArrayFileList.Add(FilePath);
				}

				BatchOutputToHtmlFiles((String[])ArrayFileList.ToArray(typeof(string)));

        	}
			else if (FileArray.Count() == 1)
			{
				//ファイルが一個の場合は編集中のウィンドウで開く
				//Open it in this window if file is one 
				OpenFile(FileArray[0]);
			}
		}

		//----------------------------------------------------------------------
		// OpenFile [ .mdファイルを開く ]
		//----------------------------------------------------------------------
		private bool OpenFile(string FilePath, bool fOpenDialog = false)
		{
			//引数 FilePath = "" の場合は「無題」編集で開始する
			// Argument "FilePath" = "" => Start editing in 'no title'

			if (FilePath == "")
			{
				_fNoTitle = true;  // No title flag
			}
			else
			{
				_fNoTitle = false;
			}

			//-----------------------------------
			//編集中のファイルがある
			if (richTextBox1.Modified == true)
			{
				//"問い合わせ"
				//"編集中のファイルがあります。保存してから終了しますか？"
				//"Question"
				//"This file being edited. Do you wish to save before exiting?"
				DialogResult ret = MessageBox.Show(Resources.MsgSaveFileToEnd,
				Resources.DialogTitleQuestion, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
				if (ret == DialogResult.Yes)
				{
					if (SaveToEditingFile() == false)
					{
						//キャンセルで抜けてきた
						//Cancel
						return (false);
					}
				}
				else if (ret == DialogResult.Cancel)
				{
					return (false);
				}

				//編集履歴に残す
				//Save file path to editing history
				foreach (AppHistory data in MarkDownSharpEditor.AppSettings.Instance.ArrayHistoryEditedFiles)
				{
					if (data.md == _MarkDownTextFilePath)
					{   //いったん削除して ( delete once ... )
						MarkDownSharpEditor.AppSettings.Instance.ArrayHistoryEditedFiles.Remove(data);
						break;
					}
				}
				AppHistory HistroyData = new AppHistory();
				HistroyData.md = _MarkDownTextFilePath;
				HistroyData.css = _SelectedCssFilePath;
				MarkDownSharpEditor.AppSettings.Instance.ArrayHistoryEditedFiles.Insert(0, HistroyData);	//先頭に挿入 ( Insert at the top )
			}

			//-----------------------------------
			//オープンダイアログ表示
			//View open file dialog
			if (fOpenDialog == true)
			{
				if (File.Exists(_MarkDownTextFilePath) == true)
				{	//編集中のファイルがあればそのディレクトリを初期フォルダーに
					//Set parent directory of editing file to initial folder 
					openFileDialog1.InitialDirectory = Path.GetDirectoryName(_MarkDownTextFilePath);
					//テンポラリファイルがあればここで削除
					//Delete it if temporary file exists
					Delete_TemporaryHtmlFilePath();
				}
				openFileDialog1.FileName = "";
				if (openFileDialog1.ShowDialog() == DialogResult.OK)
				{
					FilePath = openFileDialog1.FileName;
                    _fNoTitle = false;
				}
				else
				{
					return (false);
				}
			}

			//編集中のファイルパスとする
			//Set this file to 'editing file' path
			_MarkDownTextFilePath = FilePath;

			//-----------------------------------
			//文字コードを調査するためにテキストファイルを開く
			//Detect encoding
			if (_fNoTitle == false)
			{
				byte[] bs;
				using (FileStream fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read))
				{
					bs = new byte[fs.Length];
					fs.Read(bs, 0, bs.Length);
				}
				//文字コードを取得する
				//Get charcter encoding
				_EditingFileEncoding = GetCode(bs);
			}
			else
			{
				//「無題」はデフォルトのエンコーディング
				// Set this file to default encoding in 'No title'
				_EditingFileEncoding = Encoding.UTF8;
			}

			//ステータスバーに表示
			//View in statusbar
			toolStripStatusLabelTextEncoding.Text = _EditingFileEncoding.EncodingName;
			//編集中のエンコーディングを使用する(&D)か
			//Use encoding of editing file
			if (MarkDownSharpEditor.AppSettings.Instance.HtmlEncodingOption == 0)
			{
				toolStripStatusLabelHtmlEncoding.Text = _EditingFileEncoding.EncodingName;
			}

			//-----------------------------------
			//ペアとなるCSSファイルがあるか探索してあれば適用する
			//Apply that the pair CSS file to this file exists  
			foreach (AppHistory data in MarkDownSharpEditor.AppSettings.Instance.ArrayHistoryEditedFiles)
			{
				if (data.md == _MarkDownTextFilePath)
				{
					if (File.Exists(data.css) == true)
					{
						_SelectedCssFilePath = data.css;
						break;
					}
				}
			}

			//選択中のCSSファイル名をステータスバーに表示
			//View selected CSS file name to stausbar
			if (File.Exists(_SelectedCssFilePath) == true)
			{
				toolStripStatusLabelCssFileName.Text = Path.GetFileName(_SelectedCssFilePath);
			}
			else
			{
				toolStripStatusLabelCssFileName.Text = Resources.toolTipCssFileName; //"CSSファイル指定なし"; ( not selected CSS file)
			}

			_fConstraintChange = true;
			richTextBox1.Clear();

			//RichEditBoxの「フォント」設定
			// richTextBox1 font name setting
			var obj = MarkDownSharpEditor.AppSettings.Instance;
			FontConverter fc = new FontConverter();
			try { richTextBox1.Font = (Font)fc.ConvertFromString(obj.FontFormat); }
			catch { }
			//RichEditBoxの「フォントカラー」設定
			// richTextBox1 font color setting
			richTextBox1.ForeColor = Color.FromArgb(obj.richEditForeColor);
			//View them in statusbar
			toolStripStatusLabelFontInfo.Text = richTextBox1.Font.Name + "," + richTextBox1.Font.Size.ToString() + "pt";

			//-----------------------------------
			//テキストファイルの読み込み
			//Read text file
			if (File.Exists(FilePath) == true)
			{
				richTextBox1.Text = File.ReadAllText(FilePath, _EditingFileEncoding);
			}
			richTextBox1.BeginUpdate();
			richTextBox1.SelectionStart = richTextBox1.Text.Length;
			richTextBox1.ScrollToCaret();
			//richTextBox1の全高さを求める
			//Get height of richTextBox1 ( for webBrowser sync )
			_richEditBoxInternalHeight = richTextBox1.VerticalPosition;
			//カーソル位置を戻す
			//Restore cursor position
			richTextBox1.SelectionStart = 0;
			richTextBox1.EndUpdate();

			//変更フラグOFF
			richTextBox1.Modified = false;

			//Undoバッファに追加
			//Add all text to undo buffer
			//UndoBuffer.Clear();
			//UndoBuffer.Add(richTextBox1.Rtf);
			//undoCounter = UndoBuffer.Count;
            this.azukiRichTextBox1.Document.ClearHistory();

			//プレビュー更新
			PreviewToBrowser();

			_fConstraintChange = false;
			FormTextChange();

			return (true);

		}

		//----------------------------------------------------------------------
		// 表示するHTMLのテンポラリファイルパスを取得する
		// Get temporary HTML file path
		//----------------------------------------------------------------------
		private string Get_TemporaryHtmlFilePath(string FilePath)
		{
			string DirPath = Path.GetDirectoryName(FilePath);
			string FileName = Path.GetFileNameWithoutExtension(FilePath);

			MD5 md5Hash = MD5.Create();
			byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(FileName));
			StringBuilder sBuilder = new StringBuilder();
			for (int i = 0; i < data.Length; i++)
			{
				sBuilder.Append(data[i].ToString("x2"));
			}
			return (Path.Combine(DirPath, FileName + "_" + sBuilder.ToString() + ".html"));
		}

		//----------------------------------------------------------------------
		// 表示しているHTMLのテンポラリファイルを削除する
		// Delete temporary HTML file
		//----------------------------------------------------------------------
		private void Delete_TemporaryHtmlFilePath()
		{
			string TempHtmlFilePath;

			if (_MarkDownTextFilePath == "")
			{
				return;
			}

			TempHtmlFilePath = Get_TemporaryHtmlFilePath(_MarkDownTextFilePath);
			//見つかったときだけ削除
			//Delete it if the file exists
			if (File.Exists(TempHtmlFilePath) == true)
			{
				try
				{
					File.Delete(TempHtmlFilePath);
				}
				catch
				{
					//"エラー"
					//"テンポラリファイルの削除に失敗しました。編集中の場所に残った可能性があります。
					//"このファイルは手動で削除していただいても問題ありません。
					//"Error"
					//"Error during deleting temporary file!\na temporary file may be left in the folder the file is edited.\n"
					// This file is not a problem even if you delete it manually.
					MessageBox.Show(Resources.MsgErrorDeleteTemporaryFile + TempHtmlFilePath,
						Resources.DialogTitleError, MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
			}
		}

		//----------------------------------------------------------------------
		// ブラウザープレビューの間隔を調整
		// Ajust browser preview interval 
		//----------------------------------------------------------------------
		private void timer1_Tick(object sender, EventArgs e)
		{
			PreviewToBrowser();
			timer1.Enabled = false;
		}

		private void timer2_Tick(object sender, EventArgs e)
		{
			timer2.Enabled = false;
		}

		//----------------------------------------------------------------------
		// HACK: PreviewToBrowser [ ブラウザプレビュー ]
		//----------------------------------------------------------------------
		private void PreviewToBrowser()
		{
			//更新抑制中のときはプレビューしない
			//Do not preview in constraint to change
			if (_fConstraintChange == true)
			{
				return;
			}
			if (backgroundWorker1.IsBusy == true)
			{
				return;
			}

			string ResultText = "";
			backgroundWorker1.WorkerReportsProgress = true;
			//編集箇所にマーカーを挿入する
			//Insert marker in editing
			if (richTextBox1.SelectionStart > 0)
			{
				//int NextLineNum = richTextBox1.GetLineFromCharIndex(richTextBox1.SelectionStart) + 1;
				int ParagraphStart = richTextBox1.GetFirstCharIndexOfCurrentLine();
				//if (ParagraphStart == 0)
				//{
				//	ParagraphStart = 1;
				//}
				ResultText =
					richTextBox1.Text.Substring(0, ParagraphStart) + "<!-- edit -->" +
					richTextBox1.Text.Substring(ParagraphStart);
			}
			else
			{
				ResultText = richTextBox1.Text;
			}
			backgroundWorker1.RunWorkerAsync(ResultText);

		}

		//----------------------------------------------------------------------
		// BackgroundWorker ProgressChanged
		//----------------------------------------------------------------------
		private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			//ProgressBar1.Value = e.ProgressPercentage;
			//Label1.Text = e.ProgressPercentage.ToString();
		}

		//----------------------------------------------------------------------
		// BackgroundWorker browser preview
		//----------------------------------------------------------------------
		private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
		{
			string ResultText = (string)e.Argument;
			string MkResultText = "";

			string BackgroundColorString;
			string EncodingName;

			//編集中のファイル名
			//Editing file name
			string FileName = (_MarkDownTextFilePath == "" ? "" : Path.GetFileName(_MarkDownTextFilePath));
			//DOCTYPE
			HtmlHeader htmlHeader = new HtmlHeader();
			string DocType = htmlHeader.GetHtmlHeader(MarkDownSharpEditor.AppSettings.Instance.HtmlDocType);

			//マーキングの色づけ
			//Marker's color
			if (MarkDownSharpEditor.AppSettings.Instance.fHtmlHighLightColor == true)
			{
				Color ColorBackground = Color.FromArgb(MarkDownSharpEditor.AppSettings.Instance.HtmlHighLightColor);
				BackgroundColorString = ColorTranslator.ToHtml(ColorBackground);
			}
			else
			{
				BackgroundColorString = "none";
			}

			//指定のエンコーディング
			//Codepage
			int CodePageNum = MarkDownSharpEditor.AppSettings.Instance.CodePageNumber;
			try
			{
				Encoding enc = Encoding.GetEncoding(CodePageNum);
				//ブラウザ表示に対応したエンコーディングか
				//Is the encoding supported browser?
				if (enc.IsBrowserDisplay == true)
				{
					EncodingName = enc.WebName;
				}
				else
				{
					EncodingName = "utf-8";
				}
			}
			catch
			{
				//エンコーディングの取得に失敗した場合
				//Default encoding if failing to get encoding
				EncodingName = "utf-8";
			}
			//Header
			string header = string.Format(
@"{0}
<html>
<head>
<meta http-equiv='Content-Type' content='text/html; charset={1}' />
<link rel='stylesheet' href='{2}' type='text/css' />
<style type='text/css'>
	 ._mk {{background-color:{3}}}
</style>
<title>{4}</title>
</head>
<body>
",
			DocType,               //DOCTYPE
			EncodingName,          //エンコーディング ( encoding )
			_SelectedCssFilePath,   //適用中のCSSファイル ( Selected CSS file )
			BackgroundColorString, //編集箇所の背景色 ( background color in Editing )
			FileName);             //タイトル（＝ファイル名） ( title = file name )

			//Footer
			string footer = "</body>\n</html>";

			//-----------------------------------
			//Markdown parse ( default )
			//Markdown mkdwn = new Markdown();
			//-----------------------------------

			//-----------------------------------
			// MarkdownDeep
			// Create an instance of Markdown
			//-----------------------------------
			var mkdwn = new MarkdownDeep.Markdown();
            mkdwn.RenderPos = true; // data-pos を描画する
			// Set options
			mkdwn.ExtraMode = MarkDownSharpEditor.AppSettings.Instance.fMarkdownExtraMode;
			mkdwn.SafeMode = false;
			//-----------------------------------

			ResultText = mkdwn.Transform(ResultText);
			//表示するHTMLデータを作成
			//Creat HTML data
			ResultText = header + ResultText + footer;

			//パースされた内容から編集行を探す
			//Search editing line in parsed data
			string OneLine;
			StringReader sr = new StringReader(ResultText);
			StringWriter sw = new StringWriter();
			while ((OneLine = sr.ReadLine()) != null)
			{
				if (OneLine.IndexOf("<!-- edit -->") >= 0)
				{
					MkResultText += ("<div class='_mk'>" + OneLine + "</div>\n");
				}
				else
				{
					MkResultText += (OneLine + "\n");
				}
			}

			//エンコーディングしつつbyte値に変換する（richEditBoxは基本的にutf-8 = 65001）
			//Encode and convert it to 'byte' value ( richEditBox default encoding is utf-8 = 65001 )
			byte[] bytesData = Encoding.GetEncoding(CodePageNum).GetBytes(MkResultText);

			//-----------------------------------
			// Write to temporay file
			if (_fNoTitle == false)
			{
				//テンポラリファイルパスを取得する
				//Get temporary file path
				if (_TemporaryHtmlFilePath == "")
				{
					_TemporaryHtmlFilePath = Get_TemporaryHtmlFilePath(_MarkDownTextFilePath);
				}
				//他のプロセスからのテンポラリファイルの参照と削除を許可して開く（でないと飛ぶ）
				//Open temporary file to allow references from other processes
				using (FileStream fs = new FileStream(
					_TemporaryHtmlFilePath,
					FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read | FileShare.Delete))
				{
					fs.Write(bytesData, 0, bytesData.Length);
					e.Result = _TemporaryHtmlFilePath;
				}
			}
			//-----------------------------------
			// Navigate and view in browser
			else
			{
				//Write data as it is, if the editing data is no title  
				ResultText = Encoding.GetEncoding(CodePageNum).GetString(bytesData);
				e.Result = ResultText;
			}

		}

		private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			if (e.Error != null)
			{
				//Error!
			}
			else
			{
				if ((string)e.Result != "")
				{
					//-----------------------------------
					//スクロールバーの位置を退避しておく
					//Memorize scroll positions
					HtmlDocument doc = webBrowser1.Document;
					Point scrollpos = new Point(0, 0);
					if (doc == null)
					{
						webBrowser1.Navigate("about:blank");
					}
					else
					{
						IHTMLDocument3 doc3 = (IHTMLDocument3)webBrowser1.Document.DomDocument;
						IHTMLElement2 elm = (IHTMLElement2)doc3.documentElement;
						scrollpos = new Point(elm.scrollLeft, elm.scrollTop);
					}
					//-----------------------------------
                    System.Threading.Tasks.Task waitTask;
					if (_fNoTitle == false)
					{
						//ナビゲート
						//Browser navigate
						//webBrowser1.Navigate(@"file://" + (string)e.Result);
                        waitTask = WebBrowserNavigate(@"file://" + (string)e.Result);
						richTextBox1.Focus();
						toolStripButtonBrowserPreview.Enabled = true;
					}
					else
					{
						webBrowser1.Document.OpenNew(true);
						//webBrowser1.Document.Write((string)e.Result);
                        waitTask = WebBrowserDocumentWrite((string)e.Result);
						//ツールバーの「関連付けられたブラウザーを起動」を無効に
						//"Associated web browser" in toolbar is invalid
						toolStripButtonBrowserPreview.Enabled = false;
					}
					//-----------------------------------
					//スクロールバーの位置を復帰する
					//Restore scroll bar position
					if (doc != null)
					{
#if false
						while (webBrowser1.ReadyState != WebBrowserReadyState.Complete)
						{
							Application.DoEvents();
						}
						doc.Window.ScrollTo(scrollpos);
#endif
                        waitTask.ContinueWith((arg1) =>
                        {
                            this.BeginInvoke(new Action(() =>
                            {
                            	ScrollSyncToText();

                            	this.webBrowser1.Document.Body.AttachEventHandler("onclick", OnClickEventHandler);
                                this.webBrowser1.Document.Body.AttachEventHandler("onscroll", OnScrollEventHandler);
                            }));
                        });
					}
				}
			}
		}
        /// <summary>
        /// webBrowser コンポーネントにHTMLを出力して 
        /// DocumentComplate になるのを非同期で待ち合わせる
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        System.Threading.Tasks.Task WebBrowserDocumentWrite(string html)
        {
            if (browserWaitTimer == null)
            {
                browserWaitTimer = new Timer();
                browserWaitTimer.Tick += browserWaitTimer_Tick;
                browserWaitTimer.Enabled = true;
            }
            var obj = waitObject;
            if (obj != null)
            {
                obj.SetCanceled();
            }
            waitObject = new System.Threading.Tasks.TaskCompletionSource<string>();

            timerCount = 0;

            this.webBrowser1.DocumentText = html;

            browserWaitTimer.Enabled = true;

            return waitObject.Task;
        }

        /// <summary>
        /// webBrowser コンポーネントにHTMLを出力して 
        /// DocumentComplate になるのを非同期で待ち合わせる
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        System.Threading.Tasks.Task WebBrowserNavigate(string url)
        {
            if (browserWaitTimer == null)
            {
                browserWaitTimer = new Timer();
                browserWaitTimer.Tick += browserWaitTimer_Tick;
                browserWaitTimer.Enabled = true;
            }
            var obj = waitObject;
            if (obj != null)
            {
                obj.SetCanceled();
            }
            waitObject = new System.Threading.Tasks.TaskCompletionSource<string>();

            timerCount = 0;

            this.webBrowser1.Navigate(url);

            browserWaitTimer.Enabled = true;

            return waitObject.Task;
        }

        void browserWaitTimer_Tick(object sender, EventArgs e)
        {
            if (waitObject == null)
            {
                browserWaitTimer.Enabled = false;
                return;
            }

            timerCount++;

            if (this.webBrowser1.ReadyState == WebBrowserReadyState.Complete)
            {
                waitObject.SetResult("OK");
                waitObject = null;
                browserWaitTimer.Enabled = false;
            }
            else if (timerCount > 20)
            {
                // 反応ないので終わりにする
                waitObject.SetResult("OK");
                waitObject = null;
                browserWaitTimer.Enabled = false;
            }
        }

        System.Threading.Tasks.TaskCompletionSource<string> waitObject = null;
        int timerCount = 0;
        Timer browserWaitTimer;


		//----------------------------------------------------------------------
		// BackgroundWorker Syntax hightlighter work
		//----------------------------------------------------------------------
		private void backgroundWorker2_DoWork(object sender, DoWorkEventArgs e)
		{
			var text = e.Argument as string;
			if (string.IsNullOrEmpty(text))
			{
				e.Result = null;
				return;
			}
			if (timer2.Enabled == true)
			{
				e.Result = null;
				return;
			}

			var result = new List<SyntaxColorScheme>();
#if false
			foreach (MarkdownSyntaxKeyword mk in _MarkdownSyntaxKeywordAarray)
			{
				MatchCollection col = mk.Regex.Matches(text, 0);
				
				if (col.Count > 0)
				{
					foreach (Match m in col)
					{
						SyntaxColorScheme sytx = new SyntaxColorScheme();
						sytx.SelectionStartIndex = m.Groups[0].Index;
						sytx.SelectionLength = m.Groups[0].Length;
						sytx.ForeColor = mk.ForeColor;
						sytx.BackColor = mk.BackColor;
						result.Add(sytx);
					}
				}
			}
#endif
            e.Result = result;
		}
		//----------------------------------------------------------------------
		// BackgroundWorker Editor Syntax hightlighter progress changed
		//----------------------------------------------------------------------
		private void backgroundWorker2_ProgressChanged(object sender, ProgressChangedEventArgs e)
		{

		}
		//----------------------------------------------------------------------
		// BackgroundWorker Syntax hightlighter completed to work
		//----------------------------------------------------------------------
		private void backgroundWorker2_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			if (e.Error != null || e.Cancelled == true)
			{
				return;
			}

			var syntaxColorSchemeList = e.Result as List<SyntaxColorScheme>;
			if (syntaxColorSchemeList == null)
			{
				return;
			}

			var obj = MarkDownSharpEditor.AppSettings.Instance;

#if false
			Font fc = richTextBox1.Font;          //現在のフォント設定 ( Font option )
			bool fModify = richTextBox1.Modified;	//現在の編集状況 ( Modified flag )

			_fConstraintChange = true;

			//現在のカーソル位置 / Current cursor position
			int selectStart = this.richTextBox1.SelectionStart;
			int selectEnd = richTextBox1.SelectionLength;
			Point CurrentOffset = richTextBox1.AutoScrollOffset;

			//現在のスクロール位置 / Current scroll position
			int CurrentScrollPos = richTextBox1.VerticalPosition;
			//描画停止 / Stop to paint
			richTextBox1.BeginUpdate();

			//RichTextBoxの書式をクリアする / Clear richTextBox1 format options
			richTextBox1.ForeColor = Color.FromArgb(obj.ForeColor_MainText);
			richTextBox1.BackColor = Color.FromArgb(obj.BackColor_MainText);

			//裏でパースしていたシンタックスハイライターを反映。
			foreach (var s in syntaxColorSchemeList)
			{
				richTextBox1.Select(s.SelectionStartIndex, s.SelectionLength);
				richTextBox1.SelectionColor = s.ForeColor;
				richTextBox1.SelectionBackColor = s.BackColor;
			}
            //カーソル位置を戻す / Restore cursor position
			richTextBox1.Select(selectStart, selectEnd);
			richTextBox1.AutoScrollOffset = CurrentOffset;
			richTextBox1.VerticalPosition = CurrentScrollPos;

			//描画再開 / Resume to paint
			richTextBox1.EndUpdate();
            richTextBox1.Modified = fModify;
#endif
            _fConstraintChange = false;

			if (MarkDownSharpEditor.AppSettings.Instance.AutoBrowserPreviewInterval < 0)
			{
				//手動更新 / Manual refresh
				timer1.Enabled = false;
				return;
			}
			else
			{
				timer1.Enabled = true;
			}

		}

		//----------------------------------------------------------------------
		// プレビューページが読み込まれたら編集中の箇所へ自動スクロールする
		// Scroll to editing line when browser is loaded
		//----------------------------------------------------------------------
		private void webBrowser1_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
		{
			//更新抑制中のときは抜ける
			//Constraint to change
			if (_fConstraintChange == true)
			{
				return;
			}
			//読み込まれた表示領域の高さを取得
			//Get viewing area to load in browser component
			_WebBrowserInternalHeight = webBrowser1.Document.Body.ScrollRectangle.Height;
			//ブラウザのスクロールイベントハンドラ
			//Browser component event handler
			//webBrowser1.Document.Window.AttachEventHandler("onscroll", OnScrollEventHandler);
		}

		//----------------------------------------------------------------------
		// Browser component scrolll event
		//----------------------------------------------------------------------
		public void OnScrollEventHandler(object sender, EventArgs e)
		{
            if (this.azukiRichTextBox1.Focused) return;// 特に何もしなくてよい

            ScrollSyncToBrowser();
		}
        public void OnClickEventHandler(object sender, EventArgs e)
        {
            // マウスがクリックした要素を求める。
            var pos = this.webBrowser1.PointToClient(MousePosition);
            var elem = this.webBrowser1.Document.GetElementFromPoint(pos);
            int dataPos = GetDataPos(elem);

            while (dataPos <= 0 && elem != null)
            {
                elem = elem.Parent;
                if (elem == null) break;

                dataPos = GetDataPos(elem);
            }

            if (dataPos > 5)
            {
                // まだ完ぺきに data-pos の値が設定できていないので・・仕方ないが・・

                // 指定の文字の位置に移動
                ScrollSyncTextPos(dataPos);
            }
            else
            {
                // スクロール位置に合わせて移動
                ScrollSyncToBrowser();
            }
		}

        System.Text.RegularExpressions.Regex regDataPos = new Regex("data-pos=\"(?<pos>[0-9]+)\"");

        /// <summary>
        /// data-pos="??" の数字を取得する
        /// </summary>
        /// <param name="elem"></param>
        /// <returns></returns>
        private int GetDataPos(HtmlElement elem)
        {
            string outHtml = elem.OuterHtml;
            List<int> posList = new List<int>();
            var m = regDataPos.Match(outHtml);
            while (m.Success)
            {
                int pos;
                if (int.TryParse(m.Groups["pos"].Value, out pos))
                {
                    posList.Add(pos);
                }

                m = m.NextMatch();
            }

            if (posList.Any())
            {
                return posList.Min();
            }
            else
            {
                return 0;
            }
        }

		//----------------------------------------------------------------------
		// TODO: WebBrowserMoveCursor [RichEditBox → WebBrowser scroll follow]
		//----------------------------------------------------------------------
		private void WebBrowserMoveCursor()
		{
			if (webBrowser1.Document == null)
			{
				return;
			}

			if (richTextBox1.Focused == true)
			{
                this.ScrollSyncToText();
#if false
				//richEditBoxの内部的な高さから現在位置の割合を計算
				//Calculate current position with internal height of richTextBox1
				int LineHeight = Math.Abs(richTextBox1.GetPositionFromCharIndex(0).Y);
				float perHeight = (float)LineHeight / _richEditBoxInternalHeight;

				//その割合からwebBrowserのスクロール量を計算
				//Calculate scroll amount with the ratio
				_WebBrowserInternalHeight = webBrowser1.Document.Body.ScrollRectangle.Height;
				int y = (int)(_WebBrowserInternalHeight * perHeight);
				Point webScrollPos = new Point(0, y);
				//Follow to scroll browser component
				webBrowser1.Document.Window.ScrollTo(webScrollPos);
#endif
            }
		}

		//----------------------------------------------------------------------
		// HACK: RichEditBoxMoveCursor[ WebBrowser → RichEditBox scroll follow]
		//----------------------------------------------------------------------
		
        int beforScrollTop = -1;

        class TagPos
        {
            public int OffsetTop;
            public int OffsetHeight;
            public IHTMLElement e;

            public int? TextPos;
            public int OffsetAbsDiff;
#if DEBUG
            public string outerText;
#endif
        }

        /// <summary>
        /// 1:テキストに合わせてブラウザをスクロール
        /// 2:ブラウザに合わせてテキストをスクロール
        /// </summary>
        int ScrollSyncMode = -1;

        void Application_Idle(object sender, EventArgs e)
        {
            if (ScrollSyncMode == 1)
            {
                //テキストに合わせてブラウザをスクロール
                OnIdle_ScrollSyncToText();

                ScrollSyncMode = 0;
            }
            else if (ScrollSyncMode == 2)
            {
                // ブラウザに合わせてテキストをスクロール
                OnIdle_ScrollSyncToBrowser();

                ScrollSyncMode = 0;
            }
            else
            {
                ScrollSyncMode = -1;
            }
        }

        /// <summary>
        /// テキストに合わせてブラウザをスクロール
        /// </summary>
        private void ScrollSyncToText()
        {
            if (ScrollSyncMode == -1) {
                ScrollSyncMode = 1;
            }
        }

        /// <summary>
        /// ブラウザに合わせてテキストをスクロール
        /// </summary>
        private void ScrollSyncToBrowser()
        {
            if (ScrollSyncMode == -1) {
                ScrollSyncMode = 2;
            }
        }

        /// <summary>
        /// テキストの指定の位置を表示させる。
        /// ブラウザのクリックによって目的の位置を表示させるために利用
        /// </summary>
        /// <param name="pos"></param>
        private void ScrollSyncTextPos(int pos)
        {
            ScrollSyncMode = -1;

            if (pos >= 0 && pos < this.azukiRichTextBox1.Document.Length)
            {
                this.azukiRichTextBox1.SetSelection(pos, pos);
                this.azukiRichTextBox1.ScrollToCaret();
            }
        }
        /// <summary>
        /// ブラウザの位置と同期をとる
        /// </summary>
        private void OnIdle_ScrollSyncToBrowser()
        {
            if (this.azukiRichTextBox1.Focused) return; // スクロールさせない

            IHTMLDocument3 doc3 = (IHTMLDocument3)webBrowser1.Document.DomDocument;
            IHTMLElement2 elm = (IHTMLElement2)doc3.documentElement;

            if (beforScrollTop == elm.scrollTop) return;
            int findPos = elm.scrollTop;
            int scrollMode = 0;
            if (beforScrollTop < elm.scrollTop)
            {
                findPos = findPos + this.webBrowser1.Height;
                scrollMode = 1;
            }
            beforScrollTop = elm.scrollTop;

            var list = GetBrowserDocumentTagPos();

            // 指定の場所に近い順に並び替える
            if (scrollMode == 0)
            {
                foreach (var item in list)
                {
                    item.OffsetAbsDiff = Math.Abs(findPos - item.OffsetTop);
                }
            }
            else
            {
                foreach (var item in list)
                {
                    item.OffsetAbsDiff = Math.Abs(findPos - item.OffsetTop - item.OffsetHeight);
                }
            }

            list = list.Where(r => r.TextPos != null)
                .OrderBy(r => r.OffsetAbsDiff)
                .ThenBy(r => r.TextPos.Value).ToList();

            int findCharPos = -1;
            foreach (var item in list)
            {
                if (item.TextPos > 0)
                {
                    findCharPos = item.TextPos.Value;
                    break;
                }
            }

            // 指定の場所にスクロールする
            if (this.azukiRichTextBox1.Focused == false && findCharPos > 0)
            {
                this.richTextBox1.Select(findCharPos, 0);
                this.richTextBox1.ScrollToCaret();
            }
        }

        /// <summary>
        /// テキストの選択位置と同期をとる
        /// </summary>
        private void OnIdle_ScrollSyncToText()
        {
            int begin;
            int end;
            this.azukiRichTextBox1.GetSelection(out begin, out end);
            
            // カーソルの画面上の位置
            var curPos = this.azukiRichTextBox1.GetPositionFromIndex(begin);

            begin = this.azukiRichTextBox1.GetIndexFromPosition(new Point(0, 0));

            var list = GetBrowserDocumentTagPos().Where(r => r.TextPos != null).ToList();

            foreach (var item in list)
            {
                item.OffsetAbsDiff = Math.Abs(begin - item.TextPos.Value);
            }
            list = list.OrderBy(r => r.OffsetAbsDiff).ToList();
            var topItem = list.FirstOrDefault();
            if (topItem != null)
            {
                Point scrollPos = new Point(0,topItem.OffsetTop);
                this.webBrowser1.Document.Window.ScrollTo(scrollPos);
            }
        }

        List<TagPos> GetBrowserDocumentTagPos()
        {
            IHTMLDocument3 doc3 = (IHTMLDocument3)webBrowser1.Document.DomDocument;
            IHTMLElement2 elm = (IHTMLElement2)doc3.documentElement;

            List<TagPos> list = new List<TagPos>();

            int minDiff = elm.scrollHeight + 100;

            // 調査する対象のタグ
            var tags = doc3.getElementsByTagName("body");
            HTMLBody body = null;
            if (tags.length > 0)
            {
                body = tags.item(0) as HTMLBody;
            }

            if (body != null)
            {
                foreach (IHTMLElement e in body.all)
                {
                    var tag = new TagPos()
                    {
                        e = e,
                        OffsetTop = e.offsetTop,
                        OffsetHeight = e.offsetHeight,
                    };
#if DEBUG
                    tag.outerText = e.outerHTML;
#endif
                    string s = e.getAttribute("data-pos") as string;
                    int pos;
                    if (string.IsNullOrEmpty(s) == false)
                    {
                        if (int.TryParse(s, out pos))
                        {
                            tag.TextPos = pos;
                        }
                    }
                    list.Add(tag);
                }
            }
            return list;
        }
        
		private void RichEditBoxMoveCursor()
		{
			//ブラウザでのスクロールバーの位置
			//Position of scroll bar in browser component
			if (richTextBox1.Focused == false && webBrowser1.Document != null)
			{
				IHTMLDocument3 doc3 = (IHTMLDocument3)webBrowser1.Document.DomDocument;
				IHTMLElement2 elm = (IHTMLElement2)doc3.documentElement;
				//全高さからの割合（位置）
				//Ratio(Position) of height
				float perHeight = (float)elm.scrollTop / _WebBrowserInternalHeight;
				int y = (int)(_richEditBoxInternalHeight * perHeight);
				richTextBox1.VerticalPosition = y;
			}

		}

		//----------------------------------------------------------------------
		// webBrowser1 Navigated event
		//----------------------------------------------------------------------
		private void webBrowser1_Navigated(object sender, WebBrowserNavigatedEventArgs e)
		{
			//ブラウザー操作ボタンの有効・無効化
			toolStripButtonBack.Enabled = webBrowser1.CanGoBack;
			toolStripButtonForward.Enabled = webBrowser1.CanGoForward;
		}

		//----------------------------------------------------------------------
		// HTML形式出力 ( Output to HTML file )
		//----------------------------------------------------------------------
		private bool OutputToHtmlFile(string FilePath, string SaveToFilePath, bool fToClipboard = false)
		{

			if (File.Exists(FilePath) == false)
			{
				return (false);
			}

			//出力内容 ( Output data )
			string ResultText = "";
			//HTMLタグ ( HTML Header tag )
			string HeaderString = "";
			string FooterString = "";
			//文字コード ( Character encoding )
			string EncodingName;
			Encoding encRead = Encoding.UTF8;
			Encoding encHtml = Encoding.UTF8;

			//-----------------------------------
			//編集中のファイルパス or 投げ込まれたファイルパス
			//Editing file path or Drag & Dropped files
			string FileName = Path.GetFileName(FilePath);

			//-----------------------------------
			//DOCTYPE
			HtmlHeader htmlHeader = new HtmlHeader();
			string DocType = htmlHeader.GetHtmlHeader(MarkDownSharpEditor.AppSettings.Instance.HtmlDocType);
			//Web用の相対パス
			//Relative url
			string CssPath = RerativeFilePath(FilePath, _SelectedCssFilePath);

			//-----------------------------------
			//指定のエンコーディング
			//Codepage
			int CodePageNum = MarkDownSharpEditor.AppSettings.Instance.CodePageNumber;
			try
			{
				encHtml = Encoding.GetEncoding(CodePageNum);
				//ブラウザ表示に対応したエンコーディングか
				//Is the encoding supported browser?
				if (encHtml.IsBrowserDisplay == true)
				{
					EncodingName = encHtml.WebName;
				}
				else
				{
					EncodingName = "utf-8";
					encHtml = Encoding.UTF8;
				}
			}
			catch
			{
				//エンコーディングの取得に失敗した場合はデフォルト
				//Default encoding if failing to get encoding
				EncodingName = "utf-8";
				encHtml = Encoding.UTF8;
			}

			//HTMLのヘッダを挿入する
			//Insert HTML Header
			if (MarkDownSharpEditor.AppSettings.Instance.fHtmlOutputHeader == true)
			{
				//CSSファイルを埋め込む
				//Embeding CSS file contents
				if (MarkDownSharpEditor.AppSettings.Instance.HtmlCssFileOption == 0)
				{
					string CssContents = "";

					if (File.Exists(_SelectedCssFilePath) == true)
					{
						using (StreamReader sr = new StreamReader(_SelectedCssFilePath, encHtml))
						{
							CssContents = sr.ReadToEnd();
						}
					}

					//ヘッダ ( Header )
					HeaderString = string.Format(
@"{0}
<html>
<head>
<meta http-equiv='Content-Type' content='text/html; charset={1}' />
<title>{2}</title>
<style>
<！--
{3}
-->
</style>
</head>
<body>
",
						DocType,         //DOCTYPE
						EncodingName,    //エンコーディング ( Encoding )
						FileName,        //タイトル（＝ファイル名） ( Title = file name )
						CssContents);	   //CSSの内容 ( Contents of CSS file )
				}
				//metaタグ（外部リンキング）(Meta tag: external linking )
				else
				{
					//ヘッダ ( Header )
					HeaderString = string.Format(
@"{0}
<html>
<head>
<meta http-equiv='Content-Type' content='text/html; charset={1}' />
<link rel='stylesheet' href='{2}' type='text/css' />
<title>{3}</title>
</head>
<body>
",
				DocType,         //DOCTYPE
				EncodingName,    //エンコーディング ( Encoding )
				CssPath,         //CSSファイル（相対パス）( Relative url )
				FileName);		   //タイトル（＝ファイル名） ( Title = file name )
				}
				//フッタ ( Footer )
				FooterString = "</body>\n</html>";
			}
			else
			{
				HeaderString = "";
				FooterString = "";
			}

			//-----------------------------------
			//Markdown parse ( default )
			//Markdown mkdwn = new Markdown();
			//-----------------------------------

			//-----------------------------------
			// MarkdownDeep
			// Create an instance of Markdown
			//-----------------------------------
			var mkdwn = new MarkdownDeep.Markdown();
			// Set options
			mkdwn.ExtraMode = MarkDownSharpEditor.AppSettings.Instance.fMarkdownExtraMode;
			mkdwn.SafeMode = false;
			//-----------------------------------

			//編集中のファイル（richEditBoxの内容）
			//Editing file path
			if (_MarkDownTextFilePath == FilePath)
			{
				ResultText = mkdwn.Transform(richTextBox1.Text);
				//エンコーディング変換（richEditBoxは基本的にutf-8）
				//Convert encoding ( richEditBox default encoding is utf-8 = 65001 )
				ResultText = ConvertStringToEncoding(ResultText, Encoding.UTF8.CodePage, CodePageNum);
			}
			else
			{
				//テキストファイルを開いてその文字コードに従って読み込み
				//Detect encoding in the text file
				byte[] bs;
				using (FileStream fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read))
				{
					bs = new byte[fs.Length];
					fs.Read(bs, 0, bs.Length);
				}
				//文字コードを取得する
				//Get charcter encoding
				encRead = GetCode(bs);
				//取得したバイト列を文字列に変換
				//Convert byte values to character
				ResultText = encRead.GetString(bs);

				//UTF-8でないならいったん変換してパース
				//Convert it if encoding is not utf-8
				if (encRead != Encoding.UTF8)
				{
					ResultText = ConvertStringToEncoding(ResultText, encRead.CodePage, CodePageNum);
				}
				ResultText = mkdwn.Transform(ResultText);
			}

			//ヘッダ＋本文＋フッタ
			//Header + Contents + Footer
			ResultText = HeaderString + ResultText + FooterString;
			//出力するHTMLファイルの文字コードに合わせる
			//Ajust encoding to output HTML file
			ResultText = ConvertStringToEncoding(ResultText, Encoding.UTF8.CodePage, CodePageNum);

			if (fToClipboard == true)
			{	//クリップボードに書き込む
				//Set data to clipbord
				Clipboard.SetText(ResultText);
			}
			else
			{	//ファイルに書き込む
				//Write file
				using (StreamWriter sw = new StreamWriter(SaveToFilePath, false, encHtml))
				{
					sw.Write(ResultText);
				}
			}
			return (true);

		}

		//----------------------------------------------------------------------
		// HTML形式ファイルへのバッチ出力
		// Output HTML files in batch
		//----------------------------------------------------------------------
		private bool BatchOutputToHtmlFiles(string[] ArrrayFileList)
		{
			string OutputFilePath;
			foreach (string FilePath in ArrrayFileList)
			{
				OutputFilePath = Path.ChangeExtension(FilePath, ".html");
				if (OutputToHtmlFile(FilePath, OutputFilePath, false) == false)
				{
					return (false);
				}
			}
			return (true);
		}

		//----------------------------------------------------------------------
		// 基本パスから相対パスを取得する
		// Get relative file path from base file path
		//----------------------------------------------------------------------
		private string RerativeFilePath(string BaseFilePath, string TargetFilePath)
		{
			Uri u1 = new Uri(BaseFilePath);
			Uri u2 = new Uri(u1, TargetFilePath);
			string RelativeFilePath = u1.MakeRelativeUri(u2).ToString();
			//URLデコードして、"/" を "\" に変更する
			RelativeFilePath = System.Web.HttpUtility.UrlDecode(RelativeFilePath).Replace('/', '\\');
			return (RelativeFilePath);
		}

		//----------------------------------------------------------------------
		// テキストを指定のエンコーディング文字列に変換する
		// Convert text data to user encoding characters
		//----------------------------------------------------------------------
		public string ConvertStringToEncoding(string source, int SrcCodePage, int DestCodePage)
		{
			Encoding srcEnc;
			Encoding destEnc;
			try
			{
				srcEnc = Encoding.GetEncoding(SrcCodePage);
				destEnc = Encoding.GetEncoding(DestCodePage);
			}
			catch
			{
				//指定のコードページがおかしい（取得できない）
				//Error: Codepage is incorrect
				return (source);
			}
			//Byte配列で変換する
			//Convert to byte values
			byte[] srcByte = srcEnc.GetBytes(source);
			byte[] destByte = Encoding.Convert(srcEnc, destEnc, srcByte);
			char[] destChars = new char[destEnc.GetCharCount(destByte, 0, destByte.Length)];
			destEnc.GetChars(destByte, 0, destByte.Length, destChars, 0);
			return new string(destChars);
		}




		//======================================================================
		#region ブラウザーのツールバーメニュー ( Toolbar on browser )
		//======================================================================

		//----------------------------------------------------------------------
		// ブラウザの「戻る」 ( Browser back )
		//----------------------------------------------------------------------
		private void toolStripButtonBack_Click(object sender, EventArgs e)
		{
			if (webBrowser1.CanGoBack == true)
			{
				webBrowser1.GoBack();
			}
		}

		//----------------------------------------------------------------------
		// ブラウザの「進む」 ( Browser forward )
		//----------------------------------------------------------------------
		private void toolStripButtonForward_Click(object sender, EventArgs e)
		{
			if (webBrowser1.CanGoForward == true)
			{
				webBrowser1.GoForward();
			}

		}

		//----------------------------------------------------------------------
		// ブラウザの「更新」 ( Browser refresh )
		//----------------------------------------------------------------------
		private void toolStripButtonRefresh_Click(object sender, EventArgs e)
		{
			//手動更新設定
			//Manual to refresh browser
			if (MarkDownSharpEditor.AppSettings.Instance.fAutoBrowserPreview == false)
			{
				//プレビューしているのは編集中のファイルか
				//Is previewing file the editing file?
				if (webBrowser1.Url.AbsoluteUri == @"file://" + _TemporaryHtmlFilePath)
				{
					PreviewToBrowser();
				}
			}
			webBrowser1.Refresh();
		}

		//----------------------------------------------------------------------
		// ブラウザの「中止」 ( Browser stop )
		//----------------------------------------------------------------------
		private void toolStripButtonStop_Click(object sender, EventArgs e)
		{
			webBrowser1.Stop();
		}

		//----------------------------------------------------------------------
		// 規定のブラウザーを関連付け起動してプレビュー
		// Launch default web browser to preview
		//----------------------------------------------------------------------
		private void toolStripButtonBrowserPreview_Click(object sender, EventArgs e)
		{
			if (File.Exists(_TemporaryHtmlFilePath) == true)
			{
				System.Diagnostics.Process.Start(_TemporaryHtmlFilePath);
			}
			else
			{
				_TemporaryHtmlFilePath = "";
			}
		}
		#endregion
		//======================================================================

		//======================================================================
		#region メインメニューイベント ( Main menu events )
		//======================================================================

		//-----------------------------------
		//「新しいファイルを開く」メニュー
		// "New file" menu
		//-----------------------------------
		private void menuNewFile_Click(object sender, EventArgs e)
		{
			if (richTextBox1.Modified == true)
			{
				//"問い合わせ"
				//"編集中のファイルがあります。保存してから新しいファイルを開きますか？"
				//"Question"
				//"This file being edited. Do you wish to save before starting new file?"
				DialogResult ret = MessageBox.Show(Resources.MsgSaveFileToNewFile,
				Resources.DialogTitleQuestion, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

				if (ret == DialogResult.Yes)
				{
					if (SaveToEditingFile() == true)
					{
						_fNoTitle = false;	//無題フラグOFF
					}
					else
					{
						//キャンセルで抜けてきた
						//Cancel
						return;
					}
				}
				else if (ret == DialogResult.Cancel)
				{
					return;
				}
			}

			//前の編集していたテンポラリを削除する
			//Delete edited temporary file before
			Delete_TemporaryHtmlFilePath();

			//無題ファイルのまま編集しているのなら削除
			//Delete it if the file is no title
			if (_fNoTitle == true)
			{
				if (File.Exists(_MarkDownTextFilePath) == true)
				{
					try
					{
						File.Delete(_MarkDownTextFilePath);
					}
					catch
					{
					}
				}
			}

			//編集履歴に残す
			//Add editing history
			if (File.Exists(_MarkDownTextFilePath) == true)
			{
				foreach (AppHistory data in MarkDownSharpEditor.AppSettings.Instance.ArrayHistoryEditedFiles)
				{
					if (data.md == _MarkDownTextFilePath)
					{ //いったん削除して ( delete once ... )
						MarkDownSharpEditor.AppSettings.Instance.ArrayHistoryEditedFiles.Remove(data);
						break;
					}
				}
				AppHistory HistroyData = new AppHistory();
				HistroyData.md = _MarkDownTextFilePath;
				HistroyData.css = _SelectedCssFilePath;
				MarkDownSharpEditor.AppSettings.Instance.ArrayHistoryEditedFiles.Insert(0, HistroyData);	//先頭に挿入
			}

			_fConstraintChange = true;

			//ブラウザを空白にする
			//Be blank in browser
			webBrowser1.Navigate("about:blank");

			//テンポラリファイルがあれば削除
			//Delete it if temporary file exists
			Delete_TemporaryHtmlFilePath();
			//編集中のファイル情報をクリア
			//Clear the infomation of editing file
			_MarkDownTextFilePath = "";
			//「無題」編集開始
			//Start to edit in no title
			_fNoTitle = true;
			richTextBox1.Text = "";
			richTextBox1.Modified = false;
            azukiRichTextBox1.Document.ClearHistory();
			FormTextChange();
			_fConstraintChange = false;

		}

		//-----------------------------------
		//「新しいウィンドウを開く」メニュー
		// "New window" menu
		//-----------------------------------
		private void menuNewWindow_Click(object sender, EventArgs e)
		{
			//自分自身を起動する
			//Launch the self
			System.Diagnostics.ProcessStartInfo pInfo = new System.Diagnostics.ProcessStartInfo(Application.ExecutablePath);
			pInfo.Arguments = "/new";
			System.Diagnostics.Process p = System.Diagnostics.Process.Start(pInfo);

		}

		//-----------------------------------
		//「ファイルを開く」メニュー
		// "Open File" menu
		//-----------------------------------
		private void menuOpenFile_Click(object sender, EventArgs e)
		{
			OpenFile("", true);
		}

		//-----------------------------------
		//「ファイル」メニュー
		// "File" menu
		//-----------------------------------
		private void menuFile_Click(object sender, EventArgs e)
		{
			//編集履歴のサブメニューをつくる
			//Create submenu of editing history
			for (int i = 0; i < MarkDownSharpEditor.AppSettings.Instance.ArrayHistoryEditedFiles.Count; i++)
			{
				AppHistory History = (AppHistory)MarkDownSharpEditor.AppSettings.Instance.ArrayHistoryEditedFiles[i];
				ToolStripMenuItem m = new ToolStripMenuItem(History.md);
				m.Tag = History.css;
				m.Click += new EventHandler(HistorySubMenuItemClickHandler);
				menuHistoryFiles.DropDownItems.Add(m);
			}

		}
		//-----------------------------------
		//各履歴メニューがクリックされたとき
		//Click editing history menu event
		private void HistorySubMenuItemClickHandler(object sender, EventArgs e)
		{

			ToolStripMenuItem clickItem = (ToolStripMenuItem)sender;

			string FilePath = clickItem.Text;

			if (File.Exists(FilePath) == true)
			{
				OpenFile(FilePath);
			}
		}

		//-----------------------------------
		//編集中のファイルを保存する
		//Save to editing file
		//-----------------------------------
		private bool SaveToEditingFile(bool fSaveAs = false)
		{
			//名前が付けられていない、または別名保存指定なのでダイアログ表示
			//The file is no title, or saving as oher name
			if (_fNoTitle == true || fSaveAs == true)
			{
				if (saveFileDialog1.ShowDialog() == DialogResult.OK)
				{
					using (StreamWriter sw = new StreamWriter(saveFileDialog1.FileName, false, _EditingFileEncoding))
					{
						sw.Write(richTextBox1.Text);
						_MarkDownTextFilePath = saveFileDialog1.FileName;
					}
				}
				else
				{
					return (false);
				}
			}
			else
			{
				//上書き保存
				//Overwrite
				using (StreamWriter sw = new StreamWriter(
					_MarkDownTextFilePath,
					false,
					_EditingFileEncoding))
				{
					sw.Write(richTextBox1.Text);
				}

			}

			//Undoバッファクリア
			//Clear undo buffer
			//undoCounter = 0;
			//UndoBuffer.Clear();
            azukiRichTextBox1.Document.ClearHistory();

			_fNoTitle = false;	//無題フラグOFF
			richTextBox1.Modified = false;
			FormTextChange();

			return (true);
		}

		//-----------------------------------
		//「ファイルを保存」メニュー
		// "Save file" menu
		//-----------------------------------
		private void menuSaveFile_Click(object sender, EventArgs e)
		{
			SaveToEditingFile();
		}

		//-----------------------------------
		//「名前を付けてファイルを保存」メニュー
		// "Save As" menu
		//-----------------------------------
		private void menuSaveAsFile_Click(object sender, EventArgs e)
		{
			SaveToEditingFile(true);
		}

		//-----------------------------------
		//「HTMLファイル出力(&P)」メニュー
		// "Output to HTML file" menu
		//-----------------------------------
		private void menuOutputHtmlFile_Click(object sender, EventArgs e)
		{
			string OutputFilePath;
			string DirPath = Path.GetDirectoryName(_MarkDownTextFilePath);

			if (File.Exists(_MarkDownTextFilePath) == true)
			{
				saveFileDialog2.InitialDirectory = DirPath;
			}
			//保存ダイアログを表示する
			//Show Save dialog
			if (MarkDownSharpEditor.AppSettings.Instance.fShowHtmlSaveDialog == true)
			{
				if (saveFileDialog2.ShowDialog() == DialogResult.OK)
				{
					OutputToHtmlFile(_MarkDownTextFilePath, saveFileDialog2.FileName, false);
				}
			}
			else
			{
				//ダイアログを抑制しているので編集中のファイルのディレクトリへ保存する
				//Save to editing folder in constrainting dialog
				OutputFilePath = Path.Combine(DirPath, Path.GetFileNameWithoutExtension(_MarkDownTextFilePath)) + ".html";

				if (File.Exists(OutputFilePath) == true)
				{
					//"問い合わせ"
					//"すでに同名のファイルが存在しています。上書きして出力しますか？"
					//"Question"
					//"Same file exists.\nContinue to overwrite?"
					DialogResult ret = MessageBox.Show(
						Resources.MsgSameFileOverwrite + "\n" + OutputFilePath,
						Resources.DialogTitleQuestion, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question,
						MessageBoxDefaultButton.Button1);	// Yesがデフォルト

					if (ret == DialogResult.Yes)
					{
						//上書きしてHTMLファイルへ出力
						//Overwrite and output to HTML file
						OutputToHtmlFile(_MarkDownTextFilePath, OutputFilePath, false);
					}
					else if (ret == DialogResult.No)
					{
						//設定されてないが一応保存ダイアログを出す
						//It is no setting, but show save dialog 
						if (saveFileDialog2.ShowDialog() == DialogResult.OK)
						{
							//HTMLファイルへ出力
							//Output to HTML file
							OutputToHtmlFile(_MarkDownTextFilePath, saveFileDialog2.FileName, false);
						}
					}
					else
					{
						//キャンセル
						//Cancel
					}
				}
				else
				{
					//HTMLファイルへ出力
					//Output to HTML file
					OutputToHtmlFile(_MarkDownTextFilePath, OutputFilePath, false);
				}
			}
		}

		//-----------------------------------
		//「HTMLソースコードをクリップボードへコピー(&L)」メニュー
		// "Set HTML source code to clipboard" menu
		//-----------------------------------
		private void menuOutputHtmlToClipboard_Click(object sender, EventArgs e)
		{
			//HTMLソースをクリップボードへ出力
			//Output HTML source to clipboard
			OutputToHtmlFile(_MarkDownTextFilePath, "", true);

			//HTMLソースをクリップボードコピーしたときに確認メッセージを表示する(&M)
			//Show message to confirm when HTML source data is setting to clipboard
			if (MarkDownSharpEditor.AppSettings.Instance.fShowHtmlToClipboardMessage == true)
			{
				//"通知"
				//"クリップボードに保存されました。"
				//"Information"
				//"This file has been output to the clipboard."
				MessageBox.Show(Resources.MsgOutputToClipboard,
					Resources.DialogTitleNotice, MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
		}

		//-----------------------------------
		//「終了」メニュー
		// "Exit" menu
		//-----------------------------------
		private void menuExit_Click(object sender, EventArgs e)
		{
			Close();
		}

		//-----------------------------------
		//「編集」メニュー
		// "Edit" menu
		//-----------------------------------
		private void menuEdit_Click(object sender, EventArgs e)
		{
#if false
			if (undoCounter > 0)
			{
				menuUndo.Enabled = true;
			}
			else
			{
				menuUndo.Enabled = false;
			}

			if (undoCounter < UndoBuffer.Count && undoCounter > 0)
			{
				menuRedo.Enabled = true;
			}
			else
			{
				menuRedo.Enabled = false;
			}
#else
            menuUndo.Enabled = this.azukiRichTextBox1.Document.CanUndo;
            menuRedo.Enabled = this.azukiRichTextBox1.Document.CanRedo;
#endif
            if (richTextBox1.SelectionLength > 0)
			{
				menuCut.Enabled = true;
				menuCopy.Enabled = true;
			}
			else
			{
				menuCut.Enabled = false;
				menuCopy.Enabled = false;
			}
		}
		//-----------------------------------
		//「元に戻す」メニュー
		// "Undo" menu
		//-----------------------------------
		private void menuUndo_Click(object sender, EventArgs e)
		{
#if false
			if (UndoBuffer.Count > 0 && undoCounter > 0)
			{	//現在のカーソル位置
				//Current cursor position
				int selectStart = this.richTextBox1.SelectionStart;
				int selectEnd = richTextBox1.SelectionLength;
				//現在のスクロール位置
				//Current scroll position
				int CurrentScrollpos = richTextBox1.VerticalPosition;
				//描画停止
				//Stop to paint
				richTextBox1.BeginUpdate();

				undoCounter--;
				richTextBox1.Rtf = UndoBuffer[undoCounter];

				//カーソル位置を戻す
				//Restore cursor position
				richTextBox1.Select(selectStart, selectEnd);
				//スクロール位置を戻す
				//Restore scroll position
				richTextBox1.VerticalPosition = CurrentScrollpos;
				//描画再開
				//Resume to paint
				richTextBox1.EndUpdate();

				if (undoCounter == 0)
				{
					richTextBox1.Modified = false;
					FormTextChange();
				}
			}
#else
            this.azukiRichTextBox1.Document.Undo();
#endif

        }
		//-----------------------------------
		//「やり直す」メニュー
		// "Redo" menu
		//-----------------------------------
		private void menuRedo_Click(object sender, EventArgs e)
		{
#if false
			if (undoCounter < UndoBuffer.Count && undoCounter > 0)
			{
				undoCounter++;
				richTextBox1.Rtf = UndoBuffer[undoCounter];
				FormTextChange();
			}
#else
            this.azukiRichTextBox1.Document.Redo();
#endif
        }
		//-----------------------------------
		//「切り取り」メニュー
		// "Cut" to clipbord menu
		//-----------------------------------
		private void menuCut_Click(object sender, EventArgs e)
		{
			if (richTextBox1.SelectionLength > 0)
			{
				richTextBox1.Cut();
				FormTextChange();
			}
		}
		//-----------------------------------
		//「コピー」メニュー
		// "Copy" to clipboard menu
		//-----------------------------------
		private void menuCopy_Click(object sender, EventArgs e)
		{
			if (richTextBox1.SelectionLength > 0)
			{
				richTextBox1.Copy();
			}
		}
		//-----------------------------------
		//「貼り付け」メニュー
		// "Paste" from clipboard menu
		//-----------------------------------
		private void menuPaste_Click(object sender, EventArgs e)
		{
			IDataObject data = Clipboard.GetDataObject();
			if (data != null && data.GetDataPresent(DataFormats.Text) == true)
			{
				DataFormats.Format fmt = DataFormats.GetFormat(DataFormats.Text);
				richTextBox1.Paste(fmt);
				FormTextChange();
			}
		}
		//-----------------------------------
		//「すべてを選択」メニュー
		// "Select all" menu
		//-----------------------------------
		private void menuSelectAll_Click(object sender, EventArgs e)
		{
			richTextBox1.SelectAll();
		}

		//-----------------------------------
		//「検索」メニュー
		// "Search" menu
		//-----------------------------------
		private void menuSearch_Click(object sender, EventArgs e)
		{
			_fSearchStart = false;
			panelSearch.Visible = true;
			panelSearch.Height = 58;
			textBoxSearch.Focus();
			labelReplace.Visible = false;
			textBoxReplace.Visible = false;
			cmdReplaceAll.Visible = false;
			cmdSearchNext.Text = Resources.ButtonFindNext; //"次を検索する(&N)";
			cmdSearchPrev.Text = Resources.ButtonFindPrev; // "前を検索する(&P)";
		}
		//-----------------------------------
		//「置換」メニュー
		// "Replace" menu
		//-----------------------------------
		private void menuReplace_Click(object sender, EventArgs e)
		{
			_fSearchStart = false;
			panelSearch.Visible = true;
			panelSearch.Height = 58;
			textBoxSearch.Focus();
			labelReplace.Visible = true;
			textBoxReplace.Visible = true;
			cmdReplaceAll.Visible = true;
			cmdSearchNext.Text = Resources.ButtonReplaceNext;  //"置換して次へ(&N)";
			cmdSearchPrev.Text = Resources.ButtonReplacePrev;  //"置換して前へ(&P)";
		}

		//-----------------------------------
		// 表示の更新
		// "Refresh preview" menu
		//-----------------------------------
		private void menuViewRefresh_Click(object sender, EventArgs e)
		{
			PreviewToBrowser();
		}

		//-----------------------------------
		//「ソースとビューを均等表示する」メニュー
		// "Editor and Browser Width evenly" menu
		//-----------------------------------
		private void menuViewWidthEvenly_Click(object sender, EventArgs e)
		{
			if (menuViewWidthEvenly.Checked == true)
			{
				menuViewWidthEvenly.Checked = false;
			}
			else
			{
				menuViewWidthEvenly.Checked = true;
			}
			MarkDownSharpEditor.AppSettings.Instance.fSplitBarWidthEvenly = menuViewWidthEvenly.Checked;
		}

		//-----------------------------------
		//「言語」メニュー
		// Change "Language" menu
		//-----------------------------------
		private void menuVieｗJapanese_Click(object sender, EventArgs e)
		{
			//"問い合わせ"
			//"言語の変更を反映するには、アプリケーションの再起動が必要です。
			//今すぐ再起動しますか？"
			//"Question"
			//"To change the setting of language, it is necessary to restart the application. 
			// Do you want to restart this application now?"
			DialogResult result = MessageBox.Show(Resources.MsgRestartApplication,
				Resources.DialogTitleQuestion, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

			if (result == DialogResult.Yes)
			{
				MarkDownSharpEditor.AppSettings.Instance.Lang = "ja";
				MarkDownSharpEditor.AppSettings.Instance.SaveToXMLFile();
				Application.Restart();
			}
			else if (result == DialogResult.No)
			{
				MarkDownSharpEditor.AppSettings.Instance.Lang = "ja";
				menuVieｗJapanese.Checked = true;
				menuViewEnglish.Checked = false;
			}
			else
			{
				//Cancel
			}

		}

		private void menuViewEnglish_Click(object sender, EventArgs e)
		{
			//"問い合わせ"
			//"言語の変更を反映するには、アプリケーションの再起動が必要です。
			//今すぐ再起動しますか？"
			//"Question"
			//"To change the setting of language, it is necessary to restart the application. 
			// Do you want to restart this application now?"
			DialogResult result = MessageBox.Show(Resources.MsgRestartApplication,
				Resources.DialogTitleQuestion, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

			if (result == DialogResult.Yes)
			{
				MarkDownSharpEditor.AppSettings.Instance.Lang = "en";
				MarkDownSharpEditor.AppSettings.Instance.SaveToXMLFile();
				Application.Restart();
			}
			else if (result == DialogResult.No)
			{
				MarkDownSharpEditor.AppSettings.Instance.Lang = "en";
				MarkDownSharpEditor.AppSettings.Instance.SaveToXMLFile();
				menuVieｗJapanese.Checked = false;
				menuViewEnglish.Checked = true;
			}
			else
			{
				//Cancel
			}

		}

		//-----------------------------------
		//「ツールバーを表示する」メニュー
		// "View toolbar" menu
		//-----------------------------------
		private void menuViewToolBar_Click(object sender, EventArgs e)
		{
			if (menuViewToolBar.Checked == true)
			{
				menuViewToolBar.Checked = false;
				toolStrip1.Visible = false;
			}
			else
			{
				menuViewToolBar.Checked = true;
				toolStrip1.Visible = true;
			}
			MarkDownSharpEditor.AppSettings.Instance.fViewToolBar = toolStrip1.Visible;
		}
		//-----------------------------------
		//「ステータスバーを表示する」メニュー
		// "View statusbar" menu
		//-----------------------------------
		private void menuViewStatusBar_Click(object sender, EventArgs e)
		{
			if (menuViewStatusBar.Checked == true)
			{
				menuViewStatusBar.Checked = false;
				statusStrip1.Visible = false;
			}
			else
			{
				menuViewStatusBar.Checked = true;
				statusStrip1.Visible = true;
			}
			MarkDownSharpEditor.AppSettings.Instance.fViewStatusBar = statusStrip1.Visible;
		}

		//-----------------------------------
		//「書式フォント」メニュー
		// "Font" menu
		//-----------------------------------
		private void menuFont_Click(object sender, EventArgs e)
		{
			//bool fModify = richTextBox1.Modified;
			fontDialog1.Font = richTextBox1.Font;
			fontDialog1.Color = richTextBox1.ForeColor;
			//選択できるポイントサイズの最小・最大値
			fontDialog1.MinSize = 6;
			fontDialog1.MaxSize = 72;
			fontDialog1.FontMustExist = true;
			//横書きフォントだけを表示する
			fontDialog1.AllowVerticalFonts = false;
			//色を選択できるようにする
			fontDialog1.ShowColor = true;
			//取り消し線、下線、テキストの色などのオプションを指定不可
			fontDialog1.ShowEffects = false;
			//ダイアログを表示する
			if (fontDialog1.ShowDialog() == DialogResult.OK)
			{
				//UndoBuffer.Add(richTextBox1.Rtf);
				//undoCounter = UndoBuffer.Count;
				this.richTextBox1.TextChanged -= new System.EventHandler(this.richTextBox1_TextChanged);
				richTextBox1.Font = fontDialog1.Font;
				richTextBox1.ForeColor = fontDialog1.Color;
				//ステータスバーに表示
				toolStripStatusLabelFontInfo.Text =
					fontDialog1.Font.Name + "," + fontDialog1.Font.Size.ToString() + "pt";
				this.richTextBox1.TextChanged += new System.EventHandler(this.richTextBox1_TextChanged);
			}
			////richTextBoxの書式を変えても「変更」となるので元のステータスへ戻す
			//richTextBox1.Modified = fModify;
		}

		//-----------------------------------
		//「オプション」メニュー
		// "Option" menu
		//-----------------------------------
		private void menuOption_Click(object sender, EventArgs e)
		{
			Form3 frm3 = new Form3();
			frm3.ShowDialog();
			frm3.Dispose();

			//_MarkdownSyntaxKeywordAarray = MarkdownSyntaxKeyword.CreateKeywordList();	 //キーワードリストの更新

            highlighter = new Azuki.MarkdownHighlighter(this.azukiRichTextBox1.Document);
            this.azukiRichTextBox1.Highlighter = highlighter;
            this.azukiRichTextBox1.ColorScheme = highlighter.MarkDownColorScheme;

			if (backgroundWorker2.IsBusy == false)
			{
				//SyntaxHightlighter on BackgroundWorker
				backgroundWorker2.RunWorkerAsync(richTextBox1.Text);
			}

			//プレビュー間隔を更新
			if (MarkDownSharpEditor.AppSettings.Instance.AutoBrowserPreviewInterval > 0)
			{
				timer1.Interval = MarkDownSharpEditor.AppSettings.Instance.AutoBrowserPreviewInterval;
			}
		}

		//-----------------------------------
		// ヘルプファイルの表示
		// "Help contents" menu
		//-----------------------------------
		private void menuContents_Click(object sender, EventArgs e)
		{
			string HelpFilePath;
			string DirPath = MarkDownSharpEditor.AppSettings.GetAppDataLocalPath();

			if (MarkDownSharpEditor.AppSettings.Instance.Lang == "ja")
			{
				HelpFilePath = Path.Combine(DirPath, "help-ja.md");
			}
			else
			{
				HelpFilePath = Path.Combine(DirPath, "help.md");
			}

			if (File.Exists(HelpFilePath) == true)
			{ //別ウィンドウで開く
				//Create a new ProcessStartInfo structure.
				System.Diagnostics.ProcessStartInfo pInfo = new System.Diagnostics.ProcessStartInfo();
				//Set the file name member. 
				pInfo.FileName = HelpFilePath;
				//UseShellExecute is true by default. It is set here for illustration.
				pInfo.UseShellExecute = true;
				System.Diagnostics.Process p = System.Diagnostics.Process.Start(pInfo);
			}
			else
			{ //"エラー"
				//"ヘルプファイルがありません。開くことができませんでした。"
				//"Could not find Help file. Opening this file has failed."
				MessageBox.Show(Resources.MsgNoHelpFile,
					Resources.DialogTitleError, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
		}

		//-----------------------------------
		// 最新バージョンのチェック
		// "Check for Update"
		//-----------------------------------
		private void mnuCheckForUpdate_Click(object sender, EventArgs e)
		{
			string MsgText = "";
			Version newVersion = null;
			string url = "";
			string dt = "";
			System.Xml.XmlTextReader reader;

			/*
			 *	<?xml version="1.0" encoding="utf-8"?>
			 *		<markdownsharpeditor>
			 *		<version>1.2.1.0</version>
			 *		<date>2013/06/18</date>
			 *		<url>http://hibara.org/software/markdownsharpeditor/</url>
			 *	</markdownsharpeditor>
			 */
			string xmlURL = "http://hibara.org/software/markdownsharpeditor/app_version.xml";
			using (reader = new System.Xml.XmlTextReader(xmlURL))
			{
				reader.MoveToContent();
				string elementName = "";
				if ((reader.NodeType == System.Xml.XmlNodeType.Element) && (reader.Name == "markdownsharpeditor"))
				{
					while (reader.Read())
					{
						if (reader.NodeType == System.Xml.XmlNodeType.Element)
						{
							elementName = reader.Name;
						}
						else
						{
							if ((reader.NodeType == System.Xml.XmlNodeType.Text) && (reader.HasValue))
							{
								switch (elementName)
								{
									case "version":
										newVersion = new Version(reader.Value);
										break;
									case "url":
										url = reader.Value;
										break;
									case "date":
										dt = reader.Value;
										break;
								}
							}
						}
					}
				}
			}

			if (newVersion == null)
			{
				//Failed to get the latest version information.
				MsgText = Resources.ErrorGetNewVersion;
				MessageBox.Show(MsgText, Resources.DialogTitleError, MessageBoxButtons.OK, MessageBoxIcon.Question);
				return;
			}

			// get current version
			Version curVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
			if (curVersion.CompareTo(newVersion) < 0)
			{	//"New version was found! Do you open the download site?";
				MsgText = "Update info: ver." + newVersion + " (" + dt + " ) \n" + Resources.NewVersionFound;
				if (DialogResult.Yes == MessageBox.Show(this, MsgText, Resources.DialogTitleQuestion, MessageBoxButtons.YesNo, MessageBoxIcon.Question))
				{
					System.Diagnostics.Process.Start(url);
				}
			}
			else
			{	// You already have the latest version of this application.
				MsgText = Resources.AlreadyLatestVersion + "\nver." + newVersion + " ( " + dt + " ) ";
				MessageBox.Show(MsgText, Resources.DialogTitleInfo, MessageBoxButtons.OK, MessageBoxIcon.Question);
			}

		}

		//-----------------------------------
		// サンプル表示
		// "View Markdown sample file"
		//-----------------------------------
		private void menuViewSample_Click(object sender, EventArgs e)
		{
			string DirPath = MarkDownSharpEditor.AppSettings.GetAppDataLocalPath();
			string SampleFilePath = Path.Combine(DirPath, "sample.md");

			if (File.Exists(SampleFilePath) == true)
			{
				System.Diagnostics.ProcessStartInfo pInfo = new System.Diagnostics.ProcessStartInfo();
				pInfo.FileName = SampleFilePath;
				pInfo.UseShellExecute = true;
				System.Diagnostics.Process p = System.Diagnostics.Process.Start(pInfo);
			}
			else
			{
				//"エラー"
				//"サンプルファイルがありません。開くことができませんでした。"
				//"Error"
				//"Could not find sample MD file.\nOpening this file has failed."
				MessageBox.Show(Resources.MsgNoSampleFile,
					Resources.DialogTitleError, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
		}

		//-----------------------------------
		//「MarkDownSharpについて」メニュー
		// "About" menu
		//-----------------------------------
		private void menuAbout_Click(object sender, EventArgs e)
		{
			Form2 frm2 = new Form2();
			frm2.ShowDialog();
			frm2.Dispose();
		}

		#endregion
		//======================================================================

		//======================================================================
		#region ステータスバーイベント ( Statusbar event )
		//======================================================================

		//-----------------------------------
		// ステータスバー（CSS）
		//-----------------------------------
		private void toolStripStatusLabelCssFileName_Click(object sender, EventArgs e)
		{
			//ポップアップメニューに登録する
			//Regist item to popup menus
			contextMenu1.Items.Clear();

			foreach (string FilePath in MarkDownSharpEditor.AppSettings.Instance.ArrayCssFileList)
			{
				if (File.Exists(FilePath) == true)
				{
					ToolStripMenuItem item = new ToolStripMenuItem(Path.GetFileName(FilePath));
					item.Tag = FilePath;
					if (_SelectedCssFilePath == FilePath)
					{
						item.Checked = true;
					}
					contextMenu1.Items.Add(item);
				}
			}
			if (contextMenu1.Items.Count > 0)
			{
				contextMenu1.Tag = "css";
				contextMenu1.Show(Control.MousePosition);
			}

		}

		//-----------------------------------
		// ステータスバー（Encoding）
		//-----------------------------------
		private void toolStripStatusLabelEncoding_Click(object sender, EventArgs e)
		{
			//ポップアップメニューに登録する
			//Regist item to popup menus
			contextMenu1.Items.Clear();
			foreach (EncodingInfo ei in Encoding.GetEncodings())
			{
				if (ei.GetEncoding().IsBrowserDisplay == true)
				{
					ToolStripMenuItem item = new ToolStripMenuItem(ei.DisplayName);
					item.Tag = ei.CodePage;
					if (ei.CodePage == MarkDownSharpEditor.AppSettings.Instance.CodePageNumber)
					{
						item.Checked = true;
					}
					contextMenu1.Items.Add(item);
				}
			}
			contextMenu1.Tag = "encoding";
			contextMenu1.Show(Control.MousePosition);
		}

		//-----------------------------------
		// ステータスバー（共通のクリックイベント）
		// Common item clicked event
		//-----------------------------------
		private void contextMenu1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
		{
			//-----------------------------------
			// 適用するCSSファイル変更
			// Change selected CSS file
			//-----------------------------------
			if ((string)contextMenu1.Tag == "css")
			{
				_SelectedCssFilePath = (string)e.ClickedItem.Tag;
				toolStripStatusLabelCssFileName.Text = Path.GetFileName(_SelectedCssFilePath);
				//プレビューも更新する
				PreviewToBrowser();

			}
			//-----------------------------------
			// 出力HTMLに適用する文字コードの変更
			// Change encoding to output HTML file
			//-----------------------------------
			else if ((string)contextMenu1.Tag == "encoding")
			{
				MarkDownSharpEditor.AppSettings.Instance.CodePageNumber = (int)e.ClickedItem.Tag;
				//プレビューも更新する
				//Refresh previewing, too
				PreviewToBrowser();
			}
		}

		//----------------------------------------------------------------------
		// 検索パネル ( Search panel )
		//----------------------------------------------------------------------
		private void imgSearchExit_Click(object sender, EventArgs e)
		{
			panelSearch.Visible = false;
		}
		//-----------------------------------
		// 検索パネル「閉じる」ボタンイベント
		// Search panel close button image event
		//-----------------------------------
		private void imgSearchExit_MouseEnter(object sender, EventArgs e)
		{
			imgSearchExit.Image = imgSearchExitEnabled.Image;
		}
		private void imgSearchExit_MouseLeave(object sender, EventArgs e)
		{
			imgSearchExit.Image = imgSearchExitUnabled.Image;
		}

		//-----------------------------------
		// 検索テキストボックス
		// Search text box TextChanged event
		//-----------------------------------
		private void textBoxSearch_TextChanged(object sender, EventArgs e)
		{
			//検索をやり直し
			//Restart to search
			_fSearchStart = false;
			if (textBoxReplace.Visible == true)
			{
				if (textBoxSearch.Text == "")
				{
					cmdSearchNext.Enabled = false;
					cmdSearchPrev.Enabled = false;
					cmdReplaceAll.Enabled = false;
				}
				else
				{
					cmdSearchNext.Enabled = true;
					cmdSearchPrev.Enabled = true;
					cmdReplaceAll.Enabled = true;
				}
			}
			else
			{
				cmdSearchNext.Enabled = true;
				cmdSearchPrev.Enabled = true;
			}
		}

		//-----------------------------------
		// 検索テキストボックス
		// Search text box KeyDown event
		//-----------------------------------
		private void textBoxSearch_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Shift && e.KeyCode == Keys.Enter)
			{	// Shitf + Enter で前へ
				// Press Shift + Enter key to previous item
				cmdSearchPrev_Click(sender, e);
			}
			else if (e.KeyCode == Keys.Enter)
			{
				cmdSearchNext_Click(sender, e);
			}
			else if (e.KeyCode == Keys.Escape)
			{
				panelSearch.Visible = false;
			}
		}

		//-----------------------------------
		// 検索テキストボックス
		// Search text box KeyPress event
		//-----------------------------------
		private void textBoxSearch_KeyPress(object sender, KeyPressEventArgs e)
		{
			//EnterやEscapeキーでビープ音が鳴らないようにする
			//Constraint to beep sound with Enter & Escape key
			if (e.KeyChar == (char)Keys.Enter || e.KeyChar == (char)Keys.Escape)
			{
				e.Handled = true;
			}
		}

		//-----------------------------------
		// 置換テキストボックス
		// Search text box KeyDown event
		//-----------------------------------
		private void textBoxReplace_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Shift && e.KeyCode == Keys.Enter)
			{	// Shitf + Enter で前へ
				// Press Shift + Enter key to previous item
				cmdSearchPrev_Click(sender, e);
			}
			else if (e.KeyCode == Keys.Enter)
			{
				cmdSearchNext_Click(sender, e);
			}
			else if (e.KeyCode == Keys.Escape)
			{
				panelSearch.Visible = false;
			}
		}

		//-----------------------------------
		// 置換テキストボックス（KeyPress）
		//-----------------------------------
		private void textBoxReplace_KeyPress(object sender, KeyPressEventArgs e)
		{
			//EnterやEscapeキーでビープ音が鳴らないようにする
			//Constraint to beep sound with Enter & Escape key
			if (e.KeyChar == (char)Keys.Enter || e.KeyChar == (char)Keys.Escape)
			{
				e.Handled = true;
			}
		}

		//----------------------------------------------------------------------
		// 次を検索（または、置換して次へ）ボタン
		// Press next button
		//----------------------------------------------------------------------
		private void cmdSearchNext_Click(object sender, EventArgs e)
		{
			int StartPos;
			StringComparison sc;
			DialogResult result;
			string MsgText = "";

			if (textBoxSearch.Text != "")
			{
				//置換モードの場合は、置換してから次を検索する
				//Replace the word to search next item in Replace mode
				if (textBoxReplace.Visible == true && _fSearchStart == true)
				{
					if (richTextBox1.SelectionLength > 0)
					{
						richTextBox1.SelectedText = textBoxReplace.Text;
					}
				}

				if (chkOptionCase.Checked == true)
				{
					sc = StringComparison.Ordinal;
				}
				else
				{	//大文字と小文字を区別しない
					//Ignore case
					sc = StringComparison.OrdinalIgnoreCase;
				}

				int CurrentPos = richTextBox1.SelectionStart + 1;

				//-----------------------------------
				// 検索ワードが見つからない
				// Searching word is not found
				//-----------------------------------
				if ((StartPos = richTextBox1.Text.IndexOf(textBoxSearch.Text, CurrentPos, sc)) == -1)
				{
					//検索を開始した直後
					//Start to search after
					if (_fSearchStart == false)
					{
						//"ファイル末尾まで検索しましたが、見つかりませんでした。
						// ファイルの先頭から検索を続けますか？"
						//"The word could not find the word to the end of this file.
						// Do you wish to continue searching from the beginning of this file?"
						MsgText = Resources.MsgNotFoundToEnd;
						_fSearchStart = true;
					}
					else
					{
						//"ファイル末尾までの検索が完了しました。
						// ファイル先頭に戻って検索を続けますか？"
						//"Searching completed to the end of this file.
						// Do you wish to continue searching from the beginning of this file?"
						MsgText = Resources.MsgFindCompleteToEnd;
					}
					result = MessageBox.Show(MsgText, Resources.DialogTitleNotice,
						MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);

					if (result == DialogResult.Yes)
					{
						richTextBox1.SelectionStart = 0;
						cmdSearchNext_Click(sender, e);
					}
				}
				//-----------------------------------
				// 検索ワードが見つかった
				// Searching word were found
				//-----------------------------------
				else
				{
					//richTextBox1.HideSelection = false;
					richTextBox1.Select(StartPos, textBoxSearch.Text.Length);
					richTextBox1.ScrollToCaret();
					_fSearchStart = true; //検索開始 ( Start to search )
				}
			}

		}
		//----------------------------------------------------------------------
		// 前を検索（または、置換して前へ）ボタン
		// Press previous button
		//----------------------------------------------------------------------
		private void cmdSearchPrev_Click(object sender, EventArgs e)
		{
			int StartPos;
			StringComparison sc;
			DialogResult result;
			string MsgText = "";

			if (textBoxSearch.Text != "")
			{
				//置換モードの場合は、置換してから前を検索する
				//Replace the word to search previous item in Replace mode
				if (textBoxReplace.Visible == true && _fSearchStart == true)
				{
					if (richTextBox1.SelectionLength > 0)
					{
						richTextBox1.SelectedText = textBoxReplace.Text;
					}
				}

				if (chkOptionCase.Checked == true)
				{
					sc = StringComparison.Ordinal;
				}
				else
				{	//大文字と小文字を区別しない
					//Ignore case
					sc = StringComparison.OrdinalIgnoreCase;
				}

				int CurrentPos = richTextBox1.SelectionStart - 1;
				if (CurrentPos < 0)
				{
					CurrentPos = 0;
				}
				//-----------------------------------
				// 検索ワードが見つからない
				// Searching word is not found
				//-----------------------------------
				if ((StartPos = richTextBox1.Text.LastIndexOf(textBoxSearch.Text, CurrentPos, sc)) == -1)
				{
					//検索を開始した直後
					//Start to search after
					if (_fSearchStart == false)
					{
						//"ファイル先頭まで検索しましたが、見つかりませんでした。
						// ファイルの末尾から検索を続けますか？"
						//"The word could not find to the beginning of this file.
						// Do you wish to continue searching from the end of this file?"
						MsgText = Resources.MsgNotFoundToBegining;
						_fSearchStart = true;
					}
					else
					{
						//"ファイル先頭までの検索が完了しました。
						// ファイル末尾から検索を続けますか？"
						//"Searching completed to the beginning of this file.
						// Do you wish to continue searching from the end of this file?"
						MsgText = Resources.MsgFindCompleteToBegining;
					}

					result = MessageBox.Show(MsgText, Resources.DialogTitleNotice,
						MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);

					if (result == DialogResult.Yes)
					{
						richTextBox1.SelectionStart = richTextBox1.Text.Length - 1;
						cmdSearchPrev_Click(sender, e);
					}
				}
				//-----------------------------------
				// 検索ワードが見つかった
				// Searching word were found
				//-----------------------------------
				else
				{
					//richTextBox1.HideSelection = false;
					richTextBox1.Select(StartPos, textBoxSearch.Text.Length);
					richTextBox1.ScrollToCaret();
					_fSearchStart = true; //検索開始 ( Start to search )
				}
			}

		}

		//-----------------------------------
		// すべてを置換ボタン
		// "Replace All" button
		//-----------------------------------
		private void cmdReplaceAll_Click(object sender, EventArgs e)
		{
			int StartPos;
			StringComparison sc;
			string MsgText = "";

			if (chkOptionCase.Checked == true)
			{
				sc = StringComparison.Ordinal;
			}
			else
			{	//大文字と小文字を区別しない
				//Ignore case
				sc = StringComparison.OrdinalIgnoreCase;
			}

			int CurrentPos = 0;
			int ReplaceCount = 0;
			while ((StartPos = richTextBox1.Text.IndexOf(textBoxSearch.Text, CurrentPos, sc)) > -1)
			{
				richTextBox1.Select(StartPos, textBoxSearch.Text.Length);
				richTextBox1.ScrollToCaret();
				if (richTextBox1.SelectionLength > 0)
				{
					richTextBox1.SelectedText = textBoxReplace.Text;
					ReplaceCount++;
					CurrentPos = StartPos + textBoxReplace.Text.Length;
				}
			}

			if (ReplaceCount > 0)
			{
				//"以下のワードを" ～ "件置換しました。\n"
				//"" ～ " of the word were replaced.\n"
				MsgText = Resources.MsgThisWord + "\"" + ReplaceCount.ToString() + "\"" + Resources.MsgReplaced + "\n" +
									textBoxSearch.Text + " -> " + textBoxReplace.Text;
			}
			else
			{
				//"ご指定の検索ワードは見つかりませんでした。"
				//"The word was not found."
				MsgText = Resources.MsgNotFound;
			}

			MessageBox.Show(MsgText, Resources.DialogTitleNotice, MessageBoxButtons.OK, MessageBoxIcon.Information);
			_fSearchStart = true;

		}

		#endregion

		//======================================================================
		#region エンコーディングの判定 ( Detecting encoding )
		//======================================================================
		//
		// ここのコードはまんま、以下のサイトのものを使わせていただきました。
		// http://dobon.net/vb/dotnet/string/detectcode.html
		//
		// <summary>
		// 文字コードを判別する
		// </summary>
		// <remarks>
		// Jcode.pmのgetcodeメソッドを移植したものです。
		// Jcode.pm(http://openlab.ring.gr.jp/Jcode/index-j.html)
		// Jcode.pmのCopyright: Copyright 1999-2005 Dan Kogai
		// </remarks>
		// <param name="bytes">文字コードを調べるデータ</param>
		// <returns>適当と思われるEncodingオブジェクト。
		// 判断できなかった時はnull。</returns>
		public static Encoding GetCode(byte[] bytes)
		{
			const byte bEscape = 0x1B;
			const byte bAt = 0x40;
			const byte bDollar = 0x24;
			const byte bAnd = 0x26;
			const byte bOpen = 0x28;	//'('
			const byte bB = 0x42;
			const byte bD = 0x44;
			const byte bJ = 0x4A;
			const byte bI = 0x49;

			int len = bytes.Length;
			byte b1, b2, b3, b4;

			//Encode::is_utf8 は無視

			bool isBinary = false;
			for (int i = 0; i < len; i++)
			{
				b1 = bytes[i];
				if (b1 <= 0x06 || b1 == 0x7F || b1 == 0xFF)
				{
					//'binary'
					isBinary = true;
					if (b1 == 0x00 && i < len - 1 && bytes[i + 1] <= 0x7F)
					{
						//smells like raw unicode
						return Encoding.Unicode;
					}
				}
			}
			if (isBinary)
			{
				return null;
			}

			//not Japanese
			bool notJapanese = true;
			for (int i = 0; i < len; i++)
			{
				b1 = bytes[i];
				if (b1 == bEscape || 0x80 <= b1)
				{
					notJapanese = false;
					break;
				}
			}
			if (notJapanese)
			{
				return Encoding.ASCII;
			}

			for (int i = 0; i < len - 2; i++)
			{
				b1 = bytes[i];
				b2 = bytes[i + 1];
				b3 = bytes[i + 2];

				if (b1 == bEscape)
				{
					if (b2 == bDollar && b3 == bAt)
					{
						//JIS_0208 1978
						//JIS
						return Encoding.GetEncoding(50220);
					}
					else if (b2 == bDollar && b3 == bB)
					{
						//JIS_0208 1983
						//JIS
						return Encoding.GetEncoding(50220);
					}
					else if (b2 == bOpen && (b3 == bB || b3 == bJ))
					{
						//JIS_ASC
						//JIS
						return Encoding.GetEncoding(50220);
					}
					else if (b2 == bOpen && b3 == bI)
					{
						//JIS_KANA
						//JIS
						return Encoding.GetEncoding(50220);
					}
					if (i < len - 3)
					{
						b4 = bytes[i + 3];
						if (b2 == bDollar && b3 == bOpen && b4 == bD)
						{
							//JIS_0212
							//JIS
							return Encoding.GetEncoding(50220);
						}
						if (i < len - 5 &&
							b2 == bAnd && b3 == bAt && b4 == bEscape &&
							bytes[i + 4] == bDollar && bytes[i + 5] == bB)
						{
							//JIS_0208 1990
							//JIS
							return Encoding.GetEncoding(50220);
						}
					}
				}
			}

			//should be euc|sjis|utf8
			//use of (?:) by Hiroki Ohzaki <ohzaki@iod.ricoh.co.jp>
			int sjis = 0;
			int euc = 0;
			int utf8 = 0;
			for (int i = 0; i < len - 1; i++)
			{
				b1 = bytes[i];
				b2 = bytes[i + 1];
				if (((0x81 <= b1 && b1 <= 0x9F) || (0xE0 <= b1 && b1 <= 0xFC)) &&
					((0x40 <= b2 && b2 <= 0x7E) || (0x80 <= b2 && b2 <= 0xFC)))
				{
					//SJIS_C
					sjis += 2;
					i++;
				}
			}
			for (int i = 0; i < len - 1; i++)
			{
				b1 = bytes[i];
				b2 = bytes[i + 1];
				if (((0xA1 <= b1 && b1 <= 0xFE) && (0xA1 <= b2 && b2 <= 0xFE)) ||
					(b1 == 0x8E && (0xA1 <= b2 && b2 <= 0xDF)))
				{
					//EUC_C
					//EUC_KANA
					euc += 2;
					i++;
				}
				else if (i < len - 2)
				{
					b3 = bytes[i + 2];
					if (b1 == 0x8F && (0xA1 <= b2 && b2 <= 0xFE) &&
						(0xA1 <= b3 && b3 <= 0xFE))
					{
						//EUC_0212
						euc += 3;
						i += 2;
					}
				}
			}
			for (int i = 0; i < len - 1; i++)
			{
				b1 = bytes[i];
				b2 = bytes[i + 1];
				if ((0xC0 <= b1 && b1 <= 0xDF) && (0x80 <= b2 && b2 <= 0xBF))
				{
					//UTF8
					utf8 += 2;
					i++;
				}
				else if (i < len - 2)
				{
					b3 = bytes[i + 2];
					if ((0xE0 <= b1 && b1 <= 0xEF) && (0x80 <= b2 && b2 <= 0xBF) &&
						(0x80 <= b3 && b3 <= 0xBF))
					{
						//UTF8
						utf8 += 3;
						i += 2;
					}
				}
			}
			//M. Takahashi's suggestion
			//utf8 += utf8 / 2;

			System.Diagnostics.Debug.WriteLine(
				string.Format("sjis = {0}, euc = {1}, utf8 = {2}", sjis, euc, utf8));
			if (euc > sjis && euc > utf8)
			{
				//EUC
				return Encoding.GetEncoding(51932);
			}
			else if (sjis > euc && sjis > utf8)
			{
				//SJIS
				return Encoding.GetEncoding(932);
			}
			else if (utf8 > euc && utf8 > sjis)
			{
				//UTF8
				return Encoding.UTF8;
			}

			return null;
		}

		#endregion

		//======================================================================
		#region WebBrowserコンポーネントのカチカチ音制御

		/*
		// 以下のサイトのエントリーを参考にさせていただきました。
		// http://www.moonmile.net/blog/archives/1465

		private string keyCurrent = @"AppEvents\Schemes\Apps\Explorer\Navigating\.Current";
		private string keyDefault = @"AppEvents\Schemes\Apps\Explorer\Navigating\.Default";

		// <summary>
		// クリック音をON
		// </summary>
		// <param name="sender"></param>
		// <param name="e"></param>
		private void WebBrowserClickSoundON()
		{

			//===================================
			// Win8でKeyの取得ができないときがある？
			//===================================

			// .Defaultの値を読み込んで、.Currentに書き込み
			Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser;
			key = key.OpenSubKey(keyDefault);
			string data = (string)key.GetValue(null);
			key.Close();

			key = Microsoft.Win32.Registry.CurrentUser;
			key = key.OpenSubKey(keyCurrent, true);
			key.SetValue(null, data);
			key.Close();

		}
		// <summary>
		// クリック音をOFF
		// </summary>
		// <param name="sender"></param>
		// <param name="e"></param>
		private void WebBrowserClickSoundOFF()
		{
			// .Currnetを @"" にする。
			Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser;
			key = key.OpenSubKey(keyCurrent, true);
			key.SetValue(null, "");
			key.Close();
		}

		*/
		#endregion


	}

}
