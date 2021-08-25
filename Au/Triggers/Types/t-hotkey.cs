namespace Au.Triggers
{
	/// <summary>
	/// Flags of hotkey triggers.
	/// </summary>
	[Flags]
	public enum TKFlags : byte
	{
		/// <summary>
		/// Allow other apps to receive the key down message too.
		/// Without this flag, other apps usually receive only modifier keys. Also, OS always receives Ctrl+Alt+Delete and some other hotkeys.
		/// To receive and block key messages is used a low-level hook. Other hooks may receive blocked messages or not, depending on when they were set. 
		/// </summary>
		ShareEvent = 1,

		/// <summary>
		/// Run the action when the key and modifier keys are released.
		/// </summary>
		KeyModUp = 2,

		/// <summary>
		/// The trigger works only with left-side modifier keys.
		/// </summary>
		LeftMod = 4,

		/// <summary>
		/// The trigger works only with right-side modifier keys.
		/// </summary>
		RightMod = 8,

		/// <summary>
		/// Don't release modifier keys.
		/// Without this flag, for example if trigger is ["Ctrl+K"], when the user presses Ctrl and K down, the trigger sends Ctrl key-up event, making the key logically released, although it is still physically pressed. Then modifier keys don't interfer with the action. However functions like <see cref="keys.getMod"/> and <see cref="keys.waitForKey"/> (and any such functions in any app) will not know that the key is physically pressed; there is no API to get physical key state.
		/// <note>Unreleased modifier keys will interfere with mouse functions like <see cref="mouse.click"/>. Will not interfere with keyboard and clipboard functions of this library, because they release modifier keys, unless <b>opt.key.NoModOff</b> is true. Will not interfere with functions that send text, unless <b>opt.key.NoModOff</b> is true and <b>opt.key.TextHow</b> is <b>OKeyText.KeysX</b>.</note>.
		/// Other flags that prevent releasing modifier keys: <b>KeyUp</b>, <b>ShareEvent</b>. Then don't need this flag.
		/// </summary>
		NoModOff = 16,
	}

	/// <summary>
	/// Represents a hotkey trigger.
	/// </summary>
	public class HotkeyTrigger : ActionTrigger
	{
		internal readonly KMod modMasked, modMask;
		internal readonly TKFlags flags;
		string _paramsString;

		internal HotkeyTrigger(ActionTriggers triggers, Action<HotkeyTriggerArgs> action, KMod mod, KMod modAny, TKFlags flags, string paramsString, (string, int) source)
			: base(triggers, action, true, source) {
			const KMod csaw = KMod.Ctrl | KMod.Shift | KMod.Alt | KMod.Win;
			modMask = ~modAny & csaw;
			modMasked = mod & modMask;
			this.flags = flags;
			_paramsString = flags == 0 ? paramsString : paramsString + " (" + flags.ToString() + ")"; //print.it(_paramsString);
		}

		internal override void Run(TriggerArgs args) => RunT(args as HotkeyTriggerArgs);

		/// <summary>
		/// Returns "Hotkey".
		/// </summary>
		public override string TypeString => "Hotkey";

		/// <summary>
		/// Returns a string containing trigger parameters.
		/// </summary>
		public override string ParamsString => _paramsString;

		///
		public TKFlags Flags => flags;
	}

	/// <summary>
	/// Hotkey triggers.
	/// </summary>
	/// <example>See <see cref="ActionTriggers"/>.</example>
	public class HotkeyTriggers : ITriggers, IEnumerable<HotkeyTrigger>
	{
		ActionTriggers _triggers;
		Dictionary<int, ActionTrigger> _d = new Dictionary<int, ActionTrigger>();

		internal HotkeyTriggers(ActionTriggers triggers) {
			_triggers = triggers;
		}

		/// <summary>
		/// Adds a hotkey trigger.
		/// </summary>
		/// <param name="hotkey">
		/// A hotkey, like with the <see cref="keys.send"/> function.
		/// Can contain 0 to 4 modifier keys (Ctrl, Shift, Alt, Win) and 1 non-modifier key.
		/// Examples: "F11", "Ctrl+K", "Ctrl+Shift+Alt+Win+A".
		/// To ignore modifiers: "?+K". Then the trigger works with any combination of modifiers.
		/// To ignore a modifier: "Ctrl?+K". Then the trigger works with or without the modifier. More examples: "Ctrl?+Shift?+K", "Ctrl+Shift?+K".
		/// </param>
		/// <param name="flags"></param>
		/// <param name="f_">[](xref:caller_info)</param>
		/// <param name="l_">[](xref:caller_info)</param>
		/// <exception cref="ArgumentException">Invalid hotkey string or flags.</exception>
		/// <exception cref="InvalidOperationException">Cannot add triggers after <see cref="ActionTriggers.Run"/> was called, until it returns.</exception>
		/// <example>See <see cref="ActionTriggers"/>.</example>
		public Action<HotkeyTriggerArgs> this[string hotkey, TKFlags flags = 0, [CallerFilePath] string f_ = null, [CallerLineNumber] int l_ = 0] {
			set {
				//This could be used instead of [CallerX] parameters, but can be too slow if many triggers. Definitely too slow for menus and toolbars.
				//perf.first();
				//var uu = new StackFrame(1, true); //first time 30 ms, then 30 mcs
				//perf.next();
				//var us = uu.GetFileName(); //0
				//perf.next();
				//var ui = uu.GetFileLineNumber(); //0
				//perf.next();
				//uu = new StackFrame(2, true); //slow too
				//perf.nw();
				////print.it(us, ui);
				////print.it(uu);

				if (!keys.more.ParseHotkeyTriggerString_(hotkey, out var mod, out var modAny, out var key, false)) throw new ArgumentException("Invalid hotkey string.");
				_Add(value, key, mod, modAny, flags, hotkey, (f_, l_));
			}
		}

		/// <summary>
		/// Adds a hotkey trigger.
		/// </summary>
		/// <param name="key"></param>
		/// <param name="modKeys">
		/// Modifier keys, like with the <see cref="keys.send"/> function.
		/// Examples: "Ctrl", "Ctrl+Shift+Alt+Win".
		/// To ignore modifiers: "?". Then the trigger works with any combination of modifiers.
		/// To ignore a modifier: "Ctrl?". Then the trigger works with or without the modifier. More examples: "Ctrl?+Shift?", "Ctrl+Shift?".
		/// </param>
		/// <param name="flags"></param>
		/// <param name="f_">[](xref:caller_info)</param>
		/// <param name="l_">[](xref:caller_info)</param>
		/// <exception cref="ArgumentException">Invalid modKeys string or flags.</exception>
		/// <exception cref="InvalidOperationException">Cannot add triggers after <see cref="ActionTriggers.Run"/> was called, until it returns.</exception>
		public Action<HotkeyTriggerArgs> this[KKey key, string modKeys, TKFlags flags = 0, [CallerFilePath] string f_ = null, [CallerLineNumber] int l_ = 0] {
			set {
				var ps = key.ToString(); if (ps[0].IsAsciiDigit()) ps = "VK" + ps;
				if (!modKeys.NE()) ps = modKeys + "+" + ps;

				if (!keys.more.ParseHotkeyTriggerString_(modKeys, out var mod, out var modAny, out _, true)) throw new ArgumentException("Invalid modKeys string.");
				_Add(value, key, mod, modAny, flags, ps, (f_, l_));
			}
		}

		void _Add(Action<HotkeyTriggerArgs> action, KKey key, KMod mod, KMod modAny, TKFlags flags, string paramsString, (string, int) source) {
			if (mod == 0 && flags.HasAny((TKFlags.LeftMod | TKFlags.RightMod))) throw new ArgumentException("Invalid flags.");
			_triggers.ThrowIfRunning_();
			//actually could safely add triggers while running.
			//	Currently would need just lock(_d) in several places. Also some triggers of this type must be added before starting, else we would not have the hook etc.
			//	But probably not so useful. Makes programming more difficult. If need, can Stop, add triggers, then Run again.

			//print.it($"key={key}, mod={mod}, modAny={modAny}");
			var t = new HotkeyTrigger(_triggers, action, mod, modAny, flags, paramsString, source);
			t.DictAdd(_d, (int)key);
			_lastAdded = t;
		}

		/// <summary>
		/// The last added trigger.
		/// </summary>
		public HotkeyTrigger Last => _lastAdded;
		HotkeyTrigger _lastAdded;

		bool ITriggers.HasTriggers => _lastAdded != null;

		void ITriggers.StartStop(bool start) {
			_UpClear();
			_eatUp = 0;
		}

		internal bool HookProc(HookData.Keyboard k, TriggerHookContext thc) {
			//print.it(k.vkCode, !k.IsUp);
			Debug.Assert(!k.IsInjectedByAu); //server must ignore

			KKey key = k.vkCode;
			KMod mod = thc.Mod;
			bool up = k.IsUp;
			if (!up) _UpClear();

			if (thc.ModThis != 0) {
				if (_upTrigger != null && mod == 0 && _upKey == 0) _UpTriggered(thc);
			} else if (up) {
				if (key == _upKey) {
					_upKey = 0;
					if (_upTrigger != null && mod == 0) _UpTriggered(thc);
				}
				if (key == _eatUp) {
					_eatUp = 0;
					return true;
					//To be safer, could return false if keys.isPressed(_eatUp), but then can interfere with the trigger action.
				}
				//CONSIDER: _upTimeout.
			} else {
				//if(key == _eatUp) _eatUp = 0;
				_eatUp = 0;

				if (_d.TryGetValue((int)key, out var v)) {
					HotkeyTriggerArgs args = null;
					for (; v != null; v = v.next) {
						var x = v as HotkeyTrigger;
						if ((mod & x.modMask) != x.modMasked) continue;

						switch (x.flags & (TKFlags.LeftMod | TKFlags.RightMod)) {
						case TKFlags.LeftMod: if (thc.ModL != mod) continue; break;
						case TKFlags.RightMod: if (thc.ModR != mod) continue; break;
						}

						if (v.DisabledThisOrAll) continue;

						if (args == null) thc.args = args = new HotkeyTriggerArgs(x, thc.Window, key, mod); //may need for scope callbacks too
						else args.Trigger = x;

						if (!x.MatchScopeWindowAndFunc(thc)) continue;

						if (x.action != null) {
							if (0 != (x.flags & TKFlags.KeyModUp)) {
								_upTrigger = x;
								_upArgs = args;
								_upKey = key;
							} else {
								thc.trigger = x;
							}
						}

						//print.it(key, mod);
						if (0 != (x.flags & TKFlags.ShareEvent)) return false;

						if (thc.trigger == null) { //KeyModUp or action==null
							if (mod == KMod.Alt || mod == KMod.Win || mod == (KMod.Alt | KMod.Win)) {
								//print.it("need Ctrl");
								ThreadPool.QueueUserWorkItem(o => keys.Internal_.SendKey(KKey.Ctrl)); //disable Alt/Win menu
							}
						} else if (mod != 0) {
							if (0 == (x.flags & TKFlags.NoModOff)) thc.muteMod = TriggerActionThreads.c_modRelease;
							else if (mod == KMod.Alt || mod == KMod.Win || mod == (KMod.Alt | KMod.Win)) thc.muteMod = TriggerActionThreads.c_modCtrl;
						}

						_eatUp = key;
						return true;
					}
				}
			}
			return false;
		}

		HotkeyTrigger _upTrigger;
		HotkeyTriggerArgs _upArgs;
		KKey _upKey;
		KKey _eatUp;

		void _UpTriggered(TriggerHookContext thc) {
			thc.args = _upArgs;
			thc.trigger = _upTrigger;
			_UpClear();
		}

		void _UpClear() {
			_upTrigger = null;
			_upArgs = null;
			_upKey = 0;
		}

		/// <summary>
		/// Used by foreach to enumerate added triggers.
		/// </summary>
		public IEnumerator<HotkeyTrigger> GetEnumerator() {
			foreach (var kv in _d) {
				for (var v = kv.Value; v != null; v = v.next) {
					var x = v as HotkeyTrigger;
					yield return x;
				}
			}
		}

		IEnumerator IEnumerable.GetEnumerator() {
			throw new NotImplementedException();
		}
	}

	/// <summary>
	/// Arguments for actions of hotkey triggers.
	/// </summary>
	public class HotkeyTriggerArgs : TriggerArgs
	{
		///
		public HotkeyTrigger Trigger { get; internal set; }

		///
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override ActionTrigger TriggerBase => Trigger;

		/// <summary>
		/// The active window.
		/// </summary>
		public wnd Window { get; }

		/// <summary>
		/// The pressed key.
		/// </summary>
		public KKey Key { get; }

		/// <summary>
		/// The pressed modifier keys.
		/// </summary>
		/// <remarks>
		/// Can be useful when the trigger ignores modifiers. For example "?+F11" or "Shift?+A".
		/// </remarks>
		public KMod Mod { get; }

		///
		public HotkeyTriggerArgs(HotkeyTrigger trigger, wnd w, KKey key, KMod mod) {
			Trigger = trigger;
			Window = w; Key = key; Mod = mod;
		}
	}
}
