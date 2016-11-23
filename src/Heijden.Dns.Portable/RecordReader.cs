using System;
using System.Collections.Generic;
using System.Text;

namespace Heijden.DNS
{
	public class RecordReader
	{
		private byte[] m_Data;
		private int m_Position;
		public RecordReader(byte[] data)
		{
			m_Data = data;
			m_Position = 0;
		}

		public int Position
		{
			get
			{
				return m_Position;
			}
			set
			{
				m_Position = value;
			}
		}

		public RecordReader(byte[] data, int Position)
		{
			m_Data = data;
			m_Position = Position;
		}


		public byte ReadByte()
		{
			if (m_Position >= m_Data.Length)
				return 0;
		    return m_Data[m_Position++];
		}

		public char ReadChar()
		{
			return (char)ReadByte();
		}

		public UInt16 ReadUInt16()
		{
			return (UInt16)(ReadByte() << 8 | ReadByte());
		}

		public UInt16 ReadUInt16(int offset)
		{
			m_Position += offset;
			return ReadUInt16();
		}

		public UInt32 ReadUInt32()
		{
			return (UInt32)(ReadUInt16() << 16 | ReadUInt16());
		}

		public string ReadDomainName()
		{
			StringBuilder name = new StringBuilder();
			int length = 0;

			// get  the length of the first label
			while ((length = ReadByte()) != 0)
			{
				// top 2 bits set denotes domain name compression and to reference elsewhere
				if ((length & 0xc0) == 0xc0)
				{
					// work out the existing domain name, copy this pointer
					RecordReader newRecordReader = new RecordReader(m_Data, (length & 0x3f) << 8 | ReadByte());

					name.Append(newRecordReader.ReadDomainName());
					return name.ToString();
				}

				// if not using compression, copy a char at a time to the domain name
				while (length > 0)
				{
					name.Append(ReadChar());
					length--;
				}
				name.Append('.');
			}
			if (name.Length == 0)
				return ".";
			else
				return name.ToString();
		}

		public string ReadString()
		{
			short length = this.ReadByte();

			StringBuilder name = new StringBuilder();
			for(int intI=0;intI<length;intI++)
				name.Append(ReadChar());
			return name.ToString();
		}

		public byte[] ReadBytes(int intLength)
		{
            var result = new byte[intLength];
            Array.Copy(m_Data, m_Position, result, 0, intLength);
            m_Position += intLength;
            return result;
		}

		public Record ReadRecord(DnsEntryType type, int Length)
		{
			switch (type)
			{
				case DnsEntryType.A:
					return new RecordA(this);
				case DnsEntryType.NS:
					return new RecordNS(this);
				case DnsEntryType.MD:
					return new RecordMD(this);
				case DnsEntryType.MF:
					return new RecordMF(this);
				case DnsEntryType.CNAME:
					return new RecordCNAME(this);
				case DnsEntryType.SOA:
					return new RecordSOA(this);
				case DnsEntryType.MB:
					return new RecordMB(this);
				case DnsEntryType.MG:
					return new RecordMG(this);
				case DnsEntryType.MR:
					return new RecordMR(this);
				case DnsEntryType.NULL:
					return new RecordNULL(this);
				case DnsEntryType.WKS:
					return new RecordWKS(this);
				case DnsEntryType.PTR:
					return new RecordPTR(this);
				case DnsEntryType.HINFO:
					return new RecordHINFO(this);
				case DnsEntryType.MINFO:
					return new RecordMINFO(this);
				case DnsEntryType.MX:
					return new RecordMX(this);
				case DnsEntryType.TXT:
					return new RecordTXT(this, Length);
				case DnsEntryType.RP:
					return new RecordRP(this);
				case DnsEntryType.AFSDB:
					return new RecordAFSDB(this);
				case DnsEntryType.X25:
					return new RecordX25(this);
				case DnsEntryType.ISDN:
					return new RecordISDN(this);
				case DnsEntryType.RT:
					return new RecordRT(this);
				case DnsEntryType.NSAP:
					return new RecordNSAP(this);
				case DnsEntryType.NSAPPTR:
					return new RecordNSAPPTR(this);
				case DnsEntryType.SIG:
					return new RecordSIG(this);
				case DnsEntryType.KEY:
					return new RecordKEY(this);
				case DnsEntryType.PX:
					return new RecordPX(this);
				case DnsEntryType.GPOS:
					return new RecordGPOS(this);
				case DnsEntryType.AAAA:
					return new RecordAAAA(this);
				case DnsEntryType.LOC:
					return new RecordLOC(this);
				case DnsEntryType.NXT:
					return new RecordNXT(this);
				case DnsEntryType.EID:
					return new RecordEID(this);
				case DnsEntryType.NIMLOC:
					return new RecordNIMLOC(this);
				case DnsEntryType.SRV:
					return new RecordSRV(this);
				case DnsEntryType.ATMA:
					return new RecordATMA(this);
				case DnsEntryType.NAPTR:
					return new RecordNAPTR(this);
				case DnsEntryType.KX:
					return new RecordKX(this);
				case DnsEntryType.CERT:
					return new RecordCERT(this);
				case DnsEntryType.A6:
					return new RecordA6(this);
				case DnsEntryType.DNAME:
					return new RecordDNAME(this);
				case DnsEntryType.SINK:
					return new RecordSINK(this);
				case DnsEntryType.OPT:
					return new RecordOPT(this);
				case DnsEntryType.APL:
					return new RecordAPL(this);
				case DnsEntryType.DS:
					return new RecordDS(this);
				case DnsEntryType.SSHFP:
					return new RecordSSHFP(this);
				case DnsEntryType.IPSECKEY:
					return new RecordIPSECKEY(this);
				case DnsEntryType.RRSIG:
					return new RecordRRSIG(this);
				case DnsEntryType.NSEC:
					return new RecordNSEC(this);
				case DnsEntryType.DNSKEY:
					return new RecordDNSKEY(this);
				case DnsEntryType.DHCID:
					return new RecordDHCID(this);
				case DnsEntryType.NSEC3:
					return new RecordNSEC3(this);
				case DnsEntryType.NSEC3PARAM:
					return new RecordNSEC3PARAM(this);
				case DnsEntryType.HIP:
					return new RecordHIP(this);
				case DnsEntryType.SPF:
					return new RecordSPF(this);
				case DnsEntryType.UINFO:
					return new RecordUINFO(this);
				case DnsEntryType.UID:
					return new RecordUID(this);
				case DnsEntryType.GID:
					return new RecordGID(this);
				case DnsEntryType.UNSPEC:
					return new RecordUNSPEC(this);
				case DnsEntryType.TKEY:
					return new RecordTKEY(this);
				case DnsEntryType.TSIG:
					return new RecordTSIG(this);
				default:
					return new RecordUnknown(this);
			}
		}

	}
}
