using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace FontPocket {
    public interface IFontInstallStore {
        string DirectoryPath {get;}
        FontRegistration Read(string name);
        void Write(FontRegistration registration);
        void Delete(string name);
        void Load(string path);
        void Unload(string path);
    }
    public class WindowsFontInstallStore : IFontInstallStore {
        [DllImport("gdi32.dll",CharSet=CharSet.Unicode)] static extern int AddFontResourceEx(string file,uint flags,IntPtr reserved);
        [DllImport("gdi32.dll",CharSet=CharSet.Unicode)] static extern bool RemoveFontResourceEx(string file,uint flags,IntPtr reserved);
        public string DirectoryPath {get {return Engine.UserDir;}}
        public FontRegistration Read(string name) {using(var key=Registry.CurrentUser.OpenSubKey(Engine.RegistryPath)) {if(key==null) return null;object value=key.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames);if(value==null) return null;if(!(value is string)) throw new IOException("같은 등록 이름에 다른 종류의 값이 있습니다.");return new FontRegistration {Name=name,Data=(string)value,Kind=key.GetValueKind(name)};}}
        public void Write(FontRegistration r) {using(var key=Registry.CurrentUser.CreateSubKey(Engine.RegistryPath)) key.SetValue(r.Name,r.Data,r.Kind);}
        public void Delete(string name) {using(var key=Registry.CurrentUser.OpenSubKey(Engine.RegistryPath,true)) if(key!=null) key.DeleteValue(name,false);}
        public void Load(string path) {if(AddFontResourceEx(path,0,IntPtr.Zero)==0) throw new IOException("Windows 폰트 등록에 실패했습니다.");}
        public void Unload(string path) {RemoveFontResourceEx(path,0,IntPtr.Zero);}
    }
    public static class FontInstaller {
        public static void Install(FontItem item,HashSet<string> hashes,IFontInstallStore store,Func<string,string> validate) {
            string family=validate(item.Path);item.Hash=Engine.Hash(item.Path);if(hashes.Contains(item.Hash)) return;
            string destination=Path.Combine(store.DirectoryPath,"FontPocket-"+item.Hash+Path.GetExtension(item.Path).ToLowerInvariant());
            string name=family+" [FontPocket "+item.Hash+"]";var previous=store.Read(name);
            if(previous!=null && !String.Equals(previous.Data,destination,StringComparison.OrdinalIgnoreCase)) throw new IOException("같은 등록 이름이 다른 폰트에 사용 중입니다.");
            bool created=false,loaded=false,writeAttempted=false;
            try {
                Directory.CreateDirectory(store.DirectoryPath);
                if(!File.Exists(destination)) {File.Copy(item.Path,destination,false);created=true;}
                if(Engine.Hash(destination)!=item.Hash || validate(destination)!=family) throw new IOException("설치용 사본의 내용이 원본과 다릅니다.");
                var current=store.Read(name);if((previous==null)!=(current==null) || (previous!=null && (current.Data!=previous.Data || current.Kind!=previous.Kind))) throw new IOException("설치 중 등록 정보가 변경됐습니다.");
                store.Load(destination);loaded=true;writeAttempted=true;store.Write(new FontRegistration {Name=name,Data=destination,Kind=RegistryValueKind.String});hashes.Add(item.Hash);
            } catch(Exception error) {
                var errors=new List<string>();bool registrationSafe=true;
                if(writeAttempted) try {var current=store.Read(name);if(current!=null && current.Data==destination) {if(previous==null) store.Delete(name);else store.Write(previous);}else if(current!=null) registrationSafe=false;}catch(Exception rollback) {registrationSafe=false;errors.Add(rollback.Message);}
                if(loaded) try {store.Unload(destination);}catch(Exception rollback) {errors.Add(rollback.Message);}
                if(created && registrationSafe) try {var current=store.Read(name);if(current==null || current.Data!=destination) File.Delete(destination);}catch(IOException) {}catch(UnauthorizedAccessException) {}
                if(!registrationSafe || errors.Count>0) throw new IOException("설치 실패. 복구하지 못한 등록의 파일은 보존했습니다. "+String.Join("; ",errors.ToArray()),error);
                throw;
            }
        }
    }
}
