using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FontPocket {
    public class ProfileRestoreResult {public int Installed,Skipped,Failed;public bool Changed;}
    public static class ProfileRestoreEngine {
        public static ProfileRestoreResult Run(PreparedProfile profile,List<ManagedFont> existing,Action<string> validate,Action<string> archive,Action<string> install,Func<string,ManagedFont> findInstalled,Action<ManagedFont> disable,Func<bool> cancel,Action<string> log,Action<int,int> progress) {
            var result=new ProfileRestoreResult();
            foreach(var entry in profile.Manifest.Fonts) {if(cancel()) throw new OperationCanceledException();string path=profile.Files[entry.Hash];if(Engine.Hash(path)!=entry.Hash) throw new InvalidDataException("복원 준비 후 폰트 파일이 변경됐습니다.");validate(path);}
            var hashes=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var identities=new HashSet<string>(existing.Where(f=>!f.Disabled && f.Identity!=null).Select(f=>f.Identity));
            foreach(var font in existing) try {hashes.Add(Engine.Hash(font.Path));}catch(Exception ex) {log(font.Name+" · 기존 파일 확인 제외: "+ex.Message);}
            for(int i=0;i<profile.Manifest.Fonts.Count && !cancel();i++) {
                var entry=profile.Manifest.Fonts[i];string path=profile.Files[entry.Hash];bool installed=false;
                try {
                    if(Engine.Hash(path)!=entry.Hash) throw new InvalidDataException("복원 중 폰트 파일이 변경됐습니다.");archive(path);
                    if(entry.ArchivedOnly || hashes.Contains(entry.Hash)) {result.Skipped++;continue;}
                    var candidate=new ManagedFont {Path=path};FontLibrary.ReadMetadata(candidate);if(candidate.Metadata==null) throw new InvalidDataException("글꼴 정보를 읽을 수 없습니다.");
                    if(identities.Contains(candidate.Identity)) {result.Skipped++;log(entry.Name+" · 다른 활성 버전이 있어 보관만 했습니다.");continue;}
                    install(path);installed=true;result.Changed=true;hashes.Add(entry.Hash);identities.Add(candidate.Identity);var current=findInstalled(path);if(current==null) throw new IOException("설치 결과를 확인할 수 없습니다.");
                    if(entry.Disabled) {disable(current);identities.Remove(candidate.Identity);}result.Installed++;
                }catch(Exception ex) {result.Failed++;log(entry.Name+(installed?" · 설치 후 상태 복원 실패: ":" · 복원 실패: ")+ex.Message);}
                finally {progress(i+1,profile.Manifest.Fonts.Count);}
            }return result;
        }
    }
}
