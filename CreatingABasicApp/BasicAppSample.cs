using FirecrackerSharp.Core;
using FirecrackerSharp.Data;
using FirecrackerSharp.Data.Drives;
using FirecrackerSharp.Data.State;
using FirecrackerSharp.Host.Local;
using FirecrackerSharp.Installation;
using FirecrackerSharp.Lifecycle;

namespace CreatingABasicApp;

public static class BasicAppSample
{
    public static async Task RunAsync()
    {
        // Start by setting up the virtual host. Here, a local host is used, replace with another host if necessary
        LocalHost.Configure();
        
        // Configure the future VM
        var vmConfiguration = new VmConfiguration(
            // boot data: kernel, args, initrd
            BootSource: new VmBootSource(
                KernelImagePath: "/opt/res/kernel",
                BootArgs: "console=ttyS0 reboot=k panic=1 pci=off",
                InitrdPath: null /* we aren't using an initrd */),
            // hardware configuration
            new VmMachineConfiguration(
                MemSizeMib: 128 /* 128 MB of ram */,
                VcpuCount: 1 /* 1 vCPU */),
            // drives, here: only the rootfs
            Drives: [
                new VmDrive(
                    DriveId: "rootfs",
                    IsRootDevice: true,
                    PathOnHost: "/opt/res/rootfs.ext4")
            ]
        );

        var firecrackerInstall = new FirecrackerInstall(
            Version: "v1.7.0", // replace with the one you're using
            FirecrackerBinary: "/opt/res/firecracker",
            JailerBinary: "/opt/res/jailer");

        var firecrackerOptions = new FirecrackerOptions(
            SocketFilename: $"{Guid.NewGuid()}",
            SocketDirectory: "/tmp");

        var vmId = Random.Shared.Next(1, 10000).ToString();
        
        // Create the VM instance and boot it
        var vm = new UnrestrictedVm(vmConfiguration, firecrackerInstall, firecrackerOptions, vmId);
        var bootResult = await vm.BootAsync();
        if (bootResult.IsFailure())
        {
            Console.WriteLine("Couldn't boot VM!");
            return;
        }
        
        // Get the VM's info
        var vmInfoResponse = await vm.Management.GetInfoAsync();
        vmInfoResponse
            .IfSuccess(vmInfo =>
            {
                Console.WriteLine($"The VM is called {vmInfo.Id}");
                Console.WriteLine($"The VMM version is {vmInfo.VmmVersion}");
            })
            .IfError(error =>
            {
                Console.WriteLine($"Received an error when trying to get info: {error}");
            });
        
        // Pause and resume VM
        var pauseResponse = await vm.Management.UpdateStateAsync(new VmStateUpdate(VmStateForUpdate.Paused));
        var resumeResponse = await vm.Management.UpdateStateAsync(new VmStateUpdate(VmStateForUpdate.Resumed));
        pauseResponse
            .ChainWith(resumeResponse)
            .IfSuccess(() => Console.WriteLine("Paused and resumed VM!"))
            .IfError(error => Console.WriteLine($"Got an error when pausing and resuming: {error}"));
        
        // Run a command in the VM's TTY
        var output = await vm.TtyClient.RunBufferedCommandAsync("cat --help");
        Console.WriteLine("Received from command:");
        Console.WriteLine(output);

        // Shut down the VM
        var shutdownResult = await vm.ShutdownAsync();
        if (shutdownResult.IsFailure() || shutdownResult.IsSoftFailure())
        {
            Console.WriteLine("Couldn't shut down VM!");
        }
    }
}