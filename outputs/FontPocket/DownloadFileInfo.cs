using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace FontPocket {
    public partial class DownloadLink {
        bool IsPackage {get{return Regex.IsMatch(FileName+" "+Title,@"\.zip\b|\bZIP\b",RegexOptions.IgnoreCase)||Selection.LinkFormats(this)==3;}}
        bool IsFontFile {get{return Regex.IsMatch(FileName,@"\.(ttf|otf|ttc|otc|sfnt|woff2?)$",RegexOptions.IgnoreCase);}}
        string WeightDescription {
            get {
                var match=Regex.Match(FileName,@"(?:^|[-_])(?<weight>ExtraLight|UltraLight|ExtraBold|UltraBold|SemiBold|DemiBold|Regular|Medium|Light|Thin|Black|Heavy|Bold)(?:Italic|Oblique)?(?:\.|\[|[-_]|$)",RegexOptions.IgnoreCase);
                if(!match.Success)return "";
                switch(match.Groups["weight"].Value.ToLowerInvariant()) {
                    case "thin":return "매우 얇게(Thin)";case "extralight":case "ultralight":return "아주 얇게(ExtraLight)";
                    case "light":return "얇게(Light)";case "regular":return "보통(Regular)";case "medium":return "중간(Medium)";
                    case "semibold":case "demibold":return "약간 굵게(SemiBold)";case "bold":return "굵게(Bold)";
                    case "extrabold":case "ultrabold":return "아주 굵게(ExtraBold)";default:return "매우 굵게(Black / Heavy)";
                }
            }
        }
        string StyleDescription {
            get {
                if(IsPackage)return "폰트 묶음 — 여러 스타일이 들어 있을 수 있습니다.";
                if(!IsFontFile)return "다운로드 후 확인할 수 있습니다.";
                if(Regex.IsMatch(FileName,@"italic|oblique",RegexOptions.IgnoreCase))return "기울임체(Italic) — 강조나 인용에 사용하는 기울어진 글자입니다.";
                return "기울임 없는 서체(Upright) — 일반적인 본문과 제목에 사용하는 글자입니다.";
            }
        }
        public string ChoiceLabel {
            get {
                string name=!String.IsNullOrWhiteSpace(FamilyName)?FamilyName:String.IsNullOrWhiteSpace(Title)||Regex.IsMatch(Title,@"^(TTF|OTF|TTC|Download|다운로드)$",RegexOptions.IgnoreCase)?FileName:Title;
                string style=IsPackage?"폰트 묶음":!IsFontFile?"스타일 확인 전":Regex.IsMatch(FileName,@"italic|oblique",RegexOptions.IgnoreCase)?"기울임체(Italic)":"기울임 없는 서체(Upright)";
                if(IsFontFile&&Variant!=FontVariant.Variable&&WeightDescription!="")style+=" · "+WeightDescription;
                string kind=Variant==FontVariant.Variable?"가변(Variable)":Variant==FontVariant.Static?"정적(Static)":"종류 확인 전";
                return name+" · "+UiLanguage.Text(style+" · "+kind+" · "+FormatDescription);
            }
        }
        string FormatDescription {
            get {var match=Regex.Match(FileName,@"\.(woff2?|otc|sfnt)$",RegexOptions.IgnoreCase);if(match.Success)return match.Groups[1].Value.ToUpperInvariant()+UiLanguage.Text(" → Windows 설치용 변환");int formats=Selection.LinkFormats(this);return formats==1?"TTF":formats==2?"OTF":formats==4?"TTC":formats==3?"TTF + OTF":"ZIP / 폰트";}
        }
        public string ChoiceDetails {
            get {
                var lines=new List<string>();
                lines.Add("스타일: "+StyleDescription);
                if(IsFontFile&&Variant!=FontVariant.Variable&&WeightDescription!="")lines.Add("굵기: "+WeightDescription);
                lines.Add("종류: "+(Variant==FontVariant.Variable?"가변(Variable) — 한 파일에서 굵기 등 지원되는 속성을 조절할 수 있습니다.":Variant==FontVariant.Static?"정적(Static) — 파일마다 정해진 스타일을 사용합니다.":"다운로드 후 확인할 수 있습니다."));
                var axes=Regex.Match(FileName,@"\[(?<axes>[^\]]+)\]");
                if(axes.Success){var labels=axes.Groups["axes"].Value.Split(',').Select(a=>{switch(a.Trim()){case "wght":return "wght (굵기)";case "opsz":return "opsz (사용 크기에 맞춘 글자 형태)";case "GRAD":return "GRAD (획 농도)";case "wdth":return "wdth (글자 너비)";case "slnt":return "slnt (기울기)";case "ital":return "ital (기울임 전환)";default:return a.Trim();}});lines.Add("조절 축: "+String.Join(" · ",labels));}
                lines.Add("파일 형식: "+FormatDescription+" · "+(IsPackage?"압축을 풀어 내부 폰트를 선택합니다.":IsFontFile?"Windows에 설치할 수 있는 폰트 파일입니다.":"내용물은 다운로드 후 확인할 수 있습니다."));
                lines.Add("용량: "+(SizeBytes>=0?(SizeBytes/1024.0).ToString("N0")+" KB":"서버에서 제공하지 않았습니다."));
                lines.Add("파일명: "+FileName);
                lines.Add("스타일·조절 축은 파일명 기준 안내입니다. 정확한 굵기 범위·지원 문자는 다운로드 후 상세 정보에서 확인하세요.");
                return UiLanguage.Text(String.Join("\r\n",lines.Take(lines.Count-2)))+"\r\n"+UiLanguage.Text("파일명: ")+FileName+"\r\n"+UiLanguage.Text(lines.Last());
            }
        }
    }
}
