﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Reflection;
using Microsoft.Win32;
using System.Runtime.ExceptionServices;
using System.Drawing;
//using System.Linq;

using Au.Types;
using static Au.NoClass;
using Au.Util;

#pragma warning disable 649 //unused fields in API structs

//FUTURE: ShowCheckList

namespace Au
{
	/// <summary>
	/// Standard dialogs to show information or get user input.
	/// </summary>
	/// <remarks>
	/// You can use static functions (less code) or create class instances (more options).
	/// More info: <see cref="ShowEx"/>.
	/// 
	/// Uses task dialog API <msdn>TaskDialogIndirect</msdn>.
	/// 
	/// Cannot be used in services. Instead use <b>MessageBox.Show</b> with option ServiceNotification or DefaultDesktopOnly, or API <msdn>MessageBox</msdn> with corresponding flags.
	/// </remarks>
	/// <example>
	/// This example creates a class instance, sets properties, shows dialog, uses events, uses result.
	/// <code><![CDATA[
	/// var d = new ADialog(); //info: another constructor has the same parameters as ShowEx
	/// d.SetText("Main text.", "More text.\nSupports &lt;A HREF=\"link data\"&gt;links&lt;/A&gt; if you subscribe to HyperlinkClicked event.");
	/// d.SetButtons("1 OK|2 Cancel|3 Custom|4 Custom2");
	/// d.SetIcon(DIcon.Warning);
	/// d.SetExpandedText("Expanded info\nand more info.", true);
	/// d.FlagCanBeMinimized = true;
	/// d.SetRadioButtons("1 r1|2 r2");
	/// d.SetCheckbox("Check");
	/// d.SetTimeout(30, "OK");
	/// d.HyperlinkClicked += e =&gt; { ADialog.Show("link clicked", e.LinkHref, owner: e.hwnd); };
	/// d.ButtonClicked += e =&gt; { Print(e.Button); if(e.Button == 4) e.DoNotCloseDialog = true; };
	/// d.FlagShowProgressBar = true; d.Timer += e =&gt; { e.dialog.Send.Progress(e.TimerTimeMS / 100); };
	/// var r = d.ShowDialog();
	/// Print(r);
	/// switch(r.Button) { case 1: Print("OK"); break; case DResult.Timeout: Print("timeout"); break; }
	/// ]]></code>
	/// </example>
	//[DebuggerStepThrough]
	public class ADialog
	{
		#region API
		#region private API

		//[DllImport("comctl32.dll")]
		//static extern int TaskDialogIndirect(in TASKDIALOGCONFIG c, out int pnButton, out int pnRadioButton, out int pChecked);
		delegate int _tTaskDialogIndirect(in TASKDIALOGCONFIG c, out int pnButton, out int pnRadioButton, out int pChecked);
		static readonly _tTaskDialogIndirect TaskDialogIndirect = _GetTaskDialogIndirect();

		static _tTaskDialogIndirect _GetTaskDialogIndirect()
		{
			//Activate manifest that tells to use comctl32.dll version 6. The API is unavailable in version 5.
			//Need this if the host app does not have such manifest, eg if uses the default manifest added by Visual Studio.
			using(LibActCtx.Activate()) {
				//Also, don't use DllImport, because it uses v5 comctl32.dll if it is already loaded.
				Api.GetDelegate(out _tTaskDialogIndirect R, "comctl32.dll", "TaskDialogIndirect");
				return R;
			}
		}

		//TASKDIALOGCONFIG flags.
		[Flags]
		enum TDF_
		{
			ENABLE_HYPERLINKS = 0x0001,
			USE_HICON_MAIN = 0x0002,
			USE_HICON_FOOTER = 0x0004,
			ALLOW_DIALOG_CANCELLATION = 0x0008,
			USE_COMMAND_LINKS = 0x0010,
			USE_COMMAND_LINKS_NO_ICON = 0x0020,
			EXPAND_FOOTER_AREA = 0x0040,
			EXPANDED_BY_DEFAULT = 0x0080,
			VERIFICATION_FLAG_CHECKED = 0x0100,
			SHOW_PROGRESS_BAR = 0x0200,
			SHOW_MARQUEE_PROGRESS_BAR = 0x0400,
			CALLBACK_TIMER = 0x0800,
			POSITION_RELATIVE_TO_WINDOW = 0x1000,
			RTL_LAYOUT = 0x2000,
			NO_DEFAULT_RADIO_BUTTON = 0x4000,
			CAN_BE_MINIMIZED = 0x8000,
			//NO_SET_FOREGROUND = 0x00010000, //Win8, does not work
			SIZE_TO_CONTENT = 0x1000000,
		}

		//TASKDIALOGCONFIG buttons.
		[Flags]
		enum TDCBF_
		{
			OK = 1, Yes = 2, No = 4, Cancel = 8, Retry = 0x10, Close = 0x20,
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		unsafe struct TASKDIALOG_BUTTON
		{
			public int id;
			public char* text;
		}

		delegate int TaskDialogCallbackProc(Wnd hwnd, Native.TDN notification, LPARAM wParam, LPARAM lParam, IntPtr data);

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		unsafe struct TASKDIALOGCONFIG
		{
			public int cbSize;
			public Wnd hwndParent;
			public IntPtr hInstance;
			public TDF_ dwFlags;
			public TDCBF_ dwCommonButtons;
			public string pszWindowTitle;
			public IntPtr hMainIcon;
			public string pszMainInstruction;
			public string pszContent;
			public int cButtons;
			public TASKDIALOG_BUTTON* pButtons;
			public int nDefaultButton;
			public int cRadioButtons;
			public TASKDIALOG_BUTTON* pRadioButtons;
			public int nDefaultRadioButton;
			public string pszVerificationText;
			public string pszExpandedInformation;
			public string pszExpandedControlText;
			public string pszCollapsedControlText;
			public IntPtr hFooterIcon;
			public string pszFooter;
			public TaskDialogCallbackProc pfCallback;
			public IntPtr lpCallbackData;
			public int cxWidth;
		}

		#endregion private API
		#endregion API

		#region static options

		/// <summary>
		/// Default options used by <see cref="ADialog"/> class functions.
		/// </summary>
		public static class Options
		{
			/// <summary>
			/// Default title bar text.
			/// Default value - <see cref="ATask.Name"/>. In exe it is exe file name like "Example.exe".
			/// </summary>
			public static string DefaultTitle
			{
				get => _defaultTitle ?? ATask.Name;
				set { _defaultTitle = value; }
			}
			static string _defaultTitle;
			//consider: use [assembly: AssemblyTitle("...")]. var a=Assembly.GetEntryAssembly(); But exception if appdomain runs with DoCallBack().

			/// <summary>
			/// Right-to-left layout.
			/// </summary>
			/// <seealso cref="FlagRtlLayout"/>
			public static bool RtlLayout { get; set; }

			/// <summary>
			/// If there is no owner window, let the dialog be always on top of most other windows.
			/// </summary>
			/// <seealso cref="FlagTopmost"/>
			public static bool TopmostIfNoOwnerWindow { get; set; }

			/// <summary>
			/// Show dialogs on this screen when screen is not explicitly specified (<see cref="Screen"/>) and there is no owner window.
			/// If screen index is invalid, the 'show' method shows warning, no exception.
			/// </summary>
			public static AScreen DefaultScreen { get; set; }
			//SHOULDDO: check invalid index now

			/// <summary>
			/// If icon not specified, use <see cref="DIcon.App"/>.
			/// </summary>
			public static bool UseAppIcon { get; set; }

			/// <summary>
			/// If owner window not specified, use the active window of current thread as owner window (disable it, etc).
			/// </summary>
			/// <seealso cref="SetOwnerWindow"/>
			public static bool AutoOwnerWindow { get; set; }
		}

		#endregion static options

		TASKDIALOGCONFIG _c;

		///
		public ADialog()
		{
			_c.cbSize = Api.SizeOf(_c);
			FlagRtlLayout = Options.RtlLayout;
		}

		/// <summary>
		/// Initializes a new <see cref="ADialog"/> instance and sets main properties.
		/// Parameters etc are of <see cref="ShowEx"/>.
		/// </summary>
		public ADialog(
			string text1 = null, string text2 = null, string buttons = null, DFlags flags = 0, DIcon icon = 0, AnyWnd owner = default,
			string expandedText = null, string footerText = null, string title = null, string radioButtons = null, string checkBox = null,
			int defaultButton = 0, Coord x = default, Coord y = default, int secondsTimeout = 0, Action<DEventArgs> onLinkClick = null
			) : this()
		{
			FlagEndThread = 0 != (flags & DFlags.EndThread);
			if(0 != (flags & DFlags.Topmost)) FlagTopmost = true; //else use Options.TopmostIfNoOwnerWindow if no owner
			FlagXCancel = 0 != (flags & DFlags.XCancel);
			if(0 != (flags & DFlags.Wider)) Width = 700;
			//FlagKeyboardShortcutsVisible=0 != (flags&DFlags.KeyboardShortcutsVisible);

			SetText(text1, text2);
			SetIcon(icon);
			SetButtons(buttons, 0 != (flags & DFlags.CommandLinks));
			if(defaultButton != 0) DefaultButton = defaultButton;
			SetRadioButtons(radioButtons);
			_SetCheckboxFromText(checkBox);
			SetOwnerWindow(owner, 0 != (flags & DFlags.OwnerCenter));
			SetXY(x, y, 0 != (flags & DFlags.RawXY));
			SetTimeout(secondsTimeout);
			SetExpandedText(expandedText, 0 != (flags & DFlags.ExpandDown));
			SetFooterText(footerText);
			SetTitleBarText(title);
			if(onLinkClick != null) HyperlinkClicked += onLinkClick;
		}

		#region set properties

		void _SetFlag(TDF_ flag, bool on)
		{
			if(on) _c.dwFlags |= flag; else _c.dwFlags &= ~flag;
		}

		bool _HasFlag(TDF_ flag)
		{
			return (_c.dwFlags & flag) != 0;
		}

		/// <summary>
		/// Changes title bar text.
		/// If you don't call this method or title is null or "", dialogs will use <see cref="Options.DefaultTitle"/>.
		/// </summary>
		public void SetTitleBarText(string title)
		{
			_c.pszWindowTitle = Empty(title) ? Options.DefaultTitle : title;
			//info: if "", API uses "ProcessName.exe".
		}

		/// <summary>
		/// Sets text.
		/// </summary>
		/// <param name="text1">Main instruction. Bigger font.</param>
		/// <param name="text2">Text below main instruction.</param>
		public void SetText(string text1 = null, string text2 = null)
		{
			_c.pszMainInstruction = text1;
			_c.pszContent = text2;
		}

		/// <summary>
		/// Sets common icon.
		/// </summary>
		/// <param name="icon"></param>
		public void SetIcon(DIcon icon)
		{
			_c.hMainIcon = (IntPtr)(int)icon;
			_SetFlag(TDF_.USE_HICON_MAIN, false);
		}

		/// <summary>
		/// Sets custom icon.
		/// </summary>
		/// <param name="icon">
		/// Icon of size 32 or 16.
		/// Don't dispose it until the dialog is closed.
		/// </param>
		public void SetIcon(Icon icon)
		{
			_iconGC = icon; //keep from GC
			_c.hMainIcon = (icon == null) ? default : icon.Handle;
			_SetFlag(TDF_.USE_HICON_MAIN, _c.hMainIcon != default);
			//tested: displays original-size 32 and 16 icons, but shrinks bigger icons to 32.
			//note: for App icon ShowDialog will execute more code. The same for footer icon.
		}
		Icon _iconGC; //keep from GC

		#region buttons

		const int _idOK = 1;
		const int _idCancel = 2;
		const int _idRetry = 4;
		const int _idYes = 6;
		const int _idNo = 7;
		const int _idClose = 8;
		const int _idTimeout = int.MinValue;

		_Buttons _buttons;

		struct _Buttons
		{
			struct _Button
			{
				internal int id;
				internal string s;

				internal _Button(int id, string s) { this.id = id; this.s = s; }
			}
			List<_Button> _customButtons, _radioButtons;

			int _defaultButtonUserId;
			bool _isDefaultButtonSet;
			internal int DefaultButtonUserId { get => _defaultButtonUserId; set { _defaultButtonUserId = value; _isDefaultButtonSet = true; } }

			bool _hasXButton;

