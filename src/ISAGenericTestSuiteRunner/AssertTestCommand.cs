using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using HDLToolkit;

namespace ISAGenericTestSuiteRunner
{
	public class AssertTestCommand : TestCommand
	{
		public AssertTestCommand(int addr, int cycles, string parameters)
			: base(addr, cycles, parameters)
		{
		}

		private static Regex assertionOperation = new Regex("(?<a>.*?)(?<op>(==|!=))(?<b>.*)", RegexOptions.IgnoreCase);

		public override void Execute(TestBench test, ProcessorState state)
		{
			Match m = assertionOperation.Match(Parameters);
			if (m.Success)
			{
				string a = m.Groups["a"].Value.Trim();
				string b = m.Groups["b"].Value.Trim();
				string op = m.Groups["op"].Value.Trim();
				bool passed = false;

				if (string.Compare(op, "==", true) == 0)
				{
					if (GetValueForString(a, state) == GetValueForString(b, state))
					{
						passed = true;
					}
				}
				else if (string.Compare(op, "!=", true) == 0)
				{
					if (GetValueForString(a, state) != GetValueForString(b, state))
					{
						passed = true;
					}
				}

				if (!passed)
				{
					int stride = Convert.ToInt32(Environment.GetEnvironmentVariable("ISAG_STRIDE"));
					string fmt = "X" + (Convert.ToInt32(Environment.GetEnvironmentVariable("ISAG_WORD_SIZE")) / 4);
					Logger.Instance.WriteError("Assertion failed 0x{0:"+fmt+"}@{1}, '{2}' <> '{3:"+fmt+"} {4} {5:"+fmt+"}'",
						Address * stride, CyclesAfterEvent, Parameters,
						GetValueForString(a, state), op, GetValueForString(b, state));
				}

				test.IncrementAssertionResult(passed);
				return;
			}

			Console.WriteLine("Malformed assertion! '{0}'", Parameters);
		}

		Regex register = new Regex(@"^(?<rtype>.)(?<index>\d{1,2})(\[(?<range>.*?)\])?", RegexOptions.IgnoreCase | RegexOptions.Multiline);
		Regex range = new Regex(@"(?<start>\d{1,2})(:(?<end>\d{1,2}))?", RegexOptions.IgnoreCase | RegexOptions.Multiline);
		public int GetValueForString(string str, ProcessorState state)
		{
			Exception err = new Exception("parsing exception - bad token: " + str);
			Match m = register.Match(str);
			if (m.Success)
			{
				int i = int.Parse(m.Groups["index"].Value);
				char rt = m.Groups["rtype"].Value.ElementAt(0);
				int w = 0;
				switch (rt) {
				case ('r') :
					w = state.GpRegisters[i]; break;
				case ('s') :
					w = state.SpRegisters[i]; break;
				default :
					throw err;
				}

				string rangeStr = m.Groups["range"].Value;
				if (!string.IsNullOrEmpty(rangeStr)) {
					Match k = range.Match(rangeStr);
					if (!k.Success) {
						throw err;
					}
					int start = int.Parse(k.Groups["start"].Value);
					string endStr = k.Groups["end"].Value;
					int end = start;
					if (!string.IsNullOrEmpty(endStr))
						end = int.Parse(endStr);
					return (w >> end) &  ((1 << (start - end + 1)) - 1);
				} else {
					return w;
				}
			}

			//FIXME: delete avr specific stuff
			if (str.StartsWith("sreg", StringComparison.InvariantCultureIgnoreCase))
			{
				string strToLower = str.ToLower();
				switch (strToLower)
				{
					case "sreg":
						return state.SpRegisters[0];
					case "sreg[c]": // carry
						return (state.SpRegisters[0] >> 0) & 0x1;
					case "sreg[z]": // zero
						return (state.SpRegisters[0] >> 1) & 0x1;
					case "sreg[n]": // negative
						return (state.SpRegisters[0] >> 2) & 0x1;
					case "sreg[v]": // twos comp (v)
						return (state.SpRegisters[0] >> 3) & 0x1;
					case "sreg[s]": // signed
						return (state.SpRegisters[0] >> 4) & 0x1;
					case "sreg[h]": // half carry
						return (state.SpRegisters[0] >> 5) & 0x1;
					case "sreg[t]": // temp/transfer
						return (state.SpRegisters[0] >> 6) & 0x1;
					case "sreg[i]": // instruction
						return (state.SpRegisters[0] >> 7) & 0x1;
					default:
						break;
				}
			}

			if (str.StartsWith("pc", StringComparison.InvariantCultureIgnoreCase))
				return state.PC;

			int value = 0;
			if (int.TryParse(str, out value))
			{
				return value;
			}

			if (str.StartsWith("0x"))
			{
				if (int.TryParse(str.Substring(2), NumberStyles.HexNumber, CultureInfo.CurrentCulture, out value))
				{
					return value;
				}
			}

			bool valueBool = false;
			if (bool.TryParse(str, out valueBool))
			{
				return valueBool ? 1 : 0;
			}

			throw err;
			// unknown
			return -1;
		}
	}
}
