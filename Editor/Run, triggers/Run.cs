//#define TEST_STARTUP_SPEED

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
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
//using System.Windows.Forms;
//using System.Drawing;
using System.Linq;
using System.Xml;
//using System.Xml.Linq;
using System.IO.Pipes;

using Au;
using Au.Types;
using static Au.NoClass;
using static Program;
using Au.Compiler;
using Au.Controls;

static class Run
{
	/// <summary>
	/// Compiles and/or executes C# file or its project.
	/// If <paramref name="run"/> is false, returns 1 if compiled, 0 if failed to compile.
	/// Else returns: process id if started now, 0 if failed, (int)AuTask.ERunResult.deferred if scheduled to run later, (int)AuTask.ERunResult.editorThread if runs in editor thread.
	/// </summary>
	/// <param name="run">If true, compiles if need and executes. If false, always compiles and does not execute.</param>
	/// <param name="f">C# file. Does nothing if null or not C# file.</param>
	/// <param name="args">To pass to Main.</param>
	/// <param name="noDefer">Don't schedule to run later.</param>
	/// <param name="wrPipeName">Pipe name for AuTask.WriteResult.</param>
	/// <remarks>
	/// Saves editor text if need.
	/// Calls <see cref="Compiler.Compile"/>.
	/// Must be always called in the main UI thread (Thread.CurrentThread.ManagedThreadId == 1), because calls its file model functions.
	/// </remarks>
	public static int CompileAndRun(bool run, FileNode f, string[] args = null, bool noDefer = false, string wrPipeName = null)
	{
#if TEST_STARTUP_SPEED
		args = new string[] { Time.PerfMicroseconds.ToString() }; //and in script use this code: Print(Time.Microseconds-Convert.ToInt64(args[0]));
#endif

		Model.Save.TextNowIfNeed(onlyText: true);
		Model.Save.WorkspaceNowIfNeed(); //because the script may kill editor, eg if runs in editor thread

		if(f == null) return 0;
		if(f.FindProject(out var projFolder, out var projMain)) f = projMain;

		//can be set to run other script instead.
		//	Useful for library projects. Single files have other alternatives - move to a script project or move code to a script file.
		if(run) {
			var f2 = f.TestScript;
			if(f2 != null) {
				if(!f2.FindProject(out projFolder, out projMain)) f = f2;
				else if(projMain != f) f = projMain;
				else { Print($"<>The test script {f2.SciLink} cannot be in the project folder {projFolder.SciLink}"); return 0; }
			}
		}

		if(!f.IsCodeFile) return 0;

		bool ok = Compiler.Compile(run, out var r, f, projFolder);

		if(run && (r.role == ERole.classFile || r.role == ERole.classLibrary)) { //info: if classFile, compiler sets r.role and returns false (does not compile)
			_OnRunClassFile(f, projFolder);
			return 0;
		}

		if(!ok) return 0;
		if(!run) return 1;

		if(r.role == ERole.editorExtension) {
			RunAssembly.Run(r.file, args, r.pdbOffset, RAFlags.InEditorThread);
			return (int)AuTask.ERunResult.editorThread;
		}

		return Tasks.RunCompiled(f, r, args, noDefer, wrPipeName);
	}

	static void _OnRunClassFile(FileNode f, FileNode projFolder)
	{
		if(!s_isRegisteredLinkRCF) { s_isRegisteredLinkRCF = true; SciTags.AddCommonLinkTag("+runClass", _SciLink_RunClassFile); }
		var ids = f.IdStringWithWorkspace;
		var s2 = projFolder != null ? "" : $", project (<+runClass \"2|{ids}\">create<>) or role exeProgram (<+runClass \"1|{ids}\">add<>)";
		Print($"<>Cannot run '{f.Name}'. It is a class file without a test script (<+runClass \"3|{ids}\">create<>){s2}.");
	}

