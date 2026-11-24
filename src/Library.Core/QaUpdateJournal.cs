using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AICad.Contracts;
using Newtonsoft.Json.Linq;

namespace AICad.Application;

// Durable recovery information, independent of the transient browser snapshot.
public sealed class QaUpdateJournal
{
    readonly string warehouse,source;
    public string PendingPath {get;}
    public QaUpdateJournal(InstallConfig config)
    {
        warehouse=Path.GetFullPath(WarehousePaths.Resolve(config.ContentRoot));source=CadSourceFile.BaseLibraryPath(config.ContentRoot);
        PendingPath=Path.Combine(warehouse,"Catalog","qa-transactions","ISO_QUANGANH.pending.json");
    }
    public static string SharedHash(string path)
    {
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        using var hash=SHA256.Create();return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
    }
    public IDisposable Lock()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PendingPath)!);
        try{return new FileStream(PendingPath+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
        catch(IOException){throw new CadException("SYNC_BUSY","Một phiên QA khác đang cập nhật thư viện.");}
    }
    public JObject? Read()
    {
        if(!File.Exists(PendingPath))return null;
        var record=Json.Read<JObject>(File.ReadAllText(PendingPath));
        if(record.Value<string>("sourceId")!="ISO_QUANGANH")throw new CadException("QA_JOURNAL","Journal không thuộc nguồn thư viện này.");
        _=Folder(record);return record;
    }
    public string Folder(JObject record)
    {
        string relative=record.Value<string>("backupRelative")??"";
        if(!System.Text.RegularExpressions.Regex.IsMatch(relative,"^Versions/qa-mutations/[a-f0-9]{32}/before\\.dwg$"))throw new CadException("QA_JOURNAL","Đường dẫn bản sao không hợp lệ.");
        string path=Path.GetFullPath(Path.Combine(warehouse,relative.Replace('/',Path.DirectorySeparatorChar)));
        if(!path.StartsWith(warehouse+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new CadException("QA_JOURNAL","Bản sao nằm ngoài kho.");
        var cursor=new DirectoryInfo(Path.GetDirectoryName(path)!);
        while(cursor!=null&&cursor.FullName.StartsWith(warehouse,StringComparison.OrdinalIgnoreCase)){if(cursor.Exists&&(cursor.Attributes&FileAttributes.ReparsePoint)!=0)throw new CadException("QA_JOURNAL","Không dùng bản sao qua liên kết thư mục.");cursor=cursor.Parent;}
        return Path.GetDirectoryName(path)!;
    }
    public JObject Begin(JObject plan)
    {
        if(File.Exists(PendingPath))throw new CadException("QA_PENDING","Có cập nhật chưa hoàn tất. Mở Xử lý cập nhật trước.");
        if(plan.Value<string>("sourceHash") is string expected&&SharedHash(source)!=expected)throw new CadException("SOURCE_CHANGED","Nguồn đã đổi sau khi xem kế hoạch.");
        string id=Guid.NewGuid().ToString("N"),relative="Versions/qa-mutations/"+id+"/before.dwg";
        var record=new JObject{["schemaVersion"]=2,["sourceId"]="ISO_QUANGANH",["operation"]="cad-publish",["phase"]="PREPARED",["backupRelative"]=relative,["createdUtc"]=DateTime.UtcNow,["plan"]=plan.DeepClone()};
        string folder=Folder(record);Directory.CreateDirectory(folder);CopyChecked(source,Path.Combine(folder,"before.dwg"));record["beforeHash"]=SharedHash(source);
        Save(record);return record;
    }
    static void CopyChecked(string from,string to)
    {
        string before=SharedHash(from);
        using(var input=new FileStream(from,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var output=new FileStream(to,FileMode.CreateNew,FileAccess.Write,FileShare.None))input.CopyTo(output);
        if(SharedHash(to)!=before||SharedHash(from)!=before)throw new CadException("SOURCE_CHANGED","DWG đổi trong lúc sao lưu. Chưa ghi đè nguồn.");
    }
    public void Save(JObject record){_=Folder(record);Json.Atomic(PendingPath,record);}
    public bool CanContinue(JObject record)
    {
        string hash=record.Value<string>("afterHash")??record.Value<string>("sourceHash")??"";
        return hash!=""&&File.Exists(source)&&SharedHash(source)==hash;
    }
    public void Complete(JObject record)
    {
        if(!CanContinue(record))throw new CadException("SOURCE_CHANGED","DWG đã lưu thay đổi; chưa thể xác nhận hoàn tất.");
        record["phase"]="COMPLETE";record["completedUtc"]=DateTime.UtcNow;
        Json.Atomic(Path.Combine(Folder(record),"result.json"),record);File.Delete(PendingPath);
    }
    public string AbandonKeepingCad(JObject publishedCheckpoint)
    {
        var record=Read()??throw new CadException("QA_PENDING","Không có cập nhật chờ.");
        // Refuse cancellation while the catalog's own atomic publication needs recovery.
        if(File.Exists(Path.Combine(warehouse,"Catalog","sync","pending.json")))throw new CadException("QA_PENDING","Danh mục còn giao dịch ghi dở. QA phải kiểm tra/phục hồi danh mục trước khi hủy kế hoạch.");
        string recovery=Path.Combine(Folder(record),"recovery-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(recovery);
        CopyChecked(source,Path.Combine(recovery,"preserved-current.dwg"));
        string sourceHash=SharedHash(source);
        Json.Atomic(Path.Combine(recovery,"published-checkpoint.json"),publishedCheckpoint);
        var pointers=new JObject();
        foreach(string relative in new[]{"Catalog/sync/state.json","Catalog/resource-codes.json","Versions/templates/ISO_QUANGANH/active.json","Styles/ISO_QUANGANH/active.json","Hatches/ISO_QUANGANH/active.json"})
        {string path=Path.Combine(warehouse,relative.Replace('/',Path.DirectorySeparatorChar));if(File.Exists(path))pointers[relative]=Json.Read<JToken>(File.ReadAllText(path));}
        Json.Atomic(Path.Combine(recovery,"published-pointers.json"),pointers);
        string settings=Path.Combine(warehouse,"Catalog","addin-settings");
        if(Directory.Exists(settings))foreach(string file in Directory.GetFiles(settings,"*.json")){string copy=Path.Combine(recovery,"consumer-checkpoint",Path.GetFileName(file));Directory.CreateDirectory(Path.GetDirectoryName(copy)!);CopyChecked(file,copy);}
        if(SharedHash(source)!=sourceHash||SharedHash(Path.Combine(recovery,"preserved-current.dwg"))!=sourceHash)throw new CadException("SOURCE_CHANGED","Nguồn đã thay đổi khi lưu hồ sơ hủy. Giữ hàng chờ để kiểm tra lại.");
        Json.Atomic(Path.Combine(recovery,"decision.json"),new{status="ABANDONED_PRESERVE_CURRENT_CAD",previousPhase=record.Value<string>("phase"),atUtc=DateTime.UtcNow,sourceHash,partialPublicationKept=true,next="Read current CAD and review a new plan; no automatic rollback"});
        // Preserve the original record byte for byte; no native resource, consumer setting
        // or catalog pointer is reverted, even after a partially applied publication.
        File.Move(PendingPath,Path.Combine(recovery,"pending-original.json"));return recovery;
    }
}
