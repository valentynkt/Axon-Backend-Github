namespace BuildingBlocks.Core.Domain.Primitives;

/// <summary>
/// Base class for value objects following DDD principles.
/// Immutable and compared by value equality.
/// All value objects must implement validation.
/// </summary>
public abstract record ValueObject
{
    /// <summary>
    /// Get components for equality comparison
    /// </summary>
    protected abstract IEnumerable<object?> GetEqualityComponents();
    
    /// <summary>
    /// Validate the value object state
    /// All value objects must be valid upon creation
    /// </summary>
    public abstract Validation<Unit> Validate();
    
    /// <summary>
    /// Check if the value object is valid
    /// </summary>
    public bool IsValid => Validate().IsValid;
    
    /// <summary>
    /// Get validation errors
    /// </summary>
    public IReadOnlyList<Error> ValidationErrors => Validate().Errors;
    
    public override int GetHashCode()
    {
        return GetEqualityComponents()
            .Select(x => x?.GetHashCode() ?? 0)
            .Aggregate((x, y) => x ^ y);
    }
    
    public virtual bool Equals(ValueObject? other)
    {
        if (other is null || other.GetType() != GetType())
            return false;
            
        return GetEqualityComponents()
            .SequenceEqual(other.GetEqualityComponents());
    }
    
    /// <summary>
    /// OBSOLETE: Use Result-based factory methods instead of exception-throwing validation
    /// This method breaks the functional programming principles established in Epic 1
    /// </summary>
    [Obsolete("Use Result-based Create() factory methods. Exception-based validation violates functional principles.")]
    protected void EnsureValid()
    {
        var validation = Validate();
        if (validation.IsInvalid)
        {
            var message = string.Join("; ", validation.Errors.Select(e => e.Message));
            throw new DomainException($"Invalid {GetType().Name}: {message}");
        }
    }
}

/// <summary>
/// Base class for single-value value objects
/// All value objects must be created via Result-based factory methods
/// </summary>
public abstract record SingleValueObject<T> : ValueObject
{
    public T Value { get; }
    
    /// <summary>
    /// Protected constructor - use static Create methods for instantiation
    /// Value objects should only be created through validated factory methods
    /// </summary>
    protected SingleValueObject(T value)
    {
        Value = value;
        // No validation here - validation is responsibility of the factory method
        // This ensures consistent Result-based creation pattern
    }
    
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
    
    public override string ToString() => Value?.ToString() ?? string.Empty;
    
    public static implicit operator T(SingleValueObject<T> valueObject)
    {
        return valueObject.Value;
    }
}

/// <summary>
/// Example: Email value object with comprehensive validation
/// </summary>
public sealed record Email : SingleValueObject<string>
{
    private static readonly Regex EmailRegex = new(
        @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
        
    public const int MaxLength = 254; // RFC 5321 maximum
    
    private Email(string value) : base(value.ToLowerInvariant())
    {
    }
    
    /// <summary>
    /// Create an email with validation
    /// </summary>
    public static Result<Email> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result<Email>.Failure(Error.Validation("Email cannot be empty", "EMAIL_EMPTY"));
            
        value = value.Trim().ToLowerInvariant();
        
        if (value.Length > MaxLength)
            return Result<Email>.Failure(Error.Validation($"Email cannot exceed {MaxLength} characters", "EMAIL_TOO_LONG"));
            
        if (!EmailRegex.IsMatch(value))
            return Result<Email>.Failure(Error.Validation("Invalid email format", "EMAIL_INVALID_FORMAT"));
            
        return Result<Email>.Success(new Email(value));
    }
    
    public override Validation<Unit> Validate()
    {
        var errors = new List<Error>();
        
        if (string.IsNullOrWhiteSpace(Value))
            errors.Add(Error.Validation("Email cannot be empty", "EMAIL_EMPTY"));
        else
        {
            if (Value.Length > MaxLength)
                errors.Add(Error.Validation($"Email cannot exceed {MaxLength} characters", "EMAIL_TOO_LONG"));
                
            if (!EmailRegex.IsMatch(Value))
                errors.Add(Error.Validation("Invalid email format", "EMAIL_INVALID_FORMAT"));
        }
        
        return errors.Any() 
            ? Validation<Unit>.Invalid(errors)
            : Validation<Unit>.Valid(Unit.Value);
    }
    
    /// <summary>
    /// Get the domain part of the email
    /// </summary>
    public string Domain => Value.Split('@')[1];
    
    /// <summary>
    /// Get the local part of the email
    /// </summary>
    public string LocalPart => Value.Split('@')[0];
    
    /// <summary>
    /// Check if email is from a specific domain
    /// </summary>
    public bool IsFromDomain(string domain)
    {
        ArgumentNullException.ThrowIfNull(domain);
        return Domain.Equals(domain, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Example: Money value object with currency support
/// </summary>
public sealed record Money : ValueObject
{
    public decimal Amount { get; }
    public Currency Currency { get; }
    
    /// <summary>
    /// Private constructor - use Create() factory method for instantiation
    /// No validation here - validation is responsibility of the factory method
    /// </summary>
    private Money(decimal amount, Currency currency)
    {
        Amount = amount;
        Currency = currency;
        // No EnsureValid() call - validation done in Create() method
    }
    
    /// <summary>
    /// Create money with validation
    /// </summary>
    public static Result<Money> Create(decimal amount, Currency currency)
    {
        if (amount < 0)
            return Result<Money>.Failure(Error.Validation("Money amount cannot be negative", "MONEY_NEGATIVE"));
            
        if (currency == Currency.None)
            return Result<Money>.Failure(Error.Validation("Currency must be specified", "MONEY_NO_CURRENCY"));
            
        return Result<Money>.Success(new Money(amount, currency));
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
        
        return errors.Any() 
            ? Validation<Unit>.Invalid(errors)
            : Validation<Unit>.Valid(Unit.Value);
    }
    
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
    
    public override string ToString() => $"{Amount:F2} {Currency}";
}

/// <summary>
/// Currency enumeration
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
