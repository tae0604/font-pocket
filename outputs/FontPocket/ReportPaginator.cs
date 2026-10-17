using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace FontPocket {
    public class ReportPaginator {
        List<ManagedFont> fonts;int mode,fontIndex,characterIndex,sizeIndex,textOffset;List<int> characters;
        public ReportPaginator(List<ManagedFont> fonts,int mode) {this.fonts=fonts;this.mode=mode;}
        public void Reset() {fontIndex=characterIndex=sizeIndex=textOffset=0;characters=null;}
        public ReportPaginator Copy() {return (ReportPaginator)MemberwiseClone();}
        public bool Render(Graphics graphics,Rectangle bounds) {
            if(bounds.Width<100 || bounds.Height<120) throw new ArgumentException("인쇄 영역이 너무 작습니다. 용지나 여백을 조절하세요.");
            int y=bounds.Top;using(var header=new Font("맑은 고딕",10)) using(var small=new Font("맑은 고딕",7)) {
                while(fontIndex<fonts.Count) {
                    if(y>bounds.Bottom-100) return true;var f=fonts[fontIndex];
                    graphics.DrawString(f.Name+" · "+f.Version,header,Brushes.Black,new RectangleF(bounds.Left,y,bounds.Width,26));y+=34;
                    if(mode==2) {
                        if(characters==null) characters=OpenTypeDetails.Read(f.Path,0,false).Characters.Keys.Where(c=>c>=32 && !(c>=0x7f && c<=0x9f)).ToList();
                        using(var preview=new PreviewFont(f.Path,18)) {
                            int columns=Math.Max(1,bounds.Width/52);
                            while(characterIndex<characters.Count && y+52<=bounds.Bottom) {for(int c=0;c<columns && characterIndex<characters.Count;c++,characterIndex++) {int code=characters[characterIndex];graphics.DrawString(Char.ConvertFromUtf32(code),preview.Font,Brushes.Black,new RectangleF(bounds.Left+c*52,y,50,34));graphics.DrawString("U+"+code.ToString("X4"),small,Brushes.Gray,bounds.Left+c*52,y+35);}y+=52;}
                        }
                        if(characterIndex<characters.Count) return true;characters=null;characterIndex=0;
                    } else {
                        int[] sizes=mode==1?new[]{12,18,24,36}:new[]{18};
                        while(sizeIndex<sizes.Length) using(var preview=new PreviewFont(f.Path,sizes[sizeIndex])) using(var format=new StringFormat {FormatFlags=StringFormatFlags.LineLimit}) {
                            string remaining=FontSamples.Default.Substring(textOffset);int fitted,lines;
                            var available=new SizeF(bounds.Width,Math.Max(0,bounds.Bottom-y));var measured=graphics.MeasureString(remaining,preview.Font,available,format,out fitted,out lines);
                            if(fitted<=0) {if(y>bounds.Top+34) return true;throw new ArgumentException("견본을 인쇄할 공간이 부족합니다. 여백을 줄이세요.");}
                            graphics.DrawString(remaining,preview.Font,Brushes.Black,new RectangleF(bounds.Left,y,available.Width,available.Height),format);textOffset+=fitted;y+=(int)Math.Ceiling(measured.Height)+16;
                            if(textOffset<FontSamples.Default.Length) return true;textOffset=0;sizeIndex++;
                        }sizeIndex=0;
                    }
                    fontIndex++;y+=16;
                }
            }return false;
        }
    }
}
