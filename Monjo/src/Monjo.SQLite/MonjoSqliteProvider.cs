using System.Data.Common;
using System.Globalization;
using System.Numerics;
using Microsoft.Data.Sqlite;

namespace Monjo.SQLite
{
    /// <summary>
    /// SQLite dialect. Type affinity per SQLite conventions; booleans as INTEGER,
    /// DateTime as UTC TEXT (parsed back as UTC), Guid as "N" TEXT, and decimal as a
    /// <b>fixed-width sortable TEXT encoding</b> so that SQLite's text comparison gives
    /// exact NUMERIC results (equality, &lt;/&lt;=/&gt;=/&gt;, ORDER BY) with full decimal
    /// precision — plain decimal TEXT would compare lexicographically and order wrong.
    /// </summary>
    public sealed class SqliteDialect : SqlDialect
    {
        public override string ProviderName => "SQLite";
        public override string FalseLiteral => "0";
        public override string TrueLiteral => "1";
        public override bool SupportsNativeGuid => false;
        public override bool ReadsDateTimeAsText => true;
        public override bool ReadsDecimalAsText => true;

        public override string GetSqlType(Type clrType)
            => clrType switch
            {
                _ when clrType == typeof(string) => "TEXT",
                _ when clrType == typeof(bool) => "INTEGER",
                _ when clrType == typeof(int) => "INTEGER",
                _ when clrType == typeof(long) => "INTEGER",
                _ when clrType == typeof(short) => "INTEGER",
                _ when clrType == typeof(byte) => "INTEGER",
                _ when clrType == typeof(double) => "REAL",
                _ when clrType == typeof(float) => "REAL",
                _ when clrType == typeof(decimal) => "TEXT",
                _ when clrType == typeof(DateTime) => "TEXT",
                _ when clrType == typeof(Guid) => "TEXT",
                _ when clrType == typeof(byte[]) => "BLOB",
                _ when clrType.IsEnum => "TEXT",
                var other => throw new MonjoNotSupportedException(
                    $"CLR type '{other.Name}' has no SQLite column type mapping in Monjo.")
            };