			internal TDCBF_ SetButtons(string buttons, DStringList customButtons)
			{
				_customButtons = null;
				_mapIdUserNative = null;
				_defaultButtonUserId = 0;
				_isDefaultButtonSet = false;

				switch(customButtons.Value) {
				case null: break;
				case string s:
					_ParseStringList(s, true);
					break;
				case IEnumerable<string> e:
					int id = 0;
					foreach(var v in e) {
						if(_customButtons == null) _customButtons = new List<_Button>();
						string s = _ParseSingleString(v, ref id, true);
						_customButtons.Add(new _Button(id, s));
						DefaultButtonUserId = 1;
					}
					break;
				}

				return _ParseStringList(buttons, false);
			}

			TDCBF_ _ParseStringList(string b, bool onlyCustom)
			{
				if(Empty(b)) return 0;

				TDCBF_ commonButtons = 0;
				int id = 0, nextNativeId = 100;

				foreach(var v in b.SegSplit("|")) {
					string s = _ParseSingleString(v, ref id, onlyCustom);

					int nativeId = 0;
					if(!onlyCustom) {
						switch(s) {
						case "OK": commonButtons |= TDCBF_.OK; nativeId = _idOK; break;
						case "Yes": commonButtons |= TDCBF_.Yes; nativeId = _idYes; break;
						case "No": commonButtons |= TDCBF_.No; nativeId = _idNo; break;
						case "Cancel": commonButtons |= TDCBF_.Cancel; nativeId = _idCancel; break;
						case "Retry": commonButtons |= TDCBF_.Retry; nativeId = _idRetry; break;
						case "Close": commonButtons |= TDCBF_.Close; nativeId = _idClose; break;
						}
					}

					if(nativeId == 0) { //custom button
						if(_customButtons == null) _customButtons = new List<_Button>();
						_customButtons.Add(new _Button(id, s));
						if(id < 0) nativeId = nextNativeId++; //need to map, because native ids of positive user ids are minus user ids
					}
					if(nativeId != 0) {
						if(_mapIdUserNative == null) _mapIdUserNative = new List<_IdMapItem>();
						_mapIdUserNative.Add(new _IdMapItem(id, nativeId));
					}

					if(!_isDefaultButtonSet) DefaultButtonUserId = id;
				}

				return commonButtons;
			}

			internal void SetRadioButtons(string buttons)
			{
				_radioButtons = null;
				if(Empty(buttons)) return;

				_radioButtons = new List<_Button>();
				int id = 0;
				foreach(var v in buttons.SegSplit("|")) {
					string s = _ParseSingleString(v, ref id, false);
					_radioButtons.Add(new _Button(id, s));
				}
			}

			static string _ParseSingleString(string s, ref int id, bool dontSplit)
			{
				if(!dontSplit && ExtString.More.ParseIntAndString(s, out var i, out string r)) id = i; else { r = s; id++; }
				r = r.TrimChars("\r\n"); //API does not like newline at start, etc
				if(r.Length == 0) r = " "; //else API exception
				else r = r.Replace("\r\n", "\n"); //API adds 2 newlines for \r\n. Only for custom buttons, not for other controls/parts.
				return r;
			}

			struct _IdMapItem
			{
				internal int userId, nativeId;

				internal _IdMapItem(int userId, int nativeId) { this.userId = userId; this.nativeId = nativeId; }
			}

			List<_IdMapItem> _mapIdUserNative;

			internal int MapIdUserToNative(int userId)
			{
				if(userId == _idTimeout) return userId; //0x80000000
				if(_mapIdUserNative != null) { //common buttons, and custom buttons with negative user id
					foreach(var v in _mapIdUserNative) if(v.userId == userId) return v.nativeId;
				}
				return -userId; //custom button with positive user id
			}

			internal int MapIdNativeToUser(int nativeId)
			{
				if(nativeId == _idTimeout) return nativeId; //0x80000000
				if(nativeId <= 0) return -nativeId; //custom button with positive user id
				if(_mapIdUserNative != null) { //common buttons, and custom buttons with negative user id
					foreach(var v in _mapIdUserNative) if(v.nativeId == nativeId) return v.userId;
				}
				if(nativeId == _idOK) return nativeId; //single OK button auto-added when no buttons specified
				Debug.Assert(nativeId == _idCancel && _hasXButton);
				return 0;
			}

			/// <summary>
			/// Sets c.pButtons, c.cButtons, c.pRadioButtons and c.cRadioButtons.
			/// Later call MarshalFreeButtons.
			/// </summary>
			internal unsafe void MarshalButtons(ref TASKDIALOGCONFIG c)
			{
				c.pButtons = _MarshalButtons(false, out c.cButtons);
				c.pRadioButtons = _MarshalButtons(true, out c.cRadioButtons);

				_hasXButton = ((c.dwFlags & TDF_.ALLOW_DIALOG_CANCELLATION) != 0);
			}

			/// <summary>
			/// Frees memory allocated by MarshalButtons and sets the c members to null/0.
			/// </summary>
			internal unsafe void MarshalFreeButtons(ref TASKDIALOGCONFIG c)
			{
				AMemory.Free(c.pButtons);
				AMemory.Free(c.pRadioButtons);
				c.pButtons = null; c.pRadioButtons = null;
				c.cButtons = 0; c.cRadioButtons = 0;
			}

			unsafe TASKDIALOG_BUTTON* _MarshalButtons(bool radio, out int nButtons)
			{
				var a = radio ? _radioButtons : _customButtons;
				int n = a == null ? 0 : a.Count;
				nButtons = n;
				if(n == 0) return null;
				int nba = n * sizeof(TASKDIALOG_BUTTON), nb = nba;
				foreach(var v in a) nb += (v.s.Length + 1) * 2;
				var r = (TASKDIALOG_BUTTON*)AMemory.Alloc(nb);
				char* s = (char*)((byte*)r + nba);
				for(int i = 0; i < n; i++) {
					var v = a[i];
					r[i].id = radio ? v.id : MapIdUserToNative(v.id);
					int len = v.s.Length + 1;
					r[i].text = Api.lstrcpyn(s, v.s, len);
					s += len;
				}
				return r;
			}
		}

		/// <summary>
		/// Sets common and/or custom buttons and custom buttons style.
		/// </summary>
		/// <param name="buttons">
		/// Common and/or custom buttons, like with <see cref="Show"/>.
		/// These ids should be negative if you use <i>customButtons</i> too, because ids of <i>customButtons</i> are 1, 2, ... .
		/// </param>
		/// <param name="asCommandLinks">Custom buttons style. If false - row of classic buttons. If true - column of command-link buttons that can have multiline text.</param>
		/// <param name="customButtons">
		/// Additional custom buttons. All will be custom, even if named "OK" etc.
		/// List of labels without ids. Can be string like "One|Two|..." or string[] or List&lt;string&gt;.
		/// Button ids will be 1, 2, ... .
		/// <see cref="DefaultButton"/> will be 1. You can change it later.
		/// </param>
		public void SetButtons(string buttons, bool asCommandLinks = false, DStringList customButtons = default)
		{
			_c.dwCommonButtons = _buttons.SetButtons(buttons, customButtons);
			_SetFlag(TDF_.USE_COMMAND_LINKS, asCommandLinks);
		}

		/// <summary>
		/// Specifies which button responds to the Enter key.
		/// If 0 or not set, auto-selects.
		/// </summary>
		/// <value>Button id.</value>
		public int DefaultButton { set { _c.nDefaultButton = _buttons.MapIdUserToNative(value); } }

		/// <summary>
		/// Adds radio buttons.
		/// To get selected radio button id after closing the dialog, use the RadioButton property of the <see cref="DResult"/> variable returned by <see cref="ShowDialog"/> or the <see cref="Result"/> property.
		/// </summary>
		/// <param name="buttons">A list of strings "id text" separated by |, like "1 One|2 Two|3 Three".</param>
		/// <param name="defaultId">Check the radio button that has this id. If omitted or 0, checks the first. If negative, does not check.</param>
		public void SetRadioButtons(string buttons, int defaultId = 0)
		{
			//_radioButtonsStr = buttons;
			_buttons.SetRadioButtons(buttons);
			_c.nDefaultRadioButton = defaultId;
			_SetFlag(TDF_.NO_DEFAULT_RADIO_BUTTON, defaultId < 0);
		}

		#endregion buttons

		/// <summary>
		/// Adds check box (if text is not null/empty).
		/// To get check box state after closing the dialog, use the IsChecked property of the <see cref="DResult"/> variable returned by <see cref="ShowDialog"/> or the <see cref="Result"/> property.
		/// </summary>
		public void SetCheckbox(string text, bool check = false)
		{
			_c.pszVerificationText = text;
			_SetFlag(TDF_.VERIFICATION_FLAG_CHECKED, check);
		}

		//Parses "Text|check" etc and calls SetCheckbox.
		void _SetCheckboxFromText(string checkBox)
		{
			string text = null; bool check = false;
			if(!Empty(checkBox)) {
				string[] a = checkBox.SegSplit("|", 2);
				text = a[0];
				if(a.Length == 2) switch(a[1]) { case "true": case "check": case "checked": check = true; break; }
			}
			SetCheckbox(text, check);
		}

		/// <summary>
		/// Adds text that the user can show and hide.
		/// </summary>
		/// <param name="text">Text.</param>
		/// <param name="showInFooter">Show the text at the bottom of the dialog.</param>
		public void SetExpandedText(string text, bool showInFooter = false)
		{
			if(Empty(text)) { text = null; showInFooter = false; }
			_SetFlag(TDF_.EXPAND_FOOTER_AREA, showInFooter);
			_c.pszExpandedInformation = text;
		}

		/// <summary>
		/// Set properties of the control that shows and hides text added by <see cref="SetExpandedText"/>.
		/// </summary>
		/// <param name="defaultExpanded"></param>
		/// <param name="collapsedText"></param>
		/// <param name="expandedText"></param>
		public void SetExpandControl(bool defaultExpanded, string collapsedText = null, string expandedText = null)
		{
			_SetFlag(TDF_.EXPANDED_BY_DEFAULT, defaultExpanded);
			_c.pszCollapsedControlText = collapsedText;
			_c.pszExpandedControlText = expandedText;
		}

		/// <summary>
		/// Adds text and common icon at the bottom of the dialog.
		/// </summary>
		/// <param name="text">Text, optionally preceded by an icon character and |, like "i|Text". Icons: x error, ! warning, i info, v shield, a app.</param>
		public void SetFooterText(string text)
		{
			DIcon icon = 0;
			if(text != null && text.Length >= 2 && text[1] == '|') {
				switch(text[0]) {
				case 'x': icon = DIcon.Error; break;
				case '!': icon = DIcon.Warning; break;
				case 'i': icon = DIcon.Info; break;
				case 'v': icon = DIcon.Shield; break;
				case 'a': icon = DIcon.App; break;
				}
				text = text.Substring(2);
			}
			SetFooterText(text, icon);
		}
		/// <summary>
		/// Adds text and common icon at the bottom of the dialog.
		/// </summary>
		/// <param name="text">Text.</param>
		/// <param name="icon"></param>
		public void SetFooterText(string text, DIcon icon)
		{
			_c.pszFooter = text;
			_c.hFooterIcon = (IntPtr)(int)icon;
			_SetFlag(TDF_.USE_HICON_FOOTER, false);
		}
		/// <summary>
		/// Adds text and custom icon at the bottom of the dialog.
		/// </summary>
		/// <param name="text">Text.</param>
		/// <param name="icon">Icon of size 16. Read more: <see cref="SetIcon(Icon)"/>.</param>
		public void SetFooterText(string text, Icon icon)
		{
			_c.pszFooter = text;
			_iconFooterGC = icon; //keep from GC
			_c.hFooterIcon = (icon == null) ? default : icon.Handle;
			_SetFlag(TDF_.USE_HICON_FOOTER, _c.hFooterIcon != default);
		}
		Icon _iconFooterGC; //keep from GC

		/// <summary>
		/// Adds Edit or Combo control (if editType is not DEdit.None (0)).
		/// To get its text after closing the dialog, use the EditText property of the <see cref="DResult"/> variable returned by <see cref="ShowDialog"/> or the <see cref="Result"/> property.
		/// </summary>
		/// <param name="editType">Control type/style.</param>
		/// <param name="editText">Initial text. If editType is DEdit.Combo, it can be a string array, List or IEnumerable; the first item sets combo-box editable text, other items - combo box drop-down list items.</param>
		/// <remarks>
		/// The API TaskDialogIndirect does not have an option to add an edit control. This class itself creates it.
		/// Does not support progress bar.
		/// </remarks>
		public void SetEditControl(DEdit editType, object editText = null)
		{
			_editType = editType;
			_editText = editText;
			//will set other props later, because need to override user-set props
		}
		DEdit _editType; object _editText;

		/// <summary>
		/// Sets the width of the dialog's client area.
		/// The actual width will depend on DPI (the Windows "text size" setting).
		/// If less than default width, will be used default width.
		/// </summary>
		/// <seealso cref="DFlags.Wider"/>
		public int Width { set { _c.cxWidth = value / 2; } }

