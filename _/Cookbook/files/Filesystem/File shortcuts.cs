/// Use class <see cref="shortcutFile"/>.

/// Create shortcut to Notepad.exe.

using (var x = shortcutFile.create(@"C:\Test\Notepad.lnk")) {
	x.TargetPath = folders.System + "Notepad.exe";
	//x.Hotkey = (KMod.Ctrl | KMod.Alt, KKey.D5); //optionally set more properties
	x.Save();
}

/// Get shortcut target path.

string path = shortcutFile.getTarget(@"C:\Test\Notepad.lnk");
print.it(path);

/// Get shortcut properties.

using (var x = shortcutFile.open(@"C:\Test\Notepad.lnk")) {
	print.it(x.TargetPath);
	print.it(x.GetIconLocation(out var ii), ii);
}

/// Delete shortcut (if exists) and unregister its hotkey.

shortcutFile.delete(@"C:\Test\Notepad.lnk");
