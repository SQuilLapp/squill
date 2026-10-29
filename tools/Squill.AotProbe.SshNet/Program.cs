using System.Globalization;
using Squill.Core;
using Squill.Platform.Ssh;

try
{
    if (args.Length != 6)
    {
        Console.WriteLine("usage: Squill.AotProbe.SshNet <host> <port> <user> <password> <remoteHost> <remotePort>");
        return 2;
    }

    var settings = new SshTunnelSettings(args[0], int.Parse(args[1], CultureInfo.InvariantCulture), args[2], SshAuthMethod.Password, null);
    await using var tunnel = await new SshNetTunnelFactory().OpenAsync(settings, args[3], args[4], int.Parse(args[5], CultureInfo.InvariantCulture), CancellationToken.None);
    Console.WriteLine($"OK {tunnel.LocalPort}");
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine(ex);
    return 1;
}
