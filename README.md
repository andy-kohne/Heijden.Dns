# Heijden.Dns.Portable

A reusable DNS resolver for .NET, using netstandard 1.3

[![Build status](https://ci.appveyor.com/api/projects/status/ff0wgqvoyaoktvqn/branch/master?svg=true)](https://ci.appveyor.com/project/softlion/heijden-dns/branch/master)

# Quick start

Add this library to your pcl project.

Important note: if the project referencing this library and/or the startup project of your solution is not using 'project.json' (ie: it uses packages.config), then you need to add the following nuget packages too, otherwise strange errors/crashes will occur:

    System.Net.NetworkInformation v4.3.0+
    System.Net.Sockets v4.3.0+

# Sample Usage

Get SRV records:

    var domainName = "_autoconfig._tcp.local.";
    var resolver = new Resolver { TransportType = TransportType.Tcp };
    var recordsSrv = await resolver.Query(domainName, QType.SRV).RecordsSRV;
    foreach (var item in recordsSrv.OrderBy(it => it.PRIORITY).ThenBy(it => it.WEIGHT))
        Console.WriteLine($"SRV: {item.TARGET}:{item.PORT} priority:{item.PRIORITY} weight:{item.WEIGHT} ");

# Note on Android

There is a bug in the on Android: it does not detect the dns addresses.  
Use this code instead then set the dns server address:

        {...
                var resolver = new Resolver
                {
                    TransportType = TransportType.Tcp,
                    DnsServers = GetDnsServersOnAndroid().ToArray()
                };
        ...}

        public static IEnumerable<IPEndPoint> GetDnsServersOnAndroid()
        {
            var dnsEntries = new List<string>();
            try
            {
                var systemPropertiesClass = Java.Lang.Class.ForName("android.os.SystemProperties");
                var getMethod = systemPropertiesClass.GetMethod("get", Java.Lang.Class.FromType(typeof(Java.Lang.String)));
                //var serialnumber = (Java.Lang.String)getMethod.Invoke(systemPropertiesClass, "ril.serialnumber");
                for (var i = 1; i < 8; i++)
                {
                    var value = ((Java.Lang.String)getMethod.Invoke(null, $"net.dns{i}"))?.ToString();
                    if (!String.IsNullOrEmpty(value) && !dnsEntries.Contains(value))
                        dnsEntries.Add(value);
                }
            }
            catch (Java.Lang.Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
            }

            return dnsEntries.Select(entry => new IPEndPoint(IPAddress.Parse(entry), 53)).ToList();
        }

