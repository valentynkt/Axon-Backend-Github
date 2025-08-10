using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Domain.Primitives;
using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Options;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional.Validation;

namespace BuildingBlocks.Core.Domain.ValueObjects;

/// <summary>
/// Currency-aware monetary value object with safe arithmetic and rounding.
/// Immutable, thread-safe, and optimized for financial calculations.
/// </summary>
public sealed record Money : ValueObject
{
    #region Constants
    
    private const int DefaultPrecision = 2;
    private const int MaxPrecision = 4;
    private const MidpointRounding DefaultRounding = MidpointRounding.ToEven; // Banker's rounding
    
    #endregion
    
    #region Properties
    
    public decimal Amount { get; }
    public Currency Currency { get; }
    
    #endregion
    
    #region Constructors
    
    private Money(decimal amount, Currency currency)
    {
        Amount = amount;
        Currency = currency;
    }
    
    #endregion
    
    #region Factory Methods
    
    /// <summary>
    /// Creates Money with validation. Negative amounts are rejected by default.
    /// </summary>
    public static Result<Money> Create(decimal amount, Currency currency)
    {
        if (currency == Currency.None)
            return Result<Money>.Failure(Error.Validation("Currency must be specified", "MONEY_NO_CURRENCY"));
        
        if (amount < 0)
            return Result<Money>.Failure(Error.Validation(
                "Money amount cannot be negative", 
                "MONEY_NEGATIVE",
                new Dictionary<string, object> { ["Amount"] = amount }));
        
        // Check for overflow
        if (amount > decimal.MaxValue - 1000000m)
            return Result<Money>.Failure(Error.Validation(
                "Money amount exceeds maximum allowed value", 
                "MONEY_OVERFLOW"));
        
        return Result<Money>.Success(new Money(amount, currency));
    }
    
    /// <summary>
    /// Creates Money allowing negative amounts (for debits/adjustments).
    /// </summary>
    public static Result<Money> CreateAllowNegative(decimal amount, Currency currency)
    {
        if (currency == Currency.None)
            return Result<Money>.Failure(Error.Validation("Currency must be specified", "MONEY_NO_CURRENCY"));
        
        if (amount < decimal.MinValue + 1000000m || amount > decimal.MaxValue - 1000000m)
            return Result<Money>.Failure(Error.Validation(
                "Money amount is outside allowed range", 
                "MONEY_OUT_OF_RANGE"));
        
        return Result<Money>.Success(new Money(amount, currency));
    }
    
    /// <summary>
    /// Creates zero Money in specified currency.
    /// </summary>
    public static Money Zero(Currency currency)
    {
        if (currency == Currency.None)
            throw new ArgumentException("Currency must be specified", nameof(currency));
        
        return new Money(0m, currency);
    }
    
    /// <summary>
    /// Try pattern for performance-critical scenarios.
    /// </summary>
    public static bool TryCreate(decimal amount, Currency currency, [NotNullWhen(true)] out Money? money)
    {
        money = null;
        
        if (currency == Currency.None || amount < 0)
            return false;
        
        money = new Money(amount, currency);
        return true;
    }
    
    /// <summary>
    /// Creates Money from minor units (e.g., cents).
    /// </summary>
    public static Result<Money> FromMinorUnits(long minorUnits, Currency currency, int? precision = null)
    {
        if (currency == Currency.None)
            return Result<Money>.Failure(Error.Validation("Currency must be specified", "MONEY_NO_CURRENCY"));
        
        var scale = precision ?? GetDefaultPrecision(currency);
        if (scale < 0 || scale > MaxPrecision)
            return Result<Money>.Failure(Error.Validation(
                $"Precision must be between 0 and {MaxPrecision}", 
                "MONEY_INVALID_PRECISION"));
        
        var divisor = (decimal)Math.Pow(10, scale);
        var amount = minorUnits / divisor;
        
        return Create(amount, currency);
    }
    
