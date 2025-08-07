📦 PART 1: BUILDING BLOCKS - COMPLETE IMPLEMENTATION GUIDE

Table of Contents

1. #overview--philosophy
2. #functional-types-implementation
3. #error-handling-system
4. #domain-primitives
5. #cqrs-infrastructure
6. #cross-cutting-concerns
7. #extension-methods--helpers
8. #testing-support
9. #migration-from-current-state

  ---
1. Overview & Philosophy

Core Principles

- Immutability First: All types are immutable records or structs
- Explicit Over Implicit: No hidden behavior, everything is explicit
- Composition Over Inheritance: Favor composition and interfaces
- Zero Null References: Use Option instead of nullables
- Railway-Oriented: All operations return Result

Package Structure

src/BuildingBlocks/
├── Core/
│   ├── Results/          # Functional types (Result, Option, Either)
│   ├── Model/           # Domain primitives (Entity, Aggregate, ValueObject)
│   ├── CQRS/           # Command/Query interfaces and base classes
│   ├── Events/         # Event infrastructure
│   └── Specifications/ # Specification pattern
├── Application/
│   ├── Behaviors/      # MediatR pipeline behaviors
│   ├── Validation/     # Validation infrastructure
│   └── Services/       # Application service interfaces
├── Infrastructure/
│   ├── Persistence/    # Repository implementations
│   ├── Caching/       # Cache abstractions
│   ├── Messaging/     # Message bus abstractions
│   └── Logging/       # Structured logging
└── Web/
├── Endpoints/     # FastEndpoints base classes
├── Middleware/    # Custom middleware
└── Extensions/    # Web-specific extensions

  ---
2. Functional Types Implementation

2.1 Complete Result Implementation

// BuildingBlocks/Core/Results/Result.cs
using System.Diagnostics.CodeAnalysis;

namespace BuildingBlocks.Core.Results;

