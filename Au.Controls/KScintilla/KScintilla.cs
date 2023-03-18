using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;

namespace Au.Controls;

using static Sci;

/// <summary>
/// This .NET control wraps native Scintilla control.
/// It is not a universal Scintilla wrapper class. Just for this library and related software.
/// </summary>
/// <remarks>
/// Most functions throw ArgumentOutOfRangeException when: 1. A position or line index argument is negative. 2. Scintilla returned a negative position or line index.
/// If a position or line index argument is greater than text length or the number of lines, some functions return the text length or the last line, and it is documented; for other functions the behaviour is undefined, eg ArgumentOutOfRangeException or Scintilla's return value or like of the documented methods.
/// 
/// Function/event names start with aa, because VS intellisense cannot group by inheritance and would mix with 300 WPF functions/events.
/// </remarks>
public unsafe partial class KScintilla : HwndHost {
	wnd _w;
	WNDPROC _wndproc;
	nint _wndprocScintilla;
	nint _sciPtr;
	Sci_NotifyCallback _notifyCallback;
	internal int _dpi;

#if DEBUG
	internal bool test_; //we use many scintilla controls, but often want to test something on one of them. Then set test_ = true...
#endif

	static KScintilla() {
		filesystem.more.LoadDll64or32Bit_("Scintilla.dll");
		//filesystem.more.loadDll64or32Bit_("Lexilla.dll");
	}

	public nint AaSciPtr => _sciPtr;

	public SciImages AaImages { get; private set; }

	public SciTags AaTags { get; private set; }

	#region HwndHost

	public wnd AaWnd => _w;

	/// <summary>
	/// Invoked by <b>AaOnHandleCreated</b>, which is called by <see cref="BuildWindowCore"/> after initializing everything but before setting text and subclassing.
	/// </summary>
	public event Action AaHandleCreated;

	/// <summary>
	/// Called by <see cref="BuildWindowCore"/> after initializing everything but before setting text and subclassing.
	/// Invokes event <see cref="AaHandleCreated"/>.
	/// </summary>
	protected virtual void AaOnHandleCreated() => AaHandleCreated?.Invoke();

	protected override HandleRef BuildWindowCore(HandleRef hwndParent) {
		var wParent = (wnd)hwndParent.Handle;
		_dpi = Dpi.OfWindow(wParent);
		WS style = WS.CHILD; if (AaInitBorder) style |= WS.BORDER;
		//note: no WS_VISIBLE. WPF will manage it. It can cause visual artefacts occasionally, eg scrollbar in WPF area.
		_w = Api.CreateWindowEx(0, "Scintilla", Name, style, 0, 0, 0, 0, wParent);
		//size 0 0 is not the best, but it is a workaround for WPF bugs

		_sciPtr = _w.Send(SCI_GETDIRECTPOINTER);
		Call(SCI_SETNOTIFYCALLBACK, 0, Marshal.GetFunctionPointerForDelegate(_notifyCallback = _NotifyCallback));

		bool hasTags = AaInitTagsStyle != AaTagsStyle.NoTags;
		if (AaInitReadOnlyAlways) {
			MOD mask = 0;
			if (AaInitImages || hasTags) mask |= MOD.SC_MOD_INSERTTEXT | MOD.SC_MOD_DELETETEXT;
			Call(SCI_SETMODEVENTMASK, (int)mask);
		}
		_InitDocument();
		Call(SCI_SETSCROLLWIDTHTRACKING, 1);
		Call(SCI_SETSCROLLWIDTH, 1); //SHOULDDO: later make narrower when need, eg when folded long lines (alas there is no direct notification). Maybe use timer.
		if (!AaInitUseDefaultContextMenu) Call(SCI_USEPOPUP);
		Call(SCI_SETCARETWIDTH, Dpi.Scale(2, _dpi));

		//Need to set selection colors or layer, because the default inactive selection color is darker than active.
		//	It is 0x3F808080, but alpha is ignored if SC_LAYER_BASE (default).
		Call(SCI_SETSELECTIONLAYER, SC_LAYER_UNDER_TEXT);
		aaaSetElementColor(SC_ELEMENT_SELECTION_BACK, 0xA0A0A0A0); //use alpha to mix with indicators
		aaaSetElementColor(SC_ELEMENT_SELECTION_INACTIVE_BACK, 0x60A0A0A0);

		if (AaInitWrapVisuals) {
			Call(SCI_SETWRAPVISUALFLAGS, SC_WRAPVISUALFLAG_START | SC_WRAPVISUALFLAG_END);
			Call(SCI_SETWRAPVISUALFLAGSLOCATION, SC_WRAPVISUALFLAGLOC_END_BY_TEXT);
			Call(SCI_SETWRAPINDENTMODE, SC_WRAPINDENT_INDENT);
		}
		if (AaWrapLines) {
			Call(SCI_SETWRAPMODE, SC_WRAP_WORD);
		}

		//note: cannot set styles here, because later derived class will call aaaStyleClearAll, which sets some special styles.

		if (AaInitImages) AaImages = new SciImages(this);
		if (hasTags) AaTags = new SciTags(this);

		if (FocusManager.GetFocusScope(this) is Window fs && FocusManager.GetFocusedElement(fs) == this && Api.GetFocus() == wParent)
			Api.SetFocus(_w);

		AaOnHandleCreated();

		if (!_text.NE()) aaaSetText(_text, SciSetTextFlags.NoUndoNoNotify); //after derived classes set styles etc

		_wndprocScintilla = Api.SetWindowLongPtr(_w, GWL.WNDPROC, Marshal.GetFunctionPointerForDelegate(_wndproc = _WndProc));
		//WPF will subclass this window. It respects the GWL.WNDPROC subclass, but breaks SetWindowSubclass.

		return new HandleRef(this, _w.Handle);
	}

