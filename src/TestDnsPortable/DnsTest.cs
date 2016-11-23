using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Heijden.Dns.Portable;
using Heijden.DNS;

namespace TestDnsPortable
{
    public class DnsTest
    {
        private readonly Resolver resolver;

        public DnsTest()
        {
            resolver = new Resolver
            {
                Recursion = true,
                UseCache = true,
                TimeOut = 1000,
                Retries = 3,
                TransportType = TransportType.Tcp,
            };

            //await _resolver.SetDnsServer("8.8.8.8"); // Google Public DNS
        }

        public async Task<IList<RecordSRV>> SrvRecords(string name)
        {
            var response = await resolver.Query(name, QType.SRV);
            return response.RecordsSRV;
        }

        public async Task<IList<string>> CertRecords(string name)
        {
            var response = await resolver.Query(name, QType.CERT, QClass.IN);
            return response.RecordsCERT.Select(record => record.ToString()).ToList();
        }

        public IList<string> GetQTypes()
        {
            var types = Enum.GetValues(typeof(QType));
            return types.Cast<object>().Select((t, index) => types.GetValue(index).ToString()).ToList();
        }

        public IList<string> GetQClasses()
        {
            var types = Enum.GetValues(typeof(QClass));
            return types.Cast<object>().Select((t, index) => types.GetValue(index).ToString()).ToList();
        }
    }
}