/// <summary>
/// Result monad for railway-oriented programming.
/// Represents the outcome of an operation that can succeed or fail.
/// Thread-safe and immutable.
/// </summary>
/// <typeparam name="T">The type of the success value</typeparam>
public readonly record struct Result<T> : IResult<T>
{
private readonly T? _value;
private readonly Error? _error;
private readonly bool _isSuccess;

      private Result(T? value, Error? error, bool isSuccess)
      {
          _value = value;
          _error = error;
          _isSuccess = isSuccess;
      }

      #region Properties

      [MemberNotNullWhen(true, nameof(_value))]
      [MemberNotNullWhen(false, nameof(_error))]
      public bool IsSuccess => _isSuccess;

      public bool IsFailure => !_isSuccess;

      public T Value => _isSuccess
          ? _value!
          : throw new InvalidOperationException($"Cannot access Value of failed Result. Error: {_error}");

      public Error Error => !_isSuccess
          ? _error!
          : throw new InvalidOperationException("Cannot access Error of successful Result");

      public T? ValueOrDefault => _isSuccess ? _value : default;

      #endregion

      #region Factory Methods

      public static Result<T> Success(T value)
      {
          ArgumentNullException.ThrowIfNull(value);
          return new Result<T>(value, null, true);
      }

      public static Result<T> Failure(Error error)
      {
          ArgumentNullException.ThrowIfNull(error);
          return new Result<T>(default, error, false);
      }

      public static Result<T> Create(bool condition, T value, Error error)
      {
          return condition ? Success(value) : Failure(error);
      }

      public static Result<T> Try(Func<T> operation, Func<Exception, Error>? errorFactory = null)
      {
          try
          {
              return Success(operation());
          }
          catch (Exception ex)
          {
              var error = errorFactory?.Invoke(ex) ?? Error.FromException(ex);
              return Failure(error);
          }
      }

      public static async Task<Result<T>> TryAsync(
          Func<Task<T>> operation,
          Func<Exception, Error>? errorFactory = null)
      {
          try
          {
              var result = await operation().ConfigureAwait(false);
              return Success(result);
          }
          catch (Exception ex)
          {
              var error = errorFactory?.Invoke(ex) ?? Error.FromException(ex);
              return Failure(error);
          }
      }

      #endregion

      #region Functor Operations

      public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
      {
          ArgumentNullException.ThrowIfNull(mapper);

          return _isSuccess
              ? Result<TNew>.Success(mapper(_value!))
              : Result<TNew>.Failure(_error!);
      }

      public async Task<Result<TNew>> MapAsync<TNew>(
          Func<T, Task<TNew>> mapper,
          CancellationToken ct = default)
      {
          ArgumentNullException.ThrowIfNull(mapper);

          if (!_isSuccess)
              return Result<TNew>.Failure(_error!);

          try
          {
              var newValue = await mapper(_value!).ConfigureAwait(false);
              return Result<TNew>.Success(newValue);
          }
          catch (OperationCanceledException)
          {
              return Result<TNew>.Failure(Error.Cancelled());
          }
          catch (Exception ex)
          {
              return Result<TNew>.Failure(Error.FromException(ex));
          }
      }

      public Result<T> MapError(Func<Error, Error> mapper)
      {
          ArgumentNullException.ThrowIfNull(mapper);

          return _isSuccess
              ? this
              : Result<T>.Failure(mapper(_error!));
      }

      #endregion

      #region Monad Operations

      public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder)
      {
          ArgumentNullException.ThrowIfNull(binder);

          return _isSuccess
              ? binder(_value!)
              : Result<TNew>.Failure(_error!);
      }

      public async Task<Result<TNew>> BindAsync<TNew>(
          Func<T, Task<Result<TNew>>> binder,
          CancellationToken ct = default)
      {
          ArgumentNullException.ThrowIfNull(binder);

          if (!_isSuccess)
              return Result<TNew>.Failure(_error!);

          try
          {
              return await binder(_value!).ConfigureAwait(false);
          }
          catch (OperationCanceledException)
          {
              return Result<TNew>.Failure(Error.Cancelled());
          }
          catch (Exception ex)
          {
              return Result<TNew>.Failure(Error.FromException(ex));
          }
      }

      public Result<TNew> SelectMany<TNew>(Func<T, Result<TNew>> binder)
          => Bind(binder);

      public Result<TResult> SelectMany<TIntermediate, TResult>(
          Func<T, Result<TIntermediate>> binder,
          Func<T, TIntermediate, TResult> projector)
      {
          return Bind(value => binder(value).Map(intermediate => projector(value, intermediate)));
      }

      #endregion

      #region Applicative Operations

      public Result<TResult> Apply<TResult>(Result<Func<T, TResult>> wrappedFunction)
      {
          if (wrappedFunction.IsFailure)
              return Result<TResult>.Failure(wrappedFunction.Error);

          if (IsFailure)
              return Result<TResult>.Failure(Error);

          return Result<TResult>.Success(wrappedFunction.Value(_value!));
      }

      public static Result<TResult> Lift<T1, T2, TResult>(
          Func<T1, T2, TResult> function,
          Result<T1> r1,
          Result<T2> r2)
      {
          if (r1.IsFailure) return Result<TResult>.Failure(r1.Error);
          if (r2.IsFailure) return Result<TResult>.Failure(r2.Error);

          return Result<TResult>.Success(function(r1.Value, r2.Value));
      }

      #endregion

      #region Side Effects

      public Result<T> Tap(Action<T> action)
      {
          ArgumentNullException.ThrowIfNull(action);

          if (_isSuccess)
              action(_value!);

          return this;
      }

      public async Task<Result<T>> TapAsync(
          Func<T, Task> action,
          CancellationToken ct = default)
      {
          ArgumentNullException.ThrowIfNull(action);

          if (_isSuccess)
          {
              try
              {
                  await action(_value!).ConfigureAwait(false);
              }
              catch (OperationCanceledException)
              {
                  return Result<T>.Failure(Error.Cancelled());
              }
              catch (Exception ex)
              {
                  return Result<T>.Failure(Error.FromException(ex));
              }
          }

          return this;
      }

      public Result<T> TapError(Action<Error> action)
      {
          ArgumentNullException.ThrowIfNull(action);

          if (!_isSuccess)
              action(_error!);

          return this;
      }

      #endregion

      #region Pattern Matching

      public TResult Match<TResult>(
          Func<T, TResult> onSuccess,
          Func<Error, TResult> onFailure)
      {
          ArgumentNullException.ThrowIfNull(onSuccess);
          ArgumentNullException.ThrowIfNull(onFailure);

          return _isSuccess
              ? onSuccess(_value!)
              : onFailure(_error!);
      }

      public async Task<TResult> MatchAsync<TResult>(
          Func<T, Task<TResult>> onSuccess,
          Func<Error, Task<TResult>> onFailure,
          CancellationToken ct = default)
      {
          ArgumentNullException.ThrowIfNull(onSuccess);
          ArgumentNullException.ThrowIfNull(onFailure);

          return _isSuccess
              ? await onSuccess(_value!).ConfigureAwait(false)
              : await onFailure(_error!).ConfigureAwait(false);
      }

      public void Switch(
          Action<T> onSuccess,
          Action<Error> onFailure)
      {
          ArgumentNullException.ThrowIfNull(onSuccess);
          ArgumentNullException.ThrowIfNull(onFailure);

          if (_isSuccess)
              onSuccess(_value!);
          else
              onFailure(_error!);
      }

      #endregion

      #region Validation & Ensuring

      public Result<T> Ensure(
          Func<T, bool> predicate,
          Error error)
      {
          ArgumentNullException.ThrowIfNull(predicate);
          ArgumentNullException.ThrowIfNull(error);

          if (IsFailure) return this;

          return predicate(_value!)
              ? this
              : Result<T>.Failure(error);
      }

      public Result<T> Ensure(
          Func<T, bool> predicate,
          Func<T, Error> errorFactory)
      {
          ArgumentNullException.ThrowIfNull(predicate);
          ArgumentNullException.ThrowIfNull(errorFactory);

          if (IsFailure) return this;

          return predicate(_value!)
              ? this
              : Result<T>.Failure(errorFactory(_value!));
      }

      public async Task<Result<T>> EnsureAsync(
          Func<T, Task<bool>> predicate,
          Error error,
          CancellationToken ct = default)
      {
          ArgumentNullException.ThrowIfNull(predicate);
          ArgumentNullException.ThrowIfNull(error);

          if (IsFailure) return this;

          var isValid = await predicate(_value!).ConfigureAwait(false);
          return isValid ? this : Result<T>.Failure(error);
      }

      #endregion

      #region Recovery & Fallback

      public Result<T> Recover(Func<Error, T> recovery)
      {
          ArgumentNullException.ThrowIfNull(recovery);

          return IsFailure
              ? Result<T>.Success(recovery(_error!))
              : this;
      }

      public Result<T> RecoverWith(Func<Error, Result<T>> recovery)
      {
          ArgumentNullException.ThrowIfNull(recovery);

          return IsFailure ? recovery(_error!) : this;
      }

      public async Task<Result<T>> RecoverAsync(
          Func<Error, Task<T>> recovery,
          CancellationToken ct = default)
      {
          ArgumentNullException.ThrowIfNull(recovery);

          if (IsSuccess) return this;

          try
          {
              var recovered = await recovery(_error!).ConfigureAwait(false);
              return Result<T>.Success(recovered);
          }
          catch (Exception ex)
          {
              return Result<T>.Failure(Error.FromException(ex));
          }
      }

      public T GetOrElse(T defaultValue)
      {
          return _isSuccess ? _value! : defaultValue;
      }

      public T GetOrElse(Func<T> defaultFactory)
      {
          ArgumentNullException.ThrowIfNull(defaultFactory);
          return _isSuccess ? _value! : defaultFactory();
      }

      #endregion

      #region Combination

      public static Result<(T1, T2)> Combine<T1, T2>(
          Result<T1> r1,
          Result<T2> r2)
      {
          if (r1.IsFailure) return Result<(T1, T2)>.Failure(r1.Error);
          if (r2.IsFailure) return Result<(T1, T2)>.Failure(r2.Error);

          return Result<(T1, T2)>.Success((r1.Value, r2.Value));
      }

      public static Result<(T1, T2, T3)> Combine<T1, T2, T3>(
          Result<T1> r1,
          Result<T2> r2,
          Result<T3> r3)
      {
          if (r1.IsFailure) return Result<(T1, T2, T3)>.Failure(r1.Error);
          if (r2.IsFailure) return Result<(T1, T2, T3)>.Failure(r2.Error);
          if (r3.IsFailure) return Result<(T1, T2, T3)>.Failure(r3.Error);

          return Result<(T1, T2, T3)>.Success((r1.Value, r2.Value, r3.Value));
      }

      public static Result<IReadOnlyList<T>> Traverse(
          IEnumerable<Result<T>> results)
      {
          ArgumentNullException.ThrowIfNull(results);

          var list = new List<T>();

          foreach (var result in results)
          {
              if (result.IsFailure)
                  return Result<IReadOnlyList<T>>.Failure(result.Error);

              list.Add(result.Value);
          }

          return Result<IReadOnlyList<T>>.Success(list);
      }

      public static Result<IReadOnlyList<T>> Sequence(
          params Result<T>[] results)
      {
          return Traverse(results);
      }

      #endregion

      #region Conversion

      public Option<T> ToOption()
      {
          return _isSuccess
              ? Option<T>.Some(_value!)
              : Option<T>.None();
      }

      public Either<Error, T> ToEither()
      {
          return _isSuccess
              ? Either<Error, T>.Right(_value!)
              : Either<Error, T>.Left(_error!);
      }

      public Result ToNonGeneric()
      {
          return _isSuccess
              ? Result.Success()
              : Result.Failure(_error!);
      }

      #endregion

      #region Operators

      public static implicit operator Result<T>(Error error)
          => Failure(error);

      public static implicit operator Result<T>(T value)
          => Success(value);

      #endregion

      #region Equality

      public bool Equals(Result<T> other)
      {
          if (_isSuccess != other._isSuccess) return false;

          return _isSuccess
              ? EqualityComparer<T>.Default.Equals(_value, other._value)
              : _error!.Equals(other._error);
      }

      public override int GetHashCode()
      {
          return _isSuccess
              ? HashCode.Combine(_isSuccess, _value)
              : HashCode.Combine(_isSuccess, _error);
      }

      #endregion
}

