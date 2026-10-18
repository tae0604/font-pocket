using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FontPocket {
    public class FontAxis { public string Tag; public double Min, Default, Max; }
    public class CharacterCoverage {
        public string Name, Status; public int Present, Total;
    }
    public class OpenTypeDetails {
        public Dictionary<int, string> Names = new Dictionary<int, string>();
        public SortedDictionary<int, int> Characters = new SortedDictionary<int, int>();
        public List<FontAxis> Axes = new List<FontAxis>();
        public List<string> Checks = new List<string>();
        public int GlyphCount, FaceCount, StyleFlags; public bool CmapSupported;
        internal int FilterScripts;
        public string Name(int id) { string value; return Names.TryGetValue(id, out value) ? value : "정보 없음"; }
        public string Family { get {return Names.ContainsKey(16)?Name(16):Name(1);} }
        public string Subfamily { get {return Names.ContainsKey(17)?Name(17):Name(2);} }
        public List<CharacterCoverage> Coverage() {
            var result = new List<CharacterCoverage>();
            AddRange(result, "한글 완성형", 0xac00, 0xd7a3);
            AddRange(result, "한글 자모", 0x1100, 0x11ff);
            AddRange(result, "한글 호환 자모", 0x3131, 0x318e);
            AddRange(result, "옛한글 자모 확장 A", 0xa960, 0xa97f);
            AddRange(result, "옛한글 자모 확장 B", 0xd7b0, 0xd7ff);
            AddSet(result,"영어 · 기본 알파벳", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz");
            AddSet(result,"프랑스어 · 기본 문자 집합", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyzÀÂÆÇÉÈÊËÎÏÔŒÙÛÜŸàâæçéèêëîïôœùûüÿ");
            AddSet(result,"독일어 · 기본 문자 집합", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyzÄÖÜäöüßẞ");
            AddSet(result,"스페인어 · 기본 문자 집합", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyzÁÉÍÑÓÚÜáéíñóúü¿¡");
            AddRange(result,"히라가나 범위",0x3041,0x3096); AddRange(result,"가타카나 범위",0x30a1,0x30fa);
            AddRange(result,"한자 기본 범위",0x4e00,0x9fff); AddRange(result,"그리스 문자 범위",0x391,0x3c9);
            AddRange(result,"키릴 문자 범위",0x410,0x44f); AddRange(result,"아랍 문자 범위",0x621,0x64a);
            AddRange(result,"히브리 문자 범위",0x5d0,0x5ea); AddRange(result,"데바나가리 · 힌디어 등",0x904,0x939);
            AddRange(result,"벵골 문자 범위",0x985,0x9b9); AddRange(result,"타밀 문자 범위",0xb85,0xbb9);
            AddRange(result,"태국 문자 범위",0xe01,0xe3a); AddRange(result,"라오 문자 범위",0xe81,0xedd);
            AddRange(result,"아르메니아 문자 범위",0x531,0x556); AddRange(result,"조지아 문자 범위",0x10d0,0x10fa);
            AddRange(result,"크메르 문자 범위",0x1780,0x17b3); AddRange(result,"에티오피아 문자 범위",0x1200,0x137f);
            AddRange(result,"숫자",0x30,0x39); return result;
        }
        void AddRange(List<CharacterCoverage> rows,string name,int first,int last) { Add(rows,name,Characters.Keys.Count(c=>c>=first && c<=last),last-first+1); }
        void AddSet(List<CharacterCoverage> rows,string name,string set) { var chars=set.Distinct().ToArray(); Add(rows,name,chars.Count(c=>Characters.ContainsKey(c)),chars.Length); }
        void Add(List<CharacterCoverage> rows,string name,int n,int all) { rows.Add(new CharacterCoverage { Name=name,Present=n,Total=all,Status=!CmapSupported?"판정 불가":n==0?"없음":n==all?"문자 집합 충족":"일부 포함" }); }
        public static OpenTypeDetails Read(string path,int faceIndex,bool checksums) {
            using(var s=File.OpenRead(path)) return new Reader(s).Read(faceIndex,checksums);
        }
        internal static List<FilterFace> ReadFilter(ManagedFont font,bool scripts) {
            var result=new List<FilterFace>();
            using(var stream=File.OpenRead(font.Path)) {
                var reader=new Reader(stream);reader.FilterOnly=true;reader.IncludeScripts=scripts;
                for(int i=0;i<font.Metadata.Faces.Count;i++) {
                    var face=font.Metadata.Faces[i];var info=reader.Read(i,false);
                    var axis=info.Axes.FirstOrDefault(a=>a.Tag=="wght");
                    result.Add(new FilterFace {Variable=face.Variable,MinimumWeight=axis==null?face.Weight:axis.Min,MaximumWeight=axis==null?face.Weight:axis.Max,Scripts=info.FilterScripts});
                }
            }
            return result;
        }
        class Table { public long Offset, Length; public uint Checksum; }
        class Reader {
            Stream stream;readonly long length;
            internal bool FilterOnly,IncludeScripts;
            byte[] window=new byte[1048576];long windowStart=-1;int windowCount;
            byte ByteAt(long p) {
                if(p<0||p>=length)throw new InvalidDataException("폰트 데이터 범위를 벗어났습니다.");
                if(p<windowStart||p>=windowStart+windowCount){windowStart=p;stream.Position=p;windowCount=stream.Read(window,0,window.Length);if(windowCount==0)throw new EndOfStreamException();}
                return window[(int)(p-windowStart)];
            }
            public Reader(Stream s) { stream=s;length=s.Length; }
            byte[] Bytes(long p,int n) { if(p<0 || n<0 || p>length-n) throw new InvalidDataException("폰트 데이터 범위를 벗어났습니다."); stream.Position=p; var b=new byte[n]; int read=0,k; while(read<n && (k=stream.Read(b,read,n-read))>0) read+=k; if(read!=n) throw new EndOfStreamException(); return b; }
            int U16(long p) { return (ByteAt(p)<<8)|ByteAt(p+1); }
            uint U32(long p) { return ((uint)ByteAt(p)<<24)|((uint)ByteAt(p+1)<<16)|((uint)ByteAt(p+2)<<8)|ByteAt(p+3); }
            string Tag(long p) { return new string(new[]{(char)ByteAt(p),(char)ByteAt(p+1),(char)ByteAt(p+2),(char)ByteAt(p+3)}); }
            double Fixed(long p) { return unchecked((int)U32(p))/65536.0; }
            void Within(Table t,long p,long n) { if(p<t.Offset || n<0 || p>t.Offset+t.Length-n) throw new InvalidDataException("손상된 폰트 테이블입니다."); }
            public OpenTypeDetails Read(int index,bool checksums) {
                var d=new OpenTypeDetails(); long face=0; d.FaceCount=1;
                if(Tag(0)=="ttcf") { uint n=U32(8); if(n==0 || n>1024) throw new InvalidDataException("잘못된 TTC입니다."); d.FaceCount=(int)n; if(index<0 || index>=n) throw new ArgumentOutOfRangeException("index"); face=U32(12+index*4); }
                else if(index!=0) throw new ArgumentOutOfRangeException("index");
                uint magic=U32(face); if(magic!=0x10000 && magic!=0x4f54544f && magic!=0x74727565) throw new InvalidDataException("지원하지 않는 OpenType 헤더입니다.");
                int count=U16(face+4); if(count==0 || count>4096) throw new InvalidDataException("잘못된 테이블 개수입니다.");
                var tables=new Dictionary<string,Table>();
                for(int i=0;i<count;i++) { long p=face+12+i*16; var t=new Table { Offset=U32(p+8),Length=U32(p+12),Checksum=U32(p+4) }; if(t.Offset>length-t.Length) throw new InvalidDataException("테이블이 파일 밖을 가리킵니다."); string tag=Tag(p); if(tables.ContainsKey(tag)) throw new InvalidDataException("중복 테이블: "+tag); tables[tag]=t; }
                if(!tables.ContainsKey("name") || !tables.ContainsKey("maxp") || !tables.ContainsKey("cmap")) throw new InvalidDataException("필수 name / maxp / cmap 테이블이 없습니다.");
                if(!FilterOnly)ReadNames(tables["name"],d); var maxp=tables["maxp"]; Within(maxp,maxp.Offset,6); d.GlyphCount=U16(maxp.Offset+4); if(d.GlyphCount==0) throw new InvalidDataException("폰트에 글리프가 없습니다.");
                if(!FilterOnly||IncludeScripts)ReadCmap(tables["cmap"],d); Table fvar;
                Table styleTable;if(tables.TryGetValue("OS/2",out styleTable) && styleTable.Length>=64) {int flags=U16(styleTable.Offset+62);d.StyleFlags=((flags&32)!=0?1:0)+((flags&513)!=0?2:0);}else if(tables.TryGetValue("head",out styleTable) && styleTable.Length>=46) d.StyleFlags=U16(styleTable.Offset+44)&3;
                if(tables.TryGetValue("fvar",out fvar)) {
                    Within(fvar,fvar.Offset,16); long p=fvar.Offset+U16(fvar.Offset+4); int axes=U16(fvar.Offset+8),size=U16(fvar.Offset+10);
                    if(axes>64 || size<20) throw new InvalidDataException("가변 축 정보가 잘못됐습니다."); Within(fvar,p,(long)axes*size);
                    for(int i=0;i<axes;i++,p+=size) { var axis=new FontAxis { Tag=Tag(p),Min=Fixed(p+4),Default=Fixed(p+8),Max=Fixed(p+12) }; if(axis.Min>axis.Default || axis.Default>axis.Max) throw new InvalidDataException("가변 축 범위가 잘못됐습니다."); d.Axes.Add(axis); }
                }
                d.Checks.Add("통과 · 헤더와 "+count+"개 테이블의 파일 범위");
                d.Checks.Add(d.CmapSupported?"통과 · Unicode 문자 매핑 "+d.Characters.Count+"개":"주의 · 지원하지 않는 cmap 형식: 문자 수 / 언어 판정 불가");
                if(d.Characters.Values.Any(g=>g>=d.GlyphCount)) throw new InvalidDataException("문자 매핑이 글리프 범위를 벗어납니다.");
                if(checksums) {
                    foreach(var pair in tables) { uint sum=0; var t=pair.Value;
                        for(long off=0;off<t.Length;off+=65536) { int n=(int)Math.Min(65536,t.Length-off); var b=Bytes(t.Offset+off,n); for(int j=0;j<n;j+=4) { uint word=0; for(int k=0;k<4;k++) word=(word<<8)|(j+k<n ? b[j+k] : 0u); if(pair.Key=="head" && off+j==8) word=0; sum=unchecked(sum+word); } }
                        if(sum!=t.Checksum) d.Checks.Add("주의 · "+pair.Key+" 테이블 체크섬 불일치");
                    }
                    if(!d.Checks.Any(c=>c.Contains("체크섬 불일치"))) d.Checks.Add("통과 · 테이블 체크섬");
                }
                return d;
            }
            void ReadNames(Table t,OpenTypeDetails d) {
                Within(t,t.Offset,6); int n=U16(t.Offset+2),storage=U16(t.Offset+4); Within(t,t.Offset+6,n*12L); var scores=new Dictionary<int,int>();
                for(int i=0;i<n;i++) { long p=t.Offset+6+i*12; int platform=U16(p),encoding=U16(p+2),lang=U16(p+4),id=U16(p+6),size=U16(p+8),start=U16(p+10);
                    if(platform!=0 && !(platform==3 && (encoding==0 || encoding==1 || encoding==10))) continue;
                    long str=t.Offset+storage+start; Within(t,str,size); if(size%2!=0) continue;
                    int score=(lang==0x412?200:lang==0x409?100:lang==0?60:10)+(platform==3?3:0);
                    if(scores.ContainsKey(id) && scores[id]>=score) continue;
                    string value=Encoding.BigEndianUnicode.GetString(Bytes(str,size)).Trim('\0',' '); if(value.Length==0) continue; d.Names[id]=value; scores[id]=score;
                }
            }
            void Add(OpenTypeDetails d,int c,long glyph) { if(glyph>0 && glyph<=65535 && c>=0 && c<=0x10ffff && !(c>=0xd800 && c<=0xdfff)) { if(FilterOnly) { if(glyph>=d.GlyphCount)throw new InvalidDataException("문자 매핑이 글리프 범위를 벗어납니다.");d.FilterScripts|=FilterFace.ScriptMask(c); } else d.Characters[c]=(int)glyph; } }
            void ReadCmap(Table t,OpenTypeDetails d) {
                Within(t,t.Offset,4); int n=U16(t.Offset+2); Within(t,t.Offset+4,n*8L);
                // Choose one Unicode subtable, preferring full-repertoire mappings.
                long chosen=-1; int best=-1,format=0;
                for(int i=0;i<n;i++) { long p=t.Offset+4+i*8; int platform=U16(p),enc=U16(p+2); if(platform!=0 && !(platform==3 && (enc==1 || enc==10))) continue; long sub=t.Offset+U32(p+4); Within(t,sub,2); int f=U16(sub); int rank=f==12?100:f==13?90:f==4?80:f==6?60:f==0?40:-1; if(rank>best) { chosen=sub; best=rank; format=f; } }
                if(chosen<0) return;
                long length=(format==12 || format==13)?(long)U32(chosen+4):U16(chosen+2); Within(t,chosen,length); var subtable=new Table { Offset=chosen,Length=length }; d.CmapSupported=true;
                if(format==12 || format==13) { Within(subtable,chosen,16); uint groups=U32(chosen+12); if(groups>0x110000) throw new InvalidDataException("문자 그룹이 너무 많습니다."); Within(subtable,chosen+16,groups*12L); long previous=-1;
                    for(uint i=0;i<groups;i++) { long p=chosen+16+i*12; uint start=U32(p),end=U32(p+4),glyph=U32(p+8); if(start>end || end>0x10ffff || start<=previous) throw new InvalidDataException("문자 그룹 범위가 잘못됐습니다."); previous=end; for(int c=(int)start;c<=end;c++) { long g=format==13?glyph:(long)glyph+c-start; if(g>65535) throw new InvalidDataException("글리프 번호가 잘못됐습니다."); Add(d,c,g); } }
                } else if(format==4) { Within(subtable,chosen,16); int segments=U16(chosen+6)/2; if(segments<1) throw new InvalidDataException("문자 구간이 없습니다."); long ends=chosen+14,starts=ends+segments*2+2,deltas=starts+segments*2,ranges=deltas+segments*2; Within(subtable,ranges,segments*2L); int previous=-1;
                    for(int i=0;i<segments;i++) { int start=U16(starts+i*2),end=U16(ends+i*2),delta=U16(deltas+i*2),range=U16(ranges+i*2); if(start>end || start<=previous) throw new InvalidDataException("문자 구간이 겹칩니다."); previous=end;
                        for(int c=start;c<=end && c<65535;c++) { int g; if(range==0) g=(c+delta)&65535; else { long p=ranges+i*2+range+(c-start)*2; Within(subtable,p,2); g=U16(p); if(g!=0) g=(g+delta)&65535; } Add(d,c,g); }
                    }
                } else if(format==6) { Within(subtable,chosen,10); int first=U16(chosen+6),count=U16(chosen+8); Within(subtable,chosen+10,count*2L); for(int i=0;i<count;i++) Add(d,first+i,U16(chosen+10+i*2)); }
                else { Within(subtable,chosen+6,256); var b=Bytes(chosen+6,256); for(int i=0;i<256;i++) Add(d,i,b[i]); }
            }
        }
    }
}
