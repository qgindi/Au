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
//using System.Linq;

using Au.Types;

namespace Au.Triggers
{
	/// <summary>
	/// The main class of action triggers.
	/// </summary>
	/// <remarks>
	/// Action triggers are used to call functions (aka <i>trigger actions</i>) in a running script in response to events such as hotkey, typed text, mouse action, activated window. To launch scripts are used other ways: manually, at startup, command line, <see cref="ATask.Run"/>, output link.
	/// 
	/// This class manages action triggers. The <see cref="AScript.Triggers"/> property gets its instance. Through it you access all trigger types (hotkey, window, etc) and add triggers to them.
	/// 
	/// Code syntax to add an action trigger:
	/// <code>Triggers.TriggerType[parameters] = action;</code>
	/// Examples:
	/// <code>
	/// Triggers.Hotkey["Ctrl+K"] = o => AOutput.Write(o.Trigger);
	/// Triggers.Hotkey["Ctrl+Shift+K"] = o => {
	///		AOutput.Write("This is a trigger action (lambda function).");
	///		AOutput.Write($"It runs when you press {o.Trigger}.");
	/// };
	/// Triggers.Run();
	/// </code>
	/// 
	/// Also you can set options (<see cref="TriggerOptions"/>), window scopes (<see cref="TriggerScopes"/>) and custom scopes (<see cref="TriggerFuncs"/>) for triggers added afterwards.
	/// 
	/// Finally call <see cref="Run"/>. It runs all the time (like <b>Application.Run</b>) and launches trigger actions (functions) when need. Actions run in other thread(s) by default.
	/// 
	/// Recommended properties for scripts containing triggers:
	/// - <c>runMode blue</c> - allows other scripts to start while this script is running.
	/// - <c>ifRunning warn_restart</c> - to quickly restart the script when editing: just click the Run button.
	/// 
	/// Avoid multiple scripts with triggers. Each running instance uses some CPU. All triggers should be in single script, if possible. It's OK to run additional scripts temporarily, for example to test new triggers without restarting the main script. From trigger actions you can call <see cref="ATask.Run"/> to run other scripts in new process; see example.
	/// 
	/// Trigger actions don't inherit <b>AOpt</b> options that are set before adding triggers. The example shows two ways how to set <b>AOpt</b> options for multiple actions. Also you can set them in action code. Next action running in the same thread will not inherit <b>AOpt</b> options set by previous action; the trigger engine calls <see cref="AOpt.Reset"/> before executing an action.
	/// </remarks>
	/// <example>
	/// This is a single script with many action triggers.
	/// <code><![CDATA[
	/// using Au.Triggers; //add this above or below other 'using' directives
	/// 
	/// //you can set options for triggers added afterwards
	/// Triggers.Options.RunActionInThread(0, 500);
	/// 
	/// //you can use variables if don't want to type "Triggers.Hotkey" etc for each trigger
	/// var hk = Triggers.Hotkey;
	/// var mouse = Triggers.Mouse;
	/// var window = Triggers.Window;
	/// var tt = Triggers.Autotext;
	/// 
	/// //hotkey triggers
	/// 
	/// hk["Ctrl+K"] = o => AOutput.Write(o.Trigger); //it means: execute code "o => AOutput.Write(o.Trigger)" when I press Ctrl+K
	/// hk["Ctrl+Shift+F11"] = o => {
	/// 	AOutput.Write(o.Trigger);
	/// 	var w1 = AWnd.FindOrRun("* Notepad", run: () => AExec.Run(AFolders.System + "notepad.exe"));
	/// 	AKeys.Text("text");
	/// 	w1.Close();
	/// };
	/// hk["Win+Alt+K"] = o => ATask.Run("other script.cs"); //run other script in new process
	/// 
	/// //triggers that work only with some windows
	/// 
	/// Triggers.Of.Window("* WordPad", "WordPadClass"); //let the following triggers work only when a WordPad window is active
	/// hk["Ctrl+F5"] = o => AOutput.Write(o.Trigger, o.Window);
	/// hk["Ctrl+F6"] = o => AOutput.Write(o.Trigger, o.Window);
	/// 
	/// var notepad = Triggers.Of.Window("* Notepad"); //let the following triggers work only when a Notepad window is active
	/// hk["Ctrl+F5"] = o => AOutput.Write(o.Trigger, o.Window);
	/// hk["Ctrl+F6"] = o => AOutput.Write(o.Trigger, o.Window);
	/// 
	/// Triggers.Of.AllWindows(); //let the following triggers work with all windows
	/// 
	/// //mouse triggers
	/// 
	/// mouse[TMClick.Right, "Ctrl+Shift", TMFlags.ButtonModUp] = o => AOutput.Write(o.Trigger);
	/// mouse[TMEdge.RightInCenter50] = o => { AOutput.Write(o.Trigger); ADialog.Show("Bang!", x: Coord.Max); };
	/// mouse[TMMove.LeftRightInCenter50] = o => AWnd.SwitchActiveWindow();
	/// 
	/// Triggers.FuncOf.NextTrigger = o => AKeys.IsScrollLock; //example of a custom scope (aka context, condition)
	/// mouse[TMWheel.Forward] = o => AOutput.Write($"{o.Trigger} while ScrollLock is on");
	/// 
	/// Triggers.Of.Again(notepad); //let the following triggers work only when a Notepad window is active
	/// mouse[TMMove.LeftRightInBottom25] = o => { AOutput.Write(o.Trigger); o.Window.Close(); };
	/// Triggers.Of.AllWindows();
	/// 
	/// //window triggers. Note: window triggers don't depend on Triggers.Of.
	/// 
	/// window[TWEvent.ActiveNew, "* Notepad", "Notepad"] = o => AOutput.Write("opened Notepad window");
	/// window[TWEvent.ActiveNew, "Notepad", "#32770", contains: "Do you want to save *"] = o => {
	/// 	AOutput.Write("opened Notepad's 'Do you want to save' dialog");
	/// 	//AKeys.Key("Alt+S"); //click the Save button
	/// };
	/// 
	/// //autotext triggers
	/// 
	/// tt["los"] = o => o.Replace("Los Angeles");
	/// tt["WIndows", TAFlags.MatchCase] = o => o.Replace("Windows");
	/// tt.DefaultPostfixType = TAPostfix.None;
	/// tt["<b>"] = o => o.Replace("<b>[[|]]</b>");
	/// tt["#file"] = o => {
	/// 	o.Replace("");
	/// 	var fd = new OpenFileDialog();
	/// 	if(fd.ShowDialog() == DialogResult.OK) AKeys.Text(fd.FileName);
	/// };
	/// tt.DefaultPostfixType = default;
	/// 
	/// //shorter auto-replace code
	/// 
	/// var ts = Triggers.Autotext.SimpleReplace;
	/// ts["#so"] = "Some text"; //the same as tt["#so"] = o => o.Replace("Some text");
	/// ts["#mo"] = "More text";
	/// 
	/// //how to set AOpt options for trigger actions
	/// 
	/// //AOpt.Key.TextOption = KTextOption.Paste; //no, it won't work. It sets AOpt for this thread, not for trigger actions.
	/// Triggers.Options.BeforeAction = o => { AOpt.Key.TextOption = KTextOption.Paste; }; //the correct way. Sets AOpt before executing an action.
	/// ts["#p1"] = "text 1";
	/// ts["#p2"] = "text 2";
	/// Triggers.Options.BeforeAction = null;
	/// 
	/// //another way to set AOpt options - use AOpt.Static. It sets options for all actions in the script, not just for triggers added afterwards.
	/// 
	/// AOpt.Static.Key.PasteLength = 50;
	/// AOpt.Static.Key.Hook = h => { var w1 = h.w.Window; AOutput.Write(w1); if(w1.Name.Like("* Word")) h.opt.PasteEnter = true; };
	/// ts["#p3"] = "/* " + new string('*', 60) + " */\r\n";
	/// 
	/// //how to stop and disable/enable triggers
	/// 
	/// hk["Ctrl+Alt+Q"] = o => Triggers.Stop(); //let Triggers.Run() end its work and return
	/// hk.Last.EnabledAlways = true;
	/// 
	/// hk["Ctrl+Alt+D"] = o => Triggers.Disabled ^= true; //disable/enable triggers here
	/// hk.Last.EnabledAlways = true;
	/// 
	/// hk["Ctrl+Alt+Win+D"] = o => ActionTriggers.DisabledEverywhere ^= true; //disable/enable triggers in all processes
	/// hk.Last.EnabledAlways = true;
	/// 
	/// hk["Ctrl+F7"] = o => AOutput.Write("This trigger can be disabled/enabled with Ctrl+F8.");
	/// var t1 = hk.Last;
	/// hk["Ctrl+F8"] = o => t1.Disabled ^= true; //disable/enable a trigger
	/// 
	/// //finally call Triggers.Run(). Without it the triggers won't work.
	/// Triggers.Run();
	/// //Triggers.Run returns when is called Triggers.Stop (see the "Ctrl+Alt+Q" trigger above).
	/// AOutput.Write("called Triggers.Stop");
	/// ]]></code>
	/// </example>
	public class ActionTriggers
	{
		readonly ITriggers[] _t;
		ITriggers this[TriggerType e] => _t[(int)e];

