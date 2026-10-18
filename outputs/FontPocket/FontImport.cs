using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FontPocket {
    static class FontImport {
        internal const int Limit=128*1024*1024;
        internal static bool Supported(string path) {string ext=Path.GetExtension(path).ToLowerInvariant();return Engine.IsFont(path)||ext==".woff"||ext==".woff2"||ext==".otc"||ext==".sfnt";}
        internal static string NativeExtension(byte[] b) {
            if(b.Length<12)throw new InvalidDataException("폰트 헤더가 올바르지 않습니다.");
            string tag=Encoding.ASCII.GetString(b,0,4);if(tag=="OTTO")return ".otf";if(tag=="ttcf")return ".ttc";
            if(tag=="true"||(b[0]==0&&b[1]==1&&b[2]==0&&b[3]==0))return ".ttf";
            throw new InvalidDataException("Windows 설치용 폰트로 변환할 수 없습니다.");
        }
        internal static async Task<string> PrepareAsync(Form owner,string path,string folder) {
            if(Engine.IsFont(path))return path;
            if(!Supported(path))throw new NotSupportedException("지원하지 않는 폰트 형식입니다.");
            var file=new FileInfo(path);if(file.Length<12||file.Length>Limit)throw new InvalidDataException("변환할 폰트는 128MB 이하만 지원합니다.");
            byte[] data;
            if(file.Extension.Equals(".woff2",StringComparison.OrdinalIgnoreCase))data=await Woff2Converter.DecompressAsync(owner,path);
            else data=await Task.Run(()=>file.Extension.Equals(".woff",StringComparison.OrdinalIgnoreCase)?DecodeWoff(File.ReadAllBytes(path)):File.ReadAllBytes(path));
            string extension=NativeExtension(data);Directory.CreateDirectory(folder);string target=Path.Combine(folder,Guid.NewGuid().ToString("N")+extension);
            try {await Task.Run(delegate {File.WriteAllBytes(target,data);FontMetadata.Read(target);});return target;}
            catch {if(File.Exists(target))File.Delete(target);throw;}
        }
        static int U16(byte[] b,int p){Range(b,p,2);return b[p]*256+b[p+1];}
        static uint U32(byte[] b,int p){Range(b,p,4);return ((uint)b[p]<<24)|((uint)b[p+1]<<16)|((uint)b[p+2]<<8)|b[p+3];}
        static void Range(byte[] b,int p,int n){if(p<0||n<0||p>b.Length-n)throw new InvalidDataException("WOFF 테이블 범위가 잘못됐습니다.");}
        static void Put(byte[] b,int p,uint n){b[p]=(byte)(n>>24);b[p+1]=(byte)(n>>16);b[p+2]=(byte)(n>>8);b[p+3]=(byte)n;}
        static void Put16(byte[] b,int p,int n){b[p]=(byte)(n>>8);b[p+1]=(byte)n;}
        internal static byte[] DecodeWoff(byte[] input) {
            Range(input,0,44);if(U32(input,0)!=0x774f4646||U32(input,8)!=input.Length||U16(input,14)!=0)throw new InvalidDataException("WOFF 헤더가 올바르지 않습니다.");
            int n=U16(input,12);uint size=U32(input,16),flavor=U32(input,4);
            if(n<1||n>4096||size<12+n*16||size>Limit||flavor!=0x10000&&flavor!=0x4f54544f&&flavor!=0x74727565)throw new InvalidDataException("WOFF 출력 범위가 올바르지 않습니다.");
            Range(input,44,n*20);var output=new byte[(int)size];Put(output,0,flavor);Put16(output,4,n);int power=1,selector=0;while(power*2<=n){power*=2;selector++;}Put16(output,6,power*16);Put16(output,8,selector);Put16(output,10,n*16-power*16);
            int target=12+n*16;uint previous=0;
            for(int i=0;i<n;i++) {
                int p=44+i*20;uint tag=U32(input,p),offset=U32(input,p+4),compressed=U32(input,p+8),length=U32(input,p+12),checksum=U32(input,p+16);
                if(i>0&&tag<=previous||offset<44+n*20||offset%4!=0||compressed>length||length>Limit||offset>input.Length||compressed>input.Length-offset)throw new InvalidDataException("WOFF 테이블이 올바르지 않습니다.");previous=tag;
                Range(output,target,(int)length);byte[] table=new byte[(int)length];
                if(compressed==length)Buffer.BlockCopy(input,(int)offset,table,0,table.Length);
                else {
                    int start=(int)offset,count=(int)compressed;Range(input,start,count);if(count<6||(input[start]&15)!=8||((input[start]*256+input[start+1])%31)!=0||(input[start+1]&32)!=0)throw new InvalidDataException("WOFF 압축 헤더가 올바르지 않습니다.");
                    using(var source=new MemoryStream(input,start+2,count-6))using(var z=new DeflateStream(source,CompressionMode.Decompress)) {int read=0,k;while(read<table.Length&&(k=z.Read(table,read,table.Length-read))>0)read+=k;if(read!=table.Length||z.ReadByte()!=-1)throw new InvalidDataException("WOFF 압축 크기가 일치하지 않습니다.");}
                    uint a=1,b=0;foreach(byte v in table){a=(a+v)%65521;b=(b+a)%65521;}if((b<<16|a)!=U32(input,start+count-4))throw new InvalidDataException("WOFF 압축 데이터가 손상됐습니다.");
                }
                uint sum=0;for(int j=0;j<table.Length;j+=4){uint word=0;for(int k=0;k<4;k++)word=(word<<8)|(j+k<table.Length?table[j+k]:0u);if(tag==0x68656164&&j==8)word=0;sum=unchecked(sum+word);}if(sum!=checksum)throw new InvalidDataException("WOFF 테이블 체크섬이 일치하지 않습니다.");
                int d=12+i*16;Put(output,d,tag);Put(output,d+4,checksum);Put(output,d+8,(uint)target);Put(output,d+12,length);Buffer.BlockCopy(table,0,output,target,table.Length);target=checked(target+((table.Length+3)&~3));
            }
            if(target!=output.Length)throw new InvalidDataException("WOFF 전체 크기가 일치하지 않습니다.");return output;
        }
    }
    public partial class MainWindow {
        readonly System.Collections.Generic.Dictionary<string,string> importedSources=new System.Collections.Generic.Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        async Task AddImportedFontAsync(string path,string name,DownloadLink origin) {
            string prepared=await FontImport.PrepareAsync(this,path,Path.Combine(tempRoot,"Converted"));if(cancelled)return;
            var added=await Task.Run(()=>AddOne(prepared,name,origin));if(added!=null)importedSources[Path.GetFullPath(path)]=added.Path;
        }
    }
}
