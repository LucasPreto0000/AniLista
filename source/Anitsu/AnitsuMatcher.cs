using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
namespace AniLista {
public static class AnitsuMatcher {
 public static string Normalize(string title){
  var text=new StringBuilder();foreach(char c in (title??"").Normalize(NormalizationForm.FormD)){
   if(CharUnicodeInfo.GetUnicodeCategory(c)==UnicodeCategory.NonSpacingMark)continue;
   if(Char.IsLetterOrDigit(c))text.Append(Char.ToLowerInvariant(c));else text.Append(' ');
  }
  return String.Join(" ",text.ToString().Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries));
 }
 public static List<AnitsuCandidate> Match(string title,IEnumerable<string> aliases,IEnumerable<AnitsuCandidate> candidates){
  var keys=new HashSet<string>((aliases??new string[0]).Concat(new[]{title}).Select(Normalize).Where(s=>s.Length>0),StringComparer.Ordinal);
  return (candidates??new AnitsuCandidate[0]).Where(AnitsuApi.ValidCandidate).Where(c=>keys.Contains(Normalize(c.Name))).GroupBy(c=>c.Path,StringComparer.Ordinal).Select(g=>g.First()).ToList();
 }
}
}