		/// <summary>
		/// Initializes a new instance of this class.
		/// </summary>
		/// <remarks>
		/// In automation scripts don't need to create new instances of this class. Instead use the <see cref="AScript.Triggers"/> property to get an instance.
		/// </remarks>
		public ActionTriggers()
		{
			_t = new ITriggers[(int)TriggerType.Count];
			scopes_ = new TriggerScopes();
			funcs_ = new TriggerFuncs();
			options_ = new TriggerOptions();
		}

		//public TriggerScopes Of {
		//	get {
		//		if(_test == false){
		//			ADebug.MemorySetAnchor_();
		//			ATimer.After(500, _ => ADebug.MemoryPrint_());
		//		}

		//		var k=new StackFrame(1, true);
		//		//new StackTrace(1, true);
		//		var s = k.GetFileName();

		//		//if(_test == false) _s1 = s; else AOutput.Write(ReferenceEquals(s, _s1));

		//			 _test = true;

		//		return scopes;
		//	}
		//}
		//static bool _test;
		//static string _s1;

		/// <summary>
		/// Allows to set window scopes (working windows) for triggers.
		/// </summary>
		/// <remarks>Examples: <see cref="TriggerScopes"/>, <see cref="ActionTriggers"/>.</remarks>
		public TriggerScopes Of => scopes_;
		internal readonly TriggerScopes scopes_;

