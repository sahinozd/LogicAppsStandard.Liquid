#if !NET9_0_OR_GREATER
// ReSharper disable once CheckNamespace
namespace System.Threading;

/// <summary>
/// .NET 8 has no <c>System.Threading.Lock</c> (added in .NET 9). This class takes its place with a monitor lock.
/// The compiler turns <c>lock (x)</c> on a <c>System.Threading.Lock</c> into <see cref="EnterScope"/>, so the
/// same code compiles for both targets; on .NET 9 and later the real type is used.
/// </summary>
internal sealed class Lock
{
    private readonly object _gate = new();

    /// <summary>
    /// Enters the lock, and leaves it when the returned scope is disposed.
    /// </summary>
    /// <returns>The scope that holds the lock.</returns>
    public Scope EnterScope()
    {
        Monitor.Enter(_gate);
        return new Scope(_gate);
    }

    /// <summary>
    /// Holds the lock until it is disposed.
    /// </summary>
    public readonly ref struct Scope
    {
        private readonly object _gate;

        internal Scope(object gate)
        {
            _gate = gate;
        }

        /// <summary>
        /// Leaves the lock.
        /// </summary>
        public void Dispose()
        {
            Monitor.Exit(_gate);
        }
    }
}
#endif
