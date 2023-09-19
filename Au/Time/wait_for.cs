namespace Au {
	public static partial class wait {
		/// <summary>
		/// Waits for a user-defined condition. Until the callback function returns a value other than <c>default(T)</c>, for example <c>true</c>.
		/// </summary>
		/// <param name="secondsTimeout">Timeout, seconds. Can be 0 (infinite), &gt;0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout).</param>
		/// <param name="condition">Callback function (eg lambda). It is called repeatedly, until returns a value other than <c>default(T)</c>. The calling period depends on <i>options</i>.</param>
		/// <param name="options">Options. If null, uses <see cref="opt.wait"/>.</param>
		/// <returns>Returns the value returned by the callback function. On timeout returns <c>default(T)</c> if <i>secondsTimeout</i> is negative; else exception.</returns>
		/// <example>See <see cref="wait"/>.</example>
		/// <seealso cref="wait"/>
		public static T forCondition<T>(double secondsTimeout, Func<T> condition, OWait options = null) {
			var to = new WaitLoop(secondsTimeout, options);
			for (; ; ) {
				T r = condition();
				if (!EqualityComparer<T>.Default.Equals(r, default)) return r;
				if (!to.Sleep()) return r;
			}
		}
		//CONSIDER: rename: wait.until. Then also forMessagesAndCondition => forMessagesUntil.
		//	But not always it's good. Example: var link = wait.until(3, () => clipboard.text);
		//	Maybe wait.forFunc or wait.func. Then forMessagesFunc or funcMsg.
		
		/// <summary>
		/// Waits for a kernel object (event, mutex, etc).
		/// </summary>
		/// <param name="secondsTimeout">Timeout, seconds. Can be 0 (infinite), &gt;0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout).</param>
		/// <param name="flags"></param>
		/// <param name="handles">One or more handles of kernel objects. Max 63.</param>
		/// <returns>
		/// Returns 1-based index of the first signaled handle. Negative if abandoned mutex.
		/// On timeout returns 0 if <i>secondsTimeout</i> is negative; else exception.
		/// </returns>
		/// <exception cref="TimeoutException"><i>secondsTimeout</i> time has expired (if &gt; 0).</exception>
		/// <exception cref="AuException">Failed. For example a handle is invalid.</exception>
		/// <remarks>
		/// Uses API <msdn>WaitForMultipleObjectsEx</msdn> or <msdn>MsgWaitForMultipleObjectsEx</msdn>. Alertable.
		/// Does not use <see cref="opt.wait"/>.
		/// </remarks>
		public static int forHandle(double secondsTimeout, WHFlags flags, params IntPtr[] handles) {
			return WaitS_(secondsTimeout, flags, null, null, handles);
		}
		
		/// <summary>
		/// Waits for handles or/and msgCallback returning true or/and stopVar becoming true.
		/// Calls <c>Wait_(long timeMS, ...)</c>.
		/// </summary>
		/// <returns>
		/// <br/>• 0 if timeout (if secondsTimeout&lt;0),
		/// <br/>• 1-handles.Length if signaled,
		/// <br/>• -(1-handles.Length) if abandoned mutex,
		/// <br/>• 1+handles.Length if msgCallback returned true,
		/// <br/>• 2+handles.Length if stop became true.
		/// </returns>
		internal static int WaitS_(double secondsTimeout, WHFlags flags, object msgCallback, WaitVariable_ stopVar, params IntPtr[] handles) {
			long timeMS = _TimeoutS2MS(secondsTimeout, out bool canThrow);
			
			int r = Wait_(timeMS, flags, msgCallback, stopVar, handles);
			if (r < 0) throw new AuException(0);
			if (r == Api.WAIT_TIMEOUT) {
				if (canThrow) throw new TimeoutException();
				return 0;
			}
			r++; if (r > Api.WAIT_ABANDONED_0) r = -r;
			return r;
		}
		
		static long _TimeoutS2MS(double s, out bool canThrow) {
			canThrow = false;
			if (s == 0) return -1;
			if (s < 0) s = -s; else canThrow = true;
			return checked((long)(s * 1000d));
		}
		
		/// <summary>
		/// Waits for a signaled kernel handle. Or just sleeps, if handles is null/empty.
		/// If flag DoEvents, dispatches received messages etc.
		/// Calls API <msdn>WaitForMultipleObjectsEx</msdn> or <msdn>MsgWaitForMultipleObjectsEx</msdn> with QS_ALLINPUT. Alertable.
		/// When a handle becomes signaled, returns its 0-based index. If abandoned mutex, returns 0-based index + Api.WAIT_ABANDONED_0 (0x80).
		/// If timeMS>0, waits max timeMS and on timeout returns Api.WAIT_TIMEOUT.
		/// If failed, returns -1. Supports <see cref="lastError"/>.
		/// </summary>
		internal static int Wait_(long timeMS, WHFlags flags, params IntPtr[] handles) {
			return Wait_(timeMS, flags, null, null, handles);
		}
		
		/// <summary>
		/// The same as <see cref="Wait_(long, WHFlags, IntPtr[])"/> + can wait for message and variable.
		/// If msgCallback is not null, calls it when dispatching messages. If returns true, stops waiting and returns handles?.Length.
		/// 	If it is WPMCallback, calls it before dispatching a posted message.
		/// 	If it is Func{bool}, calls it after dispatching one or more messages.
		/// If stopVar is not null, when it becomes true stops waiting and returns handles?.Length + 1.
		/// </summary>
		internal static unsafe int Wait_(long timeMS, WHFlags flags, object msgCallback, WaitVariable_ stopVar, params IntPtr[] handles) {
			int nHandles = handles?.Length ?? 0;
			bool doEvents = flags.Has(WHFlags.DoEvents);
			Debug.Assert(doEvents || (msgCallback == null && stopVar == null));
			bool all = flags.Has(WHFlags.All) && nHandles > 1;
			
			using var mp = new MessagePump_();
			fixed (IntPtr* ha = handles) {
				for (long timePrev = 0; ;) {
					if (stopVar != null && stopVar.waitVar) return nHandles + 1;
					
					int timeSlice = (all && doEvents) ? 50 : 5000;
					if (timeMS > 0) {
						long timeNow = computer.tickCountWithoutSleep;
						if (timePrev > 0) timeMS -= timeNow - timePrev;
						if (timeMS <= 0) return Api.WAIT_TIMEOUT;
						if (timeSlice > timeMS) timeSlice = (int)timeMS;
						timePrev = timeNow;
					} else if (timeMS == 0) timeSlice = 0;
					
					int k;
					if (doEvents && !all) {
						k = Api.MsgWaitForMultipleObjectsEx(nHandles, ha, timeSlice, Api.QS_ALLINPUT, Api.MWMO_ALERTABLE | Api.MWMO_INPUTAVAILABLE);
						if (k == nHandles) { //message, COM, hook, etc
							if (mp.PumpWithCallback(msgCallback)) return nHandles;
							continue;
						}
					} else {
						if (nHandles > 0) k = Api.WaitForMultipleObjectsEx(nHandles, ha, all, timeSlice, true);
						else { k = Api.SleepEx(timeSlice, true); if (k == 0) k = Api.WAIT_TIMEOUT; }
						if (doEvents) if (mp.PumpWithCallback(msgCallback)) return nHandles;
					}
					if (k is not (Api.WAIT_TIMEOUT or Api.WAIT_IO_COMPLETION)) return k; //signaled handle, abandoned mutex, WAIT_FAILED (-1)
				}
			}
		}
		
		/// <summary>
		/// Waits for a posted message received by this thread.
		/// </summary>
		/// <param name="secondsTimeout">Timeout, seconds. Can be 0 (infinite), &gt;0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout).</param>
		/// <param name="callback">Callback function that returns true to stop waiting. More info in Remarks.</param>
		/// <returns>Returns true. On timeout returns false if <i>secondsTimeout</i> is negative; else exception.</returns>
		/// <exception cref="TimeoutException"><i>secondsTimeout</i> time has expired (if &gt; 0).</exception>
		/// <remarks>
		/// While waiting, dispatches Windows messages etc, like <see cref="doEvents(int)"/>. Before dispatching a posted message, calls the callback function. Stops waiting when it returns true. Does not dispatch the message if the function sets the message field = 0.
		/// Does not use <see cref="opt.wait"/>.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// timer.after(2000, t => { print.it("timer"); });
		/// wait.forPostedMessage(5, (ref MSG m) => { print.it(m); return m.message == 0x113; }); //WM_TIMER
		/// print.it("finished");
		/// ]]></code>
		/// </example>
		public static bool forPostedMessage(double secondsTimeout, WPMCallback callback) {
			return 1 == WaitS_(secondsTimeout, WHFlags.DoEvents, callback, null);
		}
		
		/// <summary>
		/// Waits for a condition that is changed while processing messages or other events received by this thread.
		/// </summary>
		/// <param name="secondsTimeout">Timeout, seconds. Can be 0 (infinite), &gt;0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout).</param>
		/// <param name="condition">Callback function that returns true to stop waiting. More info in Remarks.</param>
		/// <returns>Returns true. On timeout returns false if <i>secondsTimeout</i> is negative; else exception.</returns>
		/// <exception cref="TimeoutException"><i>secondsTimeout</i> time has expired (if &gt; 0).</exception>
		/// <remarks>
		/// While waiting, dispatches Windows messages etc, like <see cref="doEvents(int)"/>. After dispatching one or more messages or other events (posted messages, messages sent by other threads, hooks, etc), calls the callback function. Stops waiting when it returns true.
		/// Similar to <see cref="forCondition"/>. Differences: 1. Always dispatches messages etc. 2. Does not call the callback function when there are no messages etc.
		/// Does not use <see cref="opt.wait"/>.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// bool stop = false;
		/// timer.after(2000, t => { print.it("timer"); stop = true; });
		/// wait.forMessagesAndCondition(5, () => stop);
		/// print.it(stop);
		/// ]]></code>
		/// </example>
		public static bool forMessagesAndCondition(double secondsTimeout, Func<bool> condition) {
			return 1 == WaitS_(secondsTimeout, WHFlags.DoEvents, condition, null);
		}
		
		//rejected. Rarely used; type-limited. Let use forCondition.
		//public static bool forVariable(double secondsTimeout, in bool variable, OWait options = null) { }
		
		//FUTURE: add misc wait functions implemented using WindowsHook and WinEventHook.
	}
}

