using System.Collections.Concurrent;
using System.Data.Common;
using System.Globalization;

namespace Monjo.Sql
{
    /// <summary>
    /// Value conversion between CLR and the SQL provider. Per-row paths use cached type switches
    /// and cached enum dictionaries — no reflection, no per-row object graphs.
    /// </summary>
    public static class SqlValueConverters
    {
        /// <summary>Converts a CLR value to its database representation (null → DBNull).</summary>
        public static object ToDb(object? value)
        {
            if (value is null)
                return DBNull.Value;

            return value switch
            {
                DateTime dt => NormalizeDateTime(dt),
                Enum e => e.ToString(),
                Guid or bool or int or long or short or byte or double or float or decimal or string or byte[] => value,
                var other => throw new MonjoNotSupportedException(
                    $"Value of type '{other.GetType().Name}' is not supported by Monjo SQL providers. " +
                    "Supported types: string, bool, int, long, short, byte, double, float, decimal, DateTime, Guid, byte[], enum and their nullable variants.")
            };
        }

        /// <summary>
        /// Reads one scalar of a known CLR type. Called from the compiled row mapper with the
        /// cached <c>type</c> — a value comparison chain, not reflection.
        /// </summary>
        public static object Read(DbDataReader reader, int ordinal, Type type, bool nativeGuid, bool dateTimeAsText)
        {
            if (type == typeof(string)) return reader.GetString(ordinal);
            if (type == typeof(int)) return (int)reader.GetInt64(ordinal);
            if (type == typeof(long)) return reader.GetInt64(ordinal);
            if (type == typeof(short)) return (short)reader.GetInt64(ordinal);
            if (type == typeof(byte)) return (byte)reader.GetInt64(ordinal);
            if (type == typeof(bool)) return reader.GetBoolean(ordinal);
            if (type == typeof(double)) return reader.GetDouble(ordinal);
            if (type == typeof(float)) return (float)reader.GetDouble(ordinal);
            if (type == typeof(decimal)) return reader.GetDecimal(ordinal);
            if (type == typeof(DateTime))
            {
                // dateTimeAsText (SQLite): the stored value is UTC text; GetDateTime would apply
                // a local-timezone shift, so parse it explicitly as UTC.
                return dateTimeAsText
                    ? DateTime.Parse(reader.GetString(ordinal), CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal)
                    : NormalizeDateTime(reader.GetDateTime(ordinal));
            }
            if (type == typeof(Guid)) return nativeGuid ? reader.GetGuid(ordinal) : Guid.Parse(reader.GetString(ordinal));
            if (type == typeof(byte[])) return reader.GetBytes(ordinal);
            if (type.IsEnum) return ParseEnum(type, reader.GetString(ordinal));

            throw new MonjoNotSupportedException($"Type '{type.Name}' is not supported by Monjo SQL providers.");
        }

        /// <summary>Normalizes DateTime kind to UTC for storage/comparison (PostgreSQL timestamptz requires it; SQLite stores TEXT).</summary>
        public static DateTime NormalizeDateTime(DateTime value)
            => value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
                _ => value.ToUniversalTime()
            };

        public static Guid ParseGuid(string value) => Guid.Parse(value);

        private static readonly ConcurrentDictionary<Type, Dictionary<string, Enum>> _enumCache = new();

        /// <summary>Enum parse backed by a per-enum-type dictionary built once (case-insensitive).</summary>
        public static object ParseEnum(Type enumType, string? value)
        {
            if (value is null)
                throw new MonjoException($"Cannot convert null to enum '{enumType.Name}'.");

            var lookup = _enumCache.GetOrAdd(enumType, t =>
            {
                var names = Enum.GetNames(t);
                var dict = new Dictionary<string, Enum>(names.Length, StringComparer.OrdinalIgnoreCase);
                foreach (var name in names)
                    dict[name] = (Enum)Enum.Parse(t, name);
                return dict;
            });

            return lookup.TryGetValue(value, out var parsed)
                ? parsed
                : throw new MonjoException($"Cannot convert '{value}' to enum '{enumType.Name}'.");
        }

        /// <summary>Converts a <c>MonjoCondition.Operand</c> to the column's CLR type (shared rule — see <see cref="MonjoOperandConversion"/>).</summary>
        public static object? ConvertOperand(object? operand, Type nonNullableType)
            => MonjoOperandConversion.ConvertOperand(operand, nonNullableType);

        /// <summary>Escapes LIKE wildcards in user data so Contains filters match literally.</summary>
        public static string EscapeLike(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");
        }
    }
}