		/// <summary>
		/// Allows to set custom scopes/contexts/conditions for triggers.
		/// </summary>
		/// <remarks>More info and examples: <see cref="TriggerFuncs"/>, <see cref="ActionTriggers"/>.</remarks>
		public TriggerFuncs FuncOf => funcs_;
		internal readonly TriggerFuncs funcs_;

		/// <summary>
		/// Allows to set some options for multiple triggers and their actions.
		/// </summary>
		/// <remarks>More info and examples: <see cref="TriggerOptions"/>, <see cref="ActionTriggers"/>.</remarks>
		public TriggerOptions Options => options_;
		internal readonly TriggerOptions options_;

		ITriggers _Get(TriggerType e)
		{
			int i = (int)e;
			var t = _t[i];
			if(t == null) {
				switch(e) {
				case TriggerType.Hotkey: t = new HotkeyTriggers(this); break;
				case TriggerType.Autotext: t = new AutotextTriggers(this); break;
				case TriggerType.Mouse: t = new MouseTriggers(this); break;
				case TriggerType.Window: t = new WindowTriggers(this); break;
				default: Debug.Assert(false); break;
				}
				_t[i] = t;
			}
			return t;
		}

		/// <summary>
		/// Hotkey triggers.
		/// </summary>
		/// <example>See <see cref="ActionTriggers"/>.</example>
		public HotkeyTriggers Hotkey => _Get(TriggerType.Hotkey) as HotkeyTriggers;

		/// <summary>
		/// Autotext triggers.
		/// </summary>
		/// <example>See <see cref="ActionTriggers"/>.</example>
		public AutotextTriggers Autotext => _Get(TriggerType.Autotext) as AutotextTriggers;

		/// <summary>
		/// Mouse triggers.
		/// </summary>
		/// <example>See <see cref="ActionTriggers"/>.</example>
		public MouseTriggers Mouse => _Get(TriggerType.Mouse) as MouseTriggers;