namespace Au.Types {
	/// <summary>
	/// Flags for <see cref="wait.forHandle"/>
	/// </summary>
	[Flags]
	public enum WHFlags {
		/// <summary>
		/// Wait until all handles are signaled.
		/// </summary>
		All = 1,
		
		/// <summary>
		/// While waiting, dispatch Windows messages, events, hooks etc. Like <see cref="wait.doEvents(int)"/>.
		/// </summary>
		DoEvents = 2,
	}
	
	/// <summary>
	/// Delegate type for <see cref="wait.forPostedMessage(double, WPMCallback)"/>.
	/// </summary>
	/// <param name="m">API <msdn>MSG</msdn>.</param>
	public delegate bool WPMCallback(ref MSG m);
	
	/// <summary>
	/// Used with Wait_ etc instead of ref bool.
	/// </summary>
	internal class WaitVariable_ {
		public bool waitVar;
	}
}

//CONSIDER: in QM2 these functions are created:
//	WaitForFocus, WaitWhileWindowBusy,
//	WaitForFileReady, WaitForChangeInFolder,
//	ChromeWait, FirefoxWait
//	WaitForTime,
//	these are in System: WaitIdle, WaitForThreads,

//CONSIDER: WaitForFocusChanged
//	Eg when showing Open/SaveAs dialog, the file Edit control receives focus after 200 ms. Sending text to it works anyway, but the script fails if then it clicks OK not with keys (eg with elm).
