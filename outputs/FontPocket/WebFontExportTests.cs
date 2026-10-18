using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace FontPocket {
    static class WebFontExportTests {
        public static void Run(string root,List<string> report) {
            string font=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"arial.ttf");var info=OpenTypeDetails.Read(font,0,false);
            string target=Path.Combine(root,"web-font-original.zip");WebFontPackage.Create(font,info,null,target);
            using(var zip=ZipFile.OpenRead(target)){
                var entry=zip.GetEntry("fonts/font.ttf");if(entry==null)throw new Exception("Original web font missing");
                using(var stream=entry.Open())using(var bytes=new MemoryStream()){stream.CopyTo(bytes);if(!bytes.ToArray().SequenceEqual(File.ReadAllBytes(font)))throw new Exception("Original web font changed");}
                foreach(string name in new[]{"style.css","index.html","FONT-LICENSE.txt","README.txt"})if(zip.GetEntry(name)==null)throw new Exception("Web package file missing: "+name);
                using(var reader=new StreamReader(zip.GetEntry("style.css").Open())){string css=reader.ReadToEnd();if(!css.Contains("fonts/font.ttf")||!css.Contains("font-weight: 400"))throw new Exception("Incorrect CSS font source or weight");}
            }
            string before=Engine.Hash(target);bool rejected=false;try{WebFontPackage.Create(font,info,new byte[64],target);}catch(InvalidDataException){rejected=true;}
            if(!rejected||before!=Engine.Hash(target))throw new Exception("Invalid WOFF2 replaced existing export");
            report.Add("PASS: original web font ZIP preserves font bytes and includes usable CSS/HTML/license metadata; invalid WOFF2 cannot overwrite an existing export");
        }
    }
}
