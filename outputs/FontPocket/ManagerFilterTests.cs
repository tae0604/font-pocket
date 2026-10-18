using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FontPocket {
    static class ManagerFilterTests {
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        public static void Run(string root,List<string> report) {
            var font=new ManagedFont {Path="demo.ttf",Name="Demo",Metadata=new FontMetadata {Faces=new List<Face>{new Face{Family="Demo",Style="Variable",Variable=true,Weight=400}}}};
            var faces=new List<FilterFace>{new FilterFace{Variable=true,MinimumWeight=100,MaximumWeight=900,Scripts=1|2}};
            Check(ManagerFontFilter.Matches(font,faces,1,1,700,1),"Variable range or Korean filter failed");
            Check(!ManagerFontFilter.Matches(font,faces,2,1,700,1)&&!ManagerFontFilter.Matches(font,faces,1,2,700,1)&&!ManagerFontFilter.Matches(font,faces,1,1,1000,1)&&!ManagerFontFilter.Matches(font,faces,1,1,700,4),"Combined filter accepted a mismatching file");
            Check(FilterFace.ScriptMask(new[]{0xac00,0x41,0x3042,0x4e00,0x410,0x3b1,0x627})==127,"Script detection failed");
            var mixed=new List<FilterFace>{new FilterFace{MinimumWeight=400,MaximumWeight=400,Scripts=1},new FilterFace{MinimumWeight=700,MaximumWeight=700,Scripts=2}};
            Check(!ManagerFontFilter.Matches(font,mixed,1,0,700,1),"Weight and language were matched from different TTC faces");
            var library=new PersonalLibrary(Path.Combine(root,"date-added-test.json"));string date="2026-10-18T06:00:00.0000000Z";library.Note(font).AddedUtc=date;library.Save();Check(new PersonalLibrary(library.FilePath).Note(font).AddedUtc==date,"Added date did not survive reload");
            report.Add("PASS: combined format/type/weight/script filters use the same face and variable ranges; script coverage and added-date persistence verified");
            string source=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"arial.ttf"),copy=Path.Combine(root,"filter-cache-font.ttf"),cachePath=Path.Combine(root,"filter-cache.bin");
            File.Copy(source,copy,true);var actual=new ManagedFont {Path=copy,Metadata=FontMetadata.Read(copy)};
            var cache=new ManagerFilterCache(cachePath);Check(cache.Get(actual,false)[0].Scripts==0,"Weight-only indexing read character coverage");cache.Save();
            cache=new ManagerFilterCache(cachePath);var indexed=cache.Get(actual,true);Check((indexed[0].Scripts&2)!=0,"Language request did not upgrade weight-only cache");cache.Save();
            cache=new ManagerFilterCache(cachePath);var first=cache.Get(actual,true);Check(Object.ReferenceEquals(first,cache.Get(actual,true)),"Unchanged file was reindexed");
            File.SetLastWriteTimeUtc(copy,File.GetLastWriteTimeUtc(copy).AddSeconds(5));Check(!Object.ReferenceEquals(first,cache.Get(actual,true)),"Changed font reused stale cache");
            File.WriteAllBytes(cachePath,new byte[]{1,0,0});Check((new ManagerFilterCache(cachePath).Get(actual,true)[0].Scripts&2)!=0,"Corrupt cache prevented indexing");
            report.Add("PASS: filter cache survives reload, upgrades weight-only entries, invalidates changed files and recovers from truncation");
        }
    }
}
