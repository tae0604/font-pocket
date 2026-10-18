using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FontPocket {
    static class FontDiscoveryTests {
        static void Require(bool value,string message){if(!value)throw new Exception(message);}
        static DownloadPlan Resolve(string input,Dictionary<string,string> pages,List<string> visits) {
            return NoonnuDownload.Discover(input,()=>false,s=>{},delegate(string url,out string final){final=url;visits.Add(url);string body;if(!pages.TryGetValue(url,out body))throw new IOException("Unexpected request: "+url);return body;});
        }
        public static void Run(List<string> report) {
            var calls=new List<string>();
            var google=Resolve("https://fonts.google.com/specimen/Noto+Sans+KR",new Dictionary<string,string>{
                {"https://api.github.com/repos/google/fonts/contents/ofl/notosanskr?ref=main","[{\"type\":\"file\",\"name\":\"NotoSansKR[wght].ttf\",\"download_url\":\"https://raw.githubusercontent.com/google/fonts/main/ofl/notosanskr/NotoSansKR%5Bwght%5D.ttf\"},{\"type\":\"file\",\"name\":\"OFL.txt\",\"download_url\":\"https://raw.githubusercontent.com/google/fonts/main/ofl/notosanskr/OFL.txt\"},{\"type\":\"file\",\"name\":\"Other.ttf\",\"download_url\":\"https://other.example/Other.ttf\"}]"}
            },calls);
            Require(google.Files.Count==1&&google.Files[0].Variant==FontVariant.Variable&&calls.Count==1,"Google Fonts included non-fonts, another host, or missed variable metadata");
            report.Add("PASS: Google Fonts specimen maps only its family to official desktop binaries; variable fonts identified; unrelated files excluded");
            calls.Clear();var pinned=Resolve("https://github.com/example/typeface/releases/tag/v1.0",new Dictionary<string,string>{
                {"https://api.github.com/repos/example/typeface/releases/tags/v1.0","{\"assets\":[{\"name\":\"Typeface-TTF.zip\",\"browser_download_url\":\"https://github.com/example/typeface/releases/download/v1.0/Typeface-TTF.zip\"},{\"name\":\"Source-code.zip\",\"browser_download_url\":\"https://github.com/example/typeface/releases/download/v1.0/Source-code.zip\"}]}"}
            },calls);
            Require(pinned.Files.Count==1&&pinned.Files[0].Url.Contains("/v1.0/")&&calls.Count==1,"Pinned GitHub release ignored or source code offered");
            report.Add("PASS: explicit GitHub release tag is preserved and source-code ZIP is excluded");
            calls.Clear();var blob=Resolve("https://github.com/example/typeface/blob/main/fonts/Font.ttf",new Dictionary<string,string>(),calls);
            Require(calls.Count==0&&blob.Files.Single().Url=="https://raw.githubusercontent.com/example/typeface/main/fonts/Font.ttf","GitHub file page mistaken for downloadable HTML or another release");
            calls.Clear();var squirrel=Resolve("https://www.fontsquirrel.com/fonts/abel",new Dictionary<string,string>{{"https://www.fontsquirrel.com/fonts/abel","<a href='/fonts/download/abel'>Download TTF</a><a href='/tools/webfont-generator'>Webfont Kit</a>"}},calls);
            Require(squirrel.Files.Count==1&&squirrel.Files[0].Url.EndsWith("/fonts/download/abel")&&calls.Count==1,"Font Squirrel desktop download missed");
            report.Add("PASS: GitHub file pages resolve to raw font binaries; Font Squirrel extensionless desktop downloads found");
            calls.Clear();var generic=Resolve("https://official.example/font",new Dictionary<string,string>{{"https://official.example/font","<a href='/handler?id=17' download='Family-TTF.zip'>Download</a><a href='/source-code.zip'>Source code</a><a href='/webfont.zip'>Webfont</a><a href='/wallpaper.zip'>Wallpaper</a>"}},calls);
            Require(generic.Files.Count==1&&generic.Files[0].Url.Contains("id=17"),"Opaque download attribute missed or unrelated archives included");
            bool cancelled=false;try{NoonnuDownload.Discover("https://fonts.google.com/specimen/Roboto",()=>true,s=>{},delegate(string url,out string final){final=url;throw new Exception("Cancellation did not stop fetch");});}catch(OperationCanceledException){cancelled=true;}
            Require(cancelled,"Provider cancellation swallowed");
            report.Add("PASS: official download filename attributes recognized; web/source/unrelated ZIPs excluded; provider cancellation stops before network access");
            calls.Clear();var headers=Resolve("https://official.example/handler?id=19",new Dictionary<string,string>{{"https://official.example/handler?id=19",NoonnuDownload.HeaderDownloadPage("https://official.example/handler?id=19","attachment; filename*=UTF-8''Family%20TTF.zip","application/octet-stream")}},calls);
            Require(headers.Files.Count==1&&calls.Count==1&&headers.Files[0].Title.Contains("Family TTF.zip"),"Content-Disposition filename was missed");
            Require(NoonnuDownload.HeaderDownloadPage("https://official.example/download",null,"text/html")==null,"HTML treated as a desktop binary");
            bool manyRejected=false;try{Resolve("https://fonts.google.com/?family=Roboto:wght@400|Abel",new Dictionary<string,string>(),new List<string>());}catch(InvalidDataException){manyRejected=true;}
            Require(manyRejected,"Multi-family input silently downloaded only the first family");
            calls.Clear();var duplicate=Resolve("https://official.example/font",new Dictionary<string,string>{{"https://official.example/font","<a href='/handler?id=1'>Details</a><button data-download-url='/handler?id=1'>TTF Download</button><a href='/advert.zip' rel='sponsored'>Download ZIP</a>"}},calls);
            Require(duplicate.Files.Count==1&&duplicate.Files[0].Title=="TTF Download","Repeated URL lost download metadata or sponsored archive was included");
            report.Add("PASS: extensionless Content-Disposition downloads found without parsing binary as HTML; multi-family ambiguity rejected");
        }
    }
}
