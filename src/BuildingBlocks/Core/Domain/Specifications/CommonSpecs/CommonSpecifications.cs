using System.Linq.Expressions;
using BuildingBlocks.Core.Domain.Entities.Abstractions;

namespace BuildingBlocks.Core.Domain.Specifications.CommonSpecs;

/// <summary>
/// Reusable spec factories for common predicates. Keep tiny & composable.
/// Avoid DateTime.UtcNow inside expressions (breaks translation). Accept "now" as parameter.
/// </summary>
public static class CommonSpecifications
{
    // ---------- Null / Empty ----------

    public static Specification<T?> IsNotNull<T>() where T : class
        => Specification<T?>.Create(x => x != null);

    public static Specification<string?> NonEmptyString()
        => Specification<string?>.Create(s => !string.IsNullOrWhiteSpace(s));

    public static Specification<ICollection<T>?> NonEmptyCollection<T>()
        => Specification<ICollection<T>?>.Create(c => c != null && c.Count > 0);

    // ---------- Comparables ----------

    public static Specification<T> GreaterThan<T>(T threshold) where T : IComparable<T>
        => Specification<T>.Create(x => x.CompareTo(threshold) > 0);

    public static Specification<T> GreaterOrEqual<T>(T threshold) where T : IComparable<T>
        => Specification<T>.Create(x => x.CompareTo(threshold) >= 0);

    public static Specification<T> LessThan<T>(T threshold) where T : IComparable<T>
        => Specification<T>.Create(x => x.CompareTo(threshold) < 0);

    public static Specification<T> LessOrEqual<T>(T threshold) where T : IComparable<T>
        => Specification<T>.Create(x => x.CompareTo(threshold) <= 0);

    public static Specification<T> InRange<T>(T minInclusive, T maxInclusive) where T : IComparable<T>
        => Specification<T>.Create(x => x.CompareTo(minInclusive) >= 0 && x.CompareTo(maxInclusive) <= 0);

    public static Specification<T> Between<T>(T minExclusive, T maxExclusive) where T : IComparable<T>
        => Specification<T>.Create(x => x.CompareTo(minExclusive) > 0 && x.CompareTo(maxExclusive) < 0);

    // ---------- Strings ----------

    public static Specification<string?> MaxLength(int max)
        => Specification<string?>.Create(s => s == null || s.Length <= max);

    public static Specification<string?> MinLength(int min)
        => Specification<string?>.Create(s => s != null && s.Length >= min);

    public static Specification<string?> LengthBetween(int min, int max)
        => Specification<string?>.Create(s => s != null && s.Length >= min && s.Length <= max);

    public static Specification<string?> Matches(Func<string, bool> predicate)
        => Specification<string?>.Create(s => s != null && predicate(s));

    public static Specification<string?> Contains(string substring, StringComparison comparison = StringComparison.Ordinal)
        => Specification<string?>.Create(s => s != null && s.Contains(substring, comparison));

    public static Specification<string?> StartsWith(string prefix, StringComparison comparison = StringComparison.Ordinal)
        => Specification<string?>.Create(s => s != null && s.StartsWith(prefix, comparison));

    public static Specification<string?> EndsWith(string suffix, StringComparison comparison = StringComparison.Ordinal)
        => Specification<string?>.Create(s => s != null && s.EndsWith(suffix, comparison));

    // ---------- DateTime (pass nowUtc in to keep expressions translatable) ----------

    public static Specification<DateTime> DateAfter(DateTime date)
        => Specification<DateTime>.Create(d => d > date);

    public static Specification<DateTime> DateBefore(DateTime date)
        => Specification<DateTime>.Create(d => d < date);

    public static Specification<DateTime> DateBetween(DateTime start, DateTime end)
        => Specification<DateTime>.Create(d => d >= start && d <= end);

    public static Specification<DateTime> InPast(DateTime nowUtc)
        => Specification<DateTime>.Create(d => d < nowUtc);