    /// <summary>
    /// Creates Money by parsing a string representation.
    /// </summary>
    public static Result<Money> Parse(string value, Currency currency, IFormatProvider? provider = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result<Money>.Failure(Error.Validation("Value cannot be empty", "MONEY_PARSE_EMPTY"));
        
        provider ??= CultureInfo.InvariantCulture;
        
        // Remove currency symbols and whitespace
        var cleanValue = value.Trim()
            .Replace("$", "")
            .Replace("€", "")
            .Replace("£", "")
            .Replace("¥", "")
            .Replace("₹", "")
            .Replace(" ", "");
        
        if (!decimal.TryParse(cleanValue, NumberStyles.Currency, provider, out var amount))
            return Result<Money>.Failure(Error.Validation(
                "Invalid money format", 
                "MONEY_PARSE_INVALID",
                new Dictionary<string, object> { ["Value"] = value }));
        
        return Create(amount, currency);
    }
    
    #endregion
    
    #region Conversion Methods
    
    /// <summary>
    /// Converts to minor units (e.g., cents) with specified precision.
    /// </summary>
    public long ToMinorUnits(int? precision = null)
    {
        var scale = precision ?? GetDefaultPrecision(Currency);
        var multiplier = (decimal)Math.Pow(10, scale);
        return (long)Math.Round(Amount * multiplier, 0, DefaultRounding);
    }
    
    /// <summary>
    /// Rounds to specified decimal places.
    /// </summary>
    public Money Round(int decimals = DefaultPrecision, MidpointRounding rounding = DefaultRounding)
    {
        if (decimals < 0 || decimals > MaxPrecision)
            throw new ArgumentOutOfRangeException(nameof(decimals), $"Decimals must be between 0 and {MaxPrecision}");
        
        var rounded = Math.Round(Amount, decimals, rounding);
        return new Money(rounded, Currency);
    }
    
    /// <summary>
    /// Converts to another currency using provided exchange rate.
    /// </summary>
    public Result<Money> ConvertTo(Currency targetCurrency, decimal exchangeRate, int? precision = null)
    {
        if (targetCurrency == Currency.None)
            return Result<Money>.Failure(Error.Validation("Target currency must be specified", "MONEY_NO_TARGET_CURRENCY"));
        
        if (exchangeRate <= 0)
            return Result<Money>.Failure(Error.Validation(
                "Exchange rate must be positive", 
                "MONEY_INVALID_EXCHANGE_RATE",
                new Dictionary<string, object> { ["Rate"] = exchangeRate }));
        
        if (targetCurrency == Currency)
            return Result<Money>.Success(this);
        
        var convertedAmount = Amount * exchangeRate;
        var targetPrecision = precision ?? GetDefaultPrecision(targetCurrency);
        var rounded = Math.Round(convertedAmount, targetPrecision, DefaultRounding);
        
        return Create(rounded, targetCurrency);
    }
    
    #endregion
    
    #region Arithmetic Operations
    
    /// <summary>
    /// Adds money values (same currency required).
    /// </summary>
    public Result<Money> Add(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);
        
        if (Currency != other.Currency)
            return Result<Money>.Failure(Error.BusinessRule(
                "Cannot add money with different currencies", 
                "MONEY_CURRENCY_MISMATCH",
                new Dictionary<string, object> 
                { 
                    ["Currency1"] = Currency.ToString(),
                    ["Currency2"] = other.Currency.ToString()
                }));
        
        var sum = Amount + other.Amount;
        
        // Overflow check
        if (sum < Amount && other.Amount > 0)
            return Result<Money>.Failure(Error.BusinessRule("Addition would cause overflow", "MONEY_OVERFLOW"));
        
