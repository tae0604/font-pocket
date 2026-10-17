using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace FontPocket {
    public static class DataPaths { public static string Root { get { string test=Environment.GetEnvironmentVariable("FONTPOCKET_TEST_DATA");return String.IsNullOrWhiteSpace(test)?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FontPocket"):Path.GetFullPath(test); } } }
    public class FontNote { public string Key, Memo="", Tags=""; public bool Favorite; public List<string> Collections=new List<string>(); }
    public class FontCollection { public string Id=Guid.NewGuid().ToString("N"), Name, Query=""; public bool Smart; }
    public class PersonalData { public int Schema=1; public List<FontNote> Notes=new List<FontNote>(); public List<FontCollection> Collections=new List<FontCollection>(); }
    public class PersonalLibrary {
        public static string DefaultPath { get { return Path.Combine(DataPaths.Root,"library.json"); } }
        public PersonalData Data=new PersonalData(); public string FilePath;
        public PersonalLibrary(string path) { FilePath=path; if(File.Exists(path)) { if(new FileInfo(path).Length>16*1024*1024) throw new InvalidDataException("개인 보관 정보가 너무 큽니다."); Data=new JavaScriptSerializer { MaxJsonLength=16*1024*1024 }.Deserialize<PersonalData>(File.ReadAllText(path)); Validate(Data); } }
        public static void Validate(PersonalData data) { if(data==null || data.Schema!=1 || data.Notes==null || data.Collections==null || data.Notes.Any(n=>n==null || n.Key==null || n.Collections==null) || data.Collections.Any(c=>c==null || c.Id==null || String.IsNullOrWhiteSpace(c.Name))) throw new InvalidDataException("개인 보관 정보 형식이 잘못됐습니다."); }
        public static string Key(ManagedFont f) { return (f.Identity??f.Name)+"|"+f.Version+"|"+Path.GetExtension(f.Path).ToLowerInvariant(); }
        public FontNote Note(ManagedFont font) { string key=Key(font); var note=Data.Notes.FirstOrDefault(n=>n.Key==key); if(note==null) { note=new FontNote { Key=key }; Data.Notes.Add(note); } return note; }
        public string SearchText(ManagedFont font) { var n=Note(font); return (n.Memo??"")+" "+(n.Tags??""); }
        public bool InCollection(ManagedFont font,string id) { var n=Note(font); if(id=="favorite") return n.Favorite; var c=Data.Collections.FirstOrDefault(x=>x.Id==id); if(c==null) return true; return c.Smart?MatchesRule(font,c.Query):n.Collections.Contains(id); }
        public bool MatchesRule(ManagedFont f,string query) { foreach(string part in (query??"").Split(new[]{';'},StringSplitOptions.RemoveEmptyEntries)) { string rule=part.Trim(); if(rule=="활성") { if(f.Disabled) return false; } else if(rule=="비활성") { if(!f.Disabled) return false; } else if(rule=="가변") { if(f.Metadata==null || !f.Metadata.Variable) return false; } else if(!(f.Matches(rule) || SearchText(f).IndexOf(rule,StringComparison.OrdinalIgnoreCase)>=0)) return false; } return true; }
        public void Save() { Validate(Data); Directory.CreateDirectory(Path.GetDirectoryName(FilePath)); string tmp=FilePath+"."+Guid.NewGuid().ToString("N")+".tmp"; try { File.WriteAllText(tmp,new JavaScriptSerializer { MaxJsonLength=16*1024*1024 }.Serialize(Data),new UTF8Encoding(false)); if(File.Exists(FilePath)) File.Replace(tmp,FilePath,null); else File.Move(tmp,FilePath); } finally { if(File.Exists(tmp)) File.Delete(tmp); } }
    }
    public class ArchivedVersion {
        public string Hash, FileName, Name, Identity, Version, Format;
    }
    public class VersionLibrary {
        public string Root;
        public VersionLibrary(string root) { Root=Path.GetFullPath(root); }
        public static VersionLibrary Default { get { return new VersionLibrary(Path.Combine(DataPaths.Root,"Versions")); } }
        public string FileFor(ArchivedVersion v) { if(v==null || !System.Text.RegularExpressions.Regex.IsMatch(v.Hash??"","^[a-f0-9]{64}$") || v.FileName!=v.Hash+Path.GetExtension(v.FileName) || !Engine.IsFont(v.FileName)) throw new InvalidDataException("버전 보관 파일 경로가 잘못됐습니다."); return Path.Combine(Root,v.FileName); }
        public ArchivedVersion Capture(string path) {
            var f=new ManagedFont { Path=path }; FontLibrary.ReadMetadata(f); if(f.Metadata==null) throw new InvalidDataException("폰트 정보를 읽지 못했습니다.");
            var v=new ArchivedVersion { Hash=Engine.Hash(path),Name=f.Name,Identity=f.Identity,Version=f.Version,Format=Path.GetExtension(path).TrimStart('.').ToUpperInvariant() }; v.FileName=v.Hash+Path.GetExtension(path).ToLowerInvariant();
            Directory.CreateDirectory(Root); string target=FileFor(v);
            if(!File.Exists(target)) File.Copy(path,target,false); if(Engine.Hash(target)!=v.Hash) throw new IOException("버전 보관 파일 무결성 오류입니다.");
            string manifest=Path.Combine(Root,v.Hash+".json"),temp=manifest+".tmp";
            try { File.WriteAllText(temp,new JavaScriptSerializer().Serialize(v),new UTF8Encoding(false)); if(File.Exists(manifest)) File.Replace(temp,manifest,null); else File.Move(temp,manifest); } finally { if(File.Exists(temp)) File.Delete(temp); }
            return v;
        }
        public List<ArchivedVersion> ReadAll() { var result=new List<ArchivedVersion>(); if(!Directory.Exists(Root)) return result; foreach(string path in Directory.GetFiles(Root,"*.json")) { if(new FileInfo(path).Length>1024*1024) throw new InvalidDataException("버전 정보가 너무 큽니다."); var v=new JavaScriptSerializer().Deserialize<ArchivedVersion>(File.ReadAllText(path)); if(Path.GetFileNameWithoutExtension(path)!=v.Hash || !File.Exists(FileFor(v))) throw new InvalidDataException("버전 보관 정보를 확인하세요: "+Path.GetFileName(path)); result.Add(v); } return result; }
    }
}
