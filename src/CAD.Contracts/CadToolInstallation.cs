using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AICad.Contracts;

public sealed partial class CadToolCatalog
{
    FileStream AcquireCatalogLock()
    {
        try{return new FileStream(Path.Combine(root,".catalog-write.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
        catch(IOException){throw new CadException("TOOL_BUSY","Kho đang được cập nhật bởi phiên khác. Chỉ cập nhật từ một máy và chờ đồng bộ xong.");}
    }
    void VerifyPackage(CadToolEntry tool)
    {
        string file=PathFor(tool);
        if(!File.Exists(file)||Json.FileHash(file)!=tool.FileHash||PackageHash(Directory.GetFiles(Path.GetDirectoryName(file)!))!=tool.PackageHash)
            throw new CadException("TOOL_CHANGED","Kho add-in chưa đồng bộ đủ hoặc gói đã đổi: "+tool.Name+". Chờ đồng bộ toàn bộ _Installed và catalog.json; nếu gói bị sửa, nhập cập nhật qua Manager.");
    }
    static string Stem(string file)
    {
        string stem=Path.GetFileNameWithoutExtension(file);
        if(!Regex.IsMatch(stem,@"^[\p{L}\p{N}][\p{L}\p{N}_.-]{0,99}$"))throw new CadException("TOOL_NAME","Tên DLL không phù hợp làm tên thư mục add-in.");
        return stem;
    }
    static string VersionOf(string file)
    {
        var v=FileVersionInfo.GetVersionInfo(file);
        return string.IsNullOrEmpty(v.FileVersion)?"unversioned":v.FileMajorPart+"."+v.FileMinorPart+"."+v.FileBuildPart;
    }
    static string Fingerprint(string[] files)
    {
        string rows=string.Concat(files.OrderBy(Path.GetFileName,StringComparer.OrdinalIgnoreCase).Select(f=>Path.GetFileName(f)+"\t"+Json.FileHash(f)+"\n"));
        using var sha=System.Security.Cryptography.SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(rows))).Replace("-","").ToLowerInvariant();
    }
    void SafeDirectory(string directory)
    {
        string full=Path.GetFullPath(directory),allowed=Path.GetFullPath(Path.Combine(root,"dotnet"))+Path.DirectorySeparatorChar;
        if(!full.StartsWith(allowed,StringComparison.OrdinalIgnoreCase))throw new CadException("TOOL_PATH","Thư mục nằm ngoài dotnet.");
        for(var d=new DirectoryInfo(full);d!=null&&d.FullName.Length>=root.Length;d=d.Parent)
            if(d.Exists&&(d.Attributes&FileAttributes.ReparsePoint)!=0)throw new CadException("TOOL_PATH","Không thao tác thư mục liên kết.");
        var pending=new Stack<string>();if(Directory.Exists(full))pending.Push(full);
        while(pending.Count>0)foreach(string path in Directory.GetFileSystemEntries(pending.Pop()))
        {
            var attributes=File.GetAttributes(path);
            if((attributes&FileAttributes.ReparsePoint)!=0)throw new CadException("TOOL_PATH","Không thao tác cây thư mục chứa liên kết.");
            if((attributes&FileAttributes.Directory)!=0)pending.Push(path);
        }
    }
    static void CheckFiles(string[] expected,string directory)
    {
        var actual=Directory.GetFiles(directory);
        if(PackageHash(actual)!=PackageHash(expected))throw new CadException("TOOL_CHANGED","Gói bị thay đổi hoặc chưa sao chép đủ. Không kích hoạt bản mới.");
    }
    string Archive(string main,string[] files)
    {
        string fingerprint=Fingerprint(files),stem=Stem(main),version=VersionOf(main);
        string build=Path.Combine(root,"dotnet","_Development",stem,"versions",version,"build-"+fingerprint.Substring(0,12));
        string package=Path.Combine(build,"package");SafeDirectory(build);
        if(Directory.Exists(package)){CheckFiles(files,package);return build;}
        // Publish an immutable build only after all files were copied and verified.
        string stage=Path.Combine(root,"dotnet","_Development",stem,"working","archive-"+Guid.NewGuid().ToString("N"));SafeDirectory(stage);
        try
        {
            Directory.CreateDirectory(Path.Combine(stage,"package"));
            foreach(string file in files)File.Copy(file,Path.Combine(stage,"package",Path.GetFileName(file)));
            CheckFiles(files,Path.Combine(stage,"package"));
            if(Fingerprint(Directory.GetFiles(Path.Combine(stage,"package")))!=fingerprint)throw new CadException("TOOL_CHANGED","Nguồn thay đổi trong lúc lưu phiên bản.");
            Json.Atomic(Path.Combine(stage,"manifest.json"),new{schemaVersion=1,addin=stem,version,bundleFingerprint=fingerprint,
                fingerprintAlgorithm="SHA256 of UTF-8 filename TAB lowercase-SHA256 LF rows, names sorted OrdinalIgnoreCase",createdUtc=DateTime.UtcNow,
                statusAtCapture="manager-package-archive",sourceWorkingTree="unknown-for-existing-package",sourceProductVersion=FileVersionInfo.GetVersionInfo(main).ProductVersion,
                target=new{autoCAD="2021",framework="net48",architecture="x64"},files=files.OrderBy(Path.GetFileName,StringComparer.OrdinalIgnoreCase).Select(f=>new{name=Path.GetFileName(f),bytes=new FileInfo(f).Length,sha256=Json.FileHash(f)})});
            File.WriteAllText(Path.Combine(stage,"NOTES.md"),"# Gói được CAD Manager lưu\n\nGói DLL bất biến; đã kiểm tra SHA-256 trước khi nhập/thay thế. Nguồn và kiểm thử cần đối chiếu README/commit của add-in. Nhập lại DLL chính trong package để quay về bản này.\n",new UTF8Encoding(false));
            Directory.CreateDirectory(Path.GetDirectoryName(build)!);Directory.Move(stage,build);return build;
        }
        finally{if(Directory.Exists(stage))Directory.Delete(stage,true);}
    }
    string Install(string main,string[] files,string packageHash)
    {
        string stem=Stem(main),fingerprint=Fingerprint(files);
        string directory=Path.Combine(root,"dotnet","_Installed",stem,VersionOf(main)+"-"+fingerprint.Substring(0,12));SafeDirectory(directory);
        if(Directory.Exists(directory)){CheckFiles(files,directory);if(PackageHash(Directory.GetFiles(directory))!=packageHash)throw new CadException("TOOL_CHANGED","Gói cài không khớp danh mục.");return Path.Combine(directory,Path.GetFileName(main));}
        string stage=Path.Combine(root,"dotnet","_Development",stem,"working","install-"+Guid.NewGuid().ToString("N"));SafeDirectory(stage);
        try
        {
            Directory.CreateDirectory(stage);foreach(string file in files)File.Copy(file,Path.Combine(stage,Path.GetFileName(file)));
            if(PackageHash(Directory.GetFiles(stage))!=packageHash)throw new CadException("TOOL_CHANGED","File thay đổi trong lúc nhập. Bản đang chọn được giữ nguyên.");
            Directory.CreateDirectory(Path.GetDirectoryName(directory)!);Directory.Move(stage,directory);return Path.Combine(directory,Path.GetFileName(main));
        }
        finally{if(Directory.Exists(stage))Directory.Delete(stage,true);}
    }
    CadToolEntry ImportInstalled(string source,int revision,string? replaceId)
    {
        source=Path.GetFullPath(source);
        if(!string.Equals(Path.GetExtension(source),".dll",StringComparison.OrdinalIgnoreCase))throw new CadException("TOOL_TYPE","Chọn DLL .NET Framework 4.8.");
        if(!File.Exists(source))throw new CadException("TOOL_MISSING","Không tìm thấy file công cụ.");
        string stem=Stem(source);if(Reserved(stem))throw new CadException("TOOL_RESERVED","Không nhập DLL của AutoCAD hoặc AI CAD làm công cụ.");
        var files=Directory.GetFiles(Path.GetDirectoryName(source)!).Where(p=>new[]{".dll",".pdb"}.Contains(Path.GetExtension(p).ToLowerInvariant())&&!Reserved(Path.GetFileNameWithoutExtension(p))).ToArray();
        if(files.Length>200||files.Sum(p=>new FileInfo(p).Length)>100L*1024*1024)throw new CadException("TOOL_SIZE","Gói công cụ tối đa 100 MB / 200 file.");
        CadToolEntry? imported=null;var retired=new List<CadToolEntry>();
        Change(revision,index=>
        {
            bool axis=stem.Equals("Rainmaker.Axis",StringComparison.OrdinalIgnoreCase);
            var old=replaceId==null?(axis?index.Items.SingleOrDefault(IsLegacyAxis):null):index.Items.SingleOrDefault(t=>t.Id==replaceId)??(replaceId==AxisToolId&&axis?DefaultAxis():throw new CadException("TOOL_MISSING","Công cụ không còn trong danh mục."));
            if(old!=null&&!(axis&&IsLegacyAxis(old)))RequireAddIn(old);
            if(index.Items.Any(t=>t.Kind=="dotnet"&&t.Id!=old?.Id&&Stem(t.RelativePath).Equals(stem,StringComparison.OrdinalIgnoreCase)))throw new CadException("TOOL_DUPLICATE","Add-in này đã có. Chọn mục hiện có rồi dùng Cập nhật file.");
            BackupCatalog(index);
            if(old?.Kind=="dotnet")
            {
                VerifyPackage(old);string previous=PathFor(old);Archive(previous,Directory.GetFiles(Path.GetDirectoryName(previous)!));retired.Add(old);
            }
            string archived=Path.Combine(Archive(source,files),"package");var frozen=Directory.GetFiles(archived);string hash=PackageHash(frozen);string destination=Install(Path.Combine(archived,Path.GetFileName(source)),frozen,hash);
            imported=new CadToolEntry{Id=old?.Id??Guid.NewGuid().ToString("N"),Name=old?.Name??stem,Kind="dotnet",RelativePath=destination.Substring(root.Length+1),FileHash=Json.FileHash(destination),PackageHash=hash,
                Group=old?.Group??"",Notes=old?.Notes??"",Enabled=old?.Enabled??true,Commands=old?.Commands??new(),RibbonPins=old?.RibbonPins??new(),TabId=old?.TabId??DefaultTabId,UpdatedUtc=DateTime.UtcNow};
            if(old!=null)index.Items.Remove(old);index.Items.Add(imported);index.StorageVersion=2;
        },index=>CleanupAfterCommit(index,retired));
        return imported!;
    }
    /// <summary>Move selected legacy packages without changing IDs, settings or binary versions.</summary>
    public void MigrateInstalled(int revision)
    {
        var retired=new List<CadToolEntry>();
        Change(revision,index=>
        {
            BackupCatalog(index);
            foreach(var item in index.Items.Where(t=>t.Kind=="dotnet"))
            {
                VerifyPackage(item);string old=PathFor(item);var files=Directory.GetFiles(Path.GetDirectoryName(old)!);
                Archive(old,files);string destination=Install(old,files,item.PackageHash);
                if(!destination.Equals(old,StringComparison.OrdinalIgnoreCase))retired.Add(new CadToolEntry{Id=item.Id,Kind=item.Kind,RelativePath=item.RelativePath,PackageHash=item.PackageHash});
                item.RelativePath=destination.Substring(root.Length+1);
            }
            index.StorageVersion=2;
        },index=>CleanupAfterCommit(index,retired));
    }
    void BackupCatalog(CadToolIndex index)=>Json.Atomic(Path.Combine(root,"_InstallBackups",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8),"catalog.json"),index);
    void CleanupAfterCommit(CadToolIndex index,List<CadToolEntry> retired)
    {
        var selected=new HashSet<string>(index.Items.Where(t=>t.Kind=="dotnet").Select(t=>Path.GetDirectoryName(PathFor(t))!),StringComparer.OrdinalIgnoreCase);
        var candidates=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var old in retired)
        {
            string folder=Path.GetDirectoryName(PathFor(old))!;
            if(Path.GetFileName(folder)==old.PackageHash&&Path.GetFileName(Path.GetDirectoryName(folder))==old.Id)candidates.Add(folder);
        }
        // Retry a locked legacy folder on the next migration; only recognized, hash-valid packages qualify.
        foreach(var item in index.Items.Where(t=>t.Kind=="dotnet"))
        {
            string legacy=Path.Combine(root,"dotnet",item.Id);SafeDirectory(legacy);
            if(!Directory.Exists(legacy))continue;
            if(!Directory.EnumerateFileSystemEntries(legacy).Any()){candidates.Add(legacy);continue;}
            foreach(string folder in Directory.GetDirectories(legacy))
            {
                if(selected.Contains(folder)||!Regex.IsMatch(Path.GetFileName(folder),"^[a-f0-9]{64}$"))continue;
                if(!Directory.EnumerateFileSystemEntries(folder).Any()){candidates.Add(folder);continue;}
                SafeDirectory(folder);var files=Directory.GetFiles(folder);string main=Path.Combine(folder,Path.GetFileName(item.RelativePath));
                if(File.Exists(main)&&PackageHash(files)==Path.GetFileName(folder)){Archive(main,files);candidates.Add(folder);}
            }
        }
        string installed=Path.Combine(root,"dotnet","_Installed");
        if(Directory.Exists(installed))foreach(string group in Directory.GetDirectories(installed))foreach(string folder in Directory.GetDirectories(group))
        {
            if(selected.Contains(folder))continue;
            if(Regex.IsMatch(Path.GetFileName(folder),@"^(?:\d+\.\d+\.\d+|unversioned)-[a-f0-9]{12}$")&&!Directory.EnumerateFileSystemEntries(folder).Any()){candidates.Add(folder);continue;}
            string main=Path.Combine(folder,Path.GetFileName(group)+".dll");
            if(!File.Exists(main))continue; // Never remove unknown user-created directories.
            SafeDirectory(folder);Archive(main,Directory.GetFiles(folder));candidates.Add(folder);
        }
        var pending=new List<string>();
        foreach(string folder in candidates)
        {
            if(selected.Contains(folder)||!Directory.Exists(folder))continue;SafeDirectory(folder);
            try{
                foreach(string path in Directory.GetFileSystemEntries(folder,"*",SearchOption.AllDirectories).Concat(new[]{folder}))File.SetAttributes(path,File.GetAttributes(path)&~FileAttributes.ReadOnly);
                Directory.Delete(folder,true);string parent=Path.GetDirectoryName(folder)!;
                if(Directory.Exists(parent)&&!Directory.EnumerateFileSystemEntries(parent).Any()){File.SetAttributes(parent,File.GetAttributes(parent)&~FileAttributes.ReadOnly);Directory.Delete(parent);}
            }
            catch(IOException){pending.Add(folder.Substring(root.Length+1));}
            catch(UnauthorizedAccessException){pending.Add(folder.Substring(root.Length+1));}
        }
        if(pending.Count>0)throw new CadException("TOOL_CLEANUP_PENDING","Bản mới đã được chọn nhưng gói cũ đang bị khóa. Đóng AutoCAD ở cả hai máy rồi chạy lại chuyển cấu trúc để dọn: "+string.Join(", ",pending));
    }
}