	static void _SciLink_RunClassFile(string s)
	{
		int action = s.ToInt_(); //1 add meta role miniProgram, 2 create Script project, 3 create new test script and set "run" attribute
		var f = Model.Find(s.Substring(2), null); if(f == null) return;
		FileNode f2 = null;
		if(action == 1) { //add meta role exeProgram
			if(!Model.SetCurrentFile(f)) return;
			Model.Properties();
		} else {
			if(action == 2) { //create project
				if(!_NewItem(out f2, @"New Project\@Script")) return;
				f.FileMove(f2, Aga.Controls.Tree.NodePosition.After);
			} else { //create test script
				s = "test " + Path_.GetFileName(f.Name, true);
				if(!_NewItem(out f2, "Script.cs", s)) return;
				f.TestScript = f2;
			}

			//Creates new item above f or f's project folder.
			bool _NewItem(out FileNode ni, string template, string name = null)
			{
				bool isProject = f.FindProject(out var target, out _);
				if(!isProject) target = f;

				var text = new EdNewFileText();
				if(action == 2) {
					text.text = "Class1.Function1();\r\n";
				} else {
					text.meta = $"/*/ {(isProject ? "pr" : "c")} {f.ItemPath} /*/ ";
					text.text = $"{(isProject ? "Library." : "")}Class1.Function1();\r\n";
				}

				ni = Model.NewItem(target, Aga.Controls.Tree.NodePosition.Before, template, name, text: text);
				return ni != null;
			}
		}
	}
	static bool s_isRegisteredLinkRCF;
}

/// <summary>
/// A running automation task.
/// Starts/ends task, watches/notifies when ended.
/// </summary>
class RunningTask
{
	volatile WaitHandle _process;
	public readonly FileNode f;
	public readonly int taskId;
	public readonly int processId;
	public readonly bool isBlue;

	static int s_taskId;

	public RunningTask(FileNode f, WaitHandle hProcess, bool isBlue)
	{
		taskId = ++s_taskId;
		this.f = f;
		_process = hProcess;
		processId = Api.GetProcessId(hProcess.SafeWaitHandle.DangerousGetHandle());
		this.isBlue = isBlue;

		RegisteredWaitHandle rwh = null;
		rwh = ThreadPool.RegisterWaitForSingleObject(_process, (context, wasSignaled) => {
			rwh.Unregister(_process);
			var p = _process; _process = null;
			p.Dispose();
			Tasks.TaskEnded1(taskId);
		}, null, -1, true);
	}

	/// <summary>
	/// False if task is already ended or still not started.
	/// </summary>
	//public bool IsRunning => _process != null;
	public bool IsRunning {
		get {
			var p = _process;
			if(p == null) return false;
			return 0 != Api.WaitForSingleObject(p.SafeWaitHandle.DangerousGetHandle(), 0);
		}
	}

	/// <summary>
	/// Ends this task (kills process), if running.
	/// Returns false if fails, unlikely.
	/// </summary>
	/// <param name="onProgramExit">Called on program exit. Returns true even if fails. Does not wait.</param>
	public bool End(bool onProgramExit)
	{
		var p = _process;
		if(p != null) {
			var h = p.SafeWaitHandle.DangerousGetHandle();
			bool ok = Api.TerminateProcess(h, -1);
			if(onProgramExit) return true;
			if(ok) {
				if(0 != Api.WaitForSingleObject(h, 2000)) { Debug_.Print("process not terminated"); return false; }
			} else {
				var s = WinError.Message;
				if(0 != Api.WaitForSingleObject(h, 0)) { Debug_.Print(s); return false; }
			}
			//note: TerminateProcess kills process not immediately. Need at least several ms.
		}
		return true;
		//TODO: release pressed keys.
	}
}

/// <summary>
/// Manages running automation tasks.
/// </summary>
class RunningTasks
{
	class _WaitingTask
	{
		public readonly FileNode f;
		public readonly Compiler.CompResults r;
		public readonly string[] args;

		public _WaitingTask(FileNode f, Compiler.CompResults r, string[] args)
		{
			this.f = f; this.r = r; this.args = args;
		}
	}

	readonly List<RunningTask> _a;
	readonly List<_WaitingTask> _q; //not Queue because may need to remove item at any index
	bool _updateUI;
	volatile bool _disposed;
	Wnd _wMain;

