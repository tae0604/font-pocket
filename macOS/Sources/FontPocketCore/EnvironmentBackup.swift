import Foundation

public struct ProfileFont:Codable,Sendable{public let filename:String;public let hash:String;public let enabled:Bool}
public struct EnvironmentManifest:Codable,Sendable{public let schema:Int;public let personal:PersonalDocument;public let fonts:[ProfileFont]}
public enum EnvironmentBackup{
    public static func create(repository:FontRepository,personal:PersonalLibrary,vault:VersionVault,destination:URL) async throws{
        let fm=FileManager.default,stage=fm.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        let folder=stage.appendingPathComponent("FontPocketEnvironment"),files=folder.appendingPathComponent("Fonts"),versions=folder.appendingPathComponent("Versions")
        let temp=destination.deletingLastPathComponent().appendingPathComponent(".FontPocket-\(UUID().uuidString).zip")
        try fm.createDirectory(at:files,withIntermediateDirectories:true);try fm.createDirectory(at:versions,withIntermediateDirectories:true)
        defer{try? fm.removeItem(at:stage);try? fm.removeItem(at:temp)}
        var records:[ProfileFont]=[]
        for (record,file) in try await repository.snapshot(){try Task.checkCancellation();try fm.copyItem(at:file,to:files.appendingPathComponent(record.filename));records.append(ProfileFont(filename:record.filename,hash:record.key,enabled:record.enabled))}
        var seen=Set<String>()
        for version in try await vault.read() where seen.insert(version.filename).inserted{try Task.checkCancellation();try fm.copyItem(at:try await vault.file(version),to:versions.appendingPathComponent(version.filename))}
        let manifest=EnvironmentManifest(schema:1,personal:try await personal.read(),fonts:records)
        try JSONEncoder().encode(manifest).write(to:folder.appendingPathComponent("manifest.json"))
        _ = try await ProcessRunner.run("/usr/bin/ditto",["-c","-k","--keepParent",folder.path,temp.path]);try Task.checkCancellation()
        if fm.fileExists(atPath:destination.path){_ = try fm.replaceItemAt(destination,withItemAt:temp)}else{try fm.moveItem(at:temp,to:destination)}
    }
    public static func restore(_ archive:URL,repository:FontRepository,personal:PersonalLibrary,vault:VersionVault) async throws{
        let fm=FileManager.default,stage=fm.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try ArchivePolicy.validate(Data(contentsOf:archive,options:.mappedIfSafe));try fm.createDirectory(at:stage,withIntermediateDirectories:true)
        defer{try? fm.removeItem(at:stage)}
        _ = try await ProcessRunner.run("/usr/bin/unzip",["-qq","-n",archive.path,"-d",stage.path])
        let folder=stage.appendingPathComponent("FontPocketEnvironment"),manifest=try JSONDecoder().decode(EnvironmentManifest.self,from:Data(contentsOf:folder.appendingPathComponent("manifest.json")))
        guard manifest.schema==1 else{throw PocketError("Unsupported environment backup version")}
        for record in manifest.fonts{
            try Task.checkCancellation()
            guard ArchivePolicy.safePath(record.filename),!record.filename.contains("/"),record.hash.count==64 else{throw PocketError("Invalid backup record")}
            let file=folder.appendingPathComponent("Fonts").appendingPathComponent(record.filename)
            guard try FontReader.hash(file)==record.hash else{throw PocketError("Backup integrity check failed")};_ = try FontReader.faces(file)
        }
        let versionFiles=(try? fm.contentsOfDirectory(at:folder.appendingPathComponent("Versions"),includingPropertiesForKeys:nil)) ?? []
        for file in versionFiles{try Task.checkCancellation();_ = try FontReader.faces(file);guard file.deletingPathExtension().lastPathComponent == (try FontReader.hash(file)) else{throw PocketError("Version backup integrity check failed")}}
        for record in manifest.fonts{
            try Task.checkCancellation();let file=folder.appendingPathComponent("Fonts").appendingPathComponent(record.filename)
            _ = try await vault.capture(file)
            if try await repository.install(file),!record.enabled{try await repository.setEnabled(record.hash,enabled:false)}
        }
        for file in versionFiles{try Task.checkCancellation();_ = try await vault.capture(file)}
        var current=try await personal.read()
        current.annotations.merge(manifest.personal.annotations){old,_ in old}
        let ids=Set(current.collections.map(\.id));current.collections+=manifest.personal.collections.filter{!ids.contains($0.id)}
        try await personal.save(current)
    }
}
