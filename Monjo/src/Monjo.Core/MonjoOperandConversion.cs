using System.Globalization;

namespace Monjo
{
    /// <summary>
    /// Converts a <c>MonjoCondition.Operand</c> (usually a string coming from JSON model binding)
    /// to a column's CLR type. Shared by every provider translator so all providers interpret
    /// operands identically (enum by name, invariant-culture numeric/date parsing).
    /// </summary>
    public static class MonjoOperandConversion
    {
        public static object? ConvertOperand(object? operand, Type nonNullableType)
        {
            if (operand is null)
                return null;

            if (nonNullableType.IsEnum)
            {
                if (operand is Enum e)
                    return e;
                return Enum.Parse(nonNullableType, operand.ToString()!, true);
            }

            if (operand is string s)
            {
                if (nonNullableType == typeof(string))
                    return s;
                if (nonNullableType == typeof(DateTime))
                    return DateTime.Parse(s, CultureInfo.InvariantCulture);
                if (nonNullableType == typeof(Guid))
                    return Guid.Parse(s);
                return Convert.ChangeType(s, nonNullableType, CultureInfo.InvariantCulture);
            }

            return Convert.ChangeType(operand, nonNullableType, CultureInfo.InvariantCulture);
        }
    }
}
