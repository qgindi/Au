using System.Windows;
using System.Windows.Controls;
using Au.Controls;
using Au.Compiler;

/// <summary>
/// File type of a <see cref="FileNode"/>.
/// Saved in XML as tag name: d folder, s script, c class, n other.
/// </summary>
enum FNType : byte {
	Folder, //must be 0
	Script,
	Class,
	Other,
}

enum FNInsert {
	Inside, Before, After
}

enum FNFind {
	Any, File, Folder, CodeFile, Class/*, Script*/ //Script not useful because class files can be executable too
}

enum FNClassFileRole {
	/// <summary>Not a class file.</summary>
	None,
	/// <summary>Has meta role miniProgram/exeProgram/editorExtension.</summary>
	App,
	/// <summary>Has meta role classLibrary.</summary>
	Library,
	/// <summary>Has meta role classFile, or no meta role.</summary>
	Class,
}

class NewFileText {
	public bool replaceTemplate;
	public string text, meta;
	
	public NewFileText() { }
	
	public NewFileText(bool replaceTemplate, string text, string meta = null) {
		this.replaceTemplate = replaceTemplate;
		this.text = text;
		this.meta = meta;
	}
}

class DNewWorkspace : KDialogWindow {
	string _name, _location;
	public string ResultPath { get; private set; }
	
	public DNewWorkspace(string name, string location) {
		_name = name;
		_location = location;
		
		Title = "New workspace";
		
		var b = new wpfBuilder(this).WinSize(600);
		TextBox tName, tLocation = null;
		b.R.Add("Folder name", out tName, _name).Validation(_Validate).Focus();
		b.R.Add("Parent folder", out tLocation, _location).Validation(_Validate);
		b.R.AddButton("Browse...", _Browse).Width(70, "L");
		b.R.AddOkCancel();
		b.End();
		
		void _Browse(WBButtonClickArgs e) {
			var d = new FileOpenSaveDialog("{4D1F3AFB-DA1A-45AC-8C12-41DDA5C51CDA}") {
				InitFolderNow = filesystem.exists(tLocation.Text).Directory ? tLocation.Text : folders.ThisAppDocuments,
			};
			if (d.ShowOpen(out string s, this, selectFolder: true)) tLocation.Text = s;
		}
		
		string _Validate(FrameworkElement e) {
			var s = (e as TextBox).Text.Trim();
			if (e == tLocation) {
				if (!filesystem.exists(s).Directory) return "Folder does not exist";
			} else {
				if (pathname.isInvalidName(s) || s[0] is '.' or '_') return "Invalid filename";
				ResultPath = pathname.combine(tLocation.Text, s); //validation is when OK clicked
				if (filesystem.exists(ResultPath)) return s + " already exists";
			}
			return null;
		}
		
		//b.OkApply += e => {
		//	print.it(ResultPath); e.Cancel = true;
		//};
	}
}

class RepairWorkspace {
	const int c_markerInfo = 0;
	static HashSet<string> _clickedLinks = new();
	
	public static void Repair(bool inSelectedFolder) {
		var rootFolder = App.Model.Root;
		if (inSelectedFolder) {
			var a = FilesModel.TreeControl.SelectedItems;
			if (a.Length == 1 && a[0].IsFolder) rootFolder = a[0];
			else {
				dialog.showInfo(null, "Please right-click or select a folder.", owner: App.Hmain);
				return;
			}
		}
		
		using var workingState = Panels.Found.Prepare(PanelFound.Found.Repair, "Repair", out var b);
		if (workingState.NeedToInitControl) {
			var k = workingState.Scintilla;
			k.aaaMarkerDefine(c_markerInfo, Sci.SC_MARK_BACKGROUND, backColor: 0xEEE8AA);
		}
		
		//--------------
		
		b.Marker(c_markerInfo).Text("These items represent files that already don't exist. You may want to delete them from the Files panel.").NL();
		_Missing(rootFolder);
		void _Missing(FileNode folder) {
			foreach (var f in folder.Children()) {
				var path = f.FilePath;
				if (!filesystem.exists(path)) {
					int linkLen = 0;
					for (var s = path; !(s = pathname.getDirectory(s)).NE();) {
						if (filesystem.exists(s).Directory) { linkLen = s.Length; break; }
					}
					b.NL().Link(f, f.ItemPath); if (f.IsFolder) b.Text(f.IsLink ? " (folder link)" : " (folder)"); else if (f.IsLink) b.Text(" (file link)");
					var dir = path[..linkLen];
					b.NL().Link2(() => { run.itSafe(dir); }, $"\tMissing: {dir}").Text(path.AsSpan(linkLen..)).NL();
				} else if (f.IsFolder) {
					_Missing(f);
				}
			}
		}
		//note: don't use folder.Descendants(). It would also print files in missing folders.
		
		//--------------
		
		b.NL().Marker(c_markerInfo).Text("These files are in the workspace folder but not in the Files panel. You may want to import or delete them.").NL();
		_clickedLinks.Clear();
		_Orphaned(rootFolder);
		void _Orphaned(FileNode folder) {
			var folderPath = folder.FilePath;
			if (!filesystem.exists(folderPath, true).Directory) return;
			var hs = folder.Children().Select(o => o.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
			foreach (var v in filesystem.enumerate(folderPath, FEFlags.IgnoreInaccessible | FEFlags.UseRawPath)) {
				if (v.IsNtfsLink) continue;
				var filePath = v.FullPath;
				if (hs.Contains(filePath)) continue;
				b.NL().Link(() => { run.selectInExplorer(filePath); }, filePath); if (v.IsDirectory) b.Text(" (folder)");
				b.Text("\r\n\t").Link(() => _Import(folder, filePath), "Import");
				if (folder != App.Model.Root) b.Text(" to folder ").Link(() => { folder.SelectSingle(); FilesModel.TreeControl.Expand(folder, true); }, folder.ItemPath);
				b.NL();
			}
			
			foreach (var f in folder.Children()) {
				if (f.IsFolder) _Orphaned(f);
			}
		}
		
		void _Import(FileNode folder, string filePath) {
			if (!_clickedLinks.Add(filePath) || folder.IsAlien || !filesystem.exists(filePath)) return;
			var pos = FNInsert.Inside;
			if (folder == App.Model.Root) {
				folder = folder.FirstChild;
				if (folder != null) pos = FNInsert.Before; else folder = App.Model.Root;
			} else {
				folder.SelectSingle();
				FilesModel.TreeControl.Expand(folder, true);
			}
			//print.it(folder, filePath, pos);
			App.Model.ImportFiles(new[] { filePath }, folder, pos, ImportFlags.DontPrint);
		}
		
		//--------------
		
		List<(long size, FileNode f)> biggest = new();
		_Biggest(rootFolder);
		void _Biggest(FileNode folder) {
			foreach (var f in folder.Children()) {
				if (f.IsLink) continue;
				if (f.IsFolder) {
					_Biggest(f);
				} else if (filesystem.getProperties(f.FilePath, out var p, FAFlags.UseRawPath | FAFlags.DontThrow) && p.Size >= 50 * 1024) {
					biggest.Add((p.Size, f));
				}
			}
		}
		if (biggest.Any()) {
			b.NL().Marker(c_markerInfo).Text("Big files. You may want to delete some files if not used. Size in KB.").NL();
			foreach (var (size, f) in biggest.OrderByDescending(o => o.size)) {
				b.NL().Text($"{size / 1024} ").Link(f, f.ItemPath);
			}
		}
		
		//--------------
		
		Panels.Found.SetResults(workingState, b);
	}
}