		/// <summary>
		/// Window triggers.
		/// </summary>
		/// <example>See <see cref="ActionTriggers"/>.</example>
		public WindowTriggers Window => _Get(TriggerType.Window) as WindowTriggers;

		/// <summary>
		/// Makes triggers alive.
		/// </summary>
		/// <remarks>
		/// This function monitors hotkeys, activated windows and other events. When an event matches an added trigger, launches the thrigger's action, which runs in other thread by default.
		/// Does not return immediately. Runs until this process is terminated or <see cref="Stop"/> called.
		/// </remarks>
		/// <example>See <see cref="ActionTriggers"/>.</example>
		/// <exception cref="InvalidOperationException">Already running.</exception>
		/// <exception cref="AuException">Something failed.</exception>
		public unsafe void Run()
		{
			//ADebug.PrintLoadedAssemblies(true, true, true);

			ThrowIfRunning_();

			//bool haveTriggers = false;
			HooksThread.UsedEvents hookEvents = 0;
			_windowTriggers = null;
			for(int i = 0; i < _t.Length; i++) {
				var t = _t[i];
				if(t == null || !t.HasTriggers) continue;
				//haveTriggers = true;
				switch((TriggerType)i) {
				case TriggerType.Hotkey: hookEvents |= HooksThread.UsedEvents.Keyboard; break;
				case TriggerType.Autotext: hookEvents |= HooksThread.UsedEvents.Keyboard | HooksThread.UsedEvents.Mouse; break;
				case TriggerType.Mouse: hookEvents |= (t as MouseTriggers).UsedHookEvents_; break;
				case TriggerType.Window: _windowTriggers = t as WindowTriggers; break;
				}
			}
			//AOutput.Write(haveTriggers, (uint)llHooks);
			//if(!haveTriggers) return; //no. The message loop may be used for toolbars etc.

			if(!s_wasRun) {
				s_wasRun = true;
				AWnd.More.RegisterWindowClass(c_cn);
			}
			_wMsg = AWnd.More.CreateMessageOnlyWindow(_WndProc, c_cn);
			_mainThreadId = AThread.NativeId;
			_winTimerPeriod = 0;
			_winTimerLastTime = 0;

			if(hookEvents != 0) {
				//prevent big delay later on first LL hook event while hook proc waits
				if(!s_wasKM) {
					s_wasKM = true;
					ThreadPool.QueueUserWorkItem(_ => {
						try {
							//using var p1 = APerf.Create();
							new AWnd.Finder("*a").IsMatch(AWnd.GetWnd.Root); //if used window scopes etc
							_ = AHookWin.LowLevelHooksTimeout; //slow JIT of registry functions
							Util.AJit.Compile(typeof(ActionTriggers), nameof(_WndProc), nameof(_KeyMouseEvent));
							Util.AJit.Compile(typeof(TriggerHookContext), nameof(TriggerHookContext.InitContext), nameof(TriggerHookContext.PerfEnd), nameof(TriggerHookContext.PerfWarn));
							Util.AJit.Compile(typeof(ActionTrigger), nameof(ActionTrigger.MatchScopeWindowAndFunc));
							Util.AJit.Compile(typeof(HotkeyTriggers), nameof(HotkeyTriggers.HookProc));
							AutotextTriggers.JitCompile();
							MouseTriggers.JitCompile();
						}
						catch(Exception ex) { ADebug.Print(ex); }
					});
				}

				_thc = new TriggerHookContext(this);

				_ht = new HooksThread(hookEvents, _wMsg);
			}

			try {
				_evStop = Api.CreateEvent(false);
				_StartStopAll(true);
				IntPtr h = _evStop;
				_Wait(&h, 1);
			}
			finally {
				if(hookEvents != 0) {
					_ht.Dispose(); _ht = null;
				}
				Api.DestroyWindow(_wMsg); _wMsg = default;
				Stopping?.Invoke(this, EventArgs.Empty);
				_evStop.Dispose();
				_StartStopAll(false);
				_mainThreadId = 0;
				_threads?.Dispose(); _threads = null;
			}

			void _StartStopAll(bool start)
			{
				foreach(var t in _t) {
					if(t?.HasTriggers ?? false) t.StartStop(start);
				}
			}
		}

		int _mainThreadId;
		AWnd _wMsg;
		HooksThread _ht;
		TriggerHookContext _thc;
		static bool s_wasRun, s_wasKM;
		const string c_cn = "Au.Triggers.Hooks";