	void _InitDocument() {
		//these must be set for each document of this Scintilla window

		Call(SCI_SETCODEPAGE, Api.CP_UTF8);
		Call(SCI_SETTABWIDTH, 4);
		if (AaInitReadOnlyAlways) {
			Call(SCI_SETREADONLY, 1);
			Call(SCI_SETUNDOCOLLECTION);
		} //else if (_isReadOnly) Call(SCI_SETREADONLY, 1);
	}

	protected override void DestroyWindowCore(HandleRef hwnd) {
		WndUtil.DestroyWindow((wnd)hwnd.Handle);
		_w = default;
		_acc?.Dispose(); _acc = null;

		//workaround for: never GC-collected if disposed before removing from parent WPF element (shouldn't do it).
		if (this is IKeyboardInputSink iks) {
			Debug_.PrintIf(iks.KeyboardInputSite != null);
			iks.KeyboardInputSite?.Unregister();
		}

		//GC.ReRegisterForFinalize(this); //to detect memory leak
	}

	//~KScintilla() { print.it("~KScintilla"); } //to detect memory leak. Also enable the GC.ReRegisterForFinalize.

	nint _WndProc(wnd w, int msg, nint wp, nint lp) {
		//if (Name == "Recipe_text") WndUtil.PrintMsg(w, msg, wp, lp);
		//if(Name == "Recipe_text") WndUtil.PrintMsg(_w, msg, wp, lp, Api.WM_TIMER, Api.WM_MOUSEMOVE, Api.WM_SETCURSOR, Api.WM_NCHITTEST, Api.WM_PAINT, Api.WM_IME_SETCONTEXT, Api.WM_IME_NOTIFY);

		MButtons button = msg switch { Api.WM_LBUTTONDOWN => MButtons.Left, Api.WM_RBUTTONDOWN => MButtons.Right, Api.WM_MBUTTONDOWN => MButtons.Middle, _ => 0 };
		if (button != 0 && Api.GetFocus() != _w) {
			bool setFocus = !AaNoMouseSetFocus.Has(button);
			if (setFocus && msg == Api.WM_LBUTTONDOWN && AaInitReadOnlyAlways && !keys.gui.isAlt) { //don't focus if link clicked
				int pos = Call(SCI_CHARPOSITIONFROMPOINTCLOSE, Math2.LoShort(lp), Math2.HiShort(lp));
				if (pos >= 0) {
					if (aaaStyleHotspot(aaaStyleGetAt(pos))) setFocus = false;
					else { //indicator-link?
						uint indic = (uint)Call(SCI_INDICATORALLONFOR, pos);
						for (int i = 0; indic != 0; i++, indic >>>= 1)
							if (0 != (indic & 1) && 0 != Call(SCI_INDICGETHOVERFORE, i))
								setFocus = false;
					}
				}
			}
			if (setFocus) this.Focus();
		}

		switch (msg) {
		case Api.WM_SETFOCUS:
			if (!_inOnWmSetFocus) if (_OnWmSetFocus()) return 0;
			break;
		case Api.WM_KILLFOCUS:
			if (_inOnWmSetFocus) return 0;
			break;
		}

		//var R = CallRetPtr(msg, wp, lp); //no, then Scintilla does not process WM_NCDESTROY
		var R = Api.CallWindowProc(_wndprocScintilla, w, msg, wp, lp);

		return R;
	}

	protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
		if (msg == Api.WM_GETOBJECT) { //WPF steals it from _WndProc
			handled = true;
			return (_acc ??= new _Accessible(this)).WmGetobject(wParam, lParam);
		}
		return base.WndProc(hwnd, msg, wParam, lParam, ref handled);
	}

	protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi) {
		if (!_w.Is0 && newDpi.PixelsPerDip != oldDpi.PixelsPerDip) {
			_dpi = newDpi.PixelsPerInchY.ToInt();
			Call(SCI_SETCARETWIDTH, Dpi.Scale(2, _dpi));
			aaaMarginWidthsDpiChanged_();
		}
		base.OnDpiChanged(oldDpi, newDpi);
	}

	#region problems with focus, keyboard, destroying

	//Somehow WPF does not care about native control focus, normal keyboard work, destroying, etc.
	//1. No Tab key navigation. Also does not set focus when parent tab item selected.
	//	Workaround: override TabIntoCore and call API SetFocus.
	//2. Does not set logical focus to HwndHost when its native control is really focused. Then eg does not restore real focus after using menu.
	//	Workaround: set focus on WM_LBUTTONDOWN etc. Also on WM_SETFOCUS, with some tricks.
	//3. Steals arrow keys, Tab and Enter from native control and sets focus to other controls or closes dialog.
	//	Workaround: override TranslateAcceleratorCore, pass the keys to the control and return true.
	//4. When closing parent window, does not destroy hwnhosted controls. Instead moves to a hidden parking window, and destroys later on GC if you are careful.
	//	Need to always test whether hwnhosted controls are destroyed on GC, to avoid leaked windows + many managed objects.
	//	Eg to protect wndproc delegate from GC don't add it to a thread-static array until destroyed; let it be a field of the wrapper class.
	//	Or let app dispose the HwndHost in OnClosing. But control itself cannot reliably know when to self-destroy.
	//5. When closing parent window, briefly tries to show native control, and focus if was focused.
	//	Workaround: let app dispose the HwndHost in OnClosing.
	//Never mind: after SetFocus, Keyboard.FocusedElement is null.

	bool _OnWmSetFocus() {
		//keep logical focus on HwndHost, else will not work eg restoring of real focus when closing menu.
		if (IsVisible && Focusable) { //info: !IsVisible when closing window without disposing this (WPF bug)
			var fs = FocusManager.GetFocusScope(this);
			if (fs != null && FocusManager.GetFocusedElement(fs) != this) { //focused not by WPF
				_inOnWmSetFocus = true;
				FocusManager.SetFocusedElement(fs, this); //in some cases would work better than this.Focus()
				_inOnWmSetFocus = false;
				//all WPF 'Focus' functions make the main window focused. Then OnGotKeyboardFocus makes _w focused again.
				//	Wndproc receives wm_setfocus, wm_killfocus and wm_setfocus. Passes to scintilla only the last wm_setfocus.
				//	Can prevent this, eg set IsFocusable=false before, but then WPF in some cases does not restore focus after switching windows etc.
				//	To prevent this on click, Wndproc calls Focus, and scintilla does not call SetFocus (mod).
				//	Could not find a way to avoid this in other cases, never mind.
				return true;
			}
		}
		return false;
	}
	bool _inOnWmSetFocus;

	//Makes _w focused when called this.Focus() or Keyboard.Focus(this).
	protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e) {
		e.Handled = true;
		Api.SetFocus(_w);
		base.OnGotKeyboardFocus(e);
	}

	//Sets focus when tabbed to this or when clicked the parent tab item. Like eg WPF TextBox.
	protected override bool TabIntoCore(TraversalRequest request) {
		Focus();
		return true;
		//base.TabIntoCore(request); //empty func, returns false
	}

	protected override bool TranslateAcceleratorCore(ref System.Windows.Interop.MSG msg, ModifierKeys modifiers) {
		var m = msg.message;
		var k = (KKey)msg.wParam;
		//if (m == Api.WM_KEYDOWN) print.it(m, k);
		if (m is Api.WM_KEYDOWN or Api.WM_KEYUP /*or Api.WM_SYSKEYDOWN or Api.WM_SYSKEYUP*/)
			if (!modifiers.Has(ModifierKeys.Alt)) {
				switch (k) {
				case KKey.Left or KKey.Right or KKey.Up or KKey.Down:
				case KKey.Enter when modifiers == 0 && !aaaIsReadonly:
				case KKey.Tab when !modifiers.Has(ModifierKeys.Control) && !aaaIsReadonly:
					Call(msg.message, msg.wParam, msg.lParam); //not DispatchMessage or Send
					return true;
				case KKey.Insert when modifiers == 0: return true;
				}
			}

		return base.TranslateAcceleratorCore(ref msg, modifiers);
	}

	//Without this, user cannot type eg character 'a' in HwndHost'ed control if there is button with text like "_Apply".
	protected override bool TranslateCharCore(ref System.Windows.Interop.MSG msg, ModifierKeys modifiers) {
		if (msg.message is not Api.WM_CHAR or Api.WM_DEADCHAR) return false; //WM_SYSCHAR etc if with Alt
		if (msg.hwnd != _w.Handle) return false; //WPF bug. Eg when on key down the app makes this control focused.
		if ((int)msg.wParam <= 32) return false; //eg control chars on Ctrl+key
		_w.Send(msg.message, msg.wParam, msg.lParam); //not Call or WndProc
		return true;
	}

	#endregion

	#endregion

	void _NotifyCallback(void* cbParam, ref SCNotification n) {
		var code = n.code;
		//if(code != NOTIF.SCN_PAINTED) print.qm2.write(code.ToString());
		switch (code) {
		case NOTIF.SCN_MODIFIED:
			var mt = n.modificationType;
			//if(this.Name!= "Output_text") print.it(mt, n.position);
			if (mt.HasAny(MOD.SC_MOD_INSERTTEXT | MOD.SC_MOD_DELETETEXT)) {
				_text = null;
				_posState = default;
				_aPos.Clear();

				bool inserted = mt.Has(MOD.SC_MOD_INSERTTEXT);
				_RdOnModified(inserted, n);
				AaImages?.OnTextChanged_(inserted, n);
				AaTags?.OnTextChanged_(inserted, n);
			}
			//if(mt.Has(MOD.SC_MOD_CHANGEANNOTATION)) ChangedAnnotation?.Invoke(this, ref n);
			if (AaDisableModifiedNotifications) return;
			break;
		case NOTIF.SCN_HOTSPOTRELEASECLICK:
			AaTags?.OnLinkClick_(n.position, 0 != (n.modifiers & SCMOD_CTRL));
			break;
		}
		AaOnSciNotify(ref n);
	}

	/// <summary>
	/// Raises the <see cref="AaNotify"/> event.
	/// </summary>
	protected virtual void AaOnSciNotify(ref SCNotification n) {
		AaNotify?.Invoke(this, ref n);
		switch (n.code) {
		case NOTIF.SCN_MODIFIED:
			var e = AaTextChanged;
			if (e != null && n.modificationType.HasAny(MOD.SC_MOD_INSERTTEXT | MOD.SC_MOD_DELETETEXT)) e(this, EventArgs.Empty);
			break;
		}
	}

	public delegate void AaEventHandler(KScintilla c, ref SCNotification n);

	/// <summary>
	/// Occurs when any Scintilla notification is received.
	/// </summary>
	public event AaEventHandler AaNotify;

	/// <summary>
	/// Occurs when text changed.
	/// </summary>
	public event EventHandler AaTextChanged;

	/// <summary>
	/// Sends a Scintilla message to the control and returns int.
	/// Don't call this function from another thread.
	/// </summary>
	[DebuggerStepThrough]
	public int Call(int sciMessage, nint wParam = 0, nint lParam = 0) => (int)CallRetPtr(sciMessage, wParam, lParam);

	/// <summary>
	/// Sends a Scintilla message to the control and returns int.
	/// Don't call this function from another thread.
	/// </summary>
	[DebuggerStepThrough]
	public int Call(int sciMessage, nint wParam, void* lParam) => (int)CallRetPtr(sciMessage, wParam, (nint)lParam);

	/// <summary>
	/// Sends a Scintilla message to the control and returns int.
	/// Don't call this function from another thread.
	/// </summary>
	[DebuggerStepThrough]
	public int Call(int sciMessage, nint wParam, bool lParam) => (int)CallRetPtr(sciMessage, wParam, lParam ? 1 : 0);

	/// <summary>
	/// Sends a Scintilla message to the control and returns int.
	/// Don't call this function from another thread.
	/// </summary>
	[DebuggerStepThrough]
	public int Call(int sciMessage, bool wParam, nint lParam = 0) => (int)CallRetPtr(sciMessage, wParam ? 1 : 0, lParam);

	/// <summary>
	/// Sends a Scintilla message to the control and returns nint.
	/// Don't call this function from another thread.
	/// </summary>
	[DebuggerStepThrough]
	public nint CallRetPtr(int sciMessage, nint wParam = 0, nint lParam = 0) {
#if DEBUG
		if (AaDebugPrintMessages_) _DebugPrintMessage(sciMessage);
#endif

		Debug.Assert(!_w.Is0);
		//Debug.Assert(!_w.Is0 || this.DesignMode);
		//if(!IsHandleCreated) CreateHandle();
		//note: auto-creating handle is not good:
		//	1. May create parked control. Not good for performance.
		//	2. Can be dangerous, eg if passing a reusable buffer that also is used when creating handle.

		Debug_.PrintIf(process.thisThreadId != _w.ThreadId, "wrong thread");

		return Sci_Call(_sciPtr, sciMessage, wParam, lParam);
	}