// Non-generic Result for void operations
public readonly record struct Result : IResult
{
private readonly Error? _error;
private readonly bool _isSuccess;

      private Result(bool isSuccess, Error? error)
      {
          _isSuccess = isSuccess;
          _error = error;
      }

      public bool IsSuccess => _isSuccess;
      public bool IsFailure => !_isSuccess;

      public Error Error => !_isSuccess
          ? _error!
          : throw new InvalidOperationException("Cannot access Error of successful Result");

      public static Result Success() => new(true, null);
      public static Result Failure(Error error) => new(false, error);

      public static Result<T> Success<T>(T value) => Result<T>.Success(value);
      public static Result<T> Failure<T>(Error error) => Result<T>.Failure(error);

      // All the same operations as Result<T> but without value
      // ... (similar implementations without value parameter)
}

2.2 Option Monad

// BuildingBlocks/Core/Results/Option.cs
namespace BuildingBlocks.Core.Results;

/// <summary>
/// Option monad representing a value that may or may not exist.
/// Eliminates null reference exceptions and makes absence explicit.
/// </summary>
public readonly record struct Option<T> : IOption<T>
{
private readonly T? _value;
private readonly bool _hasValue;

      private Option(T? value, bool hasValue)
      {
          _value = value;
          _hasValue = hasValue;
      }

      #region Properties

      [MemberNotNullWhen(true, nameof(_value))]
      public bool IsSome => _hasValue;

      public bool IsNone => !_hasValue;

      public T Value => _hasValue
          ? _value!
          : throw new InvalidOperationException("Cannot access Value of None Option");

      #endregion

      #region Factory Methods

      public static Option<T> Some(T value)
      {
          ArgumentNullException.ThrowIfNull(value);
          return new Option<T>(value, true);
      }

      public static Option<T> None() => new(default, false);

      public static Option<T> From(T? value)
      {
          return value is null ? None() : Some(value);
      }

      public static Option<T> When(bool condition, T value)
      {
          return condition ? Some(value) : None();
      }

      public static Option<T> When(bool condition, Func<T> factory)
      {
          return condition ? Some(factory()) : None();
      }

      #endregion

      #region Functor Operations

      public Option<TNew> Map<TNew>(Func<T, TNew> mapper)
      {
          ArgumentNullException.ThrowIfNull(mapper);

          return _hasValue
              ? Option<TNew>.Some(mapper(_value!))
              : Option<TNew>.None();
      }

      public async Task<Option<TNew>> MapAsync<TNew>(
          Func<T, Task<TNew>> mapper,
          CancellationToken ct = default)
      {
          ArgumentNullException.ThrowIfNull(mapper);

          if (!_hasValue) return Option<TNew>.None();

          var result = await mapper(_value!).ConfigureAwait(false);
          return Option<TNew>.Some(result);
      }

      #endregion

      #region Monad Operations

      public Option<TNew> Bind<TNew>(Func<T, Option<TNew>> binder)
      {
          ArgumentNullException.ThrowIfNull(binder);

          return _hasValue ? binder(_value!) : Option<TNew>.None();
      }

      public async Task<Option<TNew>> BindAsync<TNew>(
          Func<T, Task<Option<TNew>>> binder,
          CancellationToken ct = default)
      {
          ArgumentNullException.ThrowIfNull(binder);

          return _hasValue
              ? await binder(_value!).ConfigureAwait(false)
              : Option<TNew>.None();
      }

      public Option<TNew> SelectMany<TNew>(Func<T, Option<TNew>> binder)
          => Bind(binder);

      public Option<TResult> SelectMany<TIntermediate, TResult>(
          Func<T, Option<TIntermediate>> binder,
          Func<T, TIntermediate, TResult> projector)
      {
          return Bind(value =>
              binder(value).Map(intermediate =>
                  projector(value, intermediate)));
      }

      #endregion

      #region Filter Operations

      public Option<T> Where(Func<T, bool> predicate)
      {
          ArgumentNullException.ThrowIfNull(predicate);

          return _hasValue && predicate(_value!) ? this : None();
      }

      public Option<T> Filter(Func<T, bool> predicate) => Where(predicate);

      #endregion

      #region Pattern Matching

      public TResult Match<TResult>(
          Func<T, TResult> onSome,
          Func<TResult> onNone)
      {
          ArgumentNullException.ThrowIfNull(onSome);
          ArgumentNullException.ThrowIfNull(onNone);

          return _hasValue ? onSome(_value!) : onNone();
      }

      public async Task<TResult> MatchAsync<TResult>(
          Func<T, Task<TResult>> onSome,
          Func<Task<TResult>> onNone,
          CancellationToken ct = default)
      {
          ArgumentNullException.ThrowIfNull(onSome);
          ArgumentNullException.ThrowIfNull(onNone);

          return _hasValue
              ? await onSome(_value!).ConfigureAwait(false)
              : await onNone().ConfigureAwait(false);
      }

      public void Switch(Action<T> onSome, Action onNone)
      {
          if (_hasValue)
              onSome(_value!);
          else
              onNone();
      }

      #endregion

      #region Extraction

      public T GetOrElse(T defaultValue)
      {
          return _hasValue ? _value! : defaultValue;
      }

      public T GetOrElse(Func<T> defaultFactory)
      {
          ArgumentNullException.ThrowIfNull(defaultFactory);
          return _hasValue ? _value! : defaultFactory();
      }

      public T? GetOrDefault()
      {
          return _hasValue ? _value : default;
      }

      public T GetOrThrow(string errorMessage)
      {
          return _hasValue
              ? _value!
              : throw new InvalidOperationException(errorMessage);
      }

      #endregion

      #region Side Effects

      public Option<T> Tap(Action<T> action)
      {
          ArgumentNullException.ThrowIfNull(action);

          if (_hasValue) action(_value!);
          return this;
      }

      public Option<T> TapNone(Action action)
      {
          ArgumentNullException.ThrowIfNull(action);

          if (!_hasValue) action();
          return this;
      }

      #endregion

      #region Conversion

      public Result<T> ToResult(Error errorIfNone)
      {
          return _hasValue
              ? Result<T>.Success(_value!)
              : Result<T>.Failure(errorIfNone);
      }

      public Result<T> ToResult(Func<Error> errorFactory)
      {
          ArgumentNullException.ThrowIfNull(errorFactory);

          return _hasValue
              ? Result<T>.Success(_value!)
              : Result<T>.Failure(errorFactory());
      }

      public Either<TLeft, T> ToEither<TLeft>(TLeft leftValue)
      {
          return _hasValue
              ? Either<TLeft, T>.Right(_value!)
              : Either<TLeft, T>.Left(leftValue);
      }

      public T[] ToArray()
      {
          return _hasValue ? new[] { _value! } : Array.Empty<T>();
      }

      public List<T> ToList()
      {
          return _hasValue ? new List<T> { _value! } : new List<T>();
      }

      #endregion

      #region Operators

      public static implicit operator Option<T>(T? value)
      {
          return value is null ? None() : Some(value);
      }

      #endregion
}

