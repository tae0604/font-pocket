using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Win32;

namespace FontPocket {
    static class ReliabilityTests {
        static void Check(bool pass,string message) {if(!pass) throw new Exception(message);}
        static ManagedFont Read(string path) {var f=new ManagedFont {Path=path};FontLibrary.ReadMetadata(f);return f;}
        public static void Run(string root,List<string> report) {
            string source=Path.Combine(root,"install-source.ttf");File.Copy(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"arial.ttf"),source,true);string original=Engine.Hash(source),family=Engine.Validate(source),name=family+" [FontPocket "+original+"]";
            var store=new Store(Path.Combine(root,"install-success"));var hashes=new HashSet<string>();FontInstaller.Install(new FontItem(source),hashes,store,Engine.Validate);string destination=store.Entries.Single().Value.Data;
            Check(File.Exists(destination) && Engine.Hash(destination)==original && store.Loads==1 && hashes.Contains(original),"Install did not copy/register verified font");FontInstaller.Install(new FontItem(source),hashes,store,Engine.Validate);Check(store.Loads==1,"Duplicate installation loaded twice");
            foreach(string failure in new[]{"load","write-before","write-after"}) {
                var failing=new Store(Path.Combine(root,"install-"+failure)) {FailLoad=failure=="load",FailWrite=failure=="write-before"?1:failure=="write-after"?2:0};bool rejected=false;
                try {FontInstaller.Install(new FontItem(source),new HashSet<string>(),failing,Engine.Validate);}catch(IOException) {rejected=true;}
                Check(rejected && failing.Entries.Count==0 && (!Directory.Exists(failing.DirectoryPath) || Directory.GetFiles(failing.DirectoryPath).Length==0),"Installation rollback left registration or new bytes: "+failure);
            }
            var unsafeRollback=new Store(Path.Combine(root,"install-retained")) {FailWrite=2,FailDelete=true};bool failed=false;try {FontInstaller.Install(new FontItem(source),new HashSet<string>(),unsafeRollback,Engine.Validate);}catch(IOException) {failed=true;}Check(failed && unsafeRollback.Entries.Count==1 && File.Exists(unsafeRollback.Entries.Single().Value.Data),"Failed rollback deleted registered file");
            var conflict=new Store(Path.Combine(root,"install-conflict"));conflict.Entries[name]=new FontRegistration {Name=name,Data="other-font.ttf",Kind=RegistryValueKind.String};failed=false;try {FontInstaller.Install(new FontItem(source),new HashSet<string>(),conflict,Engine.Validate);}catch(IOException) {failed=true;}Check(failed && conflict.Loads==0 && conflict.Entries[name].Data=="other-font.ttf","Installation overwrote foreign registry entry");
            string changed=Path.Combine(root,"install-changed.ttf");File.Copy(source,changed,true);var raced=new Store(Path.Combine(root,"install-race"));raced.ReadHook=delegate(int n) {if(n==1) using(var stream=new FileStream(changed,FileMode.Append)) stream.WriteByte(0);};failed=false;try {FontInstaller.Install(new FontItem(changed),new HashSet<string>(),raced,Engine.Validate);}catch(IOException) {failed=true;}Check(failed && raced.Loads==0 && raced.Entries.Count==0,"Modified installation copy was loaded");
            Check(Engine.Hash(source)==original,"Installation tests changed source");report.Add("PASS: isolated installation, duplicate skip, load/write-before/write-after rollback, registered-file preservation, registry conflict and changed-copy rejection");

            var f=Read(source);var personal=new PersonalData();var emptyVersions=new VersionLibrary(Path.Combine(root,"restore-empty-versions"));string backup=Path.Combine(root,"restore-profile.zip");f.Disabled=true;ProfileArchive.Create(backup,new List<ManagedFont>{f},personal,emptyVersions,()=>false,(n,total)=>{});var prepared=ProfileArchive.Prepare(backup,Path.Combine(root,"restore-payload"),()=>false);
            var restoreStore=new Store(Path.Combine(root,"profile-fonts"));var repo=new ActivationRepository(Path.Combine(root,"profile-disabled"),restoreStore.DirectoryPath);int progress=0;
            var restored=ProfileRestoreEngine.Run(prepared,new List<ManagedFont>(),p=>Engine.Validate(p),p=>{},p=>FontInstaller.Install(new FontItem(p),new HashSet<string>(),restoreStore,Engine.Validate),p=>restoreStore.Current(Path.Combine(restoreStore.DirectoryPath,"FontPocket-"+Engine.Hash(p)+".ttf")),font=>FontActivation.Disable(font,restoreStore,repo),()=>false,s=>{},(n,total)=>progress=n);
            Check(restored.Installed==1 && restored.Changed && restoreStore.Entries.Count==0 && repo.ReadAll(s=>{}).Count==1 && progress==1,"Inactive profile restore did not preserve disabled state");
            int installs=0,archives=0;f.Disabled=false;var duplicate=ProfileRestoreEngine.Run(prepared,new List<ManagedFont>{f},p=>{},p=>archives++,p=>installs++,p=>null,font=>{},()=>false,s=>{},(n,total)=>{});Check(duplicate.Skipped==1 && installs==0 && archives==1,"Existing font state was changed on restore");
            var other=Read(changed);var clash=ProfileRestoreEngine.Run(prepared,new List<ManagedFont>{other},p=>{},p=>{},p=>installs++,p=>null,font=>{},()=>false,s=>{},(n,total)=>{});Check(clash.Skipped==1 && installs==0,"Active version conflict was not preserved");
            bool cancel=false;string oldArchiveHash=Engine.Hash(backup);failed=false;try {ProfileArchive.Create(backup,new List<ManagedFont>{f},personal,emptyVersions,()=>cancel,(n,total)=>cancel=true);}catch(OperationCanceledException) {failed=true;}Check(failed && Engine.Hash(backup)==oldArchiveHash,"Cancelled environment backup replaced existing ZIP");
            string payload=prepared.Files.Values.Single();using(var stream=new FileStream(payload,FileMode.Append)) stream.WriteByte(0);failed=false;try {ProfileRestoreEngine.Run(prepared,new List<ManagedFont>(),p=>{},p=>{},p=>installs++,p=>null,font=>{},()=>false,s=>{},(n,total)=>{});}catch(InvalidDataException) {failed=true;}Check(failed && installs==0,"Payload modified after preparation was installed");
            report.Add("PASS: full inactive environment restore through isolated installer/activation backend; existing state and active-version conflict retained; last-step cancellation preserves ZIP; post-prepare tampering blocked");

