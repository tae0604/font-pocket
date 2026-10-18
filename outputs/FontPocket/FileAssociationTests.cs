using System;
using System.Collections.Generic;
using System.Linq;

namespace FontPocket {
    static class FileAssociationTests {
        internal static void Run(List<string> report) {
            string exe=@"C:\Example Path\FontPocket.exe",font=@"C:\Fonts\한글 글꼴.ttf";
            var values=FileAssociations.Registration(exe);
            string command=values.Single(v=>v.Key==@"Software\Classes\FontPocket.Font\shell\open\command").Data;
            if(command!="\""+exe+"\" --open \"%1\"")throw new Exception("Shell command quoting failed");
            if(values.Any(v=>v.Key.Contains("UserChoice"))||values.Any(v=>FileAssociations.Extensions.Any(e=>v.Key==@"Software\Classes\"+e)&&v.Name==""))throw new Exception("Registration overwrites file defaults");
            foreach(string ext in FileAssociations.Extensions)if(!values.Any(v=>v.Key.EndsWith(ext+@"\OpenWithProgids")&&v.Name==FileAssociations.TypeId(ext))||!values.Any(v=>v.Key==FileAssociations.Capabilities+@"\FileAssociations"&&v.Name==ext&&v.Data==FileAssociations.TypeId(ext)))throw new Exception("Missing file handler "+ext);
            if(FileAssociations.Extensions.Select(FileAssociations.TypeId).Distinct().Count()!=7)throw new Exception("Font types share a ProgID");
            foreach(string ext in FileAssociations.Extensions)if(!values.Any(v=>v.Key==@"Software\Classes\"+FileAssociations.TypeId(ext)&&v.Name==""&&v.Data==FileAssociations.TypeName(ext))||!values.Any(v=>v.Key==@"Software\Classes\"+FileAssociations.TypeId(ext)+@"\shell\open\command"&&v.Data==command))throw new Exception("Missing typed name or file open command");
            if(FileAssociations.OpenArguments(new[]{"--open",font}).Single()!=font||FileAssociations.OpenArguments(new[]{font}).Single()!=font||FileAssociations.OpenArguments(new[]{"--self-test",font}).Length!=0)throw new Exception("File activation arguments failed");
            report.Add("PASS: seven file handlers and Default Apps capabilities; quoted executable/file arguments; direct and --open activation; default associations retained without writing the registry");
        }
    }
}