        return Create(sum, Currency);
    }
    
    /// <summary>
    /// Subtracts money values (same currency required).
    /// </summary>
    public Result<Money> Subtract(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);
        
        if (Currency != other.Currency)
            return Result<Money>.Failure(Error.BusinessRule(
                "Cannot subtract money with different currencies", 
                "MONEY_CURRENCY_MISMATCH",
                new Dictionary<string, object> 
                { 
                    ["Currency1"] = Currency.ToString(),
                    ["Currency2"] = other.Currency.ToString()
                }));
        
        var difference = Amount - other.Amount;
        
        if (difference < 0)
            return Result<Money>.Failure(Error.BusinessRule(
                "Subtraction would result in negative amount", 
                "MONEY_NEGATIVE_RESULT",
                new Dictionary<string, object> 
                { 
                    ["Current"] = Amount,
                    ["Subtracting"] = other.Amount,
                    ["Result"] = difference
                }));
        
        return Create(difference, Currency);
    }
    
    /// <summary>
    /// Multiplies money by a scalar value.
    /// </summary>
    public Result<Money> Multiply(decimal multiplier, int? precision = null, MidpointRounding rounding = DefaultRounding)
    {
        if (multiplier < 0)
            return Result<Money>.Failure(Error.BusinessRule(
                "Multiplier cannot be negative", 
                "MONEY_NEGATIVE_MULTIPLIER",
                new Dictionary<string, object> { ["Multiplier"] = multiplier }));
        
        var product = Amount * multiplier;
        
        // Overflow check
        if (multiplier > 1 && product < Amount)
            return Result<Money>.Failure(Error.BusinessRule("Multiplication would cause overflow", "MONEY_OVERFLOW"));
        
        var targetPrecision = precision ?? GetDefaultPrecision(Currency);
        var rounded = Math.Round(product, targetPrecision, rounding);
        
        return Create(rounded, Currency);
    }
    
    /// <summary>
    /// Divides money by a scalar value.
    /// </summary>
    public Result<Money> Divide(decimal divisor, int? precision = null, MidpointRounding rounding = DefaultRounding)
    {
        if (divisor == 0)
            return Result<Money>.Failure(Error.BusinessRule("Cannot divide by zero", "MONEY_DIVIDE_BY_ZERO"));
        
        if (divisor < 0)
            return Result<Money>.Failure(Error.BusinessRule(
                "Divisor cannot be negative", 
                "MONEY_NEGATIVE_DIVISOR",
                new Dictionary<string, object> { ["Divisor"] = divisor }));
        
        var quotient = Amount / divisor;
        var targetPrecision = precision ?? GetDefaultPrecision(Currency);
        var rounded = Math.Round(quotient, targetPrecision, rounding);
        
        return Create(rounded, Currency);
    }
    
    /// <summary>
    /// Calculates percentage of the amount.
    /// </summary>
    public Result<Money> CalculatePercentage(decimal percentage, int? precision = null, MidpointRounding rounding = DefaultRounding)
    {
        if (percentage < 0)
            return Result<Money>.Failure(Error.BusinessRule(
                "Percentage cannot be negative", 
                "MONEY_NEGATIVE_PERCENTAGE",
                new Dictionary<string, object> { ["Percentage"] = percentage }));
        
        if (percentage > 100000) // Sanity check for unreasonable percentages
            return Result<Money>.Failure(Error.BusinessRule(
                "Percentage is unreasonably high", 
                "MONEY_PERCENTAGE_TOO_HIGH",
                new Dictionary<string, object> { ["Percentage"] = percentage }));
        
        var result = Amount * (percentage / 100m);
        var targetPrecision = precision ?? GetDefaultPrecision(Currency);
        var rounded = Math.Round(result, targetPrecision, rounding);
        
        return Create(rounded, Currency);
    }
    
    /// <summary>
    /// Allocates money proportionally across ratios while handling rounding.
    /// </summary>
    public Result<IReadOnlyList<Money>> Allocate(params int[] ratios)
    {
        if (ratios == null || ratios.Length == 0)
            return Result<IReadOnlyList<Money>>.Failure(Error.Validation("Ratios cannot be empty", "MONEY_NO_RATIOS"));
        
        if (ratios.Any(r => r < 0))
            return Result<IReadOnlyList<Money>>.Failure(Error.Validation("Ratios cannot be negative", "MONEY_NEGATIVE_RATIO"));
        
        var total = ratios.Sum();
        if (total == 0)
            return Result<IReadOnlyList<Money>>.Failure(Error.Validation("Total ratio cannot be zero", "MONEY_ZERO_RATIO"));
        
        var results = new List<Money>();
        var remainder = Amount;
        
        for (int i = 0; i < ratios.Length - 1; i++)
        {
            var share = Amount * ratios[i] / total;
            var rounded = Math.Round(share, GetDefaultPrecision(Currency), DefaultRounding);
            results.Add(new Money(rounded, Currency));
            remainder -= rounded;
        }
        
        // Last allocation gets the remainder to avoid rounding errors
        results.Add(new Money(remainder, Currency));
        
        return Result<IReadOnlyList<Money>>.Success(results.AsReadOnly());
    }
    
    #endregion
    
    #region Comparison Operations
    
    /// <summary>
    /// Compares money values (same currency required).
    /// </summary>
    public Result<int> CompareTo(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);
        
        if (Currency != other.Currency)
            return Result<int>.Failure(Error.BusinessRule(
                "Cannot compare money with different currencies", 
                "MONEY_CURRENCY_MISMATCH"));
        
        return Result<int>.Success(Amount.CompareTo(other.Amount));
    }
    
    /// <summary>
    /// Checks if this money is greater than another.
    /// </summary>
    public Result<bool> IsGreaterThan(Money other)
    {
        var comparison = CompareTo(other);
        return comparison.IsSuccess 
            ? Result<bool>.Success(comparison.Value > 0)
            : Result<bool>.Failure(comparison.Error);
    }
    
    /// <summary>
    /// Checks if this money is less than another.
    /// </summary>
    public Result<bool> IsLessThan(Money other)
    {
        var comparison = CompareTo(other);
        return comparison.IsSuccess 
            ? Result<bool>.Success(comparison.Value < 0)
            : Result<bool>.Failure(comparison.Error);
    }
    
    /// <summary>
    /// Checks if this money equals another (same currency and amount).
    /// </summary>
    public Result<bool> IsEqualTo(Money other)
    {
        var comparison = CompareTo(other);
        return comparison.IsSuccess 
            ? Result<bool>.Success(comparison.Value == 0)
            : Result<bool>.Failure(comparison.Error);
    }
    
    public bool IsZero => Amount == 0;
    public bool IsPositive => Amount > 0;
    public bool IsNegative => Amount < 0;
    
    #endregion
    
    #region Validation
    
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Currency;
        yield return Math.Round(Amount, MaxPrecision); // Round for equality to avoid floating point issues
    }
    
    public override Validation<Unit> Validate()
    {
        var errors = new List<Error>();
        
        if (Currency == Currency.None)
            errors.Add(Error.Validation("Currency must be specified", "MONEY_NO_CURRENCY"));
        
        // Note: We allow negative amounts for certain scenarios (debits, adjustments)
        // Business rules should enforce positivity where needed
        
        return errors.Count == 0 
            ? Validation.Valid(Unit.Value) 
            : Validation.Invalid<Unit>(errors);
    }
    
    #endregion
    
    #region Formatting
    
    /// <summary>
    /// Formats money with default precision.
    /// </summary>
    public override string ToString() => ToString(GetDefaultPrecision(Currency));
    
    /// <summary>
    /// Formats money with specified decimal places.
    /// </summary>
    public string ToString(int decimals)
    {
        if (decimals < 0 || decimals > MaxPrecision)
            throw new ArgumentOutOfRangeException(nameof(decimals), $"Decimals must be between 0 and {MaxPrecision}");
        
        return $"{Amount.ToString($"F{decimals}")} {Currency}";
    }
    
    /// <summary>
    /// Formats money with currency symbol for display.
    /// </summary>
    public string ToDisplayString(IFormatProvider? provider = null)
    {
        provider ??= CultureInfo.CurrentCulture;
        
        var symbol = GetCurrencySymbol(Currency);
        var formatted = Amount.ToString("N2", provider);
        
        // Place symbol based on culture
        var culture = provider as CultureInfo ?? CultureInfo.CurrentCulture;
        return culture.NumberFormat.CurrencyPositivePattern % 2 == 0
            ? $"{symbol}{formatted}"
            : $"{formatted} {symbol}";
    }
    
    /// <summary>
    /// Formats money according to ISO 4217 standard.
    /// </summary>
    public string ToIsoString() => $"{Currency} {Amount:F2}";
    
    #endregion
    
    #region Operators
    
    public static Result<Money> operator +(Money left, Money right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return left.Add(right);
    }
    
    public static Result<Money> operator -(Money left, Money right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return left.Subtract(right);
    }
    
    public static Result<Money> operator *(Money money, decimal multiplier)
    {
        ArgumentNullException.ThrowIfNull(money);
        return money.Multiply(multiplier);
    }
    
    public static Result<Money> operator /(Money money, decimal divisor)
    {
        ArgumentNullException.ThrowIfNull(money);
        return money.Divide(divisor);
    }
    
    #endregion
    
    #region Helper Methods
    
    private static int GetDefaultPrecision(Currency currency) => currency switch
    {
        Currency.JPY => 0, // Japanese Yen has no decimal places
        Currency.KWD => 3, // Kuwaiti Dinar uses 3 decimal places
        _ => 2 // Most currencies use 2 decimal places
    };
    
    private static string GetCurrencySymbol(Currency currency) => currency switch
    {
        Currency.USD => "$",
        Currency.EUR => "€",
        Currency.GBP => "£",
        Currency.JPY => "¥",
        Currency.CHF => "Fr",
        Currency.CAD => "C$",
        Currency.AUD => "A$",
        Currency.NZD => "NZ$",
        Currency.SEK => "kr",
        Currency.NOK => "kr",
        Currency.DKK => "kr",
        Currency.PLN => "zł",
        Currency.INR => "₹",
        Currency.CNY => "¥",
        Currency.KRW => "₩",
        Currency.SGD => "S$",
        Currency.HKD => "HK$",
        Currency.MXN => "$",
        Currency.BRL => "R$",
        Currency.ZAR => "R",
        Currency.RUB => "₽",
        Currency.TRY => "₺",
        Currency.AED => "د.إ",
        Currency.SAR => "﷼",
        Currency.KWD => "د.ك",
        _ => currency.ToString()
    };
    
    #endregion
}