		LPARAM _WndProc(AWnd w, int message, LPARAM wParam, LPARAM lParam)
		{
			try {
				switch(message) {
				case Api.WM_USER + 1:
					//_ht.Return((int)wParam, false); //test speed without _KeyMouseEvent
					_KeyMouseEvent((int)wParam, (HooksThread.UsedEvents)(int)lParam);
					return 0;
				case Api.WM_USER + 20:
					_windowTriggers.SimulateNew_(wParam, lParam);
					return 0;
				}
			}
			catch(Exception ex) { ADebug.Print(ex.Message); return default; }

			return Api.DefWindowProc(w, message, wParam, lParam);
		}

		unsafe void _KeyMouseEvent(int messageId, HooksThread.UsedEvents eventType)
		{
			//APerf.First();
			//APerf.Next();
			_thc.InitContext();
			//APerf.Next();
			bool eat = false;
			if(eventType == HooksThread.UsedEvents.Keyboard) {
				//AOutput.Write("key");
				if(!_ht.GetKeyData(messageId, out var data)) return;
				var k = new HookData.Keyboard(null, &data);
				_thc.InitMod(k);
				if(this[TriggerType.Hotkey] is HotkeyTriggers tk) { //if not null
					eat = tk.HookProc(k, _thc);
				}
				if(!eat /*&& _thc.trigger == null*/ && this[TriggerType.Autotext] is AutotextTriggers ta) {
					ta.HookProc(k, _thc);
				}
			} else if(eventType == HooksThread.UsedEvents.MouseEdgeMove) {
				//AOutput.Write("edge/move");
				if(this[TriggerType.Mouse] is MouseTriggers tm) {
					if(!_ht.GetEdgeMoveData(messageId, out var data)) return;
					tm.HookProcEdgeMove(data, _thc);
				}
			} else {
				//AOutput.Write(_ht.mouseMessage_);
				if(this[TriggerType.Mouse] is MouseTriggers tm) {
					if(!_ht.GetClickWheelData(messageId, out var data, out int message)) return;
					var k = new HookData.Mouse(null, message, &data);
					eat = tm.HookProcClickWheel(k, _thc);
				}
			}
			//APerf.Next();
			_thc.PerfWarn();
			//APerf.Next();

			//var mem = GC.GetTotalMemory(false);
			//if(mem != _debugMem && _debugMem != 0) AOutput.Write(mem - _debugMem);
			//_debugMem = mem;

			if(!_ht.Return(messageId, eat)) return;
			//APerf.NW();
			if(_thc.trigger != null) RunAction_(_thc.trigger, _thc.args, _thc.muteMod);
		}

		//long _debugMem;

		internal void RunAction_(ActionTrigger trigger, TriggerArgs args, int muteMod = 0)
		{
			if(trigger.action != null) {
				_threads ??= new TriggerActionThreads();
				_threads.Run(trigger, args, muteMod);
			} else Debug.Assert(muteMod == 0);
		}
		TriggerActionThreads _threads;

		/// <summary>
		/// Stops trigger engines and causes <see cref="Run"/> to return.
		/// </summary>
		/// <remarks>
		/// Does not abort threads of trigger actions that are still running.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// Triggers.Hotkey["Ctrl+T"] = o => AOutput.Write("Ctrl+T");
		/// Triggers.Hotkey["Ctrl+Q"] = o => { AOutput.Write("Ctrl+Q (stop)"); Triggers.Stop(); };
		/// Triggers.Hotkey.Last.EnabledAlways = true;
		/// Triggers.Run();
		/// AOutput.Write("stopped");
		/// ]]></code>
		/// </example>
		public void Stop()
		{
			Api.SetEvent(_evStop);
		}
		Handle_ _evStop;

		/// <summary>
		/// Occurs before <see cref="Run"/> stops trigger engines and returns.
		/// </summary>
		public event EventHandler Stopping;

		/// <summary>
		/// True if executing <see cref="Run"/>.
		/// </summary>
		internal bool Running_ => !_evStop.Is0;

		/// <summary>
		/// Throws InvalidOperationException if executing <see cref="Run"/>.
		/// </summary>
		internal void ThrowIfRunning_()
		{
			if(Running_) throw new InvalidOperationException("Must be before or after Triggers.Run.");
		}

