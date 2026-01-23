using VmmSharpEx.Options;

namespace VmmSharpEx.Scatter
{
    /// <summary>
    /// Interface defining common Scatter Operations APIs.
    /// </summary>
    public interface IScatter : IDisposable
    {
        /// <summary>
        /// Execute any prepared read, and/or write operations.
        /// </summary>
        void Execute();
    }

    /// <summary>
    /// Interface defining discrete Scatter Operations APIs with a strongly-typed 'Create' factory method.
    /// </summary>
    /// <typeparam name="TSelf"><see cref="IScatter"/> type.</typeparam>
    public interface IScatter<TSelf> : IScatter
        where TSelf : IScatter<TSelf>
    {
        /// <summary>
        /// Creates a new instance of the scatter object.
        /// </summary>
        /// <param name="vmm">Vmm instance to own this scatter object.</param>
        /// <param name="pid">Process ID (PID) to perform scatter operations within.</param>
        /// <param name="flags">Vmm flags for scatter operations.</param>
        /// <returns>Fully initialized scatter object.</returns>
        static abstract TSelf Create(Vmm vmm, uint pid, VmmFlags flags);
    }
}