            string invalidArchive=Path.Combine(root,"bad-hash-archive.zip");failed=false;try {using(var zip=ZipFile.Open(invalidArchive,ZipArchiveMode.Create)) VerifiedArchive.AddFont(zip,"font.ttf",source,new string('0',64),()=>false);}catch(IOException) {failed=true;}Check(failed,"Archive payload hash mismatch was accepted");
            for(int mode=0;mode<3;mode++) {
                var paginator=new ReportPaginator(new List<ManagedFont>{Read(source)},mode);int pages=0;bool more;
                do {using(var bitmap=new Bitmap(480,620)) using(var graphics=Graphics.FromImage(bitmap)) {graphics.Clear(Color.White);more=paginator.Render(graphics,new Rectangle(20,20,440,580));if(pages==0) bitmap.Save(Path.Combine(root,"print-mode-"+mode+".png"));}pages++;Check(pages<200,"Print pagination never completed");}while(more);
                paginator.Reset();using(var bitmap=new Bitmap(480,620)) using(var graphics=Graphics.FromImage(bitmap)) paginator.Render(graphics,new Rectangle(20,20,440,580));
            }
            report.Add("PASS: backup payload verified while streaming; catalog/waterfall/character printing paginates to completion and resets without sending any printer job");
            for(int mode=0;mode<3;mode++) {
                var size=new Windows.Foundation.Size(793.7,1122.5);var area=new Windows.Foundation.Rect(0,0,size.Width,size.Height);
                var native=NativeReportLayout.Paginate(new List<ManagedFont>{Read(source)},mode,size,area);
                Check(native.Count>0 && (mode!=2 || native.Count>1),"Native report page count incorrect");
                string first=Path.Combine(root,"native-print-"+mode+".png"),again=Path.Combine(root,"native-print-repeat-"+mode+".png");
                using(var bitmap=NativeReportLayout.Render(native[0])) {Check(bitmap.Width>bitmap.Height/2,"Native paper dimensions incorrect");bitmap.Save(first);}
                using(var bitmap=NativeReportLayout.Render(native[0])) bitmap.Save(again);
                Check(Engine.Hash(first)==Engine.Hash(again),"Native preview rendering changed saved pagination position");
                var landscape=NativeReportLayout.Paginate(new List<ManagedFont>{Read(source)},mode,new Windows.Foundation.Size(1122.5,793.7),new Windows.Foundation.Rect(0,0,1122.5,793.7));
                using(var bitmap=NativeReportLayout.Render(landscape[0])) Check(bitmap.Width>bitmap.Height,"Native landscape orientation ignored");
            }
            report.Add("PASS: native Windows print layout covers all report modes, multiple character pages, repeatable page snapshots and landscape rendering without a print job");
        }
        class Store : IFontInstallStore,IFontActivationStore {
            public string DirectoryPath {get;private set;}public Dictionary<string,FontRegistration> Entries=new Dictionary<string,FontRegistration>();public int Loads,Unloads,Reads,FailWrite;public bool FailLoad,FailDelete;public Action<int> ReadHook;
            public Store(string path) {DirectoryPath=path;}
            static FontRegistration Copy(FontRegistration e) {return e==null?null:new FontRegistration {Name=e.Name,Data=e.Data,Kind=e.Kind};}
            public FontRegistration Read(string name) {Reads++;if(ReadHook!=null) ReadHook(Reads);return Entries.ContainsKey(name)?Copy(Entries[name]):null;}
            public void Write(FontRegistration entry) {int failure=FailWrite;FailWrite=0;if(failure==1) throw new IOException("write before");Entries[entry.Name]=Copy(entry);if(failure==2) throw new IOException("write after");}
            public void Delete(string name) {if(FailDelete) throw new IOException("delete failure");Entries.Remove(name);}
            public void Load(string path) {if(FailLoad) throw new IOException("load failure");Loads++;}
            public void Unload(string path) {Unloads++;}
            public void Reload(string path) {Load(path);}
            public FontRegistration ReadEntry(string name) {return Read(name);}
            public void RestoreEntry(FontRegistration e) {Write(e);}
            public void DeleteEntry(FontRegistration e) {Delete(e.Name);}
            public bool DeleteOwnedFile(string path) {throw new Exception("Unexpected physical deletion");}
            public ManagedFont Current(string path) {var entries=Entries.Values.Where(e=>e.Data==path).Select(Copy).ToList();if(entries.Count==0) return null;var f=ReliabilityTests.Read(path);f.UserEntries=entries;return f;}
        }
    }
}