		/// <summary>
		/// Sets owner window.
		/// The owner window will be disabled, and this dialog will be on top of it.
		/// This window will be in owner's screen, if screen was not explicitly specified with the <see cref="Screen"/> property. <see cref="ADialog.Options.DefaultScreen"/> is ignored.
		/// </summary>
		/// <param name="owner">Owner window, or one of its child/descendant controls. Can be Control (eg Form) or Wnd (window handle). Can be null.</param>
		/// <param name="ownerCenter">Show the dialog in the center of the owner window. <see cref="SetXY"/> and <see cref="Screen"/> are ignored.</param>
		/// <param name="doNotDisable">Don't disable the owner window. If false, disables if it belongs to this thread.</param>
		/// <seealso cref="Options.AutoOwnerWindow"/>
		public void SetOwnerWindow(AnyWnd owner, bool ownerCenter = false, bool doNotDisable = false)
		{
			_c.hwndParent = owner.IsEmpty ? default : owner.Wnd.Window;
			_SetFlag(TDF_.POSITION_RELATIVE_TO_WINDOW, ownerCenter);
			_enableOwner = doNotDisable;
		}
		bool _enableOwner;

		/// <summary>
		/// Sets dialog position in screen.
		/// </summary>
		/// <param name="x">X position in <see cref="Screen"/>. If default(Coord) - screen center. You also can use <see cref="Coord.Reverse"/> etc.</param>
		/// <param name="y">Y position in <see cref="Screen"/>. If default(Coord) - screen center. You also can use <see cref="Coord.Reverse"/> etc.</param>
		/// <param name="rawXY">x y are relative to the primary screen (ignore <see cref="Screen"/> etc). Don't ensure that entire window is in screen.</param>
		public void SetXY(Coord x, Coord y, bool rawXY = false)
		{
			_x = x; _y = y;
			_rawXY = rawXY;
		}

		Coord _x, _y; bool _rawXY;

		/// <summary>
		/// Sets the screen (display monitor) where to show the dialog in multi-screen environment.
		/// If null or not set, will be used owner window's screen or <see cref="Options.DefaultScreen"/>.
		/// If screen index is invalid, the 'show' method shows warning, no exception.
		/// More info: <see cref="AScreen"/>, <see cref="Wnd.MoveInScreen"/>.
		/// </summary>
		public AScreen Screen { set; private get; }

		/// <summary>
		/// Let the dialog close itself after closeAfterS seconds.
		/// On timeout ShowDialog returns DResult.Timeout.
		/// Example: <c>d.SetTimeout(30, "OK");</c>
		/// </summary>
		public void SetTimeout(int closeAfterS, string timeoutActionText = null, bool noInfo = false)
		{
			_timeoutS = closeAfterS;
			_timeoutActionText = timeoutActionText;
			_timeoutNoInfo = noInfo;
		}
		int _timeoutS; bool _timeoutActive, _timeoutNoInfo; string _timeoutActionText, _timeoutFooterText;

		/// <summary>
		/// Allow to cancel even if there is no Cancel button.
		/// It adds X (Close) button to the title bar, and also allows to close the dialog with the Esc key.
		/// When the dialog is closed with the X button or Esc, the returned result button id is 0 if there is no Cancel button; else the same as when clicked the Cancel button.
		/// The same as <see cref="DFlags.XCancel"/>.
		/// </summary>
		public bool FlagXCancel { set; private get; }

		/// <summary>
		/// Right-to left layout.
		/// Default is ADialog.Options.RtlLayout.
		/// </summary>
		public bool FlagRtlLayout { set; private get; }

		/// <summary>
		/// Add 'Minimize' button to the title bar.
		/// </summary>
		public bool FlagCanBeMinimized { set; private get; }

		/// <summary>
		/// Show progress bar.
		/// </summary>
		public bool FlagShowProgressBar { set; private get; }

		/// <summary>
		/// Show progress bar that just plays an animation but does not indicate which part of the work is already done.
		/// </summary>
		public bool FlagShowMarqueeProgressBar { set; private get; }

		/// <summary>
		/// Makes the dialog window topmost or non-topmost.
		/// If true, will set topmost style when creating the dialog. If false, will not set.
		/// If null (default), the dialog will be topmost if both these are true: no owner window, ADialog.Options.TopmostIfNoOwnerWindow is true.
		/// </summary>
		public bool? FlagTopmost { set; private get; }

		/// <summary>
		/// Call <see cref="Thread.Abort()"/> if selected OK button when there are no other buttons. Also when selected Cancel, No, and on timeout.
		/// </summary>
		public bool FlagEndThread { set; private get; }

		///// <summary>
		///// Show keyboard shortcuts (underlined characters), like when you press the Alt key.
		///// Tip: to create keyboard shortcuts for custom buttons, use &amp; character, like "&amp;One|&amp;Two|T&amp;hree".
		///// </summary>
		///// <seealso cref="DFlags.KeyboardShortcutsVisible"/>
		//public bool FlagKeyboardShortcutsVisible { set; private get; }

		#endregion set properties

		Wnd _dlg;
		int _threadIdInShow;
		bool _locked;

		/// <summary>
		/// Shows the dialog.
		/// Returns selected button id and other results packed in a <see cref="DResult"/> variable.
		/// Call this method after setting text and other properties.
		/// </summary>
		/// <exception cref="Win32Exception">Failed to show dialog.</exception>
		public unsafe DResult ShowDialog()
		{
			//info: named ShowDialog, not Show, to not confuse with the static Show() which is used almost everywhere in documentation.

			_result = null;
			_isClosed = false;
			_isAppDomainEnding = false;

			SetTitleBarText(_c.pszWindowTitle); //if not set, sets default
			_EditControlInitBeforeShowDialog(); //don't reorder, must be before flags

			if(_c.hwndParent.Is0 && Options.AutoOwnerWindow) _c.hwndParent = Wnd.ThisThread.Active; //info: MessageBox.Show also does it, but it also disables all thread windows
			if(_c.hwndParent.IsAlive) {
				if(!_enableOwner && !_c.hwndParent.IsOfThisThread) _enableOwner = true;
				if(_enableOwner && !_c.hwndParent.IsEnabled(false)) _enableOwner = false;
			}

			_SetPos(true); //get screen

			_SetFlag(TDF_.SIZE_TO_CONTENT, true); //can make max 50% wider
			_SetFlag(TDF_.ALLOW_DIALOG_CANCELLATION, FlagXCancel);
			_SetFlag(TDF_.RTL_LAYOUT, FlagRtlLayout);
			_SetFlag(TDF_.CAN_BE_MINIMIZED, FlagCanBeMinimized);
			_SetFlag(TDF_.SHOW_PROGRESS_BAR, FlagShowProgressBar);
			_SetFlag(TDF_.SHOW_MARQUEE_PROGRESS_BAR, FlagShowMarqueeProgressBar);
			_SetFlag(TDF_.ENABLE_HYPERLINKS, HyperlinkClicked != null);
			_SetFlag(TDF_.CALLBACK_TIMER, (_timeoutS > 0 || Timer != null));

			_timeoutActive = false;
			if(_timeoutS > 0) {
				_timeoutActive = true;
				if(!_timeoutNoInfo) {
					_timeoutFooterText = _c.pszFooter;
					_c.pszFooter = _TimeoutFooterText(_timeoutS);
					if(_c.hFooterIcon == default) _c.hFooterIcon = (IntPtr)DIcon.Info;
				}
			}

			if(_c.hMainIcon == default && Options.UseAppIcon) SetIcon(DIcon.App);
			//if(_c.hMainIcon == (IntPtr)DIcon.App || _c.hFooterIcon == (IntPtr)DIcon.App) _c.hInstance = AModuleHandle.OfAppIcon();
			if(_c.hMainIcon == (IntPtr)DIcon.App || _c.hFooterIcon == (IntPtr)DIcon.App) _c.hInstance = AModuleHandle.OfProcessExe();
			//info: DIcon.App is IDI_APPLICATION (32512).
			//Although MSDN does not mention that IDI_APPLICATION can be used when hInstance is NULL, it works. Even works for many other undocumented system resource ids, eg 100.
			//Non-NULL hInstance is ignored for the icons specified as TD_x. It is documented and logical.
			//For App icon we could instead use icon handle, but then the small icon for the title bar and taskbar button can be distorted because shrinked from the big icon. Now extracts small icon from resources.

			_c.pfCallback = _CallbackProc;

			int rNativeButton = 0, rRadioButton = 0, rIsChecked = 0, hr = 0;
			bool hasCustomButtons = false;
			WinHook hook = null;

			try {
				_threadIdInShow = Thread.CurrentThread.ManagedThreadId;

				_buttons.MarshalButtons(ref _c);
				if(_c.pButtons == null) _SetFlag(TDF_.USE_COMMAND_LINKS | TDF_.USE_COMMAND_LINKS_NO_ICON, false); //to avoid exception
				else hasCustomButtons = true;

				if(_timeoutActive) { //Need mouse/key messages to stop countdown on click or key.
					hook = WinHook.ThreadGetMessage(_HookProc);
				}

				Wnd.Lib.EnableActivate(true);

				for(int i = 0; i < 10; i++) { //see the API bug-workaround comment below
					_LockUnlock(true); //see the API bug-workaround comment below

					hr = _CallTDI(out rNativeButton, out rRadioButton, out rIsChecked);

					//ADialog[Indirect] API bug:
					//	If called simultaneously by 2 threads, often fails and returns an unknown error code 0x800403E9.
					//Known workarounds:
					//	1. Lock. Unlock on first callback message. Now used.
					//	2. Retry. Now used only for other unexpected errors, eg out-of-memory.

					//if(hr != 0) Print("0x" + hr.ToString("X"), !_dlg.Is0);
					if(hr == 0 //succeeded
						|| hr == Api.E_INVALIDARG //will never succeed
						|| hr == unchecked((int)0x8007057A) //invalid cursor handle (custom icon disposed)
						|| !_dlg.Is0 //_dlg is set if our callback function was called; then don't retry, because the dialog was possibly shown, and only then error.
						) break;
					Thread.Sleep(30);
				}

				if(hr == 0) {
					_result = new DResult(_buttons.MapIdNativeToUser(rNativeButton), rRadioButton, rIsChecked != 0, _editText?.ToString());

					Wnd.More.WaitForAnActiveWindow();
				}
			}
			finally {
				_LockUnlock(false);

				//Normally the dialog now is destroyed and _dlg now is 0, because _SetClosed called on the destroy message.
				//But on Thread.Abort or other exception it is not called and the dialog is still alive and visible.
				//Therefore Windows shows its annoying "stopped working" UI.
				//To avoid it, destroy the dialog now. Also to avoid possible memory leaks etc.
				//However still bad if we use a timer (of ADialog API or own). We also use AAppDomain.Exit event for this.
				if(!_dlg.Is0) Api.DestroyWindow(_dlg);

				_SetClosed();
				_threadIdInShow = 0;
				hook?.Dispose();
				_buttons.MarshalFreeButtons(ref _c);
			}

			if(hr != 0) throw new Win32Exception(hr);
			if(_isAppDomainEnding) {
				//Print("closed");
				_isAppDomainEnding = false;
				//Thread.CurrentThread.Abort();
				Thread.Sleep(Timeout.Infinite); //CLR will throw ThreadAbortException
			}

			if(FlagEndThread) {
				bool endThread = false;
				switch(rNativeButton) {
				case _idCancel: case _idNo: case DResult.Timeout: endThread = true; break;
				case _idOK: endThread = (_c.dwCommonButtons == 0 || _c.dwCommonButtons == TDCBF_.OK) && !hasCustomButtons; break;
				}

				if(endThread) Thread.CurrentThread.Abort();
			}

			return _result;
		}

		[HandleProcessCorruptedStateExceptions]
		int _CallTDI(out int pnButton, out int pnRadioButton, out int pChecked)
		{
#if DEBUG
			pnButton = pnRadioButton = pChecked = 0;
			try {
#endif
			return TaskDialogIndirect(in _c, out pnButton, out pnRadioButton, out pChecked);
#if DEBUG
			}
			catch(Exception e) when(!(e is ThreadAbortException)) {
				throw new Win32Exception($"_CallTDI: {e.Message}"); //note: not just throw;, and don't add inner exception
			}

			//The API throws 'access violation' exception if some value is invalid (eg unknown flags in dwCommonButtons) or it does not like something.
			//.NET does not allow to handle such exceptions, unless we use [HandleProcessCorruptedStateExceptions] or <legacyCorruptedStateExceptionsPolicy enabled="true"/> in config file.
			//It makes dev/debug more difficult.

			//CONSIDER: don't use the API. Reinvent wheel with Form. Because:
			//	1. The API is so unreliable. Unexpected errors and even exceptions. Etc, etc.
			//	2. Has not all we need, and modifying it is so dirty. Eg adding edit control.
			//	3. Not everything is possible, eg cannot show window inactive.
			//	4. Does not auto-set enough width from text.
#endif
		}