/// <summary>
/// ISO 4217 currency codes with comprehensive coverage.
/// </summary>
public enum Currency
{
    None = 0,
    
    // Major currencies
    USD = 1,  // US Dollar
    EUR = 2,  // Euro
    GBP = 3,  // British Pound
    JPY = 4,  // Japanese Yen
    CHF = 5,  // Swiss Franc
    
    // Commonwealth currencies
    CAD = 6,  // Canadian Dollar
    AUD = 7,  // Australian Dollar
    NZD = 8,  // New Zealand Dollar
    
    // Scandinavian currencies
    SEK = 9,  // Swedish Krona
    NOK = 10, // Norwegian Krone
    DKK = 11, // Danish Krone
    
    // European currencies
    PLN = 12, // Polish Zloty
    CZK = 13, // Czech Koruna
    HUF = 14, // Hungarian Forint
    
    // Asian currencies
    INR = 15, // Indian Rupee
    CNY = 16, // Chinese Yuan
    KRW = 17, // South Korean Won
    SGD = 18, // Singapore Dollar
    HKD = 19, // Hong Kong Dollar
    THB = 20, // Thai Baht
    MYR = 21, // Malaysian Ringgit
    IDR = 22, // Indonesian Rupiah
    PHP = 23, // Philippine Peso
    
    // Americas currencies
    MXN = 24, // Mexican Peso
    BRL = 25, // Brazilian Real
    ARS = 26, // Argentine Peso
    CLP = 27, // Chilean Peso
    COP = 28, // Colombian Peso
    
    // Middle East & Africa
    ZAR = 29, // South African Rand
    AED = 30, // UAE Dirham
    SAR = 31, // Saudi Riyal
    KWD = 32, // Kuwaiti Dinar
    ILS = 33, // Israeli Shekel
    EGP = 34, // Egyptian Pound
    
    // Eastern Europe & CIS
    RUB = 35, // Russian Ruble
    UAH = 36, // Ukrainian Hryvnia
    TRY = 37, // Turkish Lira
    
    // Crypto (optional - for modern systems)
    BTC = 100, // Bitcoin (satoshi as minor unit)
    ETH = 101, // Ethereum (wei as minor unit)
    USDT = 102, // Tether
    USDC = 103, // USD Coin
}