2.3 Either<TLeft, TRight> Monad

// BuildingBlocks/Core/Results/Either.cs
namespace BuildingBlocks.Core.Results;

/// <summary>
/// Either monad representing a value that can be one of two types.
/// Typically used for error handling where Left represents error and Right represents success.
/// </summary>
public readonly record struct Either<TLeft, TRight> : IEither<TLeft, TRight>
{
private readonly TLeft? _left;
private readonly TRight? _right;
private readonly bool _isRight;

      private Either(TLeft? left, TRight? right, bool isRight)
      {
          _left = left;
          _right = right;
          _isRight = isRight;
      }

      #region Properties

      public bool IsLeft => !_isRight;
      public bool IsRight => _isRight;

      public TLeft LeftValue => !_isRight
          ? _left!
          : throw new InvalidOperationException("Cannot access Left value of Right Either");

      public TRight RightValue => _isRight
          ? _right!
          : throw new InvalidOperationException("Cannot access Right value of Left Either");

      #endregion

      #region Factory Methods

      public static Either<TLeft, TRight> Left(TLeft value)
      {
          ArgumentNullException.ThrowIfNull(value);
          return new Either<TLeft, TRight>(value, default, false);
      }

      public static Either<TLeft, TRight> Right(TRight value)
      {
          ArgumentNullException.ThrowIfNull(value);
          return new Either<TLeft, TRight>(default, value, true);
      }

      #endregion

      #region Functor Operations (Right-biased)

      public Either<TLeft, TNew> Map<TNew>(Func<TRight, TNew> mapper)
      {
          ArgumentNullException.ThrowIfNull(mapper);

          return _isRight
              ? Either<TLeft, TNew>.Right(mapper(_right!))
              : Either<TLeft, TNew>.Left(_left!);
      }

      public Either<TNew, TRight> MapLeft<TNew>(Func<TLeft, TNew> mapper)
      {
          ArgumentNullException.ThrowIfNull(mapper);

          return _isRight
              ? Either<TNew, TRight>.Right(_right!)
              : Either<TNew, TRight>.Left(mapper(_left!));
      }

      #endregion

      #region Monad Operations

      public Either<TLeft, TNew> Bind<TNew>(
          Func<TRight, Either<TLeft, TNew>> binder)
      {
          ArgumentNullException.ThrowIfNull(binder);

          return _isRight ? binder(_right!) : Either<TLeft, TNew>.Left(_left!);
      }

      public Either<TNew, TRight> BindLeft<TNew>(
          Func<TLeft, Either<TNew, TRight>> binder)
      {
          ArgumentNullException.ThrowIfNull(binder);

          return _isRight
              ? Either<TNew, TRight>.Right(_right!)
              : binder(_left!);
      }

      #endregion

      #region Bifunctor Operations

      public Either<TNewLeft, TNewRight> BiMap<TNewLeft, TNewRight>(
          Func<TLeft, TNewLeft> leftMapper,
          Func<TRight, TNewRight> rightMapper)
      {
          ArgumentNullException.ThrowIfNull(leftMapper);
          ArgumentNullException.ThrowIfNull(rightMapper);

          return _isRight
              ? Either<TNewLeft, TNewRight>.Right(rightMapper(_right!))
              : Either<TNewLeft, TNewRight>.Left(leftMapper(_left!));
      }

      #endregion

      #region Pattern Matching

      public TResult Match<TResult>(
          Func<TLeft, TResult> onLeft,
          Func<TRight, TResult> onRight)
      {
          ArgumentNullException.ThrowIfNull(onLeft);
          ArgumentNullException.ThrowIfNull(onRight);

          return _isRight ? onRight(_right!) : onLeft(_left!);
      }

      public void Switch(
          Action<TLeft> onLeft,
          Action<TRight> onRight)
      {
          ArgumentNullException.ThrowIfNull(onLeft);
          ArgumentNullException.ThrowIfNull(onRight);

          if (_isRight)
              onRight(_right!);
          else
              onLeft(_left!);
      }

      #endregion

      #region Conversion

      public Option<TRight> ToOption()
      {
          return _isRight
              ? Option<TRight>.Some(_right!)
              : Option<TRight>.None();
      }

      public Option<TLeft> ToLeftOption()
      {
          return !_isRight
              ? Option<TLeft>.Some(_left!)
              : Option<TLeft>.None();
      }

      public Result<TRight> ToResult(Func<TLeft, Error> errorMapper)
      {
          ArgumentNullException.ThrowIfNull(errorMapper);

          return _isRight
              ? Result<TRight>.Success(_right!)
              : Result<TRight>.Failure(errorMapper(_left!));
      }

      #endregion

      #region Side Effects

      public Either<TLeft, TRight> Tap(Action<TRight> action)
      {
          ArgumentNullException.ThrowIfNull(action);

          if (_isRight) action(_right!);
          return this;
      }

      public Either<TLeft, TRight> TapLeft(Action<TLeft> action)
      {
          ArgumentNullException.ThrowIfNull(action);

          if (!_isRight) action(_left!);
          return this;
      }

      #endregion
}