        public override DbConnection CreateConnection(MonjoOptions options)
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = options.ConnectionString,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = true,
                DefaultTimeout = options.Sqlite.BusyTimeoutSeconds,
            };
            return new SqliteConnection(builder.ConnectionString);
        }

        public override object ToDbValue(object value)
            => value is decimal d
                ? EncodeDecimal(d)
                : value is Guid guid
                    ? guid.ToString("N")
                    : value;

        // ------------------------------------------------------------------ decimal encoding
        //
        // SQLite has no 64-bit-precision decimal type, and Microsoft.Data.Sqlite stores
        // decimal parameters as plain TEXT, so unencoded comparisons would be LEXICOGRAPHIC
        // ("9.5" > "30") and scale-sensitive ("12.5" != "12.50"). Instead the value is stored
        // as a fixed-width, zero-padded, sign-prefixed digit string:
        //
        //   non-negative:  "1" + 29 zero-padded integer digits + 28 zero-padded fraction digits
        //   negative:      "0" + 9's-complement of the same 57 digits of |value|
        //
        // Properties: lexicographic order == numeric order (negatives first, complements
        // reverse the magnitude within the negative class); the encoding is canonical (12.5
        // and 12.50 produce the identical string); and it is lossless — every .NET decimal
        // (96-bit mantissa: at most 29 significant digits, scale 0..28, e.g. ±79228162514264337593543950335
        // and ±1E-28) fits the 57-digit field exactly, so no precision is ever lost.

        private const int DecimalIntDigits = 29;   // decimal.MaxValue has 29 integer digits
        private const int DecimalFracDigits = 28;  // decimal's maximum scale

        public override object EncodeDecimal(decimal value)
        {
            if (value >= 0m)
            {
                var digits = DigitsOfAbs(value);
                return "1" + digits;
            }

            // -(decimal.MinValue) is representable, so -value is safe.
            var digits = DigitsOfAbs(-value);
            var complement = new char[digits.Length];
            for (var i = 0; i < digits.Length; i++)
                complement[i] = (char)('9' - (digits[i] - '0'));
            return new string('0', 1) + complement;
        }

        public override decimal DecodeDecimal(string stored)
        {
            var negative = stored[0] == '0';
            var intDigits = stored.Substring(1, DecimalIntDigits);
            var fracDigits = stored.Substring(1 + DecimalIntDigits, DecimalFracDigits);
            if (negative)
            {
                intDigits = Complement(intDigits);
                fracDigits = Complement(fracDigits);
            }

            var intTrimmed = intDigits.TrimStart('0');
            if (intTrimmed.Length == 0)
                intTrimmed = "0";

            // Valid encodings always represent an in-range decimal, so the parse is exact.
            return decimal.Parse(intTrimmed + "." + fracDigits, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The 57 zero-padded digits of |value| (29 integer + 28 fraction), from the exact
        /// 96-bit mantissa — no floating-point or string-format round-trip.
        /// </summary>
        private static string DigitsOfAbs(decimal abs)
        {
            var bits = decimal.GetBits(abs);
            var scale = (bits[3] >> 16) & 0x7F;

            // Exact 96-bit mantissa (3x32-bit fields). CreateUInt32 is essential: the GetBits
            // words are signed ints, and a plain conversion would sign-extend when bit 31 of a
            // word is set (any value with a mantissa bit >= 31, i.e. most realistic amounts).
            var mantissa = BigInteger.CreateUInt32((uint)bits[0]);
            mantissa |= BigInteger.CreateUInt32((uint)bits[1]) << 32;
            mantissa |= BigInteger.CreateUInt32((uint)bits[2]) << 64;
            var unscaled = mantissa.ToString(CultureInfo.InvariantCulture).TrimStart('0');
            if (unscaled.Length == 0)
                unscaled = "0";

            string intPart, fracPart;
            if (unscaled.Length <= scale)
            {
                intPart = new string('0', DecimalIntDigits);
                var frac = new string('0', scale - unscaled.Length) + unscaled;
                fracPart = frac.Length >= DecimalFracDigits
                    ? frac.Substring(frac.Length - DecimalFracDigits)
                    : frac.PadRight(DecimalFracDigits, '0');
            }
            else
            {
                var intLen = unscaled.Length - scale;
                var intChars = unscaled.Substring(0, intLen);
                intPart = intChars.Length >= DecimalIntDigits ? intChars : intChars.PadLeft(DecimalIntDigits, '0');
                fracPart = unscaled.Substring(intLen).PadRight(DecimalFracDigits, '0');
            }

            return intPart + fracPart;
        }

        private static string Complement(string digits)
        {
            var result = new char[digits.Length];
            for (var i = 0; i < digits.Length; i++)
                result[i] = (char)('9' - (digits[i] - '0'));
            return new string(result);
        }

        /// <summary>Translates SQLite busy/locked failures into the provider-independent <see cref="MonjoBusyException"/>.</summary>
        public override Exception? TranslateException(Exception exception)
            => exception is SqliteException { SqliteErrorCode: SqliteErrorCode.Busy or SqliteErrorCode.Locked } e
                ? new MonjoBusyException(
                    "SQLite is busy: another writer holds the database lock. Increase 'Monjo:Sqlite:BusyTimeoutSeconds' " +
                    "or serialize writers (SQLite supports concurrent readers but a single writer).", e)
                : null;
    }

    /// <summary>
    /// Monjo's SQLite provider. Microsoft.Data.Sqlite connection pooling + WAL journal mode +
    /// busy timeout: reads stay concurrent, writers are serialized by SQLite itself (documented
    /// behavior for embedded workloads).
    /// </summary>
    public sealed class MonjoSqliteProvider : SqlMonjoProvider
    {
        public override string Name => "SQLite";

        public MonjoSqliteProvider(MonjoOptions options)
            : base(options, new SqliteDialect(), options.Sqlite.BusyTimeoutSeconds + 15)
        {
        }

        /// <summary>The SQLite file (connection string) is the database identity.</summary>
        internal override string DatabaseIdentity => Options.ConnectionString;

        /// <inheritdoc/>
        protected internal override async Task EnsureProviderPragmasAsync(CancellationToken cancellationToken)
        {
            // WAL is persistent; setting it again is a cheap no-op. Done once per process
            // (the readiness gate ensures that), never per request.
            //
            // Executed as a READER, not ExecuteNonQueryAsync: `PRAGMA journal_mode=WAL` RETURNS
            // A RESULT ROW (the resulting mode), and ExecuteNonQueryAsync on a statement that
            // produces rows is not a reliable way to run it. Consume the row.
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode=WAL;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                // The row holds the resulting journal mode; nothing to do with it here.
            }
        }
    }
}