	public IEnumerable<RunningTask> Items => _a;

	public RunningTasks()
	{
		_a = new List<RunningTask>();
		_q = new List<_WaitingTask>();
		_recent = new List<RecentTask>();
		_wMain = (Wnd)MainForm;
		Timer1sOr025s += _TimerUpdateUI;
	}

	public void OnWorkspaceClosed()
	{
		bool onExit = MainForm.IsClosed;

		if(onExit) {
			_disposed = true;
			Timer1sOr025s -= _TimerUpdateUI;
		}

		for(int i = _a.Count - 1; i >= 0; i--) {
			_EndTask(_a[i], onExit: onExit);
		}

		if(onExit) _a.Clear();
		_q.Clear();
		_recent.Clear();

		if(!onExit) _UpdatePanels();
	}

	/// <summary>
	/// Adds a started task (thread or process) to the 'running' and 'recent' lists.
	/// Must be called in the main thread.
	/// </summary>
	/// <param name="rt"></param>
	void _Add(RunningTask rt)
	{
		Debug.Assert(!_disposed);
		_a.Insert(0, rt);
		_RecentStarted(rt.f);
		_updateUI = true;
	}

	/// <summary>
	/// Called in a threadpool thread when a task process exited.
	/// </summary>
	/// <param name="taskId"></param>
	internal void TaskEnded1(int taskId)
	{
		if(_disposed) return;
		_wMain.Post(WM_TASK_ENDED, taskId);
	}

	/// <summary>
	/// When task ended, this message is posted to MainForm, with wParam=taskId.
	/// </summary>
	public const int WM_TASK_ENDED = Api.WM_USER + 900;

	/// <summary>
	/// Removes an ended task from the 'running' and 'recent' lists. If a task is queued and can run, starts it.
	/// When task ended, TaskEnded1 posts to MainForm message WM_TASK_ENDED with task id in wParam. MainForm calls this function.
	/// </summary>
	internal void TaskEnded2(IntPtr wParam)
	{
		if(_disposed) return;

		int taskId = (int)wParam;
		int i = _Find(taskId);
		if(i < 0) { Debug_.Print("not found. It's OK, but should be very rare, mostly with 1-core CPU."); return; }

		var rt = _a[i];
		_a.RemoveAt(i);
		_RecentEnded(rt.f);
		Au.Triggers.HooksServer.Instance?.RemoveTask(rt.processId);

		for(int j = _q.Count - 1; j >= 0; j--) {
			var t = _q[j];
			if(_CanRunNow(t.f, t.r, out _)) {
				_q.RemoveAt(j);
				RunCompiled(t.f, t.r, t.args, ignoreLimits: true);
				break;
			}
		}

		_updateUI = true;
	}

	void _TimerUpdateUI()
	{
		if(!_updateUI || !MainForm.Visible) return;
		_UpdatePanels();
	}

	void _UpdatePanels()
	{
		_updateUI = false;
		Panels.Running.UpdateList(); //~1 ms when list small, not including wmpaint
		Panels.Recent.UpdateList();
	}

	/// <summary>
	/// Returns true if one or more tasks of file f are running.
	/// </summary>
	/// <param name="f">Can be null.</param>
	public bool IsRunning(FileNode f) => null != _GetRunning(f);

	RunningTask _GetRunning(FileNode f)
	{
		for(int i = 0; i < _a.Count; i++) {
			var r = _a[i];
			if(r.f == f && r.IsRunning) return r;
		}
		return null;
	}

	/// <summary>
	/// Gets the "green" running task (meta runMode green or unspecified). Returns null if no such task.
	/// </summary>
	public RunningTask GetGreenTask()
	{
		for(int i = 0; i < _a.Count; i++) {
			var r = _a[i];
			if(!r.isBlue && r.IsRunning) return r;
		}
		return null;
	}

	//currently not used
	///// <summary>
	///// Returns all running files.
	///// For files that have multiple tasks is added 1 item in the list.
	///// Each time creates new list; caller can modify it.
	///// </summary>
	//public List<FileNode> GetRunningFiles()
	//{
	//	var a = new List<FileNode>(_a.Count);
	//	for(int i = 0; i < _a.Count; i++) {
	//		var t = _a[i];
	//		if(!a.Contains(t.f)) a.Add(t.f);
	//	}
	//	return a;
	//}