2.4 Unit Type

// BuildingBlocks/Core/Results/Unit.cs
namespace BuildingBlocks.Core.Results;

/// <summary>
/// Unit type representing void/nothing.
/// Used in functional programming to represent operations with no return value.
/// </summary>
public readonly record struct Unit : IEquatable<Unit>, IComparable<Unit>
{
private static readonly Unit _value = new();

      public static Unit Value => _value;

      public int CompareTo(Unit other) => 0;

      public override string ToString() => "()";

      public static bool operator ==(Unit left, Unit right) => true;
      public static bool operator !=(Unit left, Unit right) => false;
      public static bool operator <(Unit left, Unit right) => false;
      public static bool operator >(Unit left, Unit right) => false;
      public static bool operator <=(Unit left, Unit right) => true;
      public static bool operator >=(Unit left, Unit right) => true;

      public static implicit operator Unit(ValueTuple _) => Value;
      public static implicit operator ValueTuple(Unit _) => default;
}

  ---
3. Error Handling System

3.1 Comprehensive Error Type

// BuildingBlocks/Core/Results/Error.cs
namespace BuildingBlocks.Core.Results;

/// <summary>
/// Immutable error representation with rich metadata and categorization.
/// Supports error chaining, metadata, and proper error taxonomy.
/// </summary>
public sealed record Error : IError
{
#region Properties