    public static Specification<DateTime> InFuture(DateTime nowUtc)
        => Specification<DateTime>.Create(d => d > nowUtc);

    // ---------- Entities (soft delete / version / audit) ----------

    public static Specification<ISoftDeletable> NotDeleted()
        => Specification<ISoftDeletable>.Create(e => !e.IsDeleted);

    public static Specification<ISoftDeletable> Deleted()
        => Specification<ISoftDeletable>.Create(e => e.IsDeleted);

    public static Specification<IVersioned> VersionAtLeast(uint min)
        => Specification<IVersioned>.Create(e => e.Version >= min);

    public static Specification<IVersioned> VersionEquals(uint version)
        => Specification<IVersioned>.Create(e => e.Version == version);

    public static Specification<IAuditable> CreatedAfter(DateTime date)
        => Specification<IAuditable>.Create(e => e.CreatedAt > date);

    public static Specification<IAuditable> CreatedBefore(DateTime date)
        => Specification<IAuditable>.Create(e => e.CreatedAt < date);

    public static Specification<IAuditable> CreatedBetween(DateTime start, DateTime end)
        => Specification<IAuditable>.Create(e => e.CreatedAt >= start && e.CreatedAt <= end);

    public static Specification<IAuditable> UpdatedAfter(DateTime date)
        => Specification<IAuditable>.Create(e => e.UpdatedAt != null && e.UpdatedAt > date);

    public static Specification<IAuditable> UpdatedBefore(DateTime date)
        => Specification<IAuditable>.Create(e => e.UpdatedAt != null && e.UpdatedAt < date);

    public static Specification<IAuditable> NeverUpdated()
        => Specification<IAuditable>.Create(e => e.UpdatedAt == null);

    // ---------- Set membership ----------

    public static Specification<T> In<T>(IEnumerable<T> set)
    {
        var hash = set is HashSet<T> h ? h : new HashSet<T>(set);
        return Specification<T>.Create(x => hash.Contains(x!));
    }

    public static Specification<T> NotIn<T>(IEnumerable<T> set)
    {
        var hash = set is HashSet<T> h ? h : new HashSet<T>(set);
        return Specification<T>.Create(x => !hash.Contains(x!));
    }

    public static Specification<T> In<T>(params T[] values)    => In(values.AsEnumerable());
    public static Specification<T> NotIn<T>(params T[] values) => NotIn(values.AsEnumerable());

    // ---------- Numeric ----------

    public static Specification<int> Positive()          => Specification<int>.Create(x => x > 0);
    public static Specification<int> Negative()          => Specification<int>.Create(x => x < 0);
    public static Specification<int> NonNegative()       => Specification<int>.Create(x => x >= 0);
    public static Specification<decimal> PositiveDecimal()    => Specification<decimal>.Create(x => x > 0);
    public static Specification<decimal> NonNegativeDecimal() => Specification<decimal>.Create(x => x >= 0);

    // ---------- Boolean ----------

    public static Specification<bool> IsTrue()  => Specification<bool>.Create(x => x);
    public static Specification<bool> IsFalse() => Specification<bool>.Create(x => !x);

    // ---------- Projection-friendly ----------

    public static Specification<T> FromExpression<T>(Expression<Func<T, bool>> expr)
        => Specification<T>.Create(expr);

    // ---------- Composite builders ----------

    public static Specification<T> All<T>(params ISpecification<T>[] specs)
    {
        if (specs.Length == 0) return new TrueSpecification<T>();
        var result = specs[0] as Specification<T> ?? Specification<T>.Create(specs[0].ToExpression());
        for (int i = 1; i < specs.Length; i++)
            result = result.And(specs[i]);
        return result;
    }

    public static Specification<T> Any<T>(params ISpecification<T>[] specs)
    {
        if (specs.Length == 0) return new FalseSpecification<T>();
        var result = specs[0] as Specification<T> ?? Specification<T>.Create(specs[0].ToExpression());
        for (int i = 1; i < specs.Length; i++)
            result = result.Or(specs[i]);
        return result;
    }
}
