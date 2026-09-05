namespace Utilities.Services.Contracts
{
    public interface ITextService
    {
        int ComputeLevenshteinDistance(string source, string target);
        double CalculateSimilarity(string source, string target);
        string Normalize(string source);
        string RemovePersianCharacters(string source);
    }
}
