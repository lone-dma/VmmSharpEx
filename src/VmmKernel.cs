namespace VmmSharpEx
{
    /// <summary>
    /// The kernel class gives easy access to:
    /// - The system process(pid 4).
    /// - Kernel build number.
    /// - Kernel debug symbols(nt).
    /// </summary>
    public sealed class VmmKernel
    {
        #region Base Functionality

        private readonly Vmm _hVmm;

        private VmmKernel()
        {
            ;
        }

        internal VmmKernel(Vmm hVmm)
        {
            this._hVmm = hVmm;
        }

        /// <summary>
        /// ToString override.
        /// </summary>
        public override string ToString()
        {
            return "VmmKernel";
        }

        #endregion

        #region Specific Functionality

        /// <summary>
        /// The system process (PID 4).
        /// </summary>
        /// <returns>The system process (PID 4).</returns>
        public VmmProcess Process => new VmmProcess(_hVmm, 4);

        /// <summary>
        /// Build number of the current kernel / system.
        /// </summary>
        /// <returns>The build number of the kernel on success, 0 on fail.</returns>
        public uint Build => (uint)_hVmm.GetConfig(Vmm.CONFIG_OPT_WIN_VERSION_BUILD);

        /// <summary>
        /// Retrieve the VmmPdb object for the kernel "nt" debug symbols.
        /// </summary>
        /// <returns></returns>
        public VmmPdb Pdb => new VmmPdb(_hVmm, "nt");

        #endregion
    }
}
