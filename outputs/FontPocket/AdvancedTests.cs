using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace FontPocket {
    static class AdvancedTests {
        static void Check(bool pass,string message) { if(!pass) throw new Exception(message); }
        static void U16(byte[] b,int p,int n) { b[p]=(byte)(n>>8);b[p+1]=(byte)n; }
        static void U32(byte[] b,int p,uint n) { for(int i=0;i<4;i++) b[p+i]=(byte)(n>>(24-i*8)); }
        static byte[] Fixture(bool full) {
            string[] strings={"Pocket Test","Regular","Version 1.0"};int[] ids={1,2,5};var name=new List<byte>(new byte[42]);var header=name.ToArray();U16(header,2,3);U16(header,4,42);name=new List<byte>(header);
            int offset=0;for(int i=0;i<3;i++) { var text=Encoding.BigEndianUnicode.GetBytes(strings[i]);name.AddRange(text); }var names=name.ToArray();for(int i=0;i<3;i++) { int p=6+i*12;U16(names,p,3);U16(names,p+2,1);U16(names,p+4,0x409);U16(names,p+6,ids[i]);int n=Encoding.BigEndianUnicode.GetByteCount(strings[i]);U16(names,p+8,n);U16(names,p+10,offset);offset+=n; }
            byte[] mapping;
            if(full) { mapping=new byte[52];U16(mapping,0,12);U32(mapping,4,52);U32(mapping,12,3);uint[] codes={65,0xac00,0x1f600};for(int i=0;i<3;i++) { U32(mapping,16+i*12,codes[i]);U32(mapping,20+i*12,codes[i]);U32(mapping,24+i*12,(uint)(i+1)); } }
            else { mapping=new byte[32];U16(mapping,0,4);U16(mapping,2,32);U16(mapping,6,4);U16(mapping,14,66);U16(mapping,16,65535);U16(mapping,20,65);U16(mapping,22,65535);U16(mapping,24,65536+1-65);U16(mapping,26,1); }
            var cmap=new byte[12+mapping.Length];U16(cmap,2,1);U16(cmap,4,3);U16(cmap,6,full?10:1);U32(cmap,8,12);Array.Copy(mapping,0,cmap,12,mapping.Length);var maxp=new byte[6];U32(maxp,0,0x5000);U16(maxp,4,4);
            var tables=new Dictionary<string,byte[]> { {"name",names},{"maxp",maxp},{"cmap",cmap} };var data=new List<byte>(new byte[60]);var bytes=data.ToArray();U32(bytes,0,0x10000);U16(bytes,4,3);data=new List<byte>(bytes);int tableIndex=0;
            foreach(var pair in tables) { int at=data.Count;data.AddRange(pair.Value);while(data.Count%4!=0) data.Add(0);bytes=data.ToArray();int p=12+tableIndex*16;Array.Copy(Encoding.ASCII.GetBytes(pair.Key),0,bytes,p,4);U32(bytes,p+8,(uint)at);U32(bytes,p+12,(uint)pair.Value.Length);data=new List<byte>(bytes);tableIndex++; }return data.ToArray();
        }
        public static void Run(string root,List<string> report) {
            string full=Path.Combine(root,"unicode-fixture.ttf"),bmp=Path.Combine(root,"bmp-fixture.ttf");File.WriteAllBytes(full,Fixture(true));File.WriteAllBytes(bmp,Fixture(false));
            var detail=OpenTypeDetails.Read(full,0,false);Check(detail.CmapSupported && detail.Characters.Count==3 && detail.Characters[0x1f600]==3 && detail.GlyphCount==4,"Full Unicode cmap or glyph counts incorrect");Check(detail.Coverage().First(c=>c.Name=="한글 완성형").Present==1,"Hangul coverage incorrect");Check(OpenTypeDetails.Read(bmp,0,false).Characters.Keys.SequenceEqual(new[]{65,66}),"BMP delta mapping incorrect");
            var bad=Fixture(true);U32(bad,20,0xfffffff0);string malformed=Path.Combine(root,"bad-cmap.ttf");File.WriteAllBytes(malformed,bad);bool rejected=false;try { OpenTypeDetails.Read(malformed,0,false); }catch(InvalidDataException) {rejected=true;}Check(rejected,"Out-of-bounds OpenType accepted");report.Add("PASS: synthetic BMP/full-Unicode cmap, supplementary characters, distinct character/glyph counts, coverage and out-of-bounds rejection");
            string original=Path.Combine(root,"version-original.ttf"),newer=Path.Combine(root,"version-new.ttf");File.Copy(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"arial.ttf"),original,true);File.Copy(original,newer,true);using(var s=new FileStream(newer,FileMode.Append)) s.WriteByte(0);
            var old=Font(original);var vault=new VersionLibrary(Path.Combine(root,"versions-test"));var chosen=vault.Capture(newer);vault.Capture(original);Check(vault.ReadAll().Count==2,"Same version with different bytes was lost");
            var store=new MemoryStore(old);var repo=new ActivationRepository(Path.Combine(root,"version-disabled"),Path.Combine(root,"version-user-fonts"));rejected=false;
            try { FontVersionSwitch.Use(chosen,vault,new List<ManagedFont>{old},store,repo,p=>{throw new IOException("injected installation failure");},p=>{}); }catch(IOException) { rejected=true; }
            Check(rejected && store.Entries.Count==1 && store.Entries.Values.Single().Data==original && repo.ReadAll(s=>{}).Count==0 && File.Exists(original),"Failed version install did not restore old registration");
            FontVersionSwitch.Use(chosen,vault,new List<ManagedFont>{old},store,repo,p=>store.Entries["new"] = new FontRegistration {Name="new",Data=p,Kind=RegistryValueKind.String},p=>{});
            Check(store.Entries.Count==1 && store.Entries.ContainsKey("new") && repo.ReadAll(s=>{}).Count==1 && File.Exists(original),"Version change did not retain old bytes/state");
            var protectedFont=Font(original);protectedFont.Machine=true;store=new MemoryStore(protectedFont);rejected=false;try { FontVersionSwitch.Use(chosen,vault,new List<ManagedFont>{protectedFont},store,repo,p=>{throw new Exception("must not install");},p=>{}); }catch(InvalidOperationException) {rejected=true;}Check(rejected && store.Entries.Count==1,"Protected version overwritten");report.Add("PASS: version archive retains identical-version/different-byte files; switch preserves old bytes; failed installation rolls back; system versions protected (mock backend)");
            var library=new PersonalLibrary(Path.Combine(root,"personal-test.json"));var note=library.Note(old);note.Tags="본문, 업무";note.Memo="테스트 메모";note.Favorite=true;var collection=new FontCollection {Name="업무용"};library.Data.Collections.Add(collection);note.Collections.Add(collection.Id);library.Data.Collections.Add(new FontCollection {Name="자동",Smart=true,Query="본문;TTF;활성"});library.Save();var loaded=new PersonalLibrary(library.FilePath);Check(loaded.Note(old).Memo=="테스트 메모" && loaded.InCollection(old,collection.Id) && loaded.InCollection(old,"favorite") && loaded.MatchesRule(old,"본문;TTF;활성") && !loaded.MatchesRule(old,"비활성"),"Notes/tags/collections did not persist or filter");
            string backup=Path.Combine(root,"environment.zip");ProfileArchive.Create(backup,new List<ManagedFont>{old},loaded.Data,vault,()=>false,(n,total)=>{});var prepared=ProfileArchive.Prepare(backup,Path.Combine(root,"environment-restored"),()=>false);Check(prepared.Manifest.Fonts.Count==2 && prepared.Manifest.Fonts.Count(e=>e.ArchivedOnly)==1 && prepared.Manifest.Personal.Notes.Single().Memo==note.Memo && prepared.Files.Values.All(File.Exists),"Environment archive roundtrip lost data");
            string corrupt=Path.Combine(root,"environment-corrupt.zip");File.Copy(backup,corrupt,true);using(var zip=ZipFile.Open(corrupt,ZipArchiveMode.Update)) { var e=zip.Entries.First(x=>x.FullName.StartsWith("fonts/"));string name=e.FullName;e.Delete();using(var writer=new StreamWriter(zip.CreateEntry(name).Open())) writer.Write("tampered"); }rejected=false;try { ProfileArchive.Prepare(corrupt,Path.Combine(root,"corrupt-output"),()=>false); }catch(InvalidDataException) {rejected=true;}Check(rejected,"Tampered environment font accepted");
            report.Add("PASS: notes/tags/favorites/manual and smart collections persist; environment backup retains font bytes and archived versions; tampered payload rejected before installation");
            string variable=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"bahnschrift.ttf");if(File.Exists(variable)) {var info=OpenTypeDetails.Read(variable,0,false);if(info.Axes.Count>0) {string html=VariablePreview.BuildHtml(variable,info);Check(html.Contains("fontVariationSettings") && html.Contains("data:font/ttf;base64,") && info.Axes.All(a=>html.Contains("data-tag='"+a.Tag+"'")),"Variable axis preview omitted axes or embedded font");report.Add("PASS: actual variable font axes generate self-contained slider preview without installing or launching a browser");}}
        }
        static ManagedFont Font(string path) { var font=new ManagedFont {Path=path};FontLibrary.ReadMetadata(font);font.UserEntries.Add(new FontRegistration {Name="old",Data=path,Kind=RegistryValueKind.String});return font; }
        class MemoryStore:IFontActivationStore {
            public Dictionary<string,FontRegistration> Entries=new Dictionary<string,FontRegistration>();public MemoryStore(ManagedFont font) { foreach(var e in font.UserEntries) Entries[e.Name]=e; }
            public ManagedFont Current(string path) { var entries=Entries.Values.Where(e=>e.Data==path).ToList();return entries.Count==0?null:new ManagedFont {Path=path,UserEntries=entries}; }
            public FontRegistration ReadEntry(string name) { return Entries.ContainsKey(name)?Entries[name]:null; }
            public void Unload(string path) {}public void Reload(string path) {}public void DeleteEntry(FontRegistration e) {Entries.Remove(e.Name);}public void RestoreEntry(FontRegistration e) {Entries[e.Name]=e;}public bool DeleteOwnedFile(string path) {throw new Exception("Unexpected physical deletion");}
        }
    }
}
