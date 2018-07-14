using System;
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
//using System.Linq;

using Au.Types;
using static Au.NoClass;

namespace Au
{
	/// <summary>
	/// Blocks keyboard and/or mouse input events from reaching applications.
	/// </summary>
	/// <remarks>
	/// Uses keyboard and/or mouse hooks. Does not use API <b>BlockInput</b>, it does not work on current Windows versions.
	/// Blocks hardware-generated events and software-generated events, except if generated by functions of this library.
	/// Functions of this library that send keys or text use this class internally, to block user-pressed keys and resend them afterwards (see <see cref="ResendBlockedKeys"/>).
	/// Does not block:
	/// <list type="bullet">
	/// <item>In windows of the same thread that started blocking. For example, if your script shows a message box, the user can click its buttons.</item>
	/// <item>In windows of higher <see cref="Process_.UacInfo">UAC</see> integrity level (IL) processes, unless this process has uiAccess IL.</item>
	/// <item>In special screens such as when you press Ctrl+Alt+Delete or when you launch and admin program. See also <see cref="ResumeAfterCtrlAltDelete"/>.</item>
	/// <item>Some Windows hotkeys, such as Ctrl+Alt+Delete and Win+L.</item>
	/// <item><see cref="DoNotBlockKeys"/> keys.</item>
	/// <item>Keyboard hooks don't work in windows of this process if this process uses direct input or raw input API.</item>
	/// </list>
	/// 
	/// To stop blocking, can be used the 'using' pattern, like in the example. Or the 'try/finally' pattern, where the finally block calls <see cref="Dispose"/> or <see cref="Stop"/>. Also automatically stops when this thread ends. Users can stop with Ctrl+Alt+Delete.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// using(new BlockUserInput(BIEvents.All)) {
	/// 	Print("blocked");
	/// 	5.s();
	/// }
	/// Print("unblocked");
	/// ]]></code>
	/// </example>
	public unsafe class BlockUserInput :IDisposable
	{
		IntPtr _syncEvent, _stopEvent;
		IntPtr _threadHandle;
		Keyb _blockedKeys;
		long _startTime;
		BIEvents _block;
		int _threadId;
		bool _disposed;

		//note: don't use Api.BlockInput because:
		//	UAC. Fails if our process has Medium IL.
		//	Too limited, eg cannot block only keys or only mouse.

		/// <summary>
		/// This constructor does nothing (does not call <see cref="Start"/>).
		/// </summary>
		public BlockUserInput() { }

		/// <summary>
		/// This constructor calls <see cref="Start"/>.
		/// </summary>
		/// <exception cref="ArgumentException"><paramref name="what"/> is 0.</exception>
		public BlockUserInput(BIEvents what)
		{
			Start(what);
		}

		/// <summary>
		/// Starts blocking.
		/// </summary>
		/// <exception cref="ArgumentException"><paramref name="what"/> is 0.</exception>
		/// <exception cref="InvalidOperationException">Already started.</exception>
		public void Start(BIEvents what)
		{
			if(_disposed) throw new ObjectDisposedException(nameof(BlockUserInput));
			if(_block != 0) throw new InvalidOperationException();
			if(!what.HasAny_(BIEvents.All)) throw new ArgumentException();

			_block = what;
			_startTime = Time.Milliseconds;

			_syncEvent = Api.CreateEvent(default, false, false, null);
			_stopEvent = Api.CreateEvent(default, false, false, null);
			_threadHandle = Api.OpenThread(Api.SYNCHRONIZE, false, _threadId = Api.GetCurrentThreadId());

			ThreadPool.QueueUserWorkItem(_this => (_this as BlockUserInput)._ThreadProc(), this);

			Api.WaitForSingleObject(_syncEvent, Api.INFINITE);
			GC.KeepAlive(this);
		}

		/// <summary>
		/// Calls <see cref="Stop"/>.
		/// </summary>
		public void Dispose()
		{
			if(!_disposed) {
				_disposed = true;
				Stop();
				GC.SuppressFinalize(this);
			}
		}

		///
		~BlockUserInput() => _CloseHandles();

		void _CloseHandles()
		{
			if(_syncEvent != default) {
				Api.CloseHandle(_syncEvent); _syncEvent = default;
				Api.CloseHandle(_stopEvent); _stopEvent = default;
				Api.CloseHandle(_threadHandle); _threadHandle = default;
			}
		}

