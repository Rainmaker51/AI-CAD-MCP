using System.Text;
namespace AICad.Application;
public static class SourceAnnotationText {
 public static string Decode(string text,string style){
  if(!style.ToLowerInvariant().Contains("vn"))return text;
  const string legacy="µ¸¶·¹¨»¾¼½Æ©ÇÊÈÉË®ÌÐÎÏÑªÒÕÓÔÖ×ÝØÜÞßãáâä«åèæçé¬êíëìîïóñòô\u00adõøö÷ùúýûüþ§¤¦¡¢£¥";
  const string unicode="àáảãạăằắẳẵặâầấẩẫậđèéẻẽẹêềếểễệìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵĐÔƯĂÂÊƠ";
  var output=new StringBuilder();foreach(char c in text){int i=legacy.IndexOf(c);output.Append(i<0?c:unicode[i]);}return output.ToString();
 }
}
