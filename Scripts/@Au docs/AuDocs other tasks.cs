/*/ nuget -\SSH.NET; c Sftp.cs; c Passwords.cs; /*/
using Renci.SshNet;
using System.Text.Json;

partial class AuDocs {
	public static void CompressAndUpload(string siteDir) {
		if (1 != dialog.show("Upload?", null, "1 Yes|2 No"/*, secondsTimeout: 5*/)) return;
		var tarDir = pathname.getDirectory(siteDir);
		_Compress(siteDir, tarDir);
		_Upload(tarDir);
		_CloudflarePurgeCache();
	}
	
	static void _Compress(string siteDir, string tarDir) {
		var sevenZip = @"C:\Program Files\7-Zip\7z.exe";
		
		var tar = tarDir + @"\site.tar";
		filesystem.delete(tar);
		filesystem.delete(tarDir + @"\site.tar.gz");
		
		int r1 = run.console(out var s, sevenZip, $@"a ""{tar}""", siteDir); //add files to the root in the archive, not to "site" dir
		if (r1 != 0) { print.it(r1, s); return; }
		int r2 = run.console(out s, sevenZip, $@"a site.tar.gz site.tar", tarDir);
		if (r2 != 0) { print.it(r2, s); return; }
		
		filesystem.delete(tarDir + @"\site.tar");
		
		print.it("Compressed");
	}
	
	static void _Upload(string tarDir) {
		var ci = Sftp.GetConnectionInfo(); if (ci == null) return;
		
		const string ftpDir = "domains/libreautomate.com/public_html";
		var localFile = tarDir + @"/site.tar.gz";
		
		//upload
		using var ftp = new SftpClient(ci.host, ci.port, ci.user, ci.pass);
		ftp.ConnectAndUpload(ftpDir, localFile);
		print.it("Uploaded");
		
		//extract
		using var ssh = new SshClient(ci.host, ci.port, ci.user, ci.pass);
		ssh.Connect();
		_Cmd2("tar -zxf site.tar.gz");
		
		void _Cmd(string s, bool silent = false) {
			var c = ssh.RunCommand(s);
			//print.it($"ec={c.ExitStatus}, result={c.Result}, error={c.Error}");
			if (!silent && c.ExitStatus != 0) throw new Exception(c.Error);
		}
		
		//cd does not work when separate command
		void _Cmd2(string s, bool silent = false) => _Cmd($"cd {ftpDir} && {s}", silent);
		
		ftp.DeleteFile("site.tar.gz");
		filesystem.delete(localFile);
		
		print.it("<>Extracted to <link>https://www.libreautomate.com/</link>");
	}
	
	//file: path to a file in https://www.libreautomate.com/. If null, purges all.
	static void _CloudflarePurgeCache(string file = null) {
		var token = Passwords.Get("Cloudflare API");
		var content = internet.jsonContent(file != null ? $$"""{"files":["https://www.libreautomate.com/{{file}}"]}""" : $$"""{"purge_everything": true}""");
		var j = internet.http.Post("https://api.cloudflare.com/client/v4/zones/238431a81c22e29834a04ce68574988c/purge_cache", content, [$"Authorization: Bearer {token}"]).Json();
		var ok = (bool)j["success"];
		if (!ok) print.warning("Failed to purge Cloudflare cache.\r\nTo purge manually: Cloudflare -> Caching -> Configuration -> Purge Everything.\r\nResult:\r\n" + j);
	}
}
