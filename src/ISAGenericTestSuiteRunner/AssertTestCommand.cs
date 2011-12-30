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
		public AssertTestCommand(TestBench testBench, int addr, int cycles, string parameters)
			: base(testBench, addr, cycles, parameters)
		{
		}

		private static Regex assertionOperation = new Regex("(?<a>.*?)(?<op>(==|!=))(?<b>.*)", RegexOptions.IgnoreCase);

		public override void Execute(ProcessorState state)
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

				TestBench.IncrementAssertionResult(passed);
				return;
			}

			Console.WriteLine("Malformed assertion! '{0}'", Parameters);
		}

		
		public int GetValueForString(string str, ProcessorState state)
		{
			int value = 0;
			if (TryParseRegisterOperand(str, state, out value))
			{
				return value;
			}

			if (str.StartsWith("pc", StringComparison.InvariantCultureIgnoreCase))
			{
				return state.PC;
			}

			// Parse for a regular integer
			if (int.TryParse(str, out value))
			{
				return value;
			}

			// Parse for a hexadecimal value
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

			// Unknown value
			throw new Exception("Parsing exception - bad token: " + str);
		}

		private static Regex register = new Regex(@"^(?<type>.)(?<index>\d+)(\[(?<start>\d{1,2})(:(?<end>\d{1,2}))?\])?", RegexOptions.IgnoreCase | RegexOptions.Multiline);
		public bool TryParseRegisterOperand(string operand, ProcessorState state, out int value)
		{
			operand = TestBench.AliasManager.AliasConvertOperand(operand);
			Logger.Instance.WriteDebug("\t\tOperand aliased to '{0}'", operand);
			Match m = register.Match(operand);
			if (m.Success)
			{
				int index = 0;
				string registerType = m.Groups["type"].Value;
				bool registerValid = false;
				int registerValue = 0;

				if (int.TryParse(m.Groups["index"].Value, out index))
				{
					switch (registerType)
					{
						case "r":
							if (index < state.GpRegisters.Length)
							{
								registerValue = state.GpRegisters[index];
								registerValid = true;
							}
							break;
						case "s":
							if (index < state.SpRegisters.Length)
							{
								registerValue = state.SpRegisters[index];
								registerValid = true;
							}
							break;
					}

					if (registerValid)
					{
						// Parse the start index
						int rangeStart = 0;
						bool indexSpecified = (m.Groups["start"] != null && !string.IsNullOrEmpty(m.Groups["start"].Value));
						bool indexValid = int.TryParse(m.Groups["start"].Value, out rangeStart);
						// Parse the end index
						int rangeEnd = 0;
						bool rangeSpecified = (m.Groups["end"] != null && !string.IsNullOrEmpty(m.Groups["end"].Value));
						bool rangeValid = int.TryParse(m.Groups["end"].Value, out rangeEnd);
						rangeEnd = (rangeSpecified && rangeValid) ? rangeEnd : rangeStart;

						if (!indexSpecified)
						{
							value = registerValue;
							return true;
						}
						else if (indexSpecified && indexValid)
						{
							// Shift it down and Mask the range
							// TODO: make this work for reversed ranges
							value = (registerValue >> rangeEnd) & ((1 << (rangeStart - rangeEnd + 1)) - 1);
							return true;
						}
					}
				}
			}

			value = 0;
			return false;
		}
	}
}
