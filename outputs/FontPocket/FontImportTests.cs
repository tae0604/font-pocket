using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FontPocket {
    static class FontImportTests {
        static uint Read(byte[] b,int p){return ((uint)b[p]<<24)|((uint)b[p+1]<<16)|((uint)b[p+2]<<8)|b[p+3];}
        static void Put(byte[] b,int p,uint n){for(int i=3;i>=0;i--){b[p+i]=(byte)n;n>>=8;}}
        static void Check(bool value,string text){if(!value)throw new Exception(text);}
        static void Reject(byte[] bytes){try{FontImport.DecodeWoff(bytes);}catch(InvalidDataException){return;}throw new Exception("Malformed WOFF accepted");}
        internal static void Run(string root,List<string> report) {
            foreach(string ext in new[]{"ttf","otf","ttc","otc","sfnt","woff","woff2"})Check(FontImport.Supported("font."+ext),"Missing import format "+ext);
            Check(!FontImport.Supported("font.exe"),"Non-font import accepted");
            byte[] sfnt=File.ReadAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"arial.ttf"));int count=sfnt[4]*256+sfnt[5],position=44+count*20;
            var tables=new List<byte[]>();for(int i=0;i<count;i++){int p=12+i*16;var table=new byte[(int)Read(sfnt,p+12)];Buffer.BlockCopy(sfnt,(int)Read(sfnt,p+8),table,0,table.Length);tables.Add(table);position+=(table.Length+3)&~3;}
            var woff=new byte[position];Put(woff,0,0x774f4646);Put(woff,4,Read(sfnt,0));Put(woff,8,(uint)woff.Length);woff[12]=(byte)(count>>8);woff[13]=(byte)count;Put(woff,16,(uint)(12+count*16+tables.Sum(t=>(t.Length+3)&~3)));position=44+count*20;
            for(int i=0;i<count;i++){int src=12+i*16,dst=44+i*20;Put(woff,dst,Read(sfnt,src));Put(woff,dst+4,(uint)position);Put(woff,dst+8,(uint)tables[i].Length);Put(woff,dst+12,(uint)tables[i].Length);Put(woff,dst+16,Read(sfnt,src+4));Buffer.BlockCopy(tables[i],0,woff,position,tables[i].Length);position+=(tables[i].Length+3)&~3;}
            string decoded=Path.Combine(root,"decoded-woff.ttf");File.WriteAllBytes(decoded,FontImport.DecodeWoff(woff));Check(Engine.Validate(decoded)==FontMetadata.Read(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"arial.ttf")).Display,"WOFF restoration changed font identity");
            var broken=(byte[])woff.Clone();Put(broken,48,UInt32.MaxValue);Reject(broken);broken=(byte[])woff.Clone();broken[60]^=1;Reject(broken);broken=(byte[])woff.Clone();Put(broken,16,UInt32.MaxValue);Reject(broken);
            foreach(string ext in new[]{"woff","woff2"}) {bool fetched=false;var plan=NoonnuDownload.Discover("https://fonts.example/font."+ext,()=>false,s=>{},delegate(string url,out string final){fetched=true;final=url;throw new Exception("Font file fetched as HTML");});Check(!fetched&&plan.Files.Count==1,"Direct webfont URL not recognized");}
            report.Add("PASS: seven import formats; WOFF restored and validated by Windows without installation; invalid table bounds/checksum/output size rejected; explicit WOFF/WOFF2 URLs bypass HTML discovery");
        }
    }
}
