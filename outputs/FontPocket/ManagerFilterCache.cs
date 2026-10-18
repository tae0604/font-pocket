using System;
using System.Collections.Generic;
using System.IO;

namespace FontPocket {
    // File metadata invalidates only changed fonts; no font contents are hashed on startup.
    sealed class ManagerFilterCache {
        sealed class Entry {internal long Length,Ticks;internal bool Scripts;internal List<FilterFace> Faces;}
        readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>(StringComparer.OrdinalIgnoreCase);
        readonly string path;bool dirty;
        internal ManagerFilterCache(string file) {
            path=file;
            try {using(var r=new BinaryReader(File.OpenRead(path))) {
                if(r.ReadInt32()!=1)return;int count=r.ReadInt32();if(count<0||count>100000)return;
                for(int i=0;i<count;i++) {string key=r.ReadString();var e=new Entry {Length=r.ReadInt64(),Ticks=r.ReadInt64(),Scripts=r.ReadBoolean(),Faces=new List<FilterFace>()};
                    int faces=r.ReadInt32();if(faces<0||faces>1024)throw new InvalidDataException();
                    for(int j=0;j<faces;j++)e.Faces.Add(new FilterFace {Variable=r.ReadBoolean(),MinimumWeight=r.ReadDouble(),MaximumWeight=r.ReadDouble(),Scripts=r.ReadInt32()});entries[key]=e;
                }
            }}catch(IOException){entries.Clear();}catch(UnauthorizedAccessException){entries.Clear();}catch(ArgumentException){entries.Clear();}
        }
        internal List<FilterFace> Get(ManagedFont font,bool scripts) {
            if(font.Metadata==null)return new List<FilterFace>();
            var file=new FileInfo(font.Path);string key=file.FullName;Entry e;
            if(entries.TryGetValue(key,out e)&&e.Length==file.Length&&e.Ticks==file.LastWriteTimeUtc.Ticks&&(!scripts||e.Scripts))return e.Faces;
            var faces=ManagerFontFilter.Read(font,scripts);
            entries[key]=new Entry {Length=file.Length,Ticks=file.LastWriteTimeUtc.Ticks,Scripts=scripts,Faces=faces};dirty=true;return faces;
        }
        internal void Save() {
            if(!dirty)return;string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {Directory.CreateDirectory(Path.GetDirectoryName(path));using(var w=new BinaryWriter(File.Create(temp))) {
                w.Write(1);w.Write(entries.Count);foreach(var pair in entries) {var e=pair.Value;w.Write(pair.Key);w.Write(e.Length);w.Write(e.Ticks);w.Write(e.Scripts);w.Write(e.Faces.Count);
                    foreach(var f in e.Faces){w.Write(f.Variable);w.Write(f.MinimumWeight);w.Write(f.MaximumWeight);w.Write(f.Scripts);}
                }
            }if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);dirty=false;
            }catch(IOException){}catch(UnauthorizedAccessException){}finally{try{if(File.Exists(temp))File.Delete(temp);}catch(IOException){}catch(UnauthorizedAccessException){}}
        }
    }
}
