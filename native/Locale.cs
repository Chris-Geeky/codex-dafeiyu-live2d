using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

internal static class Locale {
    private static readonly Lazy<Dictionary<string,string>> English=new Lazy<Dictionary<string,string>>(LoadEnglish);
    private static string choice="zh-CN";
    private static readonly string SystemLanguage=CultureInfo.CurrentUICulture.TwoLetterISOLanguageName=="zh"?"zh-CN":"en";
    internal static string Choice {get{return choice;}}
    internal static bool Valid(string value) {return value=="system"||value=="zh-CN"||value=="en";}
    internal static void Select(string value) {choice=Valid(value)?value:"system";}
    internal static string Language {
        get{
            if(Choice!="system")return Choice;
            return SystemLanguage;
        }
    }
    internal static string T(string original) {
        string translated;
        return !String.IsNullOrEmpty(original)&&Language=="en"&&English.Value.TryGetValue(original,out translated)?translated:original;
    }
    private static Dictionary<string,string> LoadEnglish() {
        var entries=new Dictionary<string,string>(StringComparer.Ordinal);
        using(var stream=typeof(Locale).Assembly.GetManifestResourceStream("Dafeiyu.English"))
        using(var reader=new StreamReader(stream,Encoding.UTF8)){
            string row;
            while((row=reader.ReadLine())!=null){
                int split=row.IndexOf('\t');if(split<=0)continue;
                entries.Add(Unescape(row.Substring(0,split)),Unescape(row.Substring(split+1)));
            }
        }
        return entries;
    }
    private static string Unescape(string value) {return value.Replace("\\r","\r").Replace("\\n","\n").Replace("\\s"," ");}
}
