using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Heijden.Dns.Portable;
using Heijden.DNS;

namespace TestDnsPortable
{
    public class DnsTest
    {
        private readonly Resolver _resolver;

        public DnsTest()
        {
            _resolver = new Resolver();
            _resolver.Recursion = true;
            _resolver.UseCache = true;
            //await _resolver.SetDnsServer("8.8.8.8"); // Google Public DNS

            _resolver.TimeOut = 1000;
            _resolver.Retries = 3;
            _resolver.TransportType = TransportType.Tcp;
        }

        public async Task<IList<RecordSRV>> SrvRecords(string name)
        {
            const QType qType = QType.SRV;
            const QClass qClass = QClass.IN;

            var response = await _resolver.Query(name, qType, qClass);

            return response.RecordsSRV;
        }

        public async Task<IList<string>> CertRecords(string name)
        {
            IList<string> records = new List<string>();
            const QType qType = QType.CERT;
            const QClass qClass = QClass.IN;

            var response = await _resolver.Query(name, qType, qClass);
            
            foreach (var record in response.RecordsCERT)
                records.Add(record.ToString());

            return records;
        }

        public IList<string> GetQTypes()
        {
            IList<string> items = new List<string>();
            Array types = Enum.GetValues(typeof(QType));

            for (int index = 0; index < types.Length; index++)
            {
                items.Add(types.GetValue(index).ToString());
            }

            return items;
        }

        public IList<string> GetQClasses()
        {
            IList<string> items = new List<string>();
            Array types = Enum.GetValues(typeof(QClass));

            for (int index = 0; index < types.Length; index++)
            {
                items.Add(types.GetValue(index).ToString());
            }

            return items;
        }
    }
}