		void _LockUnlock(bool on)
		{
			var obj = "/0p4oSiwoE+7Saqf30udQQ";
			if(on) {
				Debug.Assert(!_locked);
				_locked = false;
				Monitor.Enter(obj, ref _locked);
			} else if(_locked) {
				Monitor.Exit(obj);
				_locked = false;
			}
		}

		//Need to call this twice:
		//	1. Before showing dialog, to get AScreen. Later cannot apply AScreen.OfActiveWindow, because the dialog is the active window.
		//	2. On TDN.CREATED, to move dialog if need.
		void _SetPos(bool before)
		{
			if(before) _screen = default;
			if(_HasFlag(TDF_.POSITION_RELATIVE_TO_WINDOW)) return;
			bool isXY = !_x.IsEmpty || !_y.IsEmpty;
			if(!_rawXY) {
				if(before) {
					_screen = Screen;
					if(_screen.IsNull && _c.hwndParent.Is0) _screen = Options.DefaultScreen;
					if(!_screen.IsNull) _SP_SetScreen();
				} else if(isXY || !_screen.IsNull) _dlg.MoveInScreen(_x, _y, _screen);
			} else if(isXY && !before) _dlg.Move(_x, _y);
		}

		//Use this function to avoid loading Forms assembly when don't need (in most cases). GetScreen returns Screen, which is in Forms assembly.
		[MethodImpl(MethodImplOptions.NoInlining)]
		void _SP_SetScreen() { _screen = _screen.GetScreen(); }

		AScreen _screen;

		int _CallbackProc(Wnd w, Native.TDN message, LPARAM wParam, LPARAM lParam, IntPtr data)
		{
			Action<DEventArgs> e = null;
			int R = 0;

			//Print(message);
			switch(message) {
			case Native.TDN.DIALOG_CONSTRUCTED:
				_LockUnlock(false);
				Send = new DSend(this); //note: must be before setting _dlg, because another thread may call if(d.IsOpen) d.Send.Message(..).
				_dlg = w;

				AAppDomain.Exit += _AppDomain__Exit; //closes dialog, to avoid the annoying "stopped working" UI
				break;
			case Native.TDN.DESTROYED:
				//Print(w.IsAlive); //valid
				e = Destroyed;
				break;
			case Native.TDN.CREATED:
				if(_enableOwner) _c.hwndParent.Enable(true);
				_SetPos(false);

				bool topmost = false;
				if(FlagTopmost != null) topmost = FlagTopmost.GetValueOrDefault();
				else if(_c.hwndParent.Is0) topmost = Options.TopmostIfNoOwnerWindow;
				if(topmost) w.ZorderTopmost();

				//w.SetStyleAdd(WS.THICKFRAME); //does not work

				if(_IsEdit) _EditControlCreate();

				//if(FlagKeyboardShortcutsVisible) w.Post(Api.WM_UPDATEUISTATE, 0x30002);

				//fix API bug: dialog window is hidden if process STARTUPINFO specifies hidden window
				ATimer.After(1, () => _dlg.ShowLL(true)); //use timer because at this time still invisible always

				e = Created;
				break;
			case Native.TDN.TIMER:
				if(_timeoutActive) {
					int timeElapsed = (int)wParam / 1000;
					if(timeElapsed < _timeoutS) {
						if(!_timeoutNoInfo) Send.ChangeFooterText(_TimeoutFooterText(_timeoutS - timeElapsed - 1), false);
					} else {
						_timeoutActive = false;
						Send.Close(DResult.Timeout);
					}
				}

				e = Timer;
				break;
			case Native.TDN.BUTTON_CLICKED:
				e = ButtonClicked;
				wParam = _buttons.MapIdNativeToUser((int)wParam);
				break;
			case Native.TDN.HYPERLINK_CLICKED:
				e = HyperlinkClicked;
				break;
			case Native.TDN.HELP:
				e = HelpF1;
				break;
			default:
				e = OtherEvents;
				break;
			}

			if(_IsEdit) _EditControlOnMessage(message);

			if(e != null) {
				var ed = new DEventArgs(this, _dlg, message, wParam, lParam);
				e(ed);
				R = ed.returnValue;
			}

			if(message == Native.TDN.DESTROYED) _SetClosed();

			return R;
		}

		private void _AppDomain__Exit(object sender, EventArgs e)
		{
			if(IsOpen) {
				//Print("closing");
				_isAppDomainEnding = true; //let ShowDialog not return. It will set this = false.
				Send.Close();
				if(!_dlg.IsOfThisThread) {
					while(_isAppDomainEnding) Thread.Sleep(15); //to avoid terminating this process, wait until the API modal loop ends, only then let CLR abort the dialog thread
				}
			}
		}
		bool _isAppDomainEnding;

		/// <summary>
		/// ADialog events.
		/// Occurs when the internal <msdn>TaskDialogCallbackProc</msdn> function is called by the task dialog API.
		/// </summary>
		public event Action<DEventArgs>
			Created, Destroyed, Timer, ButtonClicked, HyperlinkClicked, HelpF1, OtherEvents;

		#region async etc

		/// <summary>
		/// Shows the dialog in new thread and returns without waiting until it is closed.
		/// </summary>
		/// <remarks>
		/// Calls <see cref="ThreadWaitForOpen"/>, therefore the dialog is already open when this function returns.
		/// More info: <see cref="ShowNoWaitEx"/>
		/// </remarks>
		/// <exception cref="AggregateException">Failed to show dialog.</exception>
		public void ShowDialogNoWait()
		{
			var t = Task.Run(() => ShowDialog());
			if(!ThreadWaitForOpen()) throw t.Exception ?? new AggregateException();
		}

		/// <summary>
		/// Gets selected button id and other results packed in a DResult variable.
		/// It is the same variable as the ShowDialog return value.
		/// If the result is still unavailable (eg the dialog still not closed):
		///		If called from the same thread that called ShowDialog, returns null.
		///		If called from another thread, waits until the dialog is closed and the return value is available.
		///		Note that ShowDialogNoWait calls ShowDialog in another thread.
		/// </summary>
		public DResult Result
		{
			get
			{
				if(!_WaitWhileInShow()) return null;
				return _result;
			}
		}
		DResult _result;

		bool _WaitWhileInShow()
		{
			if(_threadIdInShow != 0) {
				if(_threadIdInShow == Thread.CurrentThread.ManagedThreadId) return false;
				while(_threadIdInShow != 0) Thread.Sleep(15);
			}
			return true;
		}

		/// <summary>
		/// Can be used by other threads to wait until the dialog is open.
		/// If returns true, the dialog is open and you can send messages to it.
		/// If returns false, the dialog is already closed or failed to show.
		/// </summary>
		public bool ThreadWaitForOpen()
		{
			_AssertIsOtherThread();
			while(!IsOpen) {
				if(_isClosed) return false;
				Thread.Sleep(15); //need ~3 loops if 15
				Time.DoEvents(); //without it this func hangs if a form is the dialog owner
			}
			return true;
		}

		/// <summary>
		/// Can be used by other threads to wait until the dialog is closed.
		/// </summary>
		public void ThreadWaitForClosed()
		{
			_AssertIsOtherThread();
			while(!_isClosed) {
				Thread.Sleep(30);
			}
			_WaitWhileInShow();
		}

		void _AssertIsOtherThread()
		{
			if(_threadIdInShow != 0 && _threadIdInShow == Thread.CurrentThread.ManagedThreadId)
				throw new AException("wrong thread");
		}

		/// <summary>
		/// Returns true if the dialog is open and your code can send messages to it.
		/// </summary>
		public bool IsOpen => !_dlg.Is0;

		void _SetClosed()
		{
			_isClosed = true;
			if(_dlg.Is0) return;
			_dlg = default;
			Send.LibClear();
			AAppDomain.Exit -= _AppDomain__Exit;
		}
		bool _isClosed;

		#endregion async etc

		#region send messages

		/// <summary>
		/// Gets dialog window handle as Wnd.
		/// Returns default(Wnd) if the dialog is not open.
		/// </summary>
		public Wnd DialogWindow => _dlg;

		/// <summary>
		/// Allows to modify dialog controls while it is open, and close the dialog.
		/// </summary>
		/// <remarks>
		/// Example: <c>d.Send.Close();</c> .
		/// Example: <c>d.Send.ChangeText2("new text", false);</c> .
		/// Example: <c>d.Send.Message(Native.TDM.CLICK_VERIFICATION, 1);</c> .
		/// 
		/// Can be used only while the dialog is open. Before showing the dialog returns null. After closing the dialog the returned variable is deactivated; its method calls are ignored.
		/// Can be used in dialog event handlers. Also can be used in another thread, for example with <see cref="ShowNoWaitEx"/> and <see cref="ShowProgressEx"/>.
		/// </remarks>
		public DSend Send { get; private set; }

		//called by DSend
		internal int LibSendMessage(Native.TDM message, LPARAM wParam = default, LPARAM lParam = default)
		{
			switch(message) {
			case Native.TDM.CLICK_BUTTON:
			case Native.TDM.ENABLE_BUTTON:
			case Native.TDM.SET_BUTTON_ELEVATION_REQUIRED_STATE:
				wParam = _buttons.MapIdUserToNative((int)wParam);
				break;
			}

			return (int)_dlg.Send((int)message, wParam, lParam);
		}

		//called by DSend
		internal void LibSetText(bool resizeDialog, Native.TDE partId, string text)
		{
			if(partId == Native.TDE.CONTENT && _editType == DEdit.Multiline) {
				text = _c.pszContent = text + c_multilineString;
			}

			_dlg.SendS((int)(resizeDialog ? Native.TDM.SET_ELEMENT_TEXT : Native.TDM.UPDATE_ELEMENT_TEXT), (int)partId, text ?? "");
			//info: null does not change text.

			if(_IsEdit) _EditControlUpdateAsync(!resizeDialog);
			//info: sometimes even UPDATE_ELEMENT_TEXT sends our control to the bottom of the Z order.
		}

		#endregion send messages

		#region hookProc, timeoutText

		//Disables timeout on click or key.
		unsafe void _HookProc(HookData.ThreadGetMessage d)
		{
			switch(d.msg->message) {
			case Api.WM_LBUTTONDOWN:
			case Api.WM_NCLBUTTONDOWN:
			case Api.WM_RBUTTONDOWN:
			case Api.WM_NCRBUTTONDOWN:
			case Api.WM_KEYDOWN:
			case Api.WM_SYSKEYDOWN:
				if(_timeoutActive && d.msg->hwnd.Window == _dlg) {
					_timeoutActive = false;
					//_TimeoutFooterTextHide();
					Send.ChangeFooterText(_timeoutFooterText, false);
				}
				break;
			}
		}

		string _TimeoutFooterText(int timeLeft)
		{
			using(new LibStringBuilder(out var b)) {
				b.Append("This dialog will disappear if not clicked in ").Append(timeLeft).Append(" s.");
				if(!Empty(_timeoutActionText)) b.AppendFormat("\nTimeout action: {0}.", _timeoutActionText);
				if(FlagRtlLayout) b.Replace(".", "");
				if(!Empty(_timeoutFooterText)) b.Append('\n').Append(_timeoutFooterText);
				return b.ToString();
			}
		}

		#endregion hookProc, timeoutText

		#region Edit control

		bool _IsEdit => _editType != DEdit.None;

		void _EditControlInitBeforeShowDialog()
		{
			if(!_IsEdit) return;
			FlagShowMarqueeProgressBar = true;
			FlagShowProgressBar = false;
			if(_c.pszContent == null) _c.pszContent = "";
			if(_c.pszExpandedInformation != null && _editType == DEdit.Multiline) _SetFlag(TDF_.EXPAND_FOOTER_AREA, true);

			//create or get cached font and calculate control height
			//note: don't use system messagebox font. ADialog API does not depend on it.
			_editFont = LibNativeFont.Verdana9Cached;
		}

		void _EditControlUpdate(bool onlyZorder = false)
		{
			if(_editWnd.Is0) return;
			if(!onlyZorder) {
				_EditControlGetPlace(out RECT r);
				_editParent.MoveLL(r.left, r.top, r.Width, r.Height);
				_editWnd.MoveLL(0, 0, r.Width, r.Height);
			}
			_editParent.ZorderTop();
		}

