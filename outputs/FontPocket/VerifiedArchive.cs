using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace FontPocket {
    static class VerifiedArchive {
        public static void AddFont(ZipArchive archive,string entryName,string source,string expectedHash,Func<bool> cancel) {
            using(var input=File.OpenRead(source)) using(var hash=SHA256.Create()) using(var output=archive.CreateEntry(entryName,CompressionLevel.Optimal).Open()) {
                if(input.Length>512L*1024*1024) throw new IOException("백업할 폰트가 512MB를 넘습니다.");
                var buffer=new byte[81920];int read;while((read=input.Read(buffer,0,buffer.Length))>0) {if(cancel()) throw new OperationCanceledException();hash.TransformBlock(buffer,0,read,buffer,0);output.Write(buffer,0,read);}
                hash.TransformFinalBlock(new byte[0],0,0);string actual=BitConverter.ToString(hash.Hash).Replace("-","").ToLowerInvariant();if(actual!=expectedHash) throw new IOException("백업 중 폰트가 변경돼 저장을 취소했습니다.");
            }
        }
    }
}