		/// <summary>
		/// Stops blocking.
		/// Plays back blocked keys if need. See <see cref="ResendBlockedKeys"/>.
		/// Does nothing if currently is not blocking.
		/// </summary>
		/// <param name="discardBlockedKeys">Do not play back blocked keys recorded because of <see cref="ResendBlockedKeys"/>.</param>
		public void Stop(bool discardBlockedKeys = false)
		{
			if(_block == 0) return;
			_block = 0;

			Api.SetEvent(_stopEvent);
			Api.WaitForSingleObject(_syncEvent, Api.INFINITE);
			_CloseHandles();

			var bk = _blockedKeys;
			if(bk != null && !discardBlockedKeys) {
				_blockedKeys = null;
				if(Time.Milliseconds - _startTime < c_maxResendTime) {
					bk.Options.NoCapsOff = bk.Options.NoModOff = true;
					try { bk.Send(); }
					catch(Exception ex) { Debug_.Print(ex.Message); }
				}
			}
		}

		const int c_maxResendTime = 10000;

		void _ThreadProc()
		{
			Util.WinHook hk = null, hm = null; Util.AccHook hwe = null;
			try {
				try {
					if(_block.Has_(BIEvents.Keys))
						hk = Util.WinHook.Keyboard(_keyHookProc ?? (_keyHookProc = _KeyHookProc));
					if(_block.HasAny_(BIEvents.MouseClicks | BIEvents.MouseMoving))
						hm = Util.WinHook.Mouse(_mouseHookProc ?? (_mouseHookProc = _MouseHookProc));
				}
				catch(AuException e1) { Debug_.Print(e1); _block = 0; return; } //failed to hook
				Api.SetEvent(_syncEvent);

				try { hwe = new Util.AccHook(AccEVENT.SYSTEM_DESKTOPSWITCH, 0, _winEventProc ?? (_winEventProc = _WinEventProc)); }
				catch(AuException e1) { Debug_.Print(e1); } //failed to hook
				//info: the hook detects Ctrl+Alt+Del, Win+L, UAC consent, etc. SystemEvents.SessionSwitch only Win+L.

				//Print("started");
				WaitFor.LibWait(-1, WHFlags.DoEvents, _stopEvent, _threadHandle);
				//Print("ended");
			}
			finally {
				hk?.Dispose();
				hm?.Dispose();
				hwe?.Dispose();
				Api.SetEvent(_syncEvent);
			}
			GC.KeepAlive(this);
		}

		Func<HookData.Keyboard, bool> _keyHookProc;
		Func<HookData.Mouse, bool> _mouseHookProc;
		Action<HookData.AccHookData> _winEventProc;

		bool _KeyHookProc(HookData.Keyboard x)
		{
			if(_DoNotBlock(x.IsInjected, x.dwExtraInfo, x.vkCode)) return false;
			//Print(message, x.vkCode);

			//if(x.vkCode == KKey.Delete && !x.IsUp) {
			//	//Could detect Ctrl+Alt+Del here. But SetWinEventHook(SYSTEM_DESKTOPSWITCH) is better.
			//}

			if(ResendBlockedKeys && Time.Milliseconds - _startTime < c_maxResendTime) {
				if(_blockedKeys == null) _blockedKeys = new Keyb(Opt.Static.Key);
				_blockedKeys.LibAddRaw(x.vkCode, (ushort)x.scanCode, x.LibSendInputFlags);
				//Print(x.vkCode);
			}
			return true;
		}

		bool _MouseHookProc(HookData.Mouse x)
		{
			bool isMMove = x.Event == HookData.MouseEvent.Move;
			switch(_block & (BIEvents.MouseClicks | BIEvents.MouseMoving)) {
			case BIEvents.MouseClicks | BIEvents.MouseMoving: break;
			case BIEvents.MouseClicks: if(isMMove) return false; break;
			case BIEvents.MouseMoving: if(!isMMove) return false; break;
			}
			return !_DoNotBlock(x.IsInjected, x.dwExtraInfo, 0, isMMove);
		}

