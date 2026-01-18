using System.Buffers;
using VmmSharpEx;
using VmmSharpEx.Options;

namespace Issue14
{
    internal class Program
    {
        static void Main()
        {
            try
            {
                Console.WriteLine("Starting up Issue #14 Test Environment...");
                using var fpga = new FPGAConnection();
                // spawn workers, churn on scatter api
                fpga.Vmm.CreateScatter(Vmm.PID_PHYSICALMEMORY);
                fpga.Vmm.CreateScatter(Vmm.PID_PHYSICALMEMORY, VmmSharpEx.Options.VmmFlags.NOCACHE);
                int count = 0;
                for (int i = 0; i < 8; i++)
                {
                    new Thread(() =>
                    {
                        LongWorker(fpga);
                    })
                    {
                        IsBackground = true
                    }.Start();
                    Console.WriteLine($"Started LongWorker {++count}");
                }
                for (int i = 0; i < 8; i++)
                {
                    SpawnTransientWorker(fpga);
                    Console.WriteLine($"Started TransientWorker {++count}");
                }
                Thread.Sleep(Timeout.Infinite);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"*** Unhandled Exception: {ex}");
            }
            finally
            {
                Console.WriteLine("Exiting Issue #14 Test Environment; Press any key to exit.");
                Console.ReadKey(intercept: true);
            }
        }

        private static void SpawnTransientWorker(FPGAConnection fpga)
        {
            Console.WriteLine("Spawning new transient worker...");
            new Thread(() =>
            {
                TransientWorker(fpga);
            })
            {
                IsBackground = true
            }.Start();
        }

        private static void LongWorker(FPGAConnection fpga)
        {
            while (true)
            {
                try
                {
                    DoReads(fpga);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unhandled Exception in {nameof(LongWorker)}: {ex}");
                }
            }
        }

        private static void TransientWorker(FPGAConnection fpga)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2, Random.Shared.Next(6, 16)));
            while (true)
            {
                try
                {
                    cts.Token.ThrowIfCancellationRequested();
                    DoReads(fpga);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unhandled Exception in {nameof(TransientWorker)}: {ex}");
                }
            }
            SpawnTransientWorker(fpga);
        }

        private static void DoReads(FPGAConnection fpga)
        {
            try
            {
                VmmFlags flags = Random.Shared.Next(0, 2) == 0 ?
                    VmmFlags.NONE : VmmFlags.NOCACHE;
                if (Random.Shared.Next(0, 2) == 0)
                {
                    var page = fpga.GetRandomPage();
                    using var read = fpga.Vmm.MemReadPooled<byte>(Vmm.PID_PHYSICALMEMORY, page.PageBase, (int)Math.Min(page.RemainingBytesInSection, 0x1000), flags);
                    if (read is not null)
                    {
                        var v = read.Memory.Span;
                    }
                }
                else
                {
                    var pages = fpga.GetRandomPages(Random.Shared.Next(4, 4096));
                    using var s = fpga.Vmm.CreateScatter(Vmm.PID_PHYSICALMEMORY, flags);
                    foreach (var page in pages)
                    {
                        uint cb = (uint)Random.Shared.Next(4, 0x01E00000);
                        s.PrepareRead(page.PageBase, cb);
                        s.Completed += (_, s) =>
                        {
                            if (s.ReadPooled<byte>(page.PageBase, (int)cb) is IMemoryOwner<byte> pooled)
                            {
                                using (pooled)
                                {
                                    var v = pooled.Memory.Span;
                                }
                            }
                        };
                    }
                    s.Execute();
                }
            }
            finally
            {
                switch (Random.Shared.Next(0, 3))
                {
                    case 0:
                        break;
                    case 1:
                        Thread.Yield();
                        break;
                    case 2:
                        Thread.Sleep(1);
                        break;
                }
            }
        }
    }
}
