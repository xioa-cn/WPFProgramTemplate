using System;

// Result 将成功值与错误值封装在同一个不可变结构体中，避免使用异常表示预期失败。
namespace RestSharp;

#nullable enable

/// <summary>表示成功值（Ok）或错误值（Err）二选一的结果。</summary>
public readonly struct Result<T, E>
    where T : notnull
    where E : notnull
{
    private readonly bool _isOk;
    private readonly T _okValue;
    private readonly E _errValue;

    private Result(bool isOk, T okValue, E errValue)
    {
        _isOk = isOk;
        _okValue = okValue;
        _errValue = errValue;
    }

    public static Result<T, E> Ok(T value) => new(true, value, default!);

    /// <summary>创建错误结果。</summary>
    public static Result<T, E> Err(E error) => new(false, default!, error);

    /// <summary>指示当前是否为成功结果。</summary>
    public bool IsOk => _isOk;

    /// <summary>指示当前是否为错误结果。</summary>
    public bool IsErr => !_isOk;

    /// <summary>当成功且成功值满足谓词时返回 true。</summary>
    public bool IsOkAnd(Func<T, bool> predicate) => _isOk && predicate(_okValue);

    public T Unwrap()
    {
        if (!_isOk)
            throw new InvalidOperationException("Unwrap on Err");
        return _okValue;
    }

    /// <summary>取出错误值；当前为 Ok 时抛出异常。</summary>
    public E UnwrapErr()
    {
        if (_isOk)
            throw new InvalidOperationException("UnwrapErr on Ok");
        return _errValue;
    }

    /// <summary>取出成功值；Err 时使用指定消息抛出异常。</summary>
    public T Expect(string msg)
    {
        if (!_isOk)
            throw new InvalidOperationException(msg);
        return _okValue;
    }

    /// <summary>取出成功值；Err 时返回备用值。</summary>
    public T UnwrapOr(T defaultValue) => _isOk ? _okValue : defaultValue;

    /// <summary>取出成功值；Err 时调用备用值工厂。</summary>
    public T UnwrapOrElse(Func<E, T> fallback) => _isOk ? _okValue : fallback(_errValue);

    /// <summary>返回适合日志和调试的文本表示。</summary>
    public override string ToString() => _isOk ? $"Ok({_okValue})" : $"Err({_errValue})";

    /// <summary>判断两个 Result 的状态和值是否相等。</summary>
    public override bool Equals(object? obj) => obj is Result<T, E> other && _isOk == other._isOk &&
                                                (_isOk
                                                    ? System.Collections.Generic.EqualityComparer<T>.Default.Equals(
                                                        _okValue, other._okValue)
                                                    : System.Collections.Generic.EqualityComparer<E>.Default.Equals(
                                                        _errValue, other._errValue));

    /// <summary>返回当前 Result 的哈希值。</summary>
    public override int GetHashCode() => _isOk
        ? System.Collections.Generic.EqualityComparer<T>.Default.GetHashCode(_okValue)
        : System.Collections.Generic.EqualityComparer<E>.Default.GetHashCode(_errValue);

    // Match：调用与当前状态对应的分支，并返回分支结果。
    public TResult Match<TResult>(Func<T, TResult> okFunc, Func<E, TResult> errFunc)
        => _isOk ? okFunc(_okValue) : errFunc(_errValue);

    /// <summary>按 Ok 或 Err 分支执行操作。</summary>
    public void Match(Action<T> okAction, Action<E> errAction)
    {
        if (_isOk) okAction(_okValue);
        else errAction(_errValue);
    }

    // Map / MapErr：只转换对应分支，另一分支保持原值。
    public Result<TNew, E> Map<TNew>(Func<T, TNew> f)
        where TNew : notnull
        => _isOk ? Result<TNew, E>.Ok(f(_okValue)) : Result<TNew, E>.Err(_errValue);

    /// <summary>映射错误值，成功值原样保留。</summary>
    public Result<T, ENew> MapErr<ENew>(Func<E, ENew> f)
        where ENew : notnull
        => _isOk ? Result<T, ENew>.Ok(_okValue) : Result<T, ENew>.Err(f(_errValue));

    // AndThen / OrElse：用于串联可能失败的操作和恢复错误。
    /// <summary>成功时调用函数继续计算，错误时保留原错误。</summary>
    public Result<TNew, E> AndThen<TNew>(Func<T, Result<TNew, E>> f)
        where TNew : notnull
        => _isOk ? f(_okValue) : Result<TNew, E>.Err(_errValue);

    /// <summary>成功时保留结果，错误时调用恢复函数。</summary>
    public Result<T, ENew> OrElse<ENew>(Func<E, Result<T, ENew>> f)
        where ENew : notnull
        => _isOk ? Result<T, ENew>.Ok(_okValue) : f(_errValue);

    // 转 Option：丢弃另一分支，只保留当前分支的值。
    /// <summary>将成功值转换为 Some，错误结果转换为 None。</summary>
    public Option<T> Ok()
        => _isOk ? Option<T>.Some(_okValue) : Option<T>.None();

    /// <summary>将错误值转换为 Some，成功结果转换为 None。</summary>
    public Option<E> Err()
        => _isOk ? Option<E>.None() : Option<E>.Some(_errValue);

    // Clone：对实现 ICloneable 的值执行深复制，否则复用不可变引用。
    /// <summary>复制当前 Result。</summary>
    public Result<T, E> Clone()
    {
        if (_isOk)
        {
            var v = _okValue is ICloneable c ? (T)c.Clone()! : _okValue;
            return Ok(v);
        }
        else
        {
            var e = _errValue is ICloneable c ? (E)c.Clone()! : _errValue;
            return Err(e);
        }
    }
}