# Heijden.Dns.Portable

A reusable DNS resolver for .NET, using netstandard 1.3

[![Build status](https://ci.appveyor.com/api/projects/status/ff0wgqvoyaoktvqn/branch/master?svg=true)](https://ci.appveyor.com/project/softlion/heijden-dns/branch/master)

# Quick start

Important note: when referencing this library from a non 'project.json' app, add these nuget packages to your app too:

    System.Net.NetworkInformation v4.1.0+
    System.Net.Sockets v4.1.0+

Get SRV records:

    var resolver = new Resolver
    {
        Recursion = true,
        UseCache = true,
        TimeOut = 1000,
        Retries = 3,
        TransportType = TransportType.Tcp,
    };

    var recordsSrv = await resolver.Query(name, QType.SRV).RecordsSRV;
    
    foreach (var item in recordsSrv.OrderBy(it => it.PRIORITY).ThenBy(it => it.WEIGHT))
        Console.WriteLine($"SRV: {item.TARGET}:{item.PORT} priority:{item.PRIORITY} weight:{item.WEIGHT} ");