		void _EditControlUpdateAsync(bool onlyZorder = false)
		{
			_editParent.Post(Api.WM_APP + 111, onlyZorder);
		}

		//used to reserve space for multiline Edit control by appending this to text2
		const string c_multilineString = "\r\n\r\n\r\n\r\n\r\n\r\n\r\n\r\n\r\n\r\n ";

		Wnd _EditControlGetPlace(out RECT r)
		{
			Wnd parent = _dlg; //don't use the DirectUIHWND control for it, it can create problems

			//We'll hide the progress bar control and create our Edit control in its place.
			Wnd prog = parent.Child(cn: "msctls_progress32", flags: WCFlags.HiddenToo);
			prog.GetRectInClientOf(parent, out r);

			if(_editType == DEdit.Multiline) {
				int top = r.top;
				if(!_c.pszContent.Ends(c_multilineString)) {
					_c.pszContent += c_multilineString;
					_dlg.SendS((int)Native.TDM.SET_ELEMENT_TEXT, (int)Native.TDE.CONTENT, _c.pszContent);
					prog.GetRectInClientOf(parent, out r); //used to calculate Edit control height: after changing text, prog is moved down, and we know its previous location...
				}
				if(_editMultilineHeight == 0) { _editMultilineHeight = r.bottom - top; } else top = r.bottom - _editMultilineHeight;
				r.top = top;
			} else {
				r.top = r.bottom - (_editFont.HeightOnScreen + 8);
			}

			prog.ShowLL(false);
			return parent;
		}
		int _editMultilineHeight;

		void _EditControlCreate()
		{
			Wnd parent = _EditControlGetPlace(out RECT r);

			//Create an intermediate "#32770" to be direct parent of the Edit control.
			//It is safer (the dialog will not receive Edit notifications) and helps to solve Tab/Esc problems.
			var pStyle = WS.CHILD | WS.VISIBLE | WS.CLIPCHILDREN | WS.CLIPSIBLINGS; //don't need WS_TABSTOP
			var pExStyle = WS_EX.NOPARENTNOTIFY; //not WS_EX.CONTROLPARENT
			_editParent = Wnd.More.CreateWindow("#32770", null, pStyle, pExStyle, r.left, r.top, r.Width, r.Height, parent);
			_editControlParentProcHolder = _EditControlParentProc;
			_editParent.SetWindowLong(Native.GWL.DWLP_DLGPROC, Marshal.GetFunctionPointerForDelegate(_editControlParentProcHolder));

			//Create Edit or ComboBox control.
			string cn = "Edit";
			var style = WS.CHILD | WS.VISIBLE; //don't need WS_TABSTOP
			switch(_editType) {
			case DEdit.Text: style |= (WS)Api.ES_AUTOHSCROLL; break;
			case DEdit.Password: style |= (WS)(Api.ES_PASSWORD | Api.ES_AUTOHSCROLL); break;
			case DEdit.Number: style |= (WS)(Api.ES_NUMBER | Api.ES_AUTOHSCROLL); break;
			case DEdit.Multiline: style |= (WS)(Api.ES_MULTILINE | Api.ES_AUTOVSCROLL | Api.ES_WANTRETURN) | WS.VSCROLL; break;
			case DEdit.Combo: style |= (WS)(Api.CBS_DROPDOWN | Api.CBS_AUTOHSCROLL) | WS.VSCROLL; cn = "ComboBox"; break;
			}
			_editWnd = Wnd.More.CreateWindowAndSetFont(cn, null, style, WS_EX.CLIENTEDGE, 0, 0, r.Width, r.Height, _editParent, customFontHandle: _editFont);

			//Init the control.
			if(_editType == DEdit.Combo) {
				if(_editText is IEnumerable<string> en) {
					bool addItem = false;
					foreach(var s in en) {
						if(!addItem) { addItem = true; _editWnd.SetText(s); } else _editWnd.SendS(Api.CB_INSERTSTRING, -1, s);
					}
				} else _editWnd.SetText(_editText?.ToString());

				RECT cbr = _editWnd.Rect;
				_editParent.ResizeLL(cbr.Width, cbr.Height); //because ComboBox resizes itself
			} else {
				_editWnd.SetText(_editText?.ToString());
				_editWnd.Send(Api.EM_SETSEL, 0, -1);
			}
			_editParent.ZorderTop();
			Wnd.ThisThread.Focus(_editWnd);
		}

		void _EditControlOnMessage(Native.TDN message)
		{
			switch(message) {
			case Native.TDN.BUTTON_CLICKED:
				_editText = _editWnd.ControlText;
				break;
			case Native.TDN.EXPANDO_BUTTON_CLICKED:
			case Native.TDN.NAVIGATED:
				_EditControlUpdateAsync(); //when expando clicked, sync does not work even with doevents
				break;
			}
		}

		/// <summary>
		/// Gets edit control handle as Wnd.
		/// </summary>
		public Wnd EditControl => _editWnd;
		Wnd _editWnd, _editParent;
		LibNativeFont _editFont;

		//Dlgproc of our intermediate #32770 control, the parent of out Edit control.
		int _EditControlParentProc(Wnd hWnd, int msg, LPARAM wParam, LPARAM lParam)
		{
			//Print(msg, wParam, lParam);
			switch(msg) {
			case Api.WM_SETFOCUS: //enables Tab when in single-line Edit control
				Wnd.ThisThread.Focus(_dlg.ChildFast(null, "DirectUIHWND"));
				return 1;
			case Api.WM_NEXTDLGCTL: //enables Tab when in multi-line Edit control
				Wnd.ThisThread.Focus(_dlg.ChildFast(null, "DirectUIHWND"));
				return 1;
			case Api.WM_CLOSE: //enables Esc when in edit control
				_dlg.Send(msg);
				return 1;
			case Api.WM_APP + 111: //async update edit control pos
				_EditControlUpdate(wParam != 0);
				return 1;
			}
			return 0;
			//tested: WM_GETDLGCODE, no results.
		}
		DLGPROC _editControlParentProcHolder;
		delegate int DLGPROC(Wnd w, int msg, LPARAM wParam, LPARAM lParam);

		#endregion Edit control

		#region Show

		/// <summary>
		/// Shows dialog.
		/// Returns selected button id and other results packed in a <see cref="DResult"/> variable.
		/// </summary>
		/// <param name="text1">Main instruction. Bigger font.</param>
		/// <param name="text2">Text below main instruction.</param>
		/// <param name="buttons">See <see cref="Show"/>. Examples: "OK|Cancel", "1 OK|2 Cancel|5 Save|4 Don't Save".</param>
		/// <param name="flags"></param>
		/// <param name="icon"></param>
		/// <param name="owner">Owner window or null. See <see cref="SetOwnerWindow"/>.</param>
		/// <param name="expandedText">Text that the user can show and hide.</param>
		/// <param name="footerText">Text at the bottom of the dialog. Icon can be specified like "i|Text", where i is: x error, ! warning, i info, v shield, a app.</param>
		/// <param name="title">Title bar text. If omitted, null or "", uses <see cref="Options.DefaultTitle"/>.</param>
		/// <param name="radioButtons">Adds radio buttons. A list of strings "id text" separated by |, like "1 One|2 Two|3 Three".</param>
		/// <param name="checkBox">If not null/"", shows a check box with this text. To make it checked, append "|true", "|check" or "|checked".</param>
		/// <param name="defaultButton">id of button that responds to the Enter key.</param>
		/// <param name="x">X position in <see cref="Screen"/>. If default - screen center. You also can use <see cref="Coord.Reverse"/> etc.</param>
		/// <param name="y">Y position in <see cref="Screen"/>. If default - screen center. You also can use <see cref="Coord.Reverse"/> etc.</param>
		/// <param name="secondsTimeout">If not 0, auto-close the dialog after this time (seconds) and set result's Button property = <see cref="DResult.Timeout"/>.</param>
		/// <param name="onLinkClick">
		/// A link-clicked event handler function, eg lambda. Enables hyperlinks in small-font text.
		/// Example:
		/// <code><![CDATA[
		/// ADialog.ShowEx("", "Text <a href=\"example\">link</a>.", onLinkClick: e => { Print(e.LinkHref); });
		/// ]]></code>
		/// </param>
		/// <remarks>
		/// The returned <see cref="DResult"/> variable has these properties: selected button id, selected radio button id, check box state.
		/// Tip: DResult supports implicit cast to int. You can use code <c>switch(ADialog.ShowEx(...))</c> instead of <c>switch(ADialog.ShowEx(...).Button)</c> .
		/// Tip: Use named arguments. Example: <c>ADialog.ShowEx("Text.", icon: DIcon.Info, title: "Title")</c> .
		/// This function allows you to use most of the dialog features, but not all. Alternatively you can create a ADialog class instance, set properties and call ShowDialog. Example in <see cref="ADialog"/> class help.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// var r = ADialog.ShowEx("Main text", "More text.", "1 OK|2 Cancel", expandedText: "Expanded text", radioButtons: "1 One|2 Two|3 Three", checkBox: "Check", secondsTimeout: 30);
		/// Print(r);
		/// switch(r) {
		/// case 1: Print("OK"); break;
		/// case DResult.Timeout: Print("timeout"); break;
		/// default: Print("Cancel"); break;
		/// }
		/// ]]></code>
		/// </example>
		/// <exception cref="Win32Exception">Failed to show dialog.</exception>
		public static DResult ShowEx(
			string text1 = null, string text2 = null, string buttons = null, DFlags flags = 0, DIcon icon = 0, AnyWnd owner = default,
			string expandedText = null, string footerText = null, string title = null, string radioButtons = null, string checkBox = null,
			int defaultButton = 0, Coord x = default, Coord y = default, int secondsTimeout = 0, Action<DEventArgs> onLinkClick = null
			)
		{
			var d = new ADialog(text1, text2, buttons, flags, icon, owner,
				expandedText, footerText, title, radioButtons, checkBox,
				defaultButton, x, y, secondsTimeout, onLinkClick);
			return d.ShowDialog();
		}

		/// <summary>
		/// Shows dialog.
		/// Returns selected button id.
		/// </summary>
		/// <param name="text1">Main instruction. Bigger font.</param>
		/// <param name="text2">Text below main instruction.</param>
		/// <param name="buttons">
		/// Button ids and labels, like "1 OK|2 Cancel|5 Save|4 Don't Save".
		/// Missing ids are auto-generated, for example "OK|Cancel|100 Custom1|Custom2" is the same as "1 OK|2 Cancel|100 Custom1|101 Custom2".
		/// The first in the list button is <i>default</i>, ie responds to the Enter key. For example, "2 No|1 Yes" adds Yes and No buttons and makes No default.
		/// Trims newlines around ids and labels. For example, "\r\n1 One\r\n|\r\n2\r\nTwo\r\n\r\n" is the same as "1 One|2 Two".
		/// 
		/// To create keyboard shortcuts, use &amp; character in custom button labels. Use &amp;&amp; for literal &amp;. Example: "1 &amp;Tuesday[]2 T&amp;hursday[]3 Saturday &amp;&amp; Sunday".
		/// 
		/// There are 6 <i>common buttons</i>: OK, Yes, No, Retry, Cancel, Close. Buttons that have other labels are <i>custom buttons</i>.
		/// How common buttons are different:
		///		1. DFlags.CommandLinks does not change their style.
		///		2. They have keyboard shortcuts that cannot be changed. Inserting &amp; in a label makes it a custom button.
		///		3. Button Cancel can be selected with the Esc key. It also adds X (Close) button in title bar, which selects Cancel.
		///		4. Always displayed in standard order (eg Yes No, never No Yes). But you can for example use "2 No|1 Yes" to set default button = No.
		///		5. The displayed button label is localized, ie different when the Windows UI language is not English.
		///	
		/// If omitted, null or "", the dialog will have OK button, id 1.
		/// You can use <see cref="DFlags.CommandLinks"/> in flags to change the style of custom buttons.
		/// See also: <see cref="SetButtons"/>.
		/// </param>
		/// <param name="flags"></param>
		/// <param name="icon"></param>
		/// <param name="owner">Owner window or null. See <see cref="SetOwnerWindow"/>.</param>
		/// <param name="expandedText">Text that the user can show and hide.</param>
		/// <remarks>
		/// Calls <see cref="ShowEx"/>.
		/// Tip: Use named arguments. Example: <c>ADialog.Show("Text.", icon: DIcon.Info)</c> .
		/// </remarks>
		/// <seealso cref="ShowInfo"/>
		/// <seealso cref="ShowWarning"/>
		/// <seealso cref="ShowError"/>
		/// <seealso cref="ShowOKCancel"/>
		/// <seealso cref="ShowYesNo"/>
		/// <seealso cref="ADebug.Dialog"/>
		/// <example>
		/// <code><![CDATA[
		/// if(ADialog.Show("Show another example?", null, "1 OK|2 Cancel", DIcon.Info) != 1) return;
		/// Print("OK");
		/// 
		/// switch(ADialog.Show("Save changes?", "More info.", "1 Save|2 Don't Save|Cancel")) {
		/// case 1: Print("save"); break;
		/// case 2: Print("don't"); break;
		/// default: Print("cancel"); break;
		/// }
		/// ]]></code>
		/// </example>
		/// <exception cref="Win32Exception">Failed to show dialog.</exception>
		public static int Show(string text1 = null, string text2 = null, string buttons = null, DFlags flags = 0, DIcon icon = 0, AnyWnd owner = default, string expandedText = null)
		{
			return ShowEx(text1, text2, buttons, flags, icon, owner, expandedText).Button;
		}

