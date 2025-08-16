using System.Runtime.InteropServices;
using VmmSharpEx.Internal;
using VmmSharpEx.Options;

namespace VmmSharpEx
{
    /// <summary>
    /// Wrapper for VMM memory callbacks. Disposes of the callback when no longer needed via <see cref="Unregister"/> or <see cref="Dispose"/>.
    /// Can only have one callback of each type registered at a time.
    /// </summary>
    public sealed class VmmMemCallback : IDisposable
    {
        private readonly Vmm _vmm;
        private readonly VmmMemCallbackType _type;
        private readonly Vmm.VmmMemCallbackFn _cb; // Root the delegate to prevent it from being garbage collected.
        private bool _disposed;

        internal VmmMemCallback(Vmm vmm, VmmMemCallbackType type, Vmm.VmmMemCallbackFn cb, IntPtr context = 0)
        {
            _vmm = vmm;
            _type = type;
            _cb = cb;
            var cbPtr = Marshal.GetFunctionPointerForDelegate(_cb);
            if (!Vmmi.VMMDLL_MemCallback(vmm, type, context, cbPtr))
                throw new VmmException("Failed to register memory callback!");
        }

        /// <summary>
        /// Unregisters the memory callback.
        /// </summary>
        public void Unregister() => Dispose();

        ~VmmMemCallback() => Dispose(false);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (Interlocked.Exchange(ref _disposed, true) == false)
            {
                // Unregister the callback
                _ = Vmmi.VMMDLL_MemCallback(_vmm, _type, IntPtr.Zero, IntPtr.Zero);
            }
        }
    }
}
