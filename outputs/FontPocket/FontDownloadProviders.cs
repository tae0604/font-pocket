using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace FontPocket {
    public static partial class NoonnuDownload {
        internal static string HeaderDownloadPage(string url,string disposition,string contentType) {
            // Extensionless download handlers expose their filename or font MIME type.
            string mime=(contentType??"").Split(';')[0].Trim().ToLowerInvariant();
            var filename=Regex.Match(disposition??"",@"filename\*?\s*=\s*(?:UTF-8''|[""']?)(?<n>[^;""']+)",RegexOptions.IgnoreCase);
            string name=filename.Success?Uri.UnescapeDataString(filename.Groups["n"].Value.Trim()):"";
            string extension=mime=="application/zip"||mime=="application/x-zip-compressed"?"zip":mime=="font/ttf"||mime=="application/x-font-ttf"?"ttf":mime=="font/otf"||mime=="application/x-font-opentype"?"otf":"";
            if(!Regex.IsMatch(name,@"\.(zip|ttf|otf|ttc|otc|sfnt|woff2?)$",RegexOptions.IgnoreCase)&&extension=="")return null;
            return "<a href=\""+WebUtility.HtmlEncode(url)+"\">Download "+WebUtility.HtmlEncode(name!=""?name:"font."+extension)+"</a>";
        }
        static Uri NormalizeFontUrl(Uri uri) {
            if(uri.Host.Equals("github.com",StringComparison.OrdinalIgnoreCase)) {
                string[] parts=uri.AbsolutePath.Trim('/').Split('/');
                if(parts.Length>=5&&parts[2]=="blob"&&Regex.IsMatch(uri.AbsolutePath,@"\.(ttf|otf|ttc|otc|sfnt|woff2?|zip)$",RegexOptions.IgnoreCase))
                    return new Uri("https://raw.githubusercontent.com/"+parts[0]+"/"+parts[1]+"/"+String.Join("/",parts.Skip(3).ToArray()));
            }
            return uri;
        }
        static bool TryGoogleFonts(Uri uri,DownloadPlan plan,Func<bool> cancelled,Action<string> report,PageFetcher fetch) {
            if(!uri.Host.Equals("fonts.google.com",StringComparison.OrdinalIgnoreCase))return false;
            string family="";
            var specimen=Regex.Match(uri.AbsolutePath,@"^/specimen/(?<family>[^/]+)",RegexOptions.IgnoreCase);
            if(specimen.Success)family=Uri.UnescapeDataString(specimen.Groups["family"].Value.Replace("+"," "));
            else {
                var query=Regex.Match(uri.Query,@"(?:\?|&)family=(?<family>[^&]+)",RegexOptions.IgnoreCase);
                if(query.Success){
                    family=Uri.UnescapeDataString(query.Groups["family"].Value.Replace("+"," "));
                    if(family.Contains("|")||Regex.Matches(uri.Query,@"(?:\?|&)family=",RegexOptions.IgnoreCase).Count>1)throw new InvalidDataException("Google Fonts의 개별 폰트 페이지 주소를 넣어 주세요.");
                    family=family.Split(':')[0];
                }
            }
            string folder=Regex.Replace(family.ToLowerInvariant(),@"[^a-z0-9]","");
            if(folder.Length==0||family.Contains("|"))throw new InvalidDataException("Google Fonts의 개별 폰트 페이지 주소를 넣어 주세요.");
            report("Google Fonts에서 설치용 폰트를 찾는 중…");
            foreach(string license in new[]{"ofl","apache","ufl"}) {
                if(cancelled())throw new OperationCanceledException();
                try {
                    string final,path=license+"/"+folder;
                    string json=fetch("https://api.github.com/repos/google/fonts/contents/"+path+"?ref=main",out final);
                    var files=new JavaScriptSerializer().DeserializeObject(json) as object[];
                    if(files==null)continue;
                    var found=new List<DownloadLink>();
                    foreach(var item in files.OfType<Dictionary<string,object>>()) {
                        object type,name,url;
                        if(!item.TryGetValue("type",out type)||!Object.Equals(type,"file")||!item.TryGetValue("name",out name)||!item.TryGetValue("download_url",out url)||!(url is string))continue;
                        var link=new DownloadLink {Url=(string)url,Title=family+" · "+Convert.ToString(name),FamilyName=family};
                        object size;if(item.TryGetValue("size",out size))link.SizeBytes=Convert.ToInt64(size);
                        Uri download;
                        if(!Uri.TryCreate(link.Url,UriKind.Absolute,out download)||download.Scheme!="https"||!download.Host.Equals("raw.githubusercontent.com",StringComparison.OrdinalIgnoreCase)||!download.AbsolutePath.StartsWith("/google/fonts/main/"+path+"/",StringComparison.Ordinal))continue;
                        if(Regex.IsMatch(Convert.ToString(name),@"\.(ttf|otf|ttc|otc|sfnt|woff2?)$",RegexOptions.IgnoreCase)) {
                            link.Variant=Regex.IsMatch(Convert.ToString(name),@"\[[^\]]+\]")?FontVariant.Variable:InferVariant(link.Url,link.Title,"");
                            found.Add(link);
                        }
                    }
                    if(found.Count>0){plan.Source="https://github.com/google/fonts/tree/main/"+path;plan.Files=found.OrderByDescending(Score).Take(100).ToList();return true;}
                }catch(OperationCanceledException){throw;}
                catch(WebException ex){var response=ex.Response as HttpWebResponse;if(response==null||response.StatusCode!=HttpStatusCode.NotFound)throw;response.Dispose();}
            }
            throw new InvalidDataException("Google Fonts에서 설치용 폰트를 찾지 못했습니다. 폰트 이름과 개별 페이지 주소를 확인해 주세요.");
        }
    }
}
