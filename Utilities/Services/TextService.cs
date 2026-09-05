using System.Text;
using Utilities.Services.Contracts;
using static Utilities.Constants.RegisterMode;

namespace Utilities.Services
{
    public class TextService : ITextService, ISingletonDependency
    {
        public double CalculateSimilarity(string source, string target)
        {
            if (source == null || target == null) return 0.0;
            if (source.Length == 0 || target.Length == 0) return 0.0;
            if (source == target) return 1.0;

            var stepsToSame = ComputeLevenshteinDistance(source, target);
            return 1.0 - stepsToSame / (double)Math.Max(source.Length, target.Length);
        }

        public int ComputeLevenshteinDistance(string source, string target)
        {
            if (source == null || target == null) return 0;
            if (source.Length == 0 || target.Length == 0) return 0;
            if (source == target) return source.Length;

            var sourceWordCount = source.Length;
            var targetWordCount = target.Length;

            // Step 1
            if (sourceWordCount == 0)
                return targetWordCount;

            if (targetWordCount == 0)
                return sourceWordCount;

            var distance = new int[sourceWordCount + 1, targetWordCount + 1];

            // Step 2
            for (var i = 0; i <= sourceWordCount; distance[i, 0] = i++)
            {
                //Pass
            }

            for (var j = 0; j <= targetWordCount; distance[0, j] = j++)
            {
                //Pass
            }

            for (var i = 1; i <= sourceWordCount; i++)
            {
                for (var j = 1; j <= targetWordCount; j++)
                {
                    // Step 3
                    var cost = target[j - 1] == source[i - 1] ? 0 : 1;

                    // Step 4
                    distance[i, j] = Math.Min(Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1),
                        distance[i - 1, j - 1] + cost);
                }
            }

            return distance[sourceWordCount, targetWordCount];
        }

        public string Normalize(string source)
        {
            var map = GetPersianCharactersMap();

            var erabs = GetErabs();


            foreach (var erab in erabs)
            {
                source = source.Replace(erab, "");
            }

            foreach (KeyValuePair<char, List<char>> item in map)
            {
                foreach (var val in item.Value)
                {
                    source = source.Replace(val, item.Key);
                }
            }

            return source;
        }

        public string RemovePersianCharacters(string source)
        {
            var map = GetPersianCharactersMap();
            var erabs = GetErabs();

            foreach (var (baseChar, alternatives) in map)
            {
                source = source.Replace(baseChar.ToString(), "");
                foreach (var alternative in alternatives)
                    source = source.Replace(alternative.ToString(), "");
            }

            foreach (var erab in erabs)
                source = source.Replace(erab, "");

            return source;
        }

        private static Dictionary<char, List<char>> GetPersianCharactersMap()
        {
            var map = new Dictionary<char, List<char>>();

            map.Add(' ', new List<char>() { '‌' }); //نیم‌فاصله    
            map.Add('ا', new List<char>() { 'ٳ', 'إ', 'ا', 'ٱ', 'آ', 'ٵ', 'ٲ', 'أ', 'ٲ' });
            map.Add('ء', new List<char>() { '۽', 'ٴ', 'ء' });
            map.Add('ب', new List<char>());
            map.Add('پ', new List<char>());
            map.Add('ت', new List<char>() { 'ٿ', 'ټ', 'ٺ', 'ت', 'ٹ' });
            map.Add('ث', new List<char>() { 'ٽ', 'ث' });
            map.Add('ج', new List<char>());
            map.Add('چ', new List<char>() { 'ڇ', 'چ', 'ڄ', 'ڃ' });
            map.Add('ح', new List<char>() { 'ځ', 'ح' });
            map.Add('خ', new List<char>() { 'ڿ', 'خ', 'څ', 'ڂ', 'خ' });
            map.Add('د', new List<char>() { 'ډ', 'ڈ', 'ۮ', 'د' });
            map.Add('ذ', new List<char>() { 'ڐ', 'ڏ', 'ڎ', 'ڍ', 'ڌ', 'ڊ', 'ڋ', 'ذ' });
            map.Add('ر', new List<char>() { 'ږ', 'ڕ', 'ڔ', 'ړ', 'ڒ', 'ر' });
            map.Add('ز', new List<char>());
            map.Add('ژ', new List<char>() { 'ۯ', 'ڙ', 'ژ', 'ڗ' });
            map.Add('س', new List<char>() { 'ښ', 'س' });
            map.Add('ش', new List<char>() { 'ۺ', 'ڜ', 'ڛ', 'ش' });
            map.Add('ص', new List<char>() { 'ص', 'ڝ' });
            map.Add('ض', new List<char>() { 'ۻ', 'ض', 'ڞ' });
            map.Add('ط', new List<char>());
            map.Add('ظ', new List<char>() { 'ڟ', 'ظ' });
            map.Add('ع', new List<char>());
            map.Add('غ', new List<char>() { 'ۼ', 'غ', 'ڠ' });
            map.Add('ف', new List<char>() { 'ڦ', 'ڥ', 'ڤ', 'ڣ', 'ڢ', 'ڡ', 'ف' });
            map.Add('ق', new List<char>() { 'ڨ', 'ڧ', 'ٯ', 'ق' });
            map.Add('ک', new List<char>() { 'ؼ', 'ػ', 'ڪ', 'ک', 'ګ', 'ڬ', 'ڭ', 'ك', 'ڮ' });
            map.Add('گ', new List<char>() { 'گ', 'ڰ', 'ڱ', 'ڲ', 'ڳ', 'ڴ' });
            map.Add('ل', new List<char>() { 'ڵ', 'ڶ', 'ڷ', 'ڸ', 'ل' });
            map.Add('م', new List<char>() { '۾', 'م' });
            map.Add('ن', new List<char>() { 'ن', 'ڽ', 'ڼ', 'ڻ', 'ں', 'ڹ' });
            map.Add('و', new List<char>() { 'ؤ', 'و', 'ٷ', 'ٶ', 'ۏ', 'ۋ', 'ۊ', 'ۉ', 'ۈ', 'ۇ', 'ۆ', 'ۅ', 'ۄ' });
            map.Add('ھ', new List<char>() { 'ۿ', 'ھ' });
            map.Add('ه', new List<char>() { 'ە', 'ۂ', 'ہ', 'ۀ', 'ه', 'ة', 'ۃ', 'ة' });
            map.Add('ی', new List<char>() { 'ئ', 'ؠ', 'ؿ', 'ؾ', 'ؽ', 'ي', 'ى', 'ٸ', 'ی', 'ۍ', 'ێ', 'ۑ', 'ې', 'ۓ', 'ے' });

            return map;
        }