	/// <summary>
	/// Ends all tasks of file f.
	/// Returns true if was running.
	/// </summary>
	/// <param name="f">Can be null.</param>
	public bool EndTasksOf(FileNode f)
	{
		bool wasRunning = false;
		for(int i = _a.Count - 1; i >= 0; i--) {
			var r = _a[i];
			if(r.f != f || !r.IsRunning) continue;
			_EndTask(r);
			wasRunning = true;
		}
		return wasRunning;
	}

	/// <summary>
	/// Ends single task, if still running.
	/// </summary>
	public void EndTask(RunningTask rt)
	{
		if(_a.Contains(rt)) _EndTask(rt);
	}

	bool _EndTask(RunningTask rt, bool onExit = false)
	{
		Debug.Assert(_a.Contains(rt));
		return rt.End(onExit);
	}

	bool _CanRunNow(FileNode f, Compiler.CompResults r, out RunningTask running)
	{
		running = null;
		switch(r.runMode) {
		case ERunMode.green: running = GetGreenTask(); break;
		case ERunMode.blue when r.ifRunning != EIfRunning.runIfBlue: running = _GetRunning(f); break;
		default: return true;
		}
		return running == null;
	}

	/// <summary>
	/// Executes the compiled assembly in new process.
	/// Returns: process id if started now, 0 if failed, (int)AuTask.ERunResult.deferred if scheduled to run later.
	/// </summary>
	/// <param name="f"></param>
	/// <param name="r"></param>
	/// <param name="args"></param>
	/// <param name="noDefer">Don't schedule to run later. If cannot run now, just return 0.</param>
	/// <param name="wrPipeName">Pipe name for AuTask.WriteResult.</param>
	/// <param name="ignoreLimits">Don't check whether the task can run now.</param>
	public unsafe int RunCompiled(FileNode f, Compiler.CompResults r, string[] args, bool noDefer = false, string wrPipeName = null, bool ignoreLimits = false)
	{
		g1:
		if(!ignoreLimits && !_CanRunNow(f, r, out var running)) {
			switch(r.ifRunning) {
			case EIfRunning.restart:
			case EIfRunning.restartOrWait:
				if(running.f == f && _EndTask(running)) goto g1;
				if(r.ifRunning == EIfRunning.restartOrWait) goto case EIfRunning.wait;
				goto case EIfRunning.runIfBlue;
			case EIfRunning.wait:
				if(noDefer) goto case EIfRunning.runIfBlue;
				_q.Insert(0, new _WaitingTask(f, r, args));
				return (int)AuTask.ERunResult.deferred;
			case EIfRunning.runIfBlue:
				string s1 = (running.f == f) ? "it" : $"{running.f.SciLink}";
				Print($"<>Cannot start {f.SciLink} because {s1} is running. Consider meta options <c green>runMode<>, <c green>ifRunning<>.");
				break;
			}
			return 0;
		}

		_SpUac uac = _SpUac.normal; int preIndex = 0;
		if(!Uac.IsUacDisabled) {
			//info: to completely disable UAC on Win7: gpedit.msc/Computer configuration/Windows settings/Security settings/Local policies/Security options/User Account Control:Run all administrators in Admin Approval Mode/Disabled. Reboot.
			//note: when UAC disabled, if our uac is System, IsUacDisabled returns false (we probably run as SYSTEM user). It's OK.
			var IL = Uac.OfThisProcess.IntegrityLevel;
			if(r.uac == EUac.inherit) {
				switch(IL) {
				case UacIL.High: preIndex = 1; break;
				case UacIL.UIAccess: uac = _SpUac.uiAccess; preIndex = 2; break;
				}
			} else {
				switch(IL) {
				case UacIL.Medium:
				case UacIL.UIAccess:
					if(r.uac == EUac.admin) uac = _SpUac.admin;
					break;
				case UacIL.High:
					if(r.uac == EUac.user) uac = _SpUac.userFromAdmin;
					break;
				case UacIL.Low:
				case UacIL.Untrusted:
				case UacIL.Unknown:
				//break;
				case UacIL.System:
				case UacIL.Protected:
					Print($"<>Cannot run {f.SciLink}. Meta option <c green>uac {r.uac}<> cannot be used when the UAC integrity level of this process is {IL}. Supported levels are Medium, High and uiAccess.");
					return 0;
					//info: cannot start Medium IL process from System process. Would need another function. Never mind.
				}
				if(r.uac == EUac.admin) preIndex = 1;
			}
		}

		string exeFile, argsString;
		_Preloaded pre = null; byte[] taskParams = null;
		if(r.notInCache) { //meta role exeProgram
			exeFile = r.file;
			argsString = args == null ? null : Au.Util.StringMisc.CommandLineFromArray(args);
		} else {
			exeFile = Folders.ThisAppBS + (r.prefer32bit ? "Au.Task32.exe" : "Au.Task.exe");

			int iFlags = r.hasConfig ? 1 : 0;
			if(r.mtaThread) iFlags |= 2;
			if(r.console) iFlags |= 4;
			taskParams = Au.Util.LibSerializer.SerializeWithSize(r.name, r.file, r.pdbOffset, iFlags, args, wrPipeName);
			wrPipeName = null;

			if(r.prefer32bit && Ver.Is64BitOS) preIndex += 3;
			pre = s_preloaded[preIndex] ?? (s_preloaded[preIndex] = new _Preloaded(preIndex));
			argsString = pre.pipeName;
		}

		int pid; WaitHandle hProcess = null; bool disconnectPipe = false;
		try {
			//Perf.First();
			var pp = pre?.hProcess;
			if(pp != null && 0 != Api.WaitForSingleObject(pp.SafeWaitHandle.DangerousGetHandle(), 0)) { //preloaded process exists
				hProcess = pp; pid = pre.pid;
				pre.hProcess = null; pre.pid = 0;
			} else {
				if(pp != null) { pp.Dispose(); pre.hProcess = null; pre.pid = 0; } //preloaded process existed but somehow ended
				(pid, hProcess) = _StartProcess(uac, exeFile, argsString, wrPipeName);
			}
			Api.AllowSetForegroundWindow(pid);

			if(pre != null) {
				//Perf.First();
				var o = new Api.OVERLAPPED { hEvent = pre.overlappedEvent.SafeWaitHandle.DangerousGetHandle() };
				if(!Api.ConnectNamedPipe(pre.hPipe, &o)) {
					int e = WinError.Code; if(e != Api.ERROR_IO_PENDING) throw new AuException(e);
					int wr = WaitHandle.WaitAny(new WaitHandle[2] { pre.overlappedEvent, hProcess });
					if(wr != 0) { Api.CancelIo(pre.hPipe); throw new AuException("*start task. Preloaded task process ended"); }
					disconnectPipe = true;
					if(!Api.GetOverlappedResult(pre.hPipe, ref o, out _, false)) throw new AuException(0);
				}
				//Perf.Next();
				if(!Api.WriteFileArr(pre.hPipe, taskParams, out _)) throw new AuException(0);
				//Perf.Next();
				Api.DisconnectNamedPipe(pre.hPipe); disconnectPipe = false;
				//Perf.NW('e');

				//start preloaded process for next task. Let it wait for pipe connection.
				if(uac != _SpUac.admin) { //we don't want second UAC consent
					try { (pre.pid, pre.hProcess) = _StartProcess(uac, exeFile, argsString, null); }
					catch(Exception ex) { Debug_.Print(ex); }
				}
			}
		}
		catch(Exception ex) {
			Print(ex);
			if(disconnectPipe) Api.DisconnectNamedPipe(pre.hPipe);
			hProcess?.Dispose();
			return 0;
		}

		var rt = new RunningTask(f, hProcess, r.runMode != ERunMode.green);
		_Add(rt);
		return pid;
	}

