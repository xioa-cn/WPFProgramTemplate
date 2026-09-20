using System;
using System.Collections.Generic;

#nullable enable

namespace RestSharp
{
   /// <summary>表示一个可能包含值（Some）或不包含值（None）的结果。</summary>
   public readonly struct Option<T>
        where T : notnull
    {
        private readonly bool _isSome;
        private readonly T _value;

        // 私有构造
        private Option(bool isSome, T value)
        {
            _isSome = isSome;
            _value = value;
        }

        // 静态工厂
        /// <summary>创建包含值的 Some。</summary>
        public static Option<T> Some(T value) => new(true, value);
        /// <summary>创建不包含值的 None。</summary>
        public static Option<T> None() => new(false, default!);

        // 属性
        /// <summary>指示当前是否包含值。</summary>
        public bool IsSome => _isSome;
        /// <summary>指示当前是否不包含值。</summary>
        public bool IsNone => !_isSome;

        /// <summary>当包含值且值满足谓词时返回 true。</summary>
        public bool IsSomeAnd(Func<T, bool> predicate) => _isSome && predicate(_value);

        // Unwrap / Expect
        public T Unwrap()
        {
            if (!_isSome)
                throw new InvalidOperationException("Unwrap on None");
            return _value;
        }

        /// <summary>取出值；None 时使用指定消息抛出异常。</summary>
        public T Expect(string msg)
        {
            if (!_isSome)
                throw new InvalidOperationException(msg);
            return _value;
        }

        /// <summary>取出值，None 时返回备用值。</summary>
        public T UnwrapOr(T defaultValue) => _isSome ? _value : defaultValue;
        /// <summary>取出值，None 时调用工厂生成备用值。</summary>
        public T UnwrapOrElse(Func<T> f) => _isSome ? _value : f();
        /// <summary>取出值，None 时返回类型默认值。</summary>
        public T? UnwrapOrDefault() => _isSome ? _value : default;

        /// <summary>返回值；None 时返回 null。</summary>
        public T? AsNullable() => _isSome ? _value : default;

        // Match 两个重载
        public TResult Match<TResult>(Func<T, TResult> some, Func<TResult> none)
            => _isSome ? some(_value) : none();

        /// <summary>按 Some 或 None 分支执行操作。</summary>
        public void Match(Action<T> some, Action none)
        {
            if (_isSome) some(_value);
            else none();
        }

        // Map / Inspect
        public Option<TNew> Map<TNew>(Func<T, TNew> f)
            where TNew : notnull
            => _isSome ? Option<TNew>.Some(f(_value)) : Option<TNew>.None();

        /// <summary>对 Some 值执行检查操作，并返回当前实例。</summary>
        public Option<T> Inspect(Action<T> action)
        {
            if (_isSome) action(_value);
            return this;
        }

        /// <summary>映射值；None 时返回备用值。</summary>
        public TNew MapOr<TNew>(TNew defaultValue, Func<T, TNew> f)
            => _isSome ? f(_value) : defaultValue;

        /// <summary>映射值；None 时调用备用值工厂。</summary>
        public TNew MapOrElse<TNew>(Func<TNew> defaultValueGetter, Func<T, TNew> f)
            => _isSome ? f(_value) : defaultValueGetter();

        // And / AndThen (FlatMap)
        /// <summary>当前为 Some 时返回 optB，否则返回 None。</summary>
        public Option<TNew> And<TNew>(Option<TNew> optB)
            where TNew : notnull
            => _isSome ? optB : Option<TNew>.None();

        /// <summary>当前为 Some 时调用函数继续计算，否则返回 None。</summary>
        public Option<TNew> AndThen<TNew>(Func<T, Option<TNew>> f)
            where TNew : notnull
            => _isSome ? f(_value) : Option<TNew>.None();

        // Filter
        /// <summary>仅当 Some 值满足谓词时保留，否则返回 None。</summary>
        public Option<T> Filter(Func<T, bool> predicate)
            => _isSome && predicate(_value) ? this : None();

        // Or / OrElse / Xor
        /// <summary>当前为 Some 时返回当前实例，否则返回 optB。</summary>
        public Option<T> Or(Option<T> optB)
            => _isSome ? this : optB;

        /// <summary>返回当前 Some；None 时调用备用 Option 工厂。</summary>
        public Option<T> OrElse(Func<Option<T>> f)
            => _isSome ? this : f();

        /// <summary>仅当两个 Option 恰好一个为 Some 时返回该 Some。</summary>
        public Option<T> Xor(Option<T> optB)
            => _isSome ^ optB._isSome ? (_isSome ? this : optB) : None();

        /// <summary>判断当前 Some 是否包含与指定对象相等的值。</summary>
        public bool Contains<TOther>(TOther x)
            => _isSome && object.Equals(x, _value);
        
        /// <summary>组合两个 Some 为元组；任一为 None 时返回 None。</summary>
        public Option<(T, T2)> Zip<T2>(Option<T2> other)
            where T2 : notnull
        {
            if (_isSome && other._isSome)
                return Option<(T, T2)>.Some((_value, other._value));
            return Option<(T, T2)>.None();
        }

        /// <summary>组合两个 Some 并应用映射函数。</summary>
        public Option<TR> ZipWith<T2, TR>(Option<T2> other, Func<T, T2, TR> f)
            where T2 : notnull
            where TR : notnull
        {
            if (_isSome && other._isSome)
                return Option<TR>.Some(f(_value, other._value));
            return Option<TR>.None();
        }

        // OkOr 转 Result
        public Result<T, E> OkOr<E>(E err)
            where E : notnull
            => _isSome ? Result<T, E>.Ok(_value) : Result<T, E>.Err(err);

        /// <summary>将 Some 转为 Ok；None 时调用错误工厂生成 Err。</summary>
        public Result<T, E> OkOrElse<E>(Func<E> errorGetter)
            where E : notnull
            => _isSome ? Result<T, E>.Ok(_value) : Result<T, E>.Err(errorGetter());

        // Clone（强类型，不使用ICloneable）
        /// <summary>复制当前 Option。</summary>
        public Option<T> Clone()
        {
            if (!_isSome) return None();
            if (_value is ICloneable c)
                return Some((T)c.Clone()!);
            return Some(_value);
        }

        /// <summary>返回适合日志和调试的文本表示。</summary>
        public override string ToString() => _isSome ? $"Some({_value})" : "None";

        /// <summary>判断两个 Option 的状态和值是否相等。</summary>
        public override bool Equals(object? obj) => obj is Option<T> other &&
            _isSome == other._isSome && (!_isSome || EqualityComparer<T>.Default.Equals(_value, other._value));

        /// <summary>返回当前 Option 的哈希值。</summary>
        public override int GetHashCode() => _isSome ? EqualityComparer<T>.Default.GetHashCode(_value) : 0;
    }
}
