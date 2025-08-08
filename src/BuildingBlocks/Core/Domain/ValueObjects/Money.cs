using BuildingBlocks.Core.Domain.Primitives;
using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Options;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional.Validation;

namespace BuildingBlocks.Core.Domain.ValueObjects;

/// <summary>
/// Money value object with currency support following Epic 2 specifications.
/// Demonstrates Result-based operations and comprehensive business logic validation.
/// </summary>
public sealed record Money : ValueObject
{
    public decimal Amount { get; }
    public Currency Currency { get; }
    
    /// <summary>
    /// Private constructor - use Create() factory method for instantiation.
    /// No validation here - validation is responsibility of the factory method.
    /// </summary>
    private Money(decimal amount, Currency currency)
    {
        Amount = amount;
        Currency = currency;
        // No EnsureValid() call - validation done in Create() method
    }
    
    /// <summary>
    /// Create money with validation using Result pattern
    /// </summary>
    public static Result<Money> Create(decimal amount, Currency currency)
    {
        if (amount < 0)
            return Result<Money>.Failure(Error.Validation("Money amount cannot be negative", "MONEY_NEGATIVE"));
            
        if (currency == Currency.None)
            return Result<Money>.Failure(Error.Validation("Currency must be specified", "MONEY_NO_CURRENCY"));
            
        return Result<Money>.Success(new Money(amount, currency));
    }
    
    /// <summary>
    /// Create zero money for a given currency
    /// </summary>
    public static Result<Money> Zero(Currency currency)
    {
        return Create(0, currency);
    }
    
    /// <summary>
    /// Try to create money, returning None for invalid inputs
    /// </summary>
    public static Option<Money> TryCreate(decimal amount, Currency currency)
    {
        var result = Create(amount, currency);
        return result.IsSuccess ? Option<Money>.Some(result.Value) : Option<Money>.None();
    }
    
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
    
    public override Validation<Unit> Validate()
    {
        var errors = new List<Error>();
        
        if (Amount < 0)
            errors.Add(Error.Validation("Money amount cannot be negative", "MONEY_NEGATIVE"));
            
        if (Currency == Currency.None)
            errors.Add(Error.Validation("Currency must be specified", "MONEY_NO_CURRENCY"));
        
        return errors.Count != 0
            ? Validation<Unit>.Invalid(errors)
            : Validation<Unit>.Valid(Unit.Value);
    }
    
    #region Arithmetic Operations
    
    /// <summary>
    /// Add money (same currency only)
    /// </summary>
    public Result<Money> Add(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);
        
        if (Currency != other.Currency)
            return Result<Money>.Failure(Error.BusinessRule("Cannot add money with different currencies", "MONEY_CURRENCY_MISMATCH"));
            
        return Create(Amount + other.Amount, Currency);
    }
    
    /// <summary>
    /// Subtract money (same currency only)
    /// </summary>
    public Result<Money> Subtract(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);
        
        if (Currency != other.Currency)
            return Result<Money>.Failure(Error.BusinessRule("Cannot subtract money with different currencies", "MONEY_CURRENCY_MISMATCH"));
            
        var newAmount = Amount - other.Amount;
        if (newAmount < 0)
            return Result<Money>.Failure(Error.BusinessRule("Resulting amount cannot be negative", "MONEY_NEGATIVE_RESULT"));
            
        return Create(newAmount, Currency);
    }
    
    /// <summary>
    /// Multiply by scalar
    /// </summary>
    public Result<Money> Multiply(decimal multiplier)
    {
        if (multiplier < 0)
            return Result<Money>.Failure(Error.BusinessRule("Money multiplier cannot be negative", "MONEY_NEGATIVE_MULTIPLIER"));
            
        return Create(Amount * multiplier, Currency);
    }
    
    /// <summary>
    /// Divide by scalar
    /// </summary>
    public Result<Money> Divide(decimal divisor)
    {
        if (divisor <= 0)
            return Result<Money>.Failure(Error.BusinessRule("Money divisor must be positive", "MONEY_INVALID_DIVISOR"));
            
        return Create(Amount / divisor, Currency);
    }
    
    #endregion
    
    #region Comparison Operations
    
    /// <summary>
    /// Check if this money is greater than another (same currency only)
    /// </summary>
    public Result<bool> IsGreaterThan(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);
        
        if (Currency != other.Currency)
            return Result<bool>.Failure(Error.BusinessRule("Cannot compare money with different currencies", "MONEY_CURRENCY_MISMATCH"));
            
        return Result<bool>.Success(Amount > other.Amount);
    }
    
    /// <summary>
    /// Check if this money is less than another (same currency only)
    /// </summary>
    public Result<bool> IsLessThan(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);
        
        if (Currency != other.Currency)
            return Result<bool>.Failure(Error.BusinessRule("Cannot compare money with different currencies", "MONEY_CURRENCY_MISMATCH"));
            
        return Result<bool>.Success(Amount < other.Amount);
    }
    
    /// <summary>
    /// Check if amount is zero
    /// </summary>
    public bool IsZero => Amount == 0;
    
    /// <summary>
    /// Check if amount is positive
    /// </summary>
    public bool IsPositive => Amount > 0;
    
    #endregion
    
    #region Formatting
    
    public override string ToString() => $"{Amount:F2} {Currency}";
    
    /// <summary>
    /// Format with custom number of decimal places
    /// </summary>
    public string ToString(int decimalPlaces)
    {
        if (decimalPlaces < 0)
            throw new ArgumentOutOfRangeException(nameof(decimalPlaces), "Decimal places cannot be negative");
            
        return $"{Amount.ToString($"F{decimalPlaces}")} {Currency}";
    }
    
    /// <summary>
    /// Format for display with currency symbol
    /// </summary>
    public string ToDisplayString()
    {
        var symbol = Currency switch
        {
            Currency.USD => "$",
            Currency.EUR => "€",
            Currency.GBP => "£",
            Currency.CAD => "C$",
            Currency.AUD => "A$",
            _ => Currency.ToString()
        };
        
        return $"{symbol}{Amount:F2}";
    }
    
    #endregion
}

/// <summary>
/// Currency enumeration for Money value object
/// </summary>
public enum Currency
{
    None = 0,
    USD = 1,
    EUR = 2,
    GBP = 3,
    CAD = 4,
    AUD = 5
}