	class _Preloaded
	{
		public readonly string pipeName;
		public readonly Microsoft.Win32.SafeHandles.SafeFileHandle hPipe;
		public readonly ManualResetEvent overlappedEvent;
		public WaitHandle hProcess;
		public int pid;

		public _Preloaded(int index)
		{
			pipeName = $@"\\.\pipe\Au.Task-{Api.GetCurrentProcessId()}-{index}";
			hPipe = Api.CreateNamedPipe(pipeName,
				Api.PIPE_ACCESS_OUTBOUND | Api.FILE_FLAG_OVERLAPPED, //use async pipe because editor would hang if task process exited without connecting. Same speed.
				Api.PIPE_TYPE_MESSAGE | Api.PIPE_REJECT_REMOTE_CLIENTS,
				1, 0, 0, 0, null);
			overlappedEvent = new ManualResetEvent(false);
		}
	}
	_Preloaded[] s_preloaded = new _Preloaded[6]; //user, admin, uiAccess, user32, admin32, uiAccess32

	/// <summary>
	/// How _StartProcess must start process.
	/// Note: it is not UAC IL of the process.
	/// </summary>
	enum _SpUac
	{
		normal, //start process of same IL as this process, but without uiAccess. It is how CreateProcess API works.
		admin, //start admin process from this user or uiAccess process
		userFromAdmin, //start user process from this admin process
		uiAccess, //start uiAccess process from this uiAccess process
	}