		/// <summary>
		/// Throws InvalidOperationException if not executing <see cref="Run"/>.
		/// </summary>
		internal void ThrowIfNotRunning_()
		{
			if(!Running_) throw new InvalidOperationException("Cannot be before or after Triggers.Run.");
		}

		/// <summary>
		/// Throws InvalidOperationException if not thread of <see cref="Run"/>.
		/// </summary>
		internal void ThrowIfNotMainThread_()
		{
			if(AThread.NativeId != _mainThreadId) throw new InvalidOperationException("Must be in thread of Triggers.Run, for example in a FuncOf function.");
		}

		/// <summary>
		/// Gets or sets whether triggers of this <see cref="ActionTriggers"/> instance are disabled.
		/// </summary>
		/// <remarks>
		/// Does not depend on <see cref="DisabledEverywhere"/>.
		/// Does not end/pause threads of trigger actions.
		/// </remarks>
		/// <seealso cref="ActionTrigger.EnabledAlways"/>
		/// <seealso cref="TriggerOptions.EnabledAlways"/>
		/// <example>
		/// <code><![CDATA[
		/// Triggers.Hotkey["Ctrl+T"] = o => AOutput.Write("Ctrl+T");
		/// Triggers.Hotkey["Ctrl+D"] = o => { AOutput.Write("Ctrl+D (disable/enable)"); Triggers.Disabled ^= true; }; //toggle
		/// Triggers.Hotkey.Last.EnabledAlways = true;
		/// Triggers.Run();
		/// ]]></code>
		/// </example>
		public bool Disabled { get; set; }

		/// <summary>
		/// Gets or sets whether triggers are disabled in all processes that use this library in this user session.
		/// </summary>
		/// <example>See <see cref="ActionTriggers"/>.</example>
		/// <seealso cref="Disabled"/>
		/// <seealso cref="TriggerOptions.EnabledAlways"/>
		public static unsafe bool DisabledEverywhere {
			get => Util.SharedMemory_.Ptr->triggers.disabled;
			set {
				if(value == DisabledEverywhere) return;
				Util.SharedMemory_.Ptr->triggers.disabled = value;
				var w = ATask.WndMsg_; if(!w.Is0) w.SendNotify(Api.WM_USER, 20); //update tray icon etc
			}
		}

		[StructLayout(LayoutKind.Sequential, Size = 16)] //note: this struct is in shared memory. Size must be same in all library versions.
		internal struct SharedMemoryData_
		{
			public bool disabled;
			public bool resetAutotext;
		}

		internal void Notify_(int message, LPARAM wParam = default, LPARAM lParam = default)
		{
			_wMsg.SendNotify(message, wParam, lParam);
		}

		unsafe int _Wait(IntPtr* ha, int nh)
		{
			for(; ; ) {
				int slice = -1;
				if(_winTimerPeriod > 0) {
					long t = ATime.PerfMilliseconds;
					if(_winTimerLastTime == 0) _winTimerLastTime = t;
					int td = (int)(t - _winTimerLastTime);
					int period = _Period();
					if(td >= period - 5) {
						_winTimerLastTime = t;
						_windowTriggers?.Timer_();
						slice = _Period();
					} else slice = period - td;

					int _Period() => _winTimerPeriod / 15 * 15 + 10;

					//This code is a variable-frequency timer that uses less CPU than Windows timer.
					//	Never mind: the timer does not work if user code creates a nested message loop in this thread. They should avoid it. It is documented, such functions must return ASAP.
				}
				var k = Api.MsgWaitForMultipleObjectsEx(nh, ha, slice, Api.QS_ALLINPUT, Api.MWMO_ALERTABLE | Api.MWMO_INPUTAVAILABLE);
				if(k == nh) { //message, COM RPC, hook, etc
					while(Api.PeekMessage(out var m, default, 0, 0, Api.PM_REMOVE)) {
						Api.TranslateMessage(m);
						Api.DispatchMessage(m);
					}
				} else if(!(k == Api.WAIT_TIMEOUT || k == Api.WAIT_IO_COMPLETION)) return k; //signaled handle, abandoned mutex, WAIT_FAILED (-1)
			}
		}
		long _winTimerLastTime;
		WindowTriggers _windowTriggers;