		/// <summary>
		/// Shows dialog with DIcon.Info icon.
		/// Calls <see cref="Show"/>.
		/// </summary>
		/// <exception cref="Win32Exception">Failed to show dialog.</exception>
		public static int ShowInfo(string text1 = null, string text2 = null, string buttons = null, DFlags flags = 0, AnyWnd owner = default, string expandedText = null)
		{
			return Show(text1, text2, buttons, flags, DIcon.Info, owner, expandedText);
		}

		/// <summary>
		/// Shows dialog with DIcon.Warning icon.
		/// Calls <see cref="Show"/>.
		/// </summary>
		/// <exception cref="Win32Exception">Failed to show dialog.</exception>
		public static int ShowWarning(string text1 = null, string text2 = null, string buttons = null, DFlags flags = 0, AnyWnd owner = default, string expandedText = null)
		{
			return Show(text1, text2, buttons, flags, DIcon.Warning, owner, expandedText);
		}

		/// <summary>
		/// Shows dialog with DIcon.Error icon.
		/// Calls <see cref="Show"/>.
		/// </summary>
		/// <exception cref="Win32Exception">Failed to show dialog.</exception>
		public static int ShowError(string text1 = null, string text2 = null, string buttons = null, DFlags flags = 0, AnyWnd owner = default, string expandedText = null)
		{
			return Show(text1, text2, buttons, flags, DIcon.Error, owner, expandedText);
		}

		/// <summary>
		/// Shows dialog with OK and Cancel buttons.
		/// Returns true if selected OK.
		/// Calls <see cref="Show"/>.
		/// </summary>
		/// <exception cref="Win32Exception">Failed to show dialog.</exception>
		public static bool ShowOKCancel(string text1 = null, string text2 = null, DFlags flags = 0, DIcon icon = 0, AnyWnd owner = default, string expandedText = null)
		{
			return 1 == Show(text1, text2, "OK|Cancel", flags, icon, owner, expandedText);
		}

		/// <summary>
		/// Shows dialog with Yes and No buttons.
		/// Returns true if selected Yes.
		/// Calls <see cref="Show"/>.
		/// </summary>
		/// <exception cref="Win32Exception">Failed to show dialog.</exception>
		public static bool ShowYesNo(string text1 = null, string text2 = null, DFlags flags = 0, DIcon icon = 0, AnyWnd owner = default, string expandedText = null)
		{
			return 1 == Show(text1, text2, "Yes|No", flags, icon, owner, expandedText);
		}

		#endregion Show

		#region ShowTextInput

		/// <summary>
		/// Shows dialog with a text edit field, buttons OK and Cancel, optionally check box, radio buttons and custom buttons.
		/// Returns results packed in a DResult variable: selected button id (1 for OK, 2 for Cancel), text and check box state.
		/// </summary>
		/// <param name="text1">Main instruction. Bigger font.</param>
		/// <param name="text2">Read-only text below main instruction, above the edit field.</param>
		/// <param name="editType">Edit field type. It can be simple text (DEdit.Text, default), multiline, number, password or combo box.</param>
		/// <param name="editText">Initial edit field text. If editType is Combo, it can be a string array, List or IEnumerable; the first item sets combo-box editable text, other items - combo box drop-down list items.</param>
		/// <param name="flags"></param>
		/// <param name="owner">Owner window or null. See <see cref="SetOwnerWindow"/>.</param>
		/// <param name="expandedText">Text that the user can show and hide.</param>
		/// <param name="footerText">Text at the bottom of the dialog. Icon can be specified like "i|Text", where i is: x error, ! warning, i info, v shield, a app.</param>
		/// <param name="title">Title bar text. If omitted, null or "", uses <see cref="Options.DefaultTitle"/>.</param>
		/// <param name="checkBox">If not empty, shows a check box with this text. To make it checked, append "|true", "|check" or "|checked".</param>
		/// <param name="radioButtons">Adds radio buttons. A list of strings "id text" separated by |, like "1 One|2 Two|3 Three".</param>
		/// <param name="x">X position in <see cref="Screen"/>. If default - screen center. You also can use <see cref="Coord.Reverse"/> etc.</param>
		/// <param name="y">Y position in <see cref="Screen"/>. If default - screen center. You also can use <see cref="Coord.Reverse"/> etc.</param>
		/// <param name="secondsTimeout">If not 0, auto-close the dialog after this time, number of seconds.</param>
		/// <param name="onLinkClick">Enables hyperlinks in small-font text. A link-clicked event handler function, like with <see cref="ShowEx"/>.</param>
		/// <param name="buttons">You can use this to add more buttons. A list of strings "id text" separated by |, like "1 OK|2 Cancel|10 Browse...". See <see cref="Show"/>.</param>
		/// <param name="onButtonClick">
		/// A button-clicked event handler function, eg lambda.
		/// Examples:
		/// <code><![CDATA[
		/// ADialog.ShowTextInputEx("Example", flags: DFlags.CommandLinks, buttons: "OK|Cancel|10 Browse\nSets edit control text.",
		///		onButtonClick: e => { if(e.Button == 10) { e.EditText = "text"; e.DoNotCloseDialog = true; } });
		/// 
		/// ADialog.ShowTextInputEx("Example", "Try to click OK while text is empty.", onButtonClick: e =>
		/// {
		/// 	if(e.Button == 1 && Empty(e.EditText)) {
		/// 		ADialog.Show("Text cannot be empty.", owner: e.hwnd);
		/// 		e.dialog.EditControl.FocusControlOfThisThread();
		/// 		e.DoNotCloseDialog = true;
		/// 	}
		/// });
		/// ]]></code>
		/// </param>
		/// <remarks>
		/// This function allows you to use most of the dialog features, but not all. Alternatively you can create a ADialog class instance, set properties and call ShowDialog. Example in <see cref="ADialog"/> class help.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// var r = ADialog.ShowTextInputEx("Example", "Comments.", checkBox: "Check");
		/// if(r.Button != 1) return;
		/// Print(r.EditText);
		/// Print(r.IsChecked);
		/// ]]></code>
		/// </example>
		/// <exception cref="Win32Exception">Failed to show dialog.</exception>
		public static DResult ShowTextInputEx(
			string text1 = null, string text2 = null,
			DEdit editType = DEdit.Text, object editText = null,
			DFlags flags = 0, AnyWnd owner = default,
			string expandedText = null, string footerText = null, string title = null, string checkBox = null, string radioButtons = null,
			Coord x = default, Coord y = default, int secondsTimeout = 0, Action<DEventArgs> onLinkClick = null,
			string buttons = "1 OK|2 Cancel", Action<DEventArgs> onButtonClick = null
			)
		{
			if(Empty(buttons)) buttons = "1 OK|2 Cancel";

			var d = new ADialog(text1, text2, buttons, flags, 0, owner,
				expandedText, footerText, title, radioButtons, checkBox,
				0, x, y, secondsTimeout, onLinkClick);

			d.SetEditControl((editType == DEdit.None) ? DEdit.Text : editType, editText);
			if(onButtonClick != null) d.ButtonClicked += onButtonClick;

			return d.ShowDialog();
		}

		/// <summary>
		/// Shows dialog with a text edit field and buttons OK and Cancel, and gets that text.
		/// Returns true if selected OK, false if Cancel.
		/// </summary>
		/// <param name="s">Variable that receives the text.</param>
		/// <param name="text1">Main instruction. Bigger font.</param>
		/// <param name="text2">Read-only text below main instruction, above the edit field.</param>
		/// <param name="editType">Edit field type.</param>
		/// <param name="editText">Initial edit field text. If editType is DEdit.Combo, it can be a string array, List or IEnumerable; the first item sets combo-box editable text, other items - combo box drop-down list items.</param>
		/// <param name="flags"></param>
		/// <param name="owner">Owner window or null. See <see cref="SetOwnerWindow"/>.</param>
		/// <remarks>
		/// Calls <see cref="ShowTextInputEx"/>.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// string s;
		/// if(!ADialog.ShowTextInput(out s, "Example")) return;
		/// Print(s);
		/// 
		/// //or you can declare the variable like this
		/// if(!ADialog.ShowTextInput(out string s2, "Example")) return;
		/// Print(s2);
		/// ]]></code>
		/// </example>
		/// <exception cref="Win32Exception">Failed to show dialog.</exception>
		public static bool ShowTextInput(
			out string s,
			string text1 = null, string text2 = null,
			DEdit editType = DEdit.Text, object editText = null,
			DFlags flags = 0, AnyWnd owner = default
			)
		{
			s = null;
			DResult r = ShowTextInputEx(text1, text2, editType, editText, flags, owner);
			if(r.Button != 1) return false;
			s = r.EditText;
			return true;
		}

		/// <summary>
		/// Shows dialog with a number edit field and buttons OK and Cancel, and gets that number.
		/// Returns true if selected OK, false if Cancel.
		/// </summary>
		/// <param name="i">Variable that receives the number.</param>
		/// <param name="text1">Main instruction. Bigger font.</param>
		/// <param name="text2">Read-only text below main instruction, above the edit field.</param>
		/// <param name="editType">Edit field type.</param>
		/// <param name="editText">Initial edit field text.</param>
		/// <param name="flags"></param>
		/// <param name="owner">Owner window or null. See <see cref="SetOwnerWindow"/>.</param>
		/// <remarks>
		/// Calls <see cref="ShowTextInputEx"/>.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// int i;
		/// if(!ADialog.ShowNumberInput(out i, "Example")) return;
		/// Print(i);
		/// ]]></code>
		/// </example>
		/// <exception cref="Win32Exception">Failed to show dialog.</exception>
		public static bool ShowNumberInput(
			out int i,
			string text1 = null, string text2 = null,
			DEdit editType = DEdit.Number, object editText = null,
			DFlags flags = 0, AnyWnd owner = default
			)
		{
			i = 0;
			if(!ShowTextInput(out string s, text1, text2, editType, editText, flags, owner)) return false;
			i = s.ToInt();
			return true;
		}

		#endregion ShowTextInput

		#region ShowList

		/// <summary>
		/// Shows dialog with a list of command-link buttons.
		/// Returns results packed in a <see cref="DResult"/> variable. Its Button property is id of the selected button, which is its 1-based index in the list; it is 0 if clicked the X (close window) button or pressed Esc.
		/// The return value can be assigned to an int variable or used in switch; then it is the id (1-based index or 0).
		/// </summary>
		/// <param name="list">List items (buttons). Can be string like "One|Two|Three" or string[] or List&lt;string&gt;. See <see cref="SetButtons"/>.</param>
		/// <param name="text1">Main instruction. Bigger font.</param>
		/// <param name="text2">Text below main instruction.</param>
		/// <param name="flags"></param>
		/// <param name="owner">Owner window or null. See <see cref="SetOwnerWindow"/>.</param>
		/// <param name="expandedText">Text that the user can show and hide.</param>
		/// <param name="footerText">Text at the bottom of the dialog. Icon can be specified like "i|Text", where i is: x error, ! warning, i info, v shield, a app.</param>
		/// <param name="title">Title bar text. If omitted, null or "", uses <see cref="Options.DefaultTitle"/>.</param>
		/// <param name="checkBox">If not empty, shows a check box with this text. To make it checked, append "|true", "|check" or "|checked".</param>
		/// <param name="defaultButton">id (1-based index) of button that responds to the Enter key.</param>
		/// <param name="x">X position in <see cref="Screen"/>. If default - screen center. You also can use <see cref="Coord.Reverse"/> etc.</param>
		/// <param name="y">Y position in <see cref="Screen"/>. If default - screen center. You also can use <see cref="Coord.Reverse"/> etc.</param>
		/// <param name="secondsTimeout">If not 0, auto-close the dialog after this time, number of seconds.</param>
		/// <param name="onLinkClick">Enables hyperlinks in small-font text. A link-clicked event handler function, like with <see cref="ShowEx"/>.</param>
		/// <remarks>
		/// This function allows you to use most of the dialog features, but not all. Alternatively you can create a ADialog class instance, set properties and call ShowDialog. Example in <see cref="ADialog"/> class help.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// int r = ADialog.ShowListEx("One|Two|Three", "Example", y: -1, secondsTimeout: 15);
		/// if(r <= 0) return; //X/Esc or timeout
		/// Print(r);
		/// ]]></code>
		/// </example>
		/// <exception cref="Win32Exception">Failed to show dialog.</exception>
		public static DResult ShowListEx(
			DStringList list, string text1 = null, string text2 = null, DFlags flags = 0, AnyWnd owner = default,
			string expandedText = null, string footerText = null, string title = null, string checkBox = null,
			int defaultButton = 0, Coord x = default, Coord y = default, int secondsTimeout = 0,
			Action<DEventArgs> onLinkClick = null
			)
		{
			var d = new ADialog(text1, text2, null, flags, 0, owner,
				expandedText, footerText, title, null, checkBox,
				defaultButton, x, y, secondsTimeout, onLinkClick);

			d.SetButtons(null, true, list);
			d.FlagXCancel = true;
			d.SetExpandedText(expandedText, true);
			return d.ShowDialog();
		}