		bool _DoNotBlock(bool isInjected, LPARAM extraInfo, KKey vk = 0, bool isMMove = false)
		{
			if(_pause) return true;
			if(isInjected) {
				if(extraInfo == DoNotBlockInjectedExtraInfo || DoNotBlockInjected) return true;
			}
			Wnd w;
			if(vk != 0) {
				var a = DoNotBlockKeys;
				if(a != null) foreach(var k in a) if(vk == k) return true;
				w = Wnd.WndActive;
			} else {
				w = isMMove ? Wnd.WndActive : Wnd.FromMouse();
				//note: don't use hook's pt, because of a bug in some OS versions.
				//note: for wheel it's better to use FromMouse.
			}
			if(w.ThreadId == _threadId) return true;
			return false;
		}

		void _WinEventProc(HookData.AccHookData x)
		{
			//the hook is called before and after Ctrl+Alt+Del screen. Only idEventThread different.
			//	GetForegroundWindow returns 0. WTSGetActiveConsoleSessionId returns main session.

			//Print("desktop switch"); //return;

			_startTime = 0; //don't resend Ctrl+Alt+Del and other blocked keys
			if(!ResumeAfterCtrlAltDelete)
				ThreadPool.QueueUserWorkItem(_this => (_this as BlockUserInput).Stop(), this);
		}

		/// <summary>
		/// Continue blocking when returned from a special screen where blocking is disabled: Ctrl+Alt+Delete, <see cref="Process_.UacInfo">UAC</see> consent, etc.
		/// </summary>
		public bool ResumeAfterCtrlAltDelete { get; set; }

		/// <summary>
		/// Record blocked keys, and play back when stopped blocking.
		/// </summary>
		/// <remarks>
		/// Will not play back if: 1. The blocking time is &gt;= 10 seconds. 2. Detected Ctrl+Alt+Delete, <see cref="Process_.UacInfo">UAC</see> consent or some other special screen. 3. Called <see cref="Pause"/>.
		/// </remarks>
		public bool ResendBlockedKeys { get; set; }

		/// <summary>
		/// Do not block software-generated key/mouse events if this extra info value was set when calling API <msdn>SendInput</msdn>.
		/// The default value is the value used by functions of this library, that is why their events are not blocked. If changed, their events are blocked, unless <see cref="DoNotBlockInjected"/> is true.
		/// </summary>
		public int DoNotBlockInjectedExtraInfo { get; set; } = Api.AuExtraInfo;

		/// <summary>
		/// Do not block software-generated key/mouse events.
		/// If false (default), only events generated by functions of this library are not blocked, unless <see cref="DoNotBlockInjectedExtraInfo"/> is changed.
		/// </summary>
		public bool DoNotBlockInjected { get; set; }

		/// <summary>
		/// Do not block these keys.
		/// Default: { KKey.Pause, KKey.PrintScreen, KKey.F1 }.
		/// </summary>
		/// <remarks>
		/// The array should contain keys without modifier key flags.
		/// </remarks>
		public static KKey[] DoNotBlockKeys { get; set; } = new KKey[] { KKey.Pause, KKey.PrintScreen, KKey.F1 };

		/// <summary>
		/// Gets or sets whether the blocking is paused.
		/// </summary>
		/// <remarks>
		/// The 'set' function is much faster than <see cref="Stop"/>/<see cref="Start"/>. Does not remove hooks etc. Discards blocked keys.
		/// </remarks>
		public bool Pause
		{
			get => _pause;
			set
			{
				_pause = value;
				_startTime = 0; //don't resend blocked keys
			}
		}
		bool _pause;
	}
}

namespace Au.Types
{
	/// <summary>
	/// Used with <see cref="BlockUserInput"/> class to specify what user input types to block (keys, mouse).
	/// </summary>
	[Flags]
	public enum BIEvents
	{
		/// <summary>
		/// Do not block.
		/// </summary>
		None,

		/// <summary>
		/// Block keys. Except if generated by functions of this library.
		/// </summary>
		Keys = 1,

		/// <summary>
		/// Block mouse clicks and wheel. Except if generated by functions of this library.
		/// </summary>
		MouseClicks = 2,

		/// <summary>
		/// Block mouse moving. Except if generated by functions of this library.
		/// </summary>
		MouseMoving = 4,

		/// <summary>
		/// Block keys, mouse clicks, wheel and mouse moving. Except if generated by functions of this library.
		/// This flag incluses flags <b>Keys</b>, <b>MouseClicks</b> and <b>MouseMoving</b>.
		/// </summary>
		All = 7,
	}


}