		internal int WinTimerPeriod_ {
			get => _winTimerPeriod;
			set {
				long t = ATime.PerfMilliseconds;
				int td = (int)(t - _winTimerLastTime);
				if(td > 10) _winTimerLastTime = t;
				_winTimerPeriod = value;
			}
		}
		int _winTimerPeriod;
	}

	enum TriggerType
	{
		Hotkey,
		Autotext,
		Mouse,
		Window,

		Count,

		//TimerAfter,
		//TimerEvery,
		//TimerAt,
	}

	interface ITriggers
	{
		/// <summary>
		/// Return true if added triggers of this type.
		/// </summary>
		bool HasTriggers { get; }

		/// <summary>
		/// Optionally start/stop trigger engine (hooks etc).
		/// </summary>
		/// <param name="start"></param>
		void StartStop(bool start);
	}

	class TriggerHookContext : WFCache
	{
		//internal readonly ActionTriggers triggers;
		AWnd _w;
		bool _haveWnd, _mouseWnd; POINT _p;

		public TriggerHookContext(ActionTriggers triggers)
		{
			//this.triggers = triggers;
			_perfList = new _ScopeTime[32];
			base.CacheName = true; //we'll call Clear(onlyName: true) at the start of each event
		}

		public AWnd Window {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get {
				if(!_haveWnd) {
					_haveWnd = true;
					_w = _mouseWnd ? AWnd.FromXY(_p, WXYFlags.NeedWindow) : AWnd.Active;
				}
				return _w;
			}
		}

		/// <summary>
		/// Trigger/action to run. Set by a hook proc of a trigger engine.
		/// </summary>
		public ActionTrigger trigger;

		/// <summary>
		/// Used with <see cref="trigger"/>.
		/// </summary>
		public TriggerArgs args;

		/// <summary>
		/// Used with <see cref="trigger"/>.
		/// Can be 0 or one of TriggerActionThreads.c_ constants.
		/// </summary>
		public int muteMod;

		///// <summary>
		///// This event was processed (not ignored). Set by a hook proc of a trigger engine.
		///// </summary>
		//public bool processed;

		/// <summary>
		/// Called before processing each hook event. Clears most properties and fields.
		/// </summary>
		public void InitContext()
		{
			_w = default; _haveWnd = _mouseWnd = false;
			base.Clear(onlyName: true);
			trigger = null; args = null; muteMod = 0;
			_perfLen = 0;
		}

		/// <summary>
		/// Tells to get window (for scope) from the specified point. If not called, will use the active window. In any case, gets window on demand.
		/// </summary>
		public void UseWndFromPoint(POINT p)
		{
			_mouseWnd = true; _p = p;
		}

		struct _ScopeTime
		{
			public int time, avgTime;
		}

		long _perfTime;
		_ScopeTime[] _perfList; //don't use List<> because its JIT is too slow in time-critical code
		int _perfLen;

		public void PerfStart()
		{
			_perfTime = ATime.PerfMicroseconds;
		}

		public void PerfEnd(bool isFunc, ref int perfTime)
		{
			long tLong = ATime.PerfMicroseconds - _perfTime;
			int time = (int)Math.Min(tLong, 1_000_000_000);

			//calc average time of this scope. Assume the first time is 0.
			if(perfTime != 0) perfTime = Math.Max(1, (int)(((long)perfTime * 7 + time) / 8));
			//AOutput.Write($"time={time}, avg={perfTime}");

			if(isFunc) time |= unchecked((int)0x80000000);
			if(_perfLen == _perfList.Length) Array.Resize(ref _perfList, _perfList.Length * 2);
			_perfList[_perfLen++] = new _ScopeTime { time = time, avgTime = perfTime };
			if(perfTime == 0) perfTime = 1;
		}