      public string Code { get; }
      public string Message { get; }
      public ErrorType Type { get; }
      public ErrorSeverity Severity { get; }
      public IReadOnlyDictionary<string, object> Metadata { get; }
      public Exception? InnerException { get; }
      public Error? InnerError { get; }
      public DateTime OccurredAt { get; }
      public string? StackTrace { get; }

      #endregion

      #region Constructor

      private Error(
          string code,
          string message,
          ErrorType type,
          ErrorSeverity severity = ErrorSeverity.Error,
          Dictionary<string, object>? metadata = null,
          Exception? innerException = null,
          Error? innerError = null,
          string? stackTrace = null)
      {
          Code = code ?? throw new ArgumentNullException(nameof(code));
          Message = message ?? throw new ArgumentNullException(nameof(message));
          Type = type;
          Severity = severity;
          Metadata = metadata?.AsReadOnly() ?? new Dictionary<string, object>().AsReadOnly();
          InnerException = innerException;
          InnerError = innerError;
          OccurredAt = DateTime.UtcNow;
          StackTrace = stackTrace ?? Environment.StackTrace;
      }

      #endregion

      #region Factory Methods - Validation Errors

      public static Error Validation(string code, string message)
          => new(code, message, ErrorType.Validation, ErrorSeverity.Warning);

      public static Error ValidationField(string field, string message)
          => new($"VALIDATION_{field.ToUpperInvariant()}",
                 message,
                 ErrorType.Validation,
                 ErrorSeverity.Warning,
                 new Dictionary<string, object> { ["Field"] = field });

      public static Error ValidationRequired(string field)
          => ValidationField(field, $"{field} is required");

      public static Error ValidationInvalid(string field, object? value = null)
      {
          var metadata = new Dictionary<string, object> { ["Field"] = field };
          if (value != null) metadata["Value"] = value;

          return new($"INVALID_{field.ToUpperInvariant()}",
                     $"{field} has invalid value",
                     ErrorType.Validation,
                     ErrorSeverity.Warning,
                     metadata);
      }

      public static Error ValidationRange(string field, object min, object max, object actual)
          => new($"OUT_OF_RANGE_{field.ToUpperInvariant()}",
                 $"{field} must be between {min} and {max}, but was {actual}",
                 ErrorType.Validation,
                 ErrorSeverity.Warning,
                 new Dictionary<string, object>
                 {
                     ["Field"] = field,
                     ["Min"] = min,
                     ["Max"] = max,
                     ["Actual"] = actual
                 });