		/// <summary>
		/// Shows dialog with a list of command-link buttons.
		/// Returns 1-based index of the selected button. Returns 0 if clicked the X (close window) button or pressed Esc.
		/// </summary>
		/// <param name="list">List items (buttons). Can be string like "One|Two|Three" or string[] or List&lt;string&gt;. See <see cref="SetButtons"/>.</param>
		/// <param name="text1">Main instruction. Bigger font.</param>
		/// <param name="text2">Text below main instruction.</param>
		/// <param name="flags"></param>
		/// <param name="owner">Owner window or null. See <see cref="SetOwnerWindow"/>.</param>
		/// <remarks>
		/// Calls <see cref="ShowListEx"/>.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// int r = ADialog.ShowList("One|Two|Three", "Example");
		/// if(r == 0) return; //X/Esc
		/// Print(r);
		/// ]]></code>
		/// </example>
		/// <exception cref="Win32Exception">Failed to show dialog.</exception>
		public static int ShowList(DStringList list, string text1 = null, string text2 = null, DFlags flags = 0, AnyWnd owner = default)
		{
			return ShowListEx(list, text1, text2, flags, owner);
		}

		#endregion ShowList

		#region ShowProgress
#pragma warning disable 1573 //missing XML documentation for parameters

		/// <summary>
		/// Shows dialog with progress bar.
		/// Creates dialog in new thread and returns without waiting until it is closed.
		/// Returns <see cref="ADialog"/> variable that can be used to communicate with the dialog using these methods and properties: <see cref="IsOpen"/>, <see cref="ThreadWaitForClosed"/>, <see cref="Result"/> (when closed), <see cref="DialogWindow"/>, <see cref="Send"/>; through the Send property you can set progress, modify controls and close the dialog (see example).
		/// Most parameters are the same as with <see cref="ShowEx"/>.
		/// </summary>
		/// <param name="marquee">Let the progress bar animate without indicating a percent of work done.</param>
		/// <remarks>
		/// This function allows you to use most of the dialog features, but not all. Alternatively you can create a ADialog class instance, set properties and call <see cref="ShowDialogNoWait"/>.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// var pd = ADialog.ShowProgressEx(false, "Working", buttons: "1 Stop", y: -1);
		/// for(int i = 1; i <= 100; i++) {
		/// 	if(!pd.IsOpen) { Print(pd.Result); break; } //if the user closed the dialog
		/// 	pd.Send.Progress(i); //don't need this if marquee
		/// 	Thread.Sleep(50); //do something in the loop
		/// }
		/// pd.Send.Close();
		/// ]]></code>
		/// </example>
		/// <exception cref="AException">Failed to show dialog.</exception>
		public static ADialog ShowProgressEx(bool marquee,
			string text1 = null, string text2 = null, string buttons = "0 Cancel", DFlags flags = 0, AnyWnd owner = default,
			string expandedText = null, string footerText = null, string title = null, string radioButtons = null, string checkBox = null,
			Coord x = default, Coord y = default, int secondsTimeout = 0, Action<DEventArgs> onLinkClick = null
		)
		{
			if(Empty(buttons)) buttons = "0 Cancel";

			var d = new ADialog(text1, text2, buttons, flags, 0, owner,
				expandedText, footerText, title, radioButtons, checkBox,
				0, x, y, secondsTimeout, onLinkClick);

			if(marquee) d.FlagShowMarqueeProgressBar = true; else d.FlagShowProgressBar = true;

			d.ShowDialogNoWait();

			if(marquee) d.Send.Message(Native.TDM.SET_PROGRESS_BAR_MARQUEE, true);

			return d;
		}

		/// <summary>
		/// Shows dialog with progress bar.
		/// Creates dialog in other thread and returns without waiting until it is closed.
		/// Returns <see cref="ADialog"/> variable that can be used to communicate with the dialog using these methods and properties: <see cref="IsOpen"/>, <see cref="ThreadWaitForClosed"/>, <see cref="Result"/> (when closed), <see cref="DialogWindow"/>, <see cref="Send"/>; through the Send property you can set progress, modify controls and close the dialog (see example).
		/// All parameters except marquee are the same as with <see cref="ShowEx"/>.
		/// </summary>
		/// <param name="marquee">Let the progress bar animate without indicating a percent of work done.</param>
		/// <remarks>
		/// Calls <see cref="ShowProgressEx"/>.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// var pd = ADialog.ShowProgress(false, "Working");
		/// for(int i = 1; i <= 100; i++) {
		/// 	if(!pd.IsOpen) { Print(pd.Result); break; } //if the user closed the dialog
		/// 	pd.Send.Progress(i); //don't need this if marquee
		/// 	Thread.Sleep(50); //do something in the loop
		/// }
		/// pd.Send.Close();
		/// ]]></code>
		/// </example>
		/// <exception cref="AException">Failed to show dialog.</exception>
		public static ADialog ShowProgress(bool marquee,
			string text1 = null, string text2 = null, string buttons = "0 Cancel", DFlags flags = 0, AnyWnd owner = default,
			Coord x = default, Coord y = default)
		{
			return ShowProgressEx(marquee, text1, text2, buttons, flags, owner, x: x, y: y);
		}

#pragma warning restore 1573 //missing XML documentation for parameters
		#endregion ShowProgress

		#region ShowNoWait
#pragma warning disable 1573 //missing XML documentation for parameters

		/// <summary>
		/// Shows dialog like <see cref="ShowEx"/> but does not wait.
		/// Creates dialog in other thread and returns without waiting until it is closed.
		/// Returns <see cref="ADialog"/> variable that can be used to communicate with the dialog using these methods and properties: <see cref="IsOpen"/>, <see cref="ThreadWaitForClosed"/>, <see cref="Result"/> (when closed), <see cref="DialogWindow"/>, <see cref="Send"/>; through the Send property you can modify controls and close the dialog (see example).
		/// Parameters are the same as with <see cref="ShowEx"/>.
		/// </summary>
		/// <remarks>
		/// This function allows you to use most of the dialog features, but not all. Alternatively you can create a ADialog class instance, set properties and call <see cref="ShowDialogNoWait"/>.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// ADialog.ShowNoWait("Simple example");
		/// 
		/// var d = ADialog.ShowNoWaitEx("Another example", "text", "1 OK|2 Cancel", y: -1, secondsTimeout: 30);
		/// 2.s(); //do something while the dialog is open
		/// d.Send.ChangeText2("new text", false);
		/// 2.s(); //do something while the dialog is open
		/// d.ThreadWaitClosed(); Print(d.Result); //wait until the dialog is closed and get result. Optional, just an example.
		/// ]]></code>
		/// </example>
		/// <exception cref="AggregateException">Failed to show dialog.</exception>
		public static ADialog ShowNoWaitEx(
			string text1 = null, string text2 = null, string buttons = null, DFlags flags = 0, DIcon icon = 0, AnyWnd owner = default,
			string expandedText = null, string footerText = null, string title = null, string radioButtons = null, string checkBox = null,
			int defaultButton = 0, Coord x = default, Coord y = default, int secondsTimeout = 0, Action<DEventArgs> onLinkClick = null
			)
		{
			var d = new ADialog(text1, text2, buttons, flags, icon, owner,
				expandedText, footerText, title, radioButtons, checkBox,
				defaultButton, x, y, secondsTimeout, onLinkClick);
			d.ShowDialogNoWait();
			return d;
		}

		/// <summary>
		/// Shows dialog like <see cref="Show"/> but does not wait.
		/// Creates dialog in other thread and returns without waiting until it is closed.
		/// Returns <see cref="ADialog"/> variable that can be used to communicate with the dialog using these methods and properties: <see cref="IsOpen"/>, <see cref="ThreadWaitForClosed"/>, <see cref="Result"/> (when closed), <see cref="DialogWindow"/>, <see cref="Send"/>. Through the <b>Send</b> property you can modify controls and close the dialog. Example in <see cref="ShowNoWaitEx"/> topic.
		/// Parameters are the same as with <see cref="Show"/>.
		/// </summary>
		/// <remarks>
		/// Calls <see cref="ShowNoWaitEx"/>.
		/// </remarks>
		/// <exception cref="AggregateException">Failed to show dialog.</exception>
		public static ADialog ShowNoWait(
			string text1 = null, string text2 = null,
			string buttons = null, DFlags flags = 0, DIcon icon = 0,
			AnyWnd owner = default
			)
		{
			return ShowNoWaitEx(text1, text2, buttons, flags, icon, owner);
		}

#pragma warning restore 1573 //missing XML documentation for parameters
		#endregion ShowNoWait
	}
}

namespace Au.Types
{
#pragma warning disable 1591 //missing XML documentation

	/// <summary>
	/// Standard icons for <see cref="ADialog.Show"/> and similar functions.
	/// </summary>
	public enum DIcon
	{
		Warning = 0xffff,
		Error = 0xfffe,
		Info = 0xfffd,
		Shield = 0xfffc,

		/// <summary>
		/// Use <msdn>IDI_APPLICATION</msdn> icon from unmanaged resources of this program file.
		/// If there are no icons - the default program icon.
		/// </summary>
		App = Api.IDI_APPLICATION
	}

	/// <summary>
	/// Text edit field type for <see cref="ADialog.ShowTextInputEx"/> and <see cref="ADialog.SetEditControl"/>.
	/// </summary>
	public enum DEdit
	{
		None, Text, Multiline, Password, Number, Combo
	}
#pragma warning restore 1591 //missing XML documentation

	/// <summary>
	/// Flags for <see cref="ADialog.Show"/> and similar functions.
	/// </summary>
	[Flags]
	public enum DFlags
	{
		/// <summary>
		/// Display custom buttons as a column of command-links, not as a row of classic buttons.
		/// Command links can have multi-line text. The first line has bigger font.
		/// More info about custom buttons: <see cref="ADialog.Show"/>.
		/// </summary>
		CommandLinks = 1,

		/// <summary>
		/// Call <see cref="Thread.Abort()"/> if selected OK button when there are no other buttons. Also when selected Cancel, No, and on timeout.
		/// The same as <see cref="ADialog.FlagEndThread"/>.
		/// </summary>
		EndThread = 2,

		/// <summary>
		/// Show expanded text in footer.
		/// </summary>
		ExpandDown = 4,

		/// <summary>
		/// Show the dialog in the center of the owner window.
		/// </summary>
		OwnerCenter = 8,

		/// <summary>
		/// x y are relative to the primary screen (ignore <see cref="ADialog.Screen"/> etc). Don't ensure thet entire window is in screen.
		/// More info: <see cref="ADialog.SetXY"/>. 
		/// </summary>
		RawXY = 16,

		/// <summary>
		/// Make the dialog a topmost window (always on top of other windows), regardless of ADialog.Options.TopmostIfNoOwnerWindow etc.
		/// More info: <see cref="ADialog.FlagTopmost"/>. 
		/// </summary>
		Topmost = 32,

		/// <summary>
		/// Set <see cref="ADialog.Width"/> = 700.
		/// </summary>
		Wider = 64,

		/// <summary>
		/// Allow to cancel even if there is no Cancel button.
		/// It adds X (Close) button to the title bar, and also allows to close the dialog with the Esc key.
		/// When the dialog is closed with the X button or Esc, the returned result button id is 0 if there is no Cancel button; else the same as when clicked the Cancel button.
		/// The same as <see cref="ADialog.FlagXCancel"/>.
		/// </summary>
		XCancel = 128,