        private static List<string> GetErabs()
        {
            var erabs = new List<string>();
            erabs.Add("ِ");
            erabs.Add("ّ");
            erabs.Add("َ");
            erabs.Add("ْ");
            erabs.Add("ُ");
            erabs.Add("ٰ");
            erabs.Add("ۖ");
            erabs.Add("ٰ");
            erabs.Add("ٓ");
            erabs.Add("ۚ");
            erabs.Add("ٌ"); //بٌ     
            erabs.Add("ۭ"); //بۭ   
            erabs.Add("ً"); //بً
            erabs.Add("ٍ"); //بٍ
            erabs.Add("ۖ"); //بۖ
            erabs.Add("ۥ"); //بۥ
            erabs.Add(Encoding.UTF8.GetString(new byte[] { 0xd9, 0x8b }));
            erabs.Add(Encoding.UTF8.GetString(new byte[] { 0xd9, 0x8c }));
            erabs.Add(Encoding.UTF8.GetString(new byte[] { 0xd9, 0x8d }));
            erabs.Add(Encoding.UTF8.GetString(new byte[] { 0xd9, 0x8e }));
            erabs.Add(Encoding.UTF8.GetString(new byte[] { 0xd9, 0x8f }));
            erabs.Add(Encoding.UTF8.GetString(new byte[] { 0xd9, 0x90 }));
            erabs.Add(Encoding.UTF8.GetString(new byte[] { 0xd9, 0x91 }));
            erabs.Add(Encoding.UTF8.GetString(new byte[] { 0xd9, 0x92 }));
            erabs.Add(Encoding.UTF8.GetString(new byte[] { 0xd9, 0x93 }));
            erabs.Add(Encoding.UTF8.GetString(new byte[] { 0xd9, 0x94 }));
            erabs.Add(Encoding.UTF8.GetString(new byte[] { 0xd9, 0x95 }));
            erabs.Add(Encoding.UTF8.GetString(new byte[] { 0xd9, 0x96 }));
            erabs.Add(Encoding.UTF8.GetString(new byte[] { 0xd9, 0x97 }));
            erabs.Add(Encoding.UTF8.GetString(new byte[] { 0xd9, 0x98 }));
            erabs.Add(Encoding.UTF8.GetString(new byte[] { 0xd9, 0x99 }));
            erabs.Add(Encoding.UTF8.GetString(new byte[] { 0xd9, 0x9a }));
            erabs.Add(Encoding.UTF8.GetString(new byte[] { 0xd9, 0x9b }));
            erabs.Add(Encoding.UTF8.GetString(new byte[] { 0xd9, 0x9c }));
            erabs.Add(Encoding.UTF8.GetString(new byte[] { 0xd9, 0x9d }));
            erabs.Add(Encoding.UTF8.GetString(new byte[] { 0xd9, 0x9e }));
            erabs.Add(Encoding.UTF8.GetString(new byte[] { 0xd9, 0x9f }));
            erabs.Add("ً");
            erabs.Add("ُ");
            erabs.Add("َ");
            erabs.Add("ٌ");
            erabs.Add("ٍ");
            erabs.Add("ِ");
            erabs.Add("ّ");
            erabs.Add("ـ");
            return erabs;
        }
    }
}