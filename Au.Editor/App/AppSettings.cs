using System.Text.Json.Serialization;

/// <summary>
/// Program settings.
/// folders.ThisAppDocuments + @".settings\Settings.json"
/// </summary>
record AppSettings : JSettings {
	//This is loaded at startup and therefore must be fast.
	//	NOTE: Don't use types that would cause to load UI dlls (WPF etc). Eg when it is a nested type and its parent class is a WPF etc control.
	//	Speed tested with .NET 5: first time 40-60 ms. Mostly to load/jit/etc dlls used in JSON deserialization, which then is fast regardless of data size.
	
	public static AppSettings Load() => Load<AppSettings>(DirBS + "Settings.json")._Loaded();
	
	AppSettings _Loaded() {
		_NE(ref user) ??= Guid.NewGuid().ToString();
		hotkeys ??= new();
		(font_output ??= new()).Loaded();
		delm ??= new();
		recorder ??= new();
		return this;
	}
	
	static void _NE(ref string s, string def) {
		if (s.NE()) s = def;
	}
	
	static ref string _NE(ref string s) {
		if (s == "") s = null;
		return ref s;
	}
	
#if IDE_LA
	public static readonly string DirBS = folders.ThisAppDocuments + @".settings_\";
#else
	public static readonly string DirBS = folders.ThisAppDocuments + @".settings\";
#endif
	
	public bool runHidden;
	public string user, workspace;
	public string[] recentWS;
	public bool checkForUpdates;
	public int checkForUpdatesDay;
	
	//When need a nested type, use record class. Everything works well; later can add/remove members like in main type.
	//Don't use record struct when need to set init values (now or in the future), because:
	//	1. Older .NET versions don't support it, or have bugs.
	//		Eg .NET 6.0.3 throws "InvalidCastException, Unable to cast object of type 'System.String' to type 'hotkeys_t'" when `public record hotkeys_t()` (need the `()` when using default field values).
	//			Works if `public record hotkeys_t(string a, string b)`, but then need 'new("a", "b")'; I don't like it etc.
	//	2. If `public record hotkeys_t(string a, string b)`, and later added a new member like `, c = "value"`, the value is null/0. The deserializer uses the default ctor.
	//		Also the same happens if somebody deletes an existing member from JSON.
	//		Works well with `public record hotkeys_t()`, but cannot use it because of 1.
	//Note: deserializer always creates new object, even if default object created. Avoid custom ctors etc.
	//If like `public hotkeys_t hotkeys = new()`, creates new object 2 times: 1. explicit new(); 2. when deserializing. Also in JSON can be `= null`. Move the `new()` to _Loaded.
	//Tuple does not work well. New members are null/0. Also item names in file are like "Item1".
	
	//Options -> Hotkeys
	public record hotkeys_t {
		public string
			tool_quick = "Ctrl+Shift+Q",
			tool_wnd = "Ctrl+Shift+W",
			tool_elm = "Ctrl+Shift+E",
			tool_uiimage
			;
	}
	public hotkeys_t hotkeys;
	
	//font of various UI parts
	public record font_t {
		public string name;
		public double size = 9;
		
		public void Loaded() {
			_NE(ref name, "Consolas");
			size = Math.Clamp(size, 6, 30);
		}
	}
	public font_t font_output;
	
	//Options -> Templates
	public int templ_use;
	//public int templ_flags;
	
	//code editor
	public bool edit_wrap, edit_noImages;
	
	//code info
	public bool ci_complGroup = true, ci_formatCompact = true, ci_formatTabIndent = true;
	public int ci_complParen; //0 spacebar, 1 always, 2 never
	public int ci_rename;
	
	//CONSIDER: option to specify completion keys/chars. See https://www.quickmacros.com/forum/showthread.php?tid=7263
	//public byte ci_complOK = 15; //1 Enter, 2 Tab, 4 Space, 8 other
	//public byte ci_complArgs = 4; //1 Enter, 2 Tab, 4 Space (instead of ci_complParen)
	//maybe option to use Tab for Space, and disable Space. Tab would add space or/and () like now Space does.
	
	//public byte ci_formatBraceNewline; //0 never, 1 always, 2 type/function
	//public byte ci_formatIndentation; //0 tab, 1 4 spaces, 2 2 spaces
	
	//panel Files
	public bool files_multiSelect;
	
	//file type icons
	public record struct icons_t {
		public string ft_script, ft_class, ft_folder, ft_folderOpen;
	}
	public icons_t icons;
	
	//panel Output
	public bool output_wrap, output_white, output_topmost;
	
	//panel Outline
	public byte outline_flags;
	
	//panel Open
	public byte openFiles_flags;
	
	//panel Recipe
	public sbyte recipe_zoom;
	
	//settings common to various tools
	public bool tools_pathUnexpand = true, tools_pathLnk;
	
	//saved positions of various windows
	public record struct wndpos_t {
		public string main, wnd, elm, uiimage, ocr, recorder, icons, symbol;
	}
	public wndpos_t wndpos;
	
	//Delm
	public record delm_t {
		public string hk_capture = "F3", hk_insert = "F4"; //for all tools
		public string wait, actionn; //named actionn because once was int action
		public int flags;
	}
	public delm_t delm;
	
	//DInputRecorder
	public record recorder_t {
		public bool keys = true, text = true, text2 = true, mouse = true, wheel, drag, move;
		public int xyIn;
		public string speed = "10";
	}
	public recorder_t recorder;
	
	//DIcons
	public int dicons_listColor;
	public bool dicons_contrastUse;
	public string dicons_contrastColor = "#E0E000";
	
	//DPortable
	public string portable_dir;
	
	//DOcr
	public Au.Tools.OcrEngineSettings ocr;
	
	//WPF preview
	public int wpfpreview_xy;
	
	//DSnippets
	public Dictionary<string, HashSet<string>> ci_hiddenSnippets;
	
	//CiGoTo
	public Dictionary<string, CiGoTo.AssemblySett> ci_gotoAsm;
	public int ci_gotoTab;
	
	//CiFindGo
	public bool ci_findgoDclick;
	
	//misc
	public int publish;
	
	//panel Find
	public string find_skip;
	public int find_searchIn, find_printSlow = 50;
	public bool find_parallel;
}

/// <summary>
/// Workspace settings.
/// WorkspaceDirectory + @"\settings.json"
/// </summary>
record WorkspaceSettings : JSettings {
	public static WorkspaceSettings Load(string jsonFile) => Load<WorkspaceSettings>(jsonFile);
	
	public record User(string guid) {
		public string startupScripts, debuggerScript, gitUrl;
		public bool gitBackup;
	}
	public User[] users;
	
	public User CurrentUser {
		get {
			if (_cu == null) {
				_cu = users?.FirstOrDefault(o => o.guid == App.Settings.user);
				if (_cu == null) {
					_cu = new User(App.Settings.user);
					users = users == null ? new[] { _cu } : users.InsertAt(0, _cu);
				}
			}
			return _cu;
		}
	}
	User _cu;
	
	public string ci_skipFolders;
}
