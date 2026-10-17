using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FontPocket {
    public class ProfileEntry { public string Hash,FileName,Key,Name; public bool Disabled,ArchivedOnly; }
    public class ProfileManifest { public int Schema=1; public PersonalData Personal; public List<ProfileEntry> Fonts=new List<ProfileEntry>(); }
    public class PreparedProfile { public ProfileManifest Manifest; public Dictionary<string,string> Files=new Dictionary<string,string>(); }
    public static class ProfileArchive {
        public static void Create(string destination,List<ManagedFont> fonts,PersonalData personal,VersionLibrary versions,Func<bool> cancel,Action<int,int> report) {
            PersonalLibrary.Validate(personal);var manifest=new ProfileManifest { Personal=personal };var paths=new Dictionary<string,string>();
            foreach(var f in fonts) { string hash=Engine.Hash(f.Path);var existing=manifest.Fonts.FirstOrDefault(v=>v.Hash==hash); if(existing!=null) { existing.Disabled=existing.Disabled && f.Disabled;continue; } var e=new ProfileEntry { Hash=hash,FileName=hash+Path.GetExtension(f.Path).ToLowerInvariant(),Key=PersonalLibrary.Key(f),Name=f.Name,Disabled=f.Disabled };manifest.Fonts.Add(e);paths[hash]=f.Path; }
            foreach(var v in versions.ReadAll()) if(!paths.ContainsKey(v.Hash)) { string path=versions.FileFor(v);if(Engine.Hash(path)!=v.Hash) throw new IOException("버전 보관 파일이 변경됐습니다.");var f=new ManagedFont { Path=path };FontLibrary.ReadMetadata(f);manifest.Fonts.Add(new ProfileEntry { Hash=v.Hash,FileName=v.FileName,Key=PersonalLibrary.Key(f),Name=f.Name,ArchivedOnly=true });paths[v.Hash]=path; }
            string temp=destination+"."+Guid.NewGuid().ToString("N")+".tmp";long total=0;
            try { using(var archive=ZipFile.Open(temp,ZipArchiveMode.Create)) {
                var entry=archive.CreateEntry("fontpocket-profile.json");using(var writer=new StreamWriter(entry.Open(),new UTF8Encoding(false))) writer.Write(new JavaScriptSerializer { MaxJsonLength=16*1024*1024 }.Serialize(manifest));
                for(int i=0;i<manifest.Fonts.Count;i++) { if(cancel()) throw new OperationCanceledException();var e=manifest.Fonts[i];long bytes=new FileInfo(paths[e.Hash]).Length;total+=bytes;if(bytes>512L*1024*1024 || total>2L*1024*1024*1024 || manifest.Fonts.Count>10000) throw new IOException("환경 백업 용량 제한을 넘었습니다.");VerifiedArchive.AddFont(archive,"fonts/"+e.FileName,paths[e.Hash],e.Hash,cancel);report(i+1,manifest.Fonts.Count); }
            } if(cancel()) throw new OperationCanceledException();if(File.Exists(destination)) File.Replace(temp,destination,null);else File.Move(temp,destination); } finally { if(File.Exists(temp)) File.Delete(temp); }
        }
        public static PreparedProfile Prepare(string archivePath,string work,Func<bool> cancel) {
            var prepared=new PreparedProfile();Directory.CreateDirectory(work);long total=0;
            using(var archive=ZipFile.OpenRead(archivePath)) {
                var manifests=archive.Entries.Where(e=>e.FullName=="fontpocket-profile.json").ToList();if(manifests.Count!=1 || manifests[0].Length>16*1024*1024) throw new InvalidDataException("환경 백업 정보가 없거나 잘못됐습니다.");
                using(var input=new StreamReader(manifests[0].Open(),Encoding.UTF8)) { var json=new StringBuilder();var buffer=new char[4096];int n;while((n=input.Read(buffer,0,buffer.Length))>0) { if(json.Length+n>16*1024*1024) throw new InvalidDataException("백업 정보가 너무 큽니다.");json.Append(buffer,0,n); }prepared.Manifest=new JavaScriptSerializer { MaxJsonLength=16*1024*1024 }.Deserialize<ProfileManifest>(json.ToString()); }
                var manifest=prepared.Manifest; if(manifest==null || manifest.Schema!=1 || manifest.Fonts==null || manifest.Fonts.Count>10000) throw new InvalidDataException("지원하지 않는 환경 백업입니다.");PersonalLibrary.Validate(manifest.Personal);
                var seen=new HashSet<string>();
                foreach(var e in manifest.Fonts) {
                    if(cancel()) throw new OperationCanceledException();
                    if(e==null || !System.Text.RegularExpressions.Regex.IsMatch(e.Hash??"","^[a-f0-9]{64}$") || e.FileName!=e.Hash+Path.GetExtension(e.FileName) || !Engine.IsFont(e.FileName) || !seen.Add(e.Hash)) throw new InvalidDataException("백업 파일 경로 / 중복 정보가 잘못됐습니다.");
                    var matches=archive.Entries.Where(x=>x.FullName=="fonts/"+e.FileName).ToList();if(matches.Count!=1) throw new InvalidDataException("백업에 폰트 파일이 없거나 중복됐습니다.");var entry=matches[0];total+=entry.Length;if(entry.Length>512L*1024*1024 || total>2L*1024*1024*1024) throw new InvalidDataException("백업 용량 제한을 넘었습니다.");
                    string target=Path.Combine(work,Guid.NewGuid().ToString("N")+Path.GetExtension(e.FileName));using(var input=entry.Open()) using(var output=File.Create(target)) { long copied=0;int n;var buffer=new byte[81920];while((n=input.Read(buffer,0,buffer.Length))>0) { copied+=n;if(copied>entry.Length || copied>512L*1024*1024) throw new InvalidDataException("잘못된 압축 크기입니다.");output.Write(buffer,0,n); }if(copied!=entry.Length) throw new InvalidDataException("압축 파일이 불완전합니다."); }
                    if(Engine.Hash(target)!=e.Hash) throw new InvalidDataException("백업 폰트 해시 불일치입니다.");var f=new ManagedFont { Path=target };FontLibrary.ReadMetadata(f);if(f.Metadata==null || PersonalLibrary.Key(f)!=e.Key) throw new InvalidDataException("백업 폰트 식별 정보가 다릅니다.");prepared.Files[e.Hash]=target;
                }
            }return prepared;
        }
    }
    public partial class MainWindow {
        async Task SaveEnvironment() {
            if(busy) return;if(!managerLoaded) await LoadManagedFonts();if(!managerLoaded) return;
            using(var save=new SaveFileDialog { Filter="Font Pocket 환경 백업|*.zip",FileName="FontPocket-환경-"+DateTime.Now.ToString("yyyyMMdd")+".zip" }) {
                if(save.ShowDialog(this)!=DialogResult.OK) return;SetBusy(true);
                try { var fonts=managedFonts.Where(f=>f.CanRemove).ToList();var snapshot=new JavaScriptSerializer { MaxJsonLength=16*1024*1024 }.Deserialize<PersonalData>(new JavaScriptSerializer { MaxJsonLength=16*1024*1024 }.Serialize(personal.Data));await Task.Run(delegate { ProfileArchive.Create(save.FileName,fonts,snapshot,VersionLibrary.Default,()=>cancelled,(n,total)=>UI(delegate { progress.Value=total==0?0:n*100/total;status.Text=n+" / "+total+" 환경 백업 중"; })); });Log("폰트·메모·태그·모음·활성 상태·보관 버전 백업 완료");status.Text="환경 백업 완료"; }
                catch(Exception ex) { DialogUI.Error(this,ex); }finally { SetBusy(false); }
            }
        }
        async Task RestoreEnvironment() {
            if(busy) return;
            using(var open=new OpenFileDialog { Filter="Font Pocket 환경 백업|*.zip" }) {
                if(open.ShowDialog(this)!=DialogResult.OK) return;PreparedProfile prepared=null;SetBusy(true);
                try { prepared=await Task.Run(()=>ProfileArchive.Prepare(open.FileName,Path.Combine(tempRoot,Guid.NewGuid().ToString("N")),()=>cancelled)); }
                catch(Exception ex) { DialogUI.Error(this,ex);return; }finally { SetBusy(false); }
                int count=prepared.Manifest.Fonts.Count(e=>!e.ArchivedOnly);
                if(LocalizedMessage.Show(this,count+"개 폰트와 개인 설정을 복원할까요?\r\n보관 버전도 복원합니다. 기존 메모·모음·설치 상태는 유지하고 없는 항목을 추가합니다.\r\n같은 글꼴의 다른 버전이 활성화되어 있으면 보관만 하고 자동 교체하지 않습니다.","환경 복원",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes) return;
                SetBusy(true);int installed=0,skipped=0,failed=0;
                try {
                    await Task.Run(delegate {
                        var hashes=Engine.InstalledHashes(s=>UI(delegate { Log(s); }));var active=FontLibrary.Read();foreach(var f in active) FontLibrary.ReadMetadata(f);
                        var disabled=ReadDisabledFonts(active);var all=active.Concat(disabled).ToList();var vault=VersionLibrary.Default;
                        var outcome=ProfileRestoreEngine.Run(prepared,all,p=>Engine.Validate(p),p=>vault.Capture(p),p=>Engine.Install(new FontItem(p),hashes),delegate(string p) {string target=Path.Combine(Engine.UserDir,"FontPocket-"+Engine.Hash(p)+Path.GetExtension(p).ToLowerInvariant());var font=FontLibrary.Read().FirstOrDefault(f=>String.Equals(f.Path,target,StringComparison.OrdinalIgnoreCase));if(font!=null) FontLibrary.ReadMetadata(font);return font;},f=>FontActivation.Disable(f,new WindowsFontActivationStore(),ActivationRepository.Default),()=>cancelled,s=>UI(delegate {Log(s);}),(n,total)=>UI(delegate {progress.Value=n*100/total;status.Text=n+" / "+total+" 복원 중";}));
                        installed=outcome.Installed;skipped=outcome.Skipped;failed=outcome.Failed;if(outcome.Changed) Engine.Notify();
                    });
                    if(!cancelled) { var old=personal.Data;var merged=new JavaScriptSerializer { MaxJsonLength=16*1024*1024 }.Deserialize<PersonalData>(new JavaScriptSerializer { MaxJsonLength=16*1024*1024 }.Serialize(old)); foreach(var c in prepared.Manifest.Personal.Collections) if(!merged.Collections.Any(x=>x.Id==c.Id)) merged.Collections.Add(c);foreach(var n in prepared.Manifest.Personal.Notes) if(!merged.Notes.Any(x=>x.Key==n.Key)) merged.Notes.Add(n);personal.Data=merged;try { personal.Save(); }catch { personal.Data=old;throw; }RefreshCollections(); }
                    status.Text=(cancelled?"복원 중단":"환경 복원 완료")+" · 설치 "+installed+" / 유지·보관 "+skipped+" / 실패 "+failed;Log(status.Text);
                }catch(Exception ex) { DialogUI.Error(this,ex); }finally { SetBusy(false);managerLoaded=false;backupLoaded=false; }await LoadManagedFonts();
            }
        }
    }
}