		//This was implemented, it's easy, but then I changed my mind, don't need too many features.
		///// <summary>
		///// Show keyboard shortcuts (underlined characters), like when you press the Alt key.
		///// More info: <see cref="ADialog.FlagKeyboardShortcutsVisible"/>.
		///// </summary>
		//KeyboardShortcutsVisible = ,

		//NoTaskbarButton = , //not so useful
		//NeverActivate = , //don't know how to implement. TDF_NO_SET_FOREGROUND does not work. LockSetForegroundWindow does not work if we can activate windows. HCBT_ACTIVATE can prevent activating but does not prevent deactivating.
		//AlwaysActivate = , //Don't use. Always allow. Because after AllowActivate (which is also used by Activate etc) always activates dialogs regardless of anything. As well as in uiAccess process.
	}

	/// <summary>
	/// Result of <see cref="ADialog.ShowEx"/> and similar functions: button id, radio button id, check box state, edit field text.
	/// </summary>
	public class DResult
	{
		internal DResult(int button, int radioButton, bool isChecked, string editText)
		{
			Button = button; RadioButton = radioButton; IsChecked = isChecked; EditText = editText;
		}

		/// <summary>
		/// Returned <see cref="Button"/> value on timeout.
		/// </summary>
		public const int Timeout = int.MinValue;

		/// <summary>
		/// Gets selected button id.
		/// On timeout it is DResult.Timeout.
		/// </summary>
		public int Button { get; set; }

		/// <summary>
		/// Gets selected (checked) radio button id.
		/// </summary>
		public int RadioButton { get; set; }

		/// <summary>
		/// Gets check box state.
		/// </summary>
		public bool IsChecked { get; set; }

		/// <summary>
		/// Gets edit field text.
		/// </summary>
		public string EditText { get; set; }

		/// <summary>
		/// Converts DResult to int.
		/// Allows to use code <c>switch(ADialog.ShowEx(...))</c> instead of <c>switch(ADialog.ShowEx(...).Button)</c> .
		/// </summary>
		public static implicit operator int(DResult r) { return r.Button; }

		/// <summary>
		/// Formats string $"Button={Button}, RadioButton={RadioButton}, IsChecked={IsChecked}, EditText={EditText}".
		/// </summary>
		public override string ToString()
		{
			return $"Button={Button}, RadioButton={RadioButton}, IsChecked={IsChecked}, EditText={EditText}";
		}
	}

	/// <summary>
	/// Arguments for <see cref="ADialog"/> event handlers.
	/// </summary>
	/// <remarks>
	/// To return a non-zero value from the callback function, assign the value to the <b>returnValue</b> field.
	/// More info: <msdn>TaskDialogCallbackProc</msdn>.
	/// </remarks>
	public class DEventArgs :EventArgs
	{
		internal DEventArgs(ADialog obj_, Wnd hwnd_, Native.TDN message_, LPARAM wParam_, LPARAM lParam_)
		{
			dialog = obj_; hwnd = hwnd_; message = message_; wParam = wParam_;
			LinkHref = (message_ == Native.TDN.HYPERLINK_CLICKED) ? Marshal.PtrToStringUni(lParam_) : null;
		}

#pragma warning disable 1591 //missing XML documentation
		public ADialog dialog;
		public Wnd hwnd;
		/// <summary>Reference: <msdn>task dialog notifications</msdn>.</summary>
		public Native.TDN message;
		public LPARAM wParam;
		public int returnValue;
#pragma warning restore 1591 //missing XML documentation

		/// <summary>
		/// Clicked hyperlink href attribute value. Use in <see cref="ADialog.HyperlinkClicked"/> event handler.
		/// </summary>
		public string LinkHref { get; private set; }

		/// <summary>
		/// Clicked button id. Use in <see cref="ADialog.ButtonClicked"/> event handler.
		/// </summary>
		public int Button => (int)wParam;

		/// <summary>
		/// Dialog timer time in milliseconds. Use in <see cref="ADialog.Timer"/> event handler.
		/// The event handler can set <b>returnValue</b>=1 to reset this.
		/// </summary>
		public int TimerTimeMS => (int)wParam;

		/// <summary>
		/// Your <see cref="ADialog.ButtonClicked"/> event handler function can use this to prevent closing the dialog.
		/// </summary>
		public bool DontCloseDialog { set { returnValue = value ? 1 : 0; } }

		/// <summary>
		/// Gets or sets edit field text.
		/// </summary>
		public string EditText
		{
			get => dialog.EditControl.ControlText;
			set { dialog.EditControl.SetText(value); }
		}
	}

	/// <summary>
	/// Can be used through <see cref="ADialog.Send"/>, to interact with dialog while it is open.
	/// </summary>
	/// <remarks>
	/// Example (in an event handler): <c>e.dialog.Close();</c>
	/// </remarks>
	public class DSend
	{
		volatile ADialog _tdo;

		internal DSend(ADialog tdo) { _tdo = tdo; }
		internal void LibClear() { _tdo = null; }

		/// <summary>
		/// Sends a message to the dialog.
		/// </summary>
		/// <remarks>
		/// Call this method while the dialog is open, eg in an event handler.
		/// Example (in an event handler): <c>e.dialog.Send.Message(Native.TDM.CLICK_VERIFICATION, 1);</c>
		/// Also there are several other functions to send some messages: change text, close dialog, enable/disable buttons, update progress.
		/// Reference: <msdn>task dialog messages</msdn>.
		/// NAVIGATE_PAGE currently not supported.
		/// </remarks>
		public int Message(Native.TDM message, LPARAM wParam = default, LPARAM lParam = default)
		{
			return _tdo?.LibSendMessage(message, wParam, lParam) ?? 0;
		}

		void _SetText(bool resizeDialog, Native.TDE partId, string text)
		{
			_tdo?.LibSetText(resizeDialog, partId, text);
		}

		/// <summary>
		/// Changes the main big-font text.
		/// </summary>
		/// <remarks>
		/// Call this method while the dialog is open, eg in an event handler.
		/// </remarks>
		public void ChangeText1(string text, bool resizeDialog)
		{
			_SetText(resizeDialog, Native.TDE.MAIN_INSTRUCTION, text);
		}

		/// <summary>
		/// Changes the main small-font text.
		/// </summary>
		/// <remarks>
		/// Call this method while the dialog is open, eg in an event handler.
		/// </remarks>
		public void ChangeText2(string text, bool resizeDialog)
		{
			_SetText(resizeDialog, Native.TDE.CONTENT, text);
		}

		/// <summary>
		/// Changes the footer text.
		/// </summary>
		/// <remarks>
		/// Call this method while the dialog is open, eg in an event handler.
		/// </remarks>
		public void ChangeFooterText(string text, bool resizeDialog)
		{
			_SetText(resizeDialog, Native.TDE.FOOTER, text);
		}

		/// <summary>
		/// Changes the expanded area text.
		/// </summary>
		/// <remarks>
		/// Call this method while the dialog is open, eg in an event handler.
		/// </remarks>
		public void ChangeExpandedText(string text, bool resizeDialog)
		{
			_SetText(resizeDialog, Native.TDE.EXPANDED_INFORMATION, text);
		}

#if false //currently not implemented
		/// <summary>
		/// Applies new properties to the dialog while it is already open.
		/// Call this method while the dialog is open, eg in an event handler, after setting new properties.
		/// Sends message Native.TDM.NAVIGATE_PAGE.
		/// </summary>
		public void Reconstruct()
		{
			var td = _tdo; if(td == null) return;
			_ApiSendMessageTASKDIALOGCONFIG(_dlg, (uint)Native.TDM.NAVIGATE_PAGE, 0, ref td._c);
		}

		[DllImport("user32.dll", EntryPoint = "SendMessageW")]
		static extern LPARAM _ApiSendMessageTASKDIALOGCONFIG(Wnd hWnd, uint msg, LPARAM wParam, in TASKDIALOGCONFIG c);
#endif
		/// <summary>
		/// Clicks a button. Normally it closes the dialog.
		/// </summary>
		/// <param name="buttonId">A button id or some other number that will be returned by ShowDialog.</param>
		/// <remarks>
		/// Call this method while the dialog is open, eg in an event handler.
		/// Sends message Native.TDM.CLICK_BUTTON.
		/// </remarks>
		public bool Close(int buttonId = 0)
		{
			return 0 != Message(Native.TDM.CLICK_BUTTON, buttonId);
		}

		/// <summary>
		/// Enables or disables a button.
		/// </summary>
		/// <remarks>
		/// Call this method while the dialog is open, eg in an event handler.
		/// Example: <c>d.Created += e => { e.dialog.Send.EnableButton(4, false); };</c>
		/// Sends message Native.TDM.ENABLE_BUTTON.
		/// </remarks>
		public void EnableButton(int buttonId, bool enable)
		{
			Message(Native.TDM.ENABLE_BUTTON, buttonId, enable);
		}

		/// <summary>
		/// Sets progress bar value, 0 to 100.
		/// </summary>
		/// <remarks>
		/// Call this method while the dialog is open, eg in an event handler.
		/// Sends message Native.TDM.SET_PROGRESS_BAR_POS.
		/// </remarks>
		public int Progress(int percent)
		{
			return Message(Native.TDM.SET_PROGRESS_BAR_POS, percent);
		}
	}

	#region public API
#pragma warning disable 1591 //missing XML documentation
	public static partial class Native
	{
		/// <summary>
		/// Messages that your <see cref="ADialog"/> event handler can send to the dialog.
		/// </summary>
		public enum TDM :uint
		{
			NAVIGATE_PAGE = WM_USER + 101,
			CLICK_BUTTON = WM_USER + 102, // wParam = button id
			SET_MARQUEE_PROGRESS_BAR = WM_USER + 103, // wParam = 0 (nonMarque) wParam != 0 (Marquee)
			SET_PROGRESS_BAR_STATE = WM_USER + 104, // wParam = new progress state (0, 1 or 2)
			SET_PROGRESS_BAR_RANGE = WM_USER + 105, // lParam = AMath.MakeUint(min, max)
			SET_PROGRESS_BAR_POS = WM_USER + 106, // wParam = new position
			SET_PROGRESS_BAR_MARQUEE = WM_USER + 107, // wParam = 0 (stop marquee), wParam != 0 (start marquee), lParam = speed (milliseconds between repaints)
			SET_ELEMENT_TEXT = WM_USER + 108, // wParam = element (enum Native.TDE), lParam = new element text (string)
			CLICK_RADIO_BUTTON = WM_USER + 110, // wParam = radio button id
			ENABLE_BUTTON = WM_USER + 111, // wParam = button id, lParam = 0 (disable), lParam != 0 (enable)
			ENABLE_RADIO_BUTTON = WM_USER + 112, // wParam = radio button id, lParam = 0 (disable), lParam != 0 (enable)
			CLICK_VERIFICATION = WM_USER + 113, // wParam = 0 (unchecked), 1 (checked), lParam = 1 (set key focus)
			UPDATE_ELEMENT_TEXT = WM_USER + 114, // wParam = element (enum Native.TDE), lParam = new element text (string)
			SET_BUTTON_ELEVATION_REQUIRED_STATE = WM_USER + 115, // wParam = button id, lParam = 0 (elevation not required), lParam != 0 (elevation required)
			UPDATE_ICON = WM_USER + 116  // wParam = icon element (enum Native.TDIE), lParam = new icon (icon handle or DIcon)
		}
		const uint WM_USER = Api.WM_USER;

		/// <summary>
		/// Notification messages that your <see cref="ADialog"/> event handler receives.
		/// </summary>
		public enum TDN :uint
		{
			CREATED = 0,
			NAVIGATED = 1,
			BUTTON_CLICKED = 2,
			HYPERLINK_CLICKED = 3,
			TIMER = 4,
			DESTROYED = 5,
			RADIO_BUTTON_CLICKED = 6,
			DIALOG_CONSTRUCTED = 7,
			VERIFICATION_CLICKED = 8,
			HELP = 9,
			EXPANDO_BUTTON_CLICKED = 10
		}

		/// <summary>
		/// Constants for Native.TDM.SET_ELEMENT_TEXT and Native.TDM.UPDATE_ELEMENT_TEXT messages and ADialog.Send.Text().
		/// Used with <see cref="ADialog"/>.
		/// </summary>
		public enum TDE
		{
			CONTENT,
			EXPANDED_INFORMATION,
			FOOTER,
			MAIN_INSTRUCTION
		}

		/// <summary>
		/// Constants for Native.TDM.UPDATE_ICON message used with <see cref="ADialog"/>.
		/// </summary>
		public enum TDIE
		{
			ICON_MAIN,
			ICON_FOOTER
		}
	}

#pragma warning restore 1591 //missing XML documentation
	#endregion public API
}