		public void PerfWarn()
		{
			if(_perfLen == 0) return;
			long ttTrue = 0, ttCompare = 0;
			for(int i = 0; i < _perfLen; i++) {
				var v = _perfList[i];
				int t = v.time & 0x7fffffff, ta;
				ttTrue += t;
				if(v.avgTime == 0) ta = Math.Max(t - 150_000, 0); //first time. Can be slow JIT and assembly loading.
				else ta = Math.Min(t, v.avgTime);
				ttCompare += ta;
			}
			ttCompare /= 1000; ttTrue /= 1000;
			//AOutput.Write(ttTrue, ttCompare);
			if(ttCompare <= 25 && (ttTrue < 200 || ttTrue < AHookWin.LowLevelHooksTimeout - 100)) return;
			var b = new StringBuilder();
			b.AppendFormat("<>Warning: Too slow trigger scope detection (Triggers.Of or Triggers.FuncOf). Time: {0} ms. Task name: {1}. <fold>", ttTrue, ATask.Name);
			for(int i = 0; i < _perfLen; i++) {
				var v = _perfList[i];
				int t = v.time & 0x7fffffff;
				b.AppendFormat("\t{0} {1,8}.{2:D3} ms", v.time < 0 ? "F" : "W", t / 1000, t % 1000);
				if(v.avgTime > 0) b.AppendFormat(", average {0}.{1:D3} ms", v.avgTime / 1000, v.avgTime % 1000);
				b.AppendLine();
			}
			b.Append("* W - Triggers.Of (window); F - Triggers.FuncOf.</fold>");
			ThreadPool.QueueUserWorkItem(s1 => AOutput.Write(s1), b.ToString()); //4 ms first time. Async because Write() JIT slow.
		}

		/// <summary>
		/// Currently pressed modifier keys. Valid only in hotkey and autotext triggers.
		/// </summary>
		public KMod Mod => _mod;

		/// <summary>
		/// Currently pressed left-side modifier keys. Valid only in hotkey and autotext triggers.
		/// </summary>
		public KMod ModL => _modL;

		/// <summary>
		/// Currently pressed right-side modifier keys. Valid only in hotkey and autotext triggers.
		/// </summary>
		public KMod ModR => _modR;

		/// <summary>
		/// Not 0 if this key event is a modifier key. Valid only in hotkey and autotext triggers.
		/// </summary>
		public KMod ModThis => _modThis;

		KMod _mod, _modL, _modR, _modThis;
		long _lastKeyTime;

		/// <summary>
		/// Called before processing each keyboard hook event.
		/// Updates Mod, ModL, ModR, IsThisKeyMod. They are used by hotkey and autotext triggers.
		/// </summary>
		public void InitMod(HookData.Keyboard k)
		{
			KMod modL = 0, modR = 0;
			switch(k.vkCode) {
			case KKey.LCtrl: modL = KMod.Ctrl; break;
			case KKey.LShift: modL = KMod.Shift; break;
			case KKey.LAlt: modL = KMod.Alt; break;
			case KKey.Win: modL = KMod.Win; break;
			case KKey.RCtrl: modR = KMod.Ctrl; break;
			case KKey.RShift: modR = KMod.Shift; break;
			case KKey.RAlt: modR = KMod.Alt; break;
			case KKey.RWin: modR = KMod.Win; break;
			}

			if((_modThis = (modL | modR)) != 0) {
				if(k.IsUp) {
					_modL &= ~modL; _modR &= ~modR;
				} else {
					_modL |= modL; _modR |= modR;
				}
				_mod = _modL | _modR;
			} else if(!k.IsUp) {
				//We cannot trust _mod, because hooks are unreliable. We may not receive some events because of hook timeout, other hooks, OS quirks, etc. Also triggers may start while a modifier key is pressed.
				//And we cannot use AKeys.IsPressed, because our triggers release modifiers. Also Key() etc. Then triggers could not be auto-repeated.
				//We use both. If IsPressed(mod), add mod to _mod. Else remove from _mod after >5 s since the last seen key event. The max auto-repeat delay that you can set in CP is ~1 s.
				TrigUtil.GetModLR(out modL, out modR);
				//ADebug.PrintIf(modL != _modL || modR != _modR, $"KEY={k.vkCode}    modL={modL}  _modL={_modL}    modR={modR}  _modR={_modR}"); //normally should be only when auto-repeating a trigger
				_modL |= modL; _modR |= modR;
				long time = ATime.WinMilliseconds;
				if(time - _lastKeyTime > 5000) {
					_modL &= modL; _modR &= modR;
				}
				_mod = _modL | _modR;
				//AOutput.Write(_mod, k.vkCode);
				_lastKeyTime = time;
			}
		}
	}
}
