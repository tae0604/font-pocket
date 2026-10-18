using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FontPocket {
    sealed class FilterFace {
        public bool Variable;public double MinimumWeight,MaximumWeight;public int Scripts;
        public static int ScriptMask(int c){int result=0;if(c>=0xac00&&c<=0xd7a3||c>=0x1100&&c<=0x11ff)result|=1;if(c>=0x41&&c<=0x5a||c>=0x61&&c<=0x7a)result|=2;if(c>=0x3040&&c<=0x30ff)result|=4;if(c>=0x3400&&c<=0x9fff)result|=8;if(c>=0x400&&c<=0x52f)result|=16;if(c>=0x370&&c<=0x3ff)result|=32;if(c>=0x600&&c<=0x6ff)result|=64;return result;}
        public static int ScriptMask(IEnumerable<int> chars){int result=0;foreach(int c in chars)result|=ScriptMask(c);return result;}

    }
    static class ManagerFontFilter {
        internal static List<FilterFace> Read(ManagedFont font) {return Read(font,true);}
        internal static List<FilterFace> Read(ManagedFont font,bool scripts) {
            if(!scripts&&font.Metadata!=null&&!font.Metadata.Variable)return font.Metadata.Faces.Select(f=>new FilterFace {Variable=false,MinimumWeight=f.Weight,MaximumWeight=f.Weight}).ToList();
            return font.Metadata==null?new List<FilterFace>():OpenTypeDetails.ReadFilter(font,scripts);
        }
        internal static bool Matches(ManagedFont font,List<FilterFace> indexed,int format,int variant,int weight,int script) {
            if(format>0&&Path.GetExtension(font.Path).ToLowerInvariant()!=new[]{"",".ttf",".otf",".ttc"}[format])return false;
            if(font.Metadata==null)return variant==0&&weight==0&&script==0;
            if(indexed==null&&script!=0)return false;
            var faces=indexed??font.Metadata.Faces.Select(f=>new FilterFace {Variable=f.Variable,MinimumWeight=f.Weight,MaximumWeight=f.Weight}).ToList();
            return faces.Any(f=>(variant==0||(variant==1?f.Variable:!f.Variable))&&(weight==0||f.MinimumWeight<=weight&&f.MaximumWeight>=weight)&&(script==0||(f.Scripts&script)!=0));
        }
    }
    public partial class MainWindow {
        readonly ComboBox managerWeight=new PocketComboBox(),managerLanguage=new PocketComboBox(),managerVariant=new PocketComboBox(),managerFormat=new PocketComboBox();
        Dictionary<string,List<FilterFace>> managerFilterIndex=new Dictionary<string,List<FilterFace>>(StringComparer.OrdinalIgnoreCase);bool changingManagerFilters;
        Control CreateAdvancedManagerFilters() {
            var row=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty,Name="ManagerAdvancedFilters"};
            ConfigureFilter(row,managerWeight,"굵기",new[]{"모든 굵기","100 · Thin","200 · ExtraLight","300 · Light","400 · Regular","500 · Medium","600 · SemiBold","700 · Bold","800 · ExtraBold","900 · Black"},135);
            ConfigureFilter(row,managerLanguage,"언어",new[]{"모든 언어","한국어(한글)","영어(라틴)","일본어(가나)","중국어(한자)","키릴 문자","그리스 문자","아랍 문자"},130);
            ConfigureFilter(row,managerVariant,"종류",new[]{"모든 종류","가변(Variable)","정적(Static)"},130);
            ConfigureFilter(row,managerFormat,"형식",new[]{"모든 형식","TTF","OTF","TTC"},110);
            managerWeight.Name="ManagerWeight";managerLanguage.Name="ManagerLanguage";managerVariant.Name="ManagerVariant";managerFormat.Name="ManagerFormat";
            var clear=MakeButton("초기화",65,delegate {changingManagerFilters=true;foreach(var combo in new[]{managerWeight,managerLanguage,managerVariant,managerFormat})combo.SelectedIndex=0;changingManagerFilters=false;RefreshManagerList();},false);clear.Height=32;row.Controls.Add(clear);return row;
        }
        void ConfigureFilter(FlowLayoutPanel row,ComboBox combo,string label,string[] choices,int width){if(UiLanguage.Code=="en")width=combo==managerWeight?120:combo==managerLanguage?125:combo==managerVariant?120:115;row.Controls.Add(new Label {Text=label,AutoSize=true,Margin=new Padding(0,7,5,0)});combo.DropDownStyle=ComboBoxStyle.DropDownList;combo.Width=width;combo.Margin=new Padding(0,0,10,0);combo.Items.AddRange(choices);combo.SelectedIndex=0;row.Controls.Add(combo);controls.Add(combo);combo.SelectedIndexChanged+=async delegate {if(!changingManagerFilters&&!busy)await ApplyManagerFiltersAsync();};}
        bool NeedsManagerIndex {get{return managerLanguage.SelectedIndex>0||managerWeight.SelectedIndex>0;}}
        async Task BuildManagerFilterIndex() {
            bool scripts=managerLanguage.SelectedIndex>0;var cache=new ManagerFilterCache(System.IO.Path.Combine(DataPaths.Root,"filter-index.bin"));
            var missing=managedFonts.ToArray();
            var progressReport=new Progress<int>(n=>{if(!IsDisposed&&busy)status.Text=UiLanguage.Text("필터용 폰트 정보를 읽는 중…")+" "+n+" / "+missing.Length;});
            var added=await Task.Run(delegate {var result=new Dictionary<string,List<FilterFace>>(StringComparer.OrdinalIgnoreCase);for(int i=0;i<missing.Length;i++){if(cancelled)break;try{result[missing[i].Path]=cache.Get(missing[i],scripts);}catch(Exception){result[missing[i].Path]=new List<FilterFace>();}if(i%8==0||i==missing.Length-1)((IProgress<int>)progressReport).Report(i+1);}cache.Save();return result;});
            foreach(var pair in added)managerFilterIndex[pair.Key]=pair.Value;
        }
        async Task ApplyManagerFiltersAsync() {
            if(NeedsManagerIndex){SetBusy(true);try{await BuildManagerFilterIndex();if(cancelled){changingManagerFilters=true;managerLanguage.SelectedIndex=managerWeight.SelectedIndex=0;changingManagerFilters=false;}}catch(Exception ex){Log(ex.Message);}finally{if(!IsDisposed)SetBusy(false);}}
            if(!IsDisposed){RefreshManagerList();status.Text=UiLanguage.Text("필터 적용 완료 · 언어는 문자 포함 기준입니다.");}
        }
        bool MatchesAdvancedManagerFilters(ManagedFont font){List<FilterFace> entry;managerFilterIndex.TryGetValue(font.Path,out entry);int language=Math.Max(0,managerLanguage.SelectedIndex);return ManagerFontFilter.Matches(font,entry,Math.Max(0,managerFormat.SelectedIndex),Math.Max(0,managerVariant.SelectedIndex),Math.Max(0,managerWeight.SelectedIndex)*100,language==0?0:1<<(language-1));}
        void RememberFontDates(){if(personal==null)return;bool changed=false;foreach(var font in managedFonts){var note=personal.Note(font);if(String.IsNullOrEmpty(note.AddedUtc)&&font.AddedUtc>new DateTime(1970,1,1)){note.AddedUtc=font.AddedUtc.ToString("o",CultureInfo.InvariantCulture);changed=true;}}if(changed)try{personal.Save();}catch(Exception ex){Log(UiLanguage.Text("추가 날짜 저장 실패: ")+ex.Message);}}
        DateTime AddedDate(ManagedFont font){DateTime result;if(personal!=null&&DateTime.TryParse(personal.Note(font).AddedUtc,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out result))return result.ToLocalTime();return font.AddedUtc>new DateTime(1970,1,1)?font.AddedUtc.ToLocalTime():DateTime.MinValue;}
    }
}
