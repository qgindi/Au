
partial class PanelDebug {
	class _Debugger {
		consoleProcess _p;
		FileStream _fs;
		Action<string> _events;
		bool _ignoreEvents;
		Action _readEvents;
		
		public _Debugger(Action<string> events) {
			_events = events;
			_p = new(folders.ThisAppBS + @"Roslyn\netcoredbg.exe", $"--interpreter=mi");
			//info: From netcoredbg we need only netcoredbg.exe, dbgshim.dll, ManagedPart.dll, Microsoft.CodeAnalysis.dll and Microsoft.CodeAnalysis.CSharp.dll.
			//	The default netcoredbg contains 2 unused Roslyn scripting dlls (why?).
			//	Also these Roslyn dlls are very old.
			//	I moved just netcoredbg.exe, dbgshim.dll and ManagedPart.dll to the Roslyn folder. Now it uses the new Roslyn dlls.
			//	Also I modified the ManagedPart csproj. The ManagedPart.dll is the output of the modified project.
			//	Although netcoredbg has dbgshim.dll, I'm using newer: https://www.nuget.org/packages/Microsoft.Diagnostics.DbgShim.win-x64
			
			if (SendSync(0, $"-handshake") != "^done") { //waits until the debugger is ready to process commands. Then we can measure the speed of other sync commands at startup.
				_Print("Failed to start debugger.");
				return;
			}
			
			_fs = new(new Microsoft.Win32.SafeHandles.SafeFileHandle(_p.OutputHandle_, ownsHandle: false), FileAccess.Read);
			_readEvents = _ReadEvents;
			timer.after(200, _ => _readEvents());
		}
		
		void _ReadEvents() {
			//print.it(">>>");
			while (!_ignoreEvents && _p != null) {
				int canRead = _p.CanReadNow_;
				if (canRead == 0) break;
				if (canRead < 0 || !_p.ReadLine(out var k)) {
					_Print("Debugger crashed.");
					_events("^exit");
					return;
				}
				//print.qm2.write($"_ReadEvents:  {k}");
				if (k is not ("(gdb)" or "")) {
					try { _events(k); }
					catch (Exception e1) {
						print.it(e1);
						_events("^exit");
						return;
					}
				}
			}
			//print.it("<<<");
			if (_p == null) return;
			_fs.BeginRead(Array.Empty<byte>(), 0, 0, ar => {
				while (_ignoreEvents) Thread.Sleep(10);
				App.Dispatcher.InvokeAsync(_readEvents);
			}, null);
		}
		
		public void Dispose() {
			if (_p == null) return;
			_p.Dispose();
			_p = null;
			_fs.Dispose();
		}
		
		bool _Write(string s) {
			try { _p.Write(s); }
			catch (AuException e1) {
				_Print("Debugger crashed.");
				Debug_.Print(e1);
				Dispose();
				App.Dispatcher.InvokeAsync(() => _events("^exit"));
				return false;
			}
			return true;
		}
		
		/// <summary>
		/// Writes s. Does not read.
		/// </summary>
		public void Send(string s) {
			_Write(s);
		}
		
		/// <summary>
		/// Writes token+s, like "5-command".
		/// Then synchronously reads until received a line that starts with the token followed by '^'. For other received lines calls the events callback.
		/// </summary>
		/// <param name="token">A positive number. 0 is used by this class.</param>
		/// <param name="noEvent">Called on events. Return true to no call the events callback.</param>
		/// <returns>The received line without token. Returns null if the debugger process ended.</returns>
		public string SendSync(int token, string s, Func<string, bool> noEvent = null) {
			var st = token.ToS();
			if (!_Write(st + s)) return null;
			_ignoreEvents = true;
			try {
				while (_p.ReadLine(out var k)) {
					//print.qm2.write($"SendSync:  {k}");
					if (k.Starts(st) && k.Eq(st.Length, '^')) return k[st.Length..];
					if (k is not ("(gdb)" or "")) {
						if (noEvent?.Invoke(k) == true) continue;
						_events(k);
					}
				}
			}
			finally { _ignoreEvents = false; }
			return null;
		}
	}
}