#if DEBUG
	static void _DebugPrintMessage(int sciMessage) {
		if (sciMessage < SCI_START) return;
		switch (sciMessage) {
		case SCI_COUNTCODEUNITS:
		case SCI_POSITIONRELATIVECODEUNITS:
		case SCI_CANUNDO:
		case SCI_CANREDO:
		case SCI_GETREADONLY:
		case SCI_GETSELECTIONEMPTY:
			//case SCI_GETTEXTLENGTH:
			return;
		}
		if (s_debugPM == null) {
			s_debugPM = new();
			foreach (var v in typeof(Sci).GetFields()) {
				var s = v.Name;
				//print.it(v.Name);
				if (s.Starts("SCI_")) s_debugPM.Add((int)v.GetRawConstantValue(), s);
			}
		}
		if (!s_debugPM.TryGetValue(sciMessage, out var k)) {
			k = sciMessage.ToString();
		}
		print.qm2.write(k);
	}
	static Dictionary<int, string> s_debugPM;

	internal bool AaDebugPrintMessages_ { get; set; }
#endif

	#region properties

	/// <summary>
	/// Border style.
	/// Must be set before creating control handle.
	/// </summary>
	public virtual bool AaInitBorder { get; set; }

	/// <summary>
	/// Use the default Scintilla's context menu.
	/// Must be set before creating control handle.
	/// </summary>
	public virtual bool AaInitUseDefaultContextMenu { get; set; }

	/// <summary>
	/// This control is used just to display text, not to edit.
	/// Must be set before creating control handle.
	/// </summary>
	public virtual bool AaInitReadOnlyAlways { get; set; }

	/// <summary>
	/// Whether to show images specified in tags like &lt;image "image file path"&gt;, including icons of non-image file types.
	/// Must be set before creating control handle.
	/// If false, <see cref="AaImages"/> property is null.
	/// </summary>
	public virtual bool AaInitImages { get; set; }

	/// <summary>
	/// See <see cref="AaInitTagsStyle"/>.
	/// </summary>
	public enum AaTagsStyle {
		/// <summary>Don't support tags. The <see cref="AaTags"/> property is null.</summary>
		NoTags,

		/// <summary>Let <see cref="aaaText"/>, aaaSetText and aaaAppendText parse tags when the text has prefix "&lt;&gt;".</summary>
		AutoWithPrefix,

		/// <summary>Let <see cref="aaaText"/>, aaaSetText and aaaAppendText parse tags always.</summary>
		AutoAlways,

		/// <summary>Tags are parsed only when calling Tags.AddText.</summary>
		User,
	}

	/// <summary>
	/// Whether and when supports tags.
	/// Must be set before creating control handle.
	/// </summary>
	public virtual AaTagsStyle AaInitTagsStyle { get; set; }

	/// <summary>
	/// Whether to show arrows etc to make wrapped lines more visible.
	/// Must be set before creating control handle.
	/// </summary>
	public virtual bool AaInitWrapVisuals { get; set; } = true;

	/// <summary>
	/// Word-wrap.
	/// </summary>
	public virtual bool AaWrapLines {
		get => _wrapLines;
		set {
			if (value != _wrapLines) {
				_wrapLines = value;
				if (!_w.Is0) Call(SCI_SETWRAPMODE, value ? SC_WRAP_WORD : 0);
			}
		}
	}
	bool _wrapLines;

	/// <summary>
	/// Whether uses Enter key.
	/// If null (default), false if <see cref="AaInitReadOnlyAlways"/> is true.
	/// </summary>
	public bool? AaUsesEnter { get; set; }

	/// <summary>
	/// On SCN_MODIFIED notifications suppress <see cref="AaOnSciNotify"/>, <see cref="AaNotify"/> and <see cref="AaTextChanged"/>.
	/// Use to temporarily disable 'modified' notifications. Never use SCI_SETMODEVENTMASK, because then the control would stop working correctly.
	/// </summary>
	public bool AaDisableModifiedNotifications { get; set; }

	/// <summary>
	/// Don't set focus on mouse left/right/middle button down.
	/// </summary>
	public MButtons AaNoMouseSetFocus { get; set; }

	#endregion

	#region range data

	struct _RangeData {
		public int from, to;
		public object data;
	}

	List<_RangeData> _rd;
	bool _rdLocked;

	/// <summary>
	/// Attaches any data to a range of text. Like a hidden indicator with attached data of any type.
	/// </summary>
	/// <exception cref="InvalidOperationException">Called from <see cref="AaRangeDataRemoved"/>.</exception>
	public void AaRangeDataAdd(bool utf16, Range r, object data) {
		if (_rdLocked) throw new InvalidOperationException("Called from event handler.");
		var (from, to) = aaaNormalizeRange(utf16, r);

		_rd ??= new();
		_rd.Add(new() { from = from, to = to, data = data });
	}

	/// <summary>
	/// Gets data of type <i>T</i> attached to a range of text with <see cref="AaRangeDataAdd"/> at the specified position.
	/// </summary>
	/// <param name="data">Receives data. Use type <b>object</b> to get data of any type.</param>
	/// <returns>true if <i>pos</i> is in a range added with <see cref="AaRangeDataAdd"/> and the range data type is <i>T</i> (or inherited).</returns>
	/// <exception cref="InvalidOperationException">Called from <see cref="AaRangeDataRemoved"/>.</exception>
	public bool AaRangeDataGet<T>(bool utf16, int pos, out T data) where T : class
		=> AaRangeDataGet(utf16, pos, out data, out _, out _);

	/// <summary>
	/// Gets data of type <i>T</i> attached to a range of text with <see cref="AaRangeDataAdd"/> at the specified position.
	/// </summary>
	/// <param name="data">Receives data. Use type <b>object</b> to get data of any type.</param>
	/// <returns>true if <i>pos</i> is in a range added with <see cref="AaRangeDataAdd"/> and the range data type is <i>T</i> (or inherited).</returns>
	/// <exception cref="InvalidOperationException">Called from <see cref="AaRangeDataRemoved"/>.</exception>
	public bool AaRangeDataGet<T>(bool utf16, int pos, out T data, out int from, out int to) where T : class {
		if (_rdLocked) throw new InvalidOperationException("Called from AaRangeDataRemoved.");
		pos = _ParamPos(utf16, pos);
		foreach (ref var v in _rd.AsSpan()) {
			if (pos >= v.from && pos < v.to && v.data is T d) {
				data = d;
				from = v.from;
				to = v.to;
				return true;
			}
		}
		data = null;
		from = 0;
		to = 0;
		return false;
	}

	/// <summary>
	/// When a text range registered with <see cref="AaRangeDataAdd"/> removed (when control text changed).
	/// </summary>
	/// <remarks>
	/// The event handler must not modify control text and must not call <b>AaRangeDataX</b> functions.
	/// </remarks>
	public event Action<object> AaRangeDataRemoved;

	void _RdOnModified(bool inserted, in SCNotification n) {
		if (_rd.NE_()) return;
		if (_rdLocked) throw new InvalidOperationException("Called from event handler.");
		_rdLocked = true;
		try {
			int start = n.position, end = start + n.length, len = n.length;

			if (inserted) {
				foreach (ref var v in _rd.AsSpan()) {
					if (start < v.to) {
						if (start <= v.from) v.from += len;
						v.to += len;
					}
				}
			} else {
				if (start == 0 && aaaLen8 == 0) { //deleted all text
					if (AaRangeDataRemoved != null) foreach (var v in _rd) AaRangeDataRemoved(v.data);
					_rd.Clear();
					return;
				}

				System.Collections.BitArray remove = null;
				int j = -1;
				foreach (ref var v in _rd.AsSpan()) {
					j++;
					if (start < v.to) {
						if (end < v.from) {
							v.from -= len;
							v.to -= len;
						} else if (start <= v.from) {
							if (end < v.to) {
								v.to -= len;
								v.from = start;
							} else {
								remove ??= new(_rd.Count);
								remove[j] = true;
								AaRangeDataRemoved?.Invoke(v.data);
							}
						} else if (end < v.to) {
							v.to -= len;
						} else {
							v.to = start;
						}
					}
				}

				if (remove != null) {
					for (int i = remove.Count; --i >= 0;) {
						if (remove[i]) _rd.RemoveAt(i);
					}
				}
			}
		}
		finally { _rdLocked = false; }

		//print.it("ranges", _rd.Select(o => (o.from, o.to, o.data)));
	}

	#endregion

	#region acc

	_Accessible _acc;

	class _Accessible : HwndHostAccessibleBase_ {
		readonly KScintilla _sci;

		internal _Accessible(KScintilla sci) : base(sci, sci.AaWnd) {
			_sci = sci;
		}

		public override ERole Role(int child) => _sci.AaAccessibleRole;

		public override string Name(int child) => _sci.AaAccessibleName;

		public override string Description(int child) => _sci.AaAccessibleDescription;

		public override string Value(int child) => _sci.AaAccessibleValue;

		public override EState State(int child) {
			var r = base.State(child);
			if (_sci.aaaIsReadonly) r |= EState.READONLY;
			return r;
		}
	}

	protected virtual ERole AaAccessibleRole => ERole.TEXT;

	protected virtual string AaAccessibleName => Name;

	protected virtual string AaAccessibleDescription => null;

	protected virtual string AaAccessibleValue => AaInitReadOnlyAlways ? aaaText?.Limit(0xffff) : null;

	#endregion
}
