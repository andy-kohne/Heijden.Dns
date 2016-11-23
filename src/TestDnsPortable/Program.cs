using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TestDnsPortable
{
    class Program
    {
        static void Main(string[] args)
        {
            var dontWait = Run();
            Console.ReadKey();
        }

        static async Task Run()
        {
            var dnsTest = new DnsTest();

            //var r = await dnsTest.CertRecords("direct.sitenv.org")
            //    WriteList(r.ToList());

            var name = "_sip._udp.sip.voice.google.com";
            Console.WriteLine($"SRV records for {name}");
            var r = await dnsTest.SrvRecords(name);
            foreach (var item in r.OrderBy(it => it.PRIORITY).ThenBy(it => it.WEIGHT))
                Console.WriteLine($"SRV: {item.TARGET}:{item.PORT} priority:{item.PRIORITY} weight:{item.WEIGHT} ");

            //Console.WriteLine("Available QTypes");
            //WriteList(dnsTest.GetQTypes());

            //Console.WriteLine("Available QClasses");
            //WriteList(dnsTest.GetQClasses());

            Console.WriteLine("Finished");
        }

        private static void WriteList(List<string> list)
        {
            if(list.Count == 0)
                Console.WriteLine("no result");

            foreach (string item in list)
                Console.WriteLine(item);
        }
    }
}