      #endregion

      #region Factory Methods - Not Found Errors

      public static Error NotFound(string resource, object id)
          => new("NOT_FOUND",
                 $"{resource} with id '{id}' was not found",
                 ErrorType.NotFound,
                 ErrorSeverity.Warning,
                 new Dictionary<string, object>
                 {
                     ["Resource"] = resource,
                     ["Id"] = id
                 });

      public static Error NotFound(string message)
          => new("NOT_FOUND", message, ErrorType.NotFound, ErrorSeverity.Warning);

      #endregion

      #region Factory Methods - Conflict Errors

      public static Error Conflict(string message)
          => new("CONFLICT", message, ErrorType.Conflict);

      public static Error AlreadyExists(string resource, object id)
          => new("ALREADY_EXISTS",
                 $"{resource} with id '{id}' already exists",
                 ErrorType.Conflict,
                 ErrorSeverity.Warning,
                 new Dictionary<string, object>
                 {
                     ["Resource"] = resource,
                     ["Id"] = id
                 });

      public static Error ConcurrencyConflict(string resource, object id, long expectedVersion, long actualVersion)
          => new("CONCURRENCY_CONFLICT",
                 $"Concurrency conflict on {resource} with id '{id}'",
                 ErrorType.Conflict,
                 ErrorSeverity.Warning,
                 new Dictionary<string, object>
                 {
                     ["Resource"] = resource,
                     ["Id"] = id,
                     ["ExpectedVersion"] = expectedVersion,
                     ["ActualVersion"] = actualVersion
                 });

      #endregion

      #region Factory Methods - Authorization Errors

      public static Error Unauthorized(string message = "Unauthorized access")
          => new("UNAUTHORIZED", message, ErrorType.Unauthorized, ErrorSeverity.Warning);

      public static Error Forbidden(string message = "Access forbidden")
          => new("FORBIDDEN", message, ErrorType.Forbidden, ErrorSeverity.Warning);

      public static Error InsufficientPermissions(string operation, string resource)
          => new("INSUFFICIENT_PERMISSIONS",
                 $"Insufficient permissions to {operation} {resource}",
                 ErrorType.Forbidden,
                 ErrorSeverity.Warning,
                 new Dictionary<string, object>
                 {
                     ["Operation"] = operation,
                     ["Resource"] = resource
                 });

      #endregion

      #region Factory Methods - Business Rule Errors

      public static Error BusinessRule(string code, string message)
          => new(code, message, ErrorType.BusinessRule);

      public static Error InvalidOperation(string operation, string reason)
          => new($"INVALID_{operation.ToUpperInvariant()}",
                 $"Cannot perform {operation}: {reason}",
                 ErrorType.BusinessRule);

      public static Error PreConditionFailed(string condition)
          => new("PRECONDITION_FAILED",
                 $"Precondition failed: {condition}",
                 ErrorType.BusinessRule);

      #endregion

      #region Factory Methods - External Service Errors

      public static Error External(string service, string message, Exception? ex = null)
          => new($"EXTERNAL_{service.ToUpperInvariant()}",
                 message,
                 ErrorType.External,
                 ErrorSeverity.Error,
                 new Dictionary<string, object> { ["Service"] = service },
                 ex);

      public static Error Timeout(string operation, int timeoutMs)
          => new("TIMEOUT",
                 $"Operation '{operation}' timed out after {timeoutMs}ms",
                 ErrorType.External,
                 ErrorSeverity.Error,
                 new Dictionary<string, object>
                 {
                     ["Operation"] = operation,
                     ["TimeoutMs"] = timeoutMs
                 });

      public static Error ServiceUnavailable(string service)
          => new($"{service.ToUpperInvariant()}_UNAVAILABLE",
                 $"{service} service is currently unavailable",
                 ErrorType.External,
                 ErrorSeverity.Error);

      #endregion

      #region Factory Methods - Internal Errors

      public static Error Internal(string message, Exception? ex = null)
          => new("INTERNAL_ERROR",
                 message,
                 ErrorType.Internal,
                 ErrorSeverity.Critical,
                 innerException: ex);

      public static Error FromException(Exception ex)
      {
          ArgumentNullException.ThrowIfNull(ex);

          return new(ex.GetType().Name.ToUpperInvariant(),
                     ex.Message,
                     ErrorType.Internal,
                     ErrorSeverity.Critical,
                     new Dictionary<string, object>
                     {
                         ["ExceptionType"] = ex.GetType().FullName!,
                         ["Source"] = ex.Source ?? "Unknown"
                     },
                     ex);
      }

      public static Error Unexpected(string context, Exception? ex = null)
          => new("UNEXPECTED_ERROR",
                 $"An unexpected error occurred in {context}",
                 ErrorType.Internal,
                 ErrorSeverity.Critical,
                 new Dictionary<string, object> { ["Context"] = context },
                 ex);

      #endregion

      #region Factory Methods - Special Cases