	/// <summary>
	/// Starts task process.
	/// Returns (processId, processHandle). Throws if failed.
	/// </summary>
	static (int pid, WaitHandle hProcess) _StartProcess(_SpUac uac, string exeFile, string args, string wrPipeName)
	{
		if(wrPipeName != null) wrPipeName = "AuTask.WriteResult.pipe=" + wrPipeName;
		if(uac == _SpUac.admin) {
			if(wrPipeName != null) throw new AuException($"*start process '{exeFile}' as admin and enable AuTask.WriteResult"); //cannot pass environment variables. //rare //FUTURE
			var k = Shell.Run(exeFile, args, SRFlags.Admin | SRFlags.NeedProcessHandle);
			return (k.ProcessId, k.ProcessHandle);
			//note: don't try to start task without UAC consent. It is not secure.
			//	Normally Au editor runs as admin in admin user account, and don't need to go through this.
		} else {
			var psr = uac == _SpUac.userFromAdmin
				? Process_.LibStartUserIL(exeFile, args, wrPipeName, Process_.StartResult.Need.WaitHandle)
				: Process_.LibStart(exeFile, args, uac == _SpUac.uiAccess, wrPipeName, Process_.StartResult.Need.WaitHandle);
			return (psr.pid, psr.waitHandle);
		}
	}

	int _Find(int taskId)
	{
		for(int i = 0; i < _a.Count; i++) {
			if(_a[i].taskId == taskId) return i;
		}
		return -1;
	}

	#region recent tasks

	public class RecentTask
	{
		public readonly FileNode f;
		public bool running;
		//FUTURE: startTime, endTime

		public RecentTask(FileNode f)
		{
			this.f = f;
			running = true;
		}
	}

	List<RecentTask> _recent;

	public IEnumerable<RecentTask> Recent => _recent;

	int _RecentFind(FileNode f)
	{
		for(int i = 0; i < _recent.Count; i++) if(_recent[i].f == f) return i;
		return -1;
	}

	void _RecentStarted(FileNode f)
	{
		int i = _RecentFind(f);
		if(i >= 0) {
			var x = _recent[i];
			x.running = true;
			if(i > 0) {
				for(int j = i; j > 0; j--) _recent[j] = _recent[j - 1];
				_recent[0] = x;
			}
		} else {
			if(_recent.Count > 100) _recent.RemoveRange(100, _recent.Count - 100);

			_recent.Insert(0, new RecentTask(f));
		}
	}

	void _RecentEnded(FileNode f)
	{
		if(IsRunning(f)) return;
		int i = _RecentFind(f);
		Debug.Assert(i >= 0 || f.Model != Model); if(i < 0) return;
		_recent[i].running = false;
	}

	#endregion
}
