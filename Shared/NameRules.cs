using System.Text;
namespace JanjaCord.Shared;
public static class NameRules
{
    public static string Normalize(string value)=>value.Trim().Normalize(NormalizationForm.FormKC);
    public static string Key(string value)=>Normalize(value).ToUpperInvariant();
    public static bool Valid(string? value)=>value!=null&&Normalize(value).Length is >=2 and <=32&&Normalize(value).All(c=>char.IsLetterOrDigit(c)||c is ' ' or '_' or '-' or '.');
}
