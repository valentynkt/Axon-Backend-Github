using Axon.Shared.Domain.Specifications;
using System.Linq.Expressions;

namespace Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Base;

/// <summary>
/// Base class for testing domain specifications with comprehensive test coverage.
/// Ensures specifications follow DDD patterns and compose correctly.
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("Domain")]
[Category("Specification")]
public abstract class SpecificationTestBase<TEntity, TSpec> : DomainTestBase
    where TSpec : ISpecification<TEntity>
    where TEntity : class
{
    /// <summary>
    /// Tests that a specification correctly identifies matching entities.
    /// </summary>
    protected void TestSpecificationMatches(
        TSpec specification,
        IEnumerable<TEntity> testData,
        IEnumerable<TEntity> expectedMatches)
    {
        var matches = testData.Where(specification.IsSatisfiedBy).ToList();
        var expected = expectedMatches.ToList();
        
        matches.Count.ShouldBe(expected.Count,
            $"Expected {expected.Count} matches but found {matches.Count}");
        
        foreach (var expectedMatch in expected)
        {
            matches.ShouldContain(expectedMatch,
                $"Expected entity was not matched by specification");
        }
    }

    /// <summary>
    /// Tests specification with expression tree compilation.
    /// </summary>
    protected void TestSpecificationExpression(
        TSpec specification,
        IQueryable<TEntity> queryable,
        IEnumerable<TEntity> expectedResults)
    {
        var expression = specification.ToExpression();
        var results = queryable.Where(expression).ToList();
        var expected = expectedResults.ToList();
        
        results.Count.ShouldBe(expected.Count,
            $"Expression query returned {results.Count} results but expected {expected.Count}");
        
        foreach (var expectedResult in expected)
        {
            results.ShouldContain(expectedResult);
        }
    }

    /// <summary>
    /// Tests AND composition of specifications.
    /// </summary>
    protected void TestAndComposition(
        TSpec spec1,
        TSpec spec2,
        IEnumerable<TEntity> testData,
        IEnumerable<TEntity> expectedMatches)
    {
        var compositeSpec = new AndSpecification<TEntity>(spec1, spec2);
        
        var matches = testData.Where(compositeSpec.IsSatisfiedBy).ToList();
        var expected = expectedMatches.ToList();
        
        matches.Count.ShouldBe(expected.Count,
            $"AND composition should match {expected.Count} entities");
        
        // Verify that all matches satisfy both specifications
        foreach (var match in matches)
        {
            spec1.IsSatisfiedBy(match).ShouldBeTrue("Match should satisfy first specification");
            spec2.IsSatisfiedBy(match).ShouldBeTrue("Match should satisfy second specification");
        }
    }

    /// <summary>
    /// Tests OR composition of specifications.
    /// </summary>
    protected void TestOrComposition(
        TSpec spec1,
        TSpec spec2,
        IEnumerable<TEntity> testData,
        IEnumerable<TEntity> expectedMatches)
    {
        var compositeSpec = new OrSpecification<TEntity>(spec1, spec2);
        
        var matches = testData.Where(compositeSpec.IsSatisfiedBy).ToList();
        var expected = expectedMatches.ToList();
        
        matches.Count.ShouldBe(expected.Count,
            $"OR composition should match {expected.Count} entities");
        
        // Verify that all matches satisfy at least one specification
        foreach (var match in matches)
        {
            (spec1.IsSatisfiedBy(match) || spec2.IsSatisfiedBy(match)).ShouldBeTrue(
                "Match should satisfy at least one specification");
        }
    }

    /// <summary>
    /// Tests NOT composition of specifications.
    /// </summary>
    protected void TestNotComposition(
        TSpec specification,
        IEnumerable<TEntity> testData)
    {
        var notSpec = new NotSpecification<TEntity>(specification);
        
        foreach (var entity in testData)
        {
            var satisfiesOriginal = specification.IsSatisfiedBy(entity);
            var satisfiesNot = notSpec.IsSatisfiedBy(entity);
            
            satisfiesNot.ShouldBe(!satisfiesOriginal,
                "NOT specification should return opposite of original");
        }
    }

    /// <summary>
    /// Tests specification with edge cases.
    /// </summary>
    protected void TestEdgeCases(
        TSpec specification,
        params (TEntity Entity, bool ShouldMatch, string Description)[] edgeCases)
    {
        foreach (var (entity, shouldMatch, description) in edgeCases)
        {
            TestContext.WriteLine($"Testing edge case: {description}");
            
            var matches = specification.IsSatisfiedBy(entity);
            
            if (shouldMatch)
            {
                matches.ShouldBeTrue($"Edge case '{description}' should match");
            }
            else
            {
                matches.ShouldBeFalse($"Edge case '{description}' should not match");
            }
        }
    }

    /// <summary>
    /// Tests specification performance with large datasets.
    /// </summary>
    protected void TestPerformance(
        TSpec specification,
        IEnumerable<TEntity> largeDataset,
        TimeSpan maxDuration)
    {
        var data = largeDataset.ToList();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        var results = data.Where(specification.IsSatisfiedBy).ToList();
        
        stopwatch.Stop();
        
        stopwatch.Elapsed.ShouldBeLessThan(maxDuration,
            $"Specification should process {data.Count} entities in less than {maxDuration.TotalMilliseconds}ms");
        
        TestContext.WriteLine($"Processed {data.Count} entities in {stopwatch.ElapsedMilliseconds}ms, found {results.Count} matches");
    }

    /// <summary>
    /// Tests that specification can be used in LINQ to SQL scenarios.
    /// </summary>
    protected void TestLinqTranslation(
        TSpec specification,
        Func<Expression<Func<TEntity, bool>>, string> getSql)
    {
        var expression = specification.ToExpression();
        var sql = getSql(expression);
        
        sql.ShouldNotBeNullOrEmpty("Specification should translate to SQL");
        TestContext.WriteLine($"SQL Translation: {sql}");
    }

    /// <summary>
    /// Tests specification with null handling.
    /// </summary>
    protected void TestNullHandling(TSpec specification)
    {
        var exception = Catch(() => specification.IsSatisfiedBy(null!));
        
        // Specifications should handle null gracefully
        if (exception != null)
        {
            exception.ShouldBeOfType<ArgumentNullException>(
                "If specification doesn't handle null, it should throw ArgumentNullException");
        }
        else
        {
            // If no exception, should return false for null
            specification.IsSatisfiedBy(null!).ShouldBeFalse(
                "Specification should return false for null entity");
        }
    }

    /// <summary>
    /// Tests complex specification chains.
    /// </summary>
    protected void TestComplexChain(
        IEnumerable<TSpec> specifications,
        Func<IEnumerable<ISpecification<TEntity>>, ISpecification<TEntity>> combiner,
        IEnumerable<TEntity> testData,
        IEnumerable<TEntity> expectedMatches)
    {
        var combinedSpec = combiner(specifications.Cast<ISpecification<TEntity>>());
        
        var matches = testData.Where(combinedSpec.IsSatisfiedBy).ToList();
        var expected = expectedMatches.ToList();
        
        matches.Count.ShouldBe(expected.Count);
        
        foreach (var expectedMatch in expected)
        {
            matches.ShouldContain(expectedMatch);
        }
    }

    /// <summary>
    /// Helper to create AND specification.
    /// </summary>
    protected class AndSpecification<T> : ISpecification<T> where T : class
    {
        private readonly ISpecification<T> _left;
        private readonly ISpecification<T> _right;

        public AndSpecification(ISpecification<T> left, ISpecification<T> right)
        {
            _left = left;
            _right = right;
        }

        public bool IsSatisfiedBy(T entity)
        {
            return _left.IsSatisfiedBy(entity) && _right.IsSatisfiedBy(entity);
        }

        public Expression<Func<T, bool>> ToExpression()
        {
            var leftExpr = _left.ToExpression();
            var rightExpr = _right.ToExpression();
            
            var parameter = Expression.Parameter(typeof(T));
            var body = Expression.AndAlso(
                Expression.Invoke(leftExpr, parameter),
                Expression.Invoke(rightExpr, parameter));
            
            return Expression.Lambda<Func<T, bool>>(body, parameter);
        }
    }

    /// <summary>
    /// Helper to create OR specification.
    /// </summary>
    protected class OrSpecification<T> : ISpecification<T> where T : class
    {
        private readonly ISpecification<T> _left;
        private readonly ISpecification<T> _right;

        public OrSpecification(ISpecification<T> left, ISpecification<T> right)
        {
            _left = left;
            _right = right;
        }

        public bool IsSatisfiedBy(T entity)
        {
            return _left.IsSatisfiedBy(entity) || _right.IsSatisfiedBy(entity);
        }

        public Expression<Func<T, bool>> ToExpression()
        {
            var leftExpr = _left.ToExpression();
            var rightExpr = _right.ToExpression();
            
            var parameter = Expression.Parameter(typeof(T));
            var body = Expression.OrElse(
                Expression.Invoke(leftExpr, parameter),
                Expression.Invoke(rightExpr, parameter));
            
            return Expression.Lambda<Func<T, bool>>(body, parameter);
        }
    }

    /// <summary>
    /// Helper to create NOT specification.
    /// </summary>
    protected class NotSpecification<T> : ISpecification<T> where T : class
    {
        private readonly ISpecification<T> _inner;

        public NotSpecification(ISpecification<T> inner)
        {
            _inner = inner;
        }

        public bool IsSatisfiedBy(T entity)
        {
            return !_inner.IsSatisfiedBy(entity);
        }

        public Expression<Func<T, bool>> ToExpression()
        {
            var innerExpr = _inner.ToExpression();
            
            var parameter = Expression.Parameter(typeof(T));
            var body = Expression.Not(Expression.Invoke(innerExpr, parameter));
            
            return Expression.Lambda<Func<T, bool>>(body, parameter);
        }
    }
}