      public static Error Cancelled(string operation = "Operation")
          => new("CANCELLED",
                 $"{operation} was cancelled",
                 ErrorType.Cancelled,
                 ErrorSeverity.Info);

      public static Error None()
          => new("NONE", "No error", ErrorType.None, ErrorSeverity.Info);

      #endregion

      #region Fluent API

      public Error WithMetadata(string key, object value)
      {
          var newMetadata = new Dictionary<string, object>(Metadata) { [key] = value };
          return this with { Metadata = newMetadata.AsReadOnly() };
      }

      public Error WithMetadata(IEnumerable<KeyValuePair<string, object>> metadata)
      {
          var newMetadata = new Dictionary<string, object>(Metadata);
          foreach (var kvp in metadata)
          {
              newMetadata[kvp.Key] = kvp.Value;
          }
          return this with { Metadata = newMetadata.AsReadOnly() };
      }

      public Error WithInnerError(Error inner)
          => this with { InnerError = inner };

      public Error WithSeverity(ErrorSeverity severity)
          => this with { Severity = severity };

      public Error WithStackTrace(string stackTrace)
          => this with { StackTrace = stackTrace };

      #endregion

      #region Helpers

      public bool IsValidation => Type == ErrorType.Validation;
      public bool IsNotFound => Type == ErrorType.NotFound;
      public bool IsConflict => Type == ErrorType.Conflict;
      public bool IsUnauthorized => Type == ErrorType.Unauthorized;
      public bool IsForbidden => Type == ErrorType.Forbidden;
      public bool IsBusinessRule => Type == ErrorType.BusinessRule;
      public bool IsExternal => Type == ErrorType.External;
      public bool IsInternal => Type == ErrorType.Internal;
      public bool IsCancelled => Type == ErrorType.Cancelled;

      public bool IsCritical => Severity == ErrorSeverity.Critical;
      public bool IsError => Severity == ErrorSeverity.Error;
      public bool IsWarning => Severity == ErrorSeverity.Warning;
      public bool IsInfo => Severity == ErrorSeverity.Info;

      #endregion

      #region ToString

      public override string ToString()
      {
          var sb = new StringBuilder();
          sb.AppendLine($"[{Type}] {Code}: {Message}");

          if (Metadata.Any())
          {
              sb.AppendLine("Metadata:");
              foreach (var kvp in Metadata)
              {
                  sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
              }
          }

          if (InnerError != null)
          {
              sb.AppendLine($"Inner Error: {InnerError}");
          }

          if (InnerException != null)
          {
              sb.AppendLine($"Inner Exception: {InnerException.Message}");
          }

          return sb.ToString();
      }

      #endregion
}

/// <summary>
/// Error type categorization for proper handling and HTTP status mapping
/// </summary>
public enum ErrorType
{
None = 0,
Validation = 1,
NotFound = 2,
Conflict = 3,
Unauthorized = 4,
Forbidden = 5,
BusinessRule = 6,
External = 7,
Internal = 8,
Cancelled = 9
}

/// <summary>
/// Error severity for logging and alerting
/// </summary>
public enum ErrorSeverity
{
Info = 0,
Warning = 1,
Error = 2,
Critical = 3
}

3.2 Error Extensions

// BuildingBlocks/Core/Results/ErrorExtensions.cs
namespace BuildingBlocks.Core.Results;

public static class ErrorExtensions
{
/// <summary>
/// Maps error type to HTTP status code
/// </summary>
public static int ToHttpStatusCode(this Error error)
{
return error.Type switch
{
ErrorType.Validation => 400,
ErrorType.NotFound => 404,
ErrorType.Conflict => 409,
ErrorType.Unauthorized => 401,
ErrorType.Forbidden => 403,
ErrorType.BusinessRule => 422,
ErrorType.External => 502,
ErrorType.Internal => 500,
ErrorType.Cancelled => 499,
_ => 500
};
}

      /// <summary>
      /// Converts error to ProblemDetails for API responses
      /// </summary>
      public static ProblemDetails ToProblemDetails(this Error error, string? instance = null)
      {
          var problemDetails = new ProblemDetails
          {
              Title = error.Code,
              Detail = error.Message,
              Status = error.ToHttpStatusCode(),
              Type = $"https://api.axon.com/errors/{error.Code.ToLowerInvariant()}",
              Instance = instance
          };

          foreach (var (key, value) in error.Metadata)
          {
              problemDetails.Extensions[key] = value;
          }

          if (error.InnerError != null)
          {
              problemDetails.Extensions["innerError"] = error.InnerError.ToProblemDetails();
          }

          return problemDetails;
      }

      /// <summary>
      /// Flattens error hierarchy into a list
      /// </summary>
      public static IEnumerable<Error> Flatten(this Error error)
      {
          yield return error;

          if (error.InnerError != null)
          {
              foreach (var inner in error.InnerError.Flatten())
              {
                  yield return inner;
              }
          }
      }

      /// <summary>
      /// Gets the root cause error
      /// </summary>
      public static Error GetRootCause(this Error error)
      {
          var current = error;
          while (current.InnerError != null)
          {
              current = current.InnerError;
          }
          return current;
      }
}

  ---