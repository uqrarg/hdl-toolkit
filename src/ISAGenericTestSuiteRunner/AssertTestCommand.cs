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
		enum Operation { EQ, NE }
		
		private Processor.Property a;
		private Processor.Property b;
		private Operation o;
		
		//FIXME: implement the rest of the relational operators
		private static Regex assertionOperation = new Regex("(?<a>.*?)(?<op>(==|!=))(?<b>.*)", RegexOptions.IgnoreCase);
		
		public AssertTestCommand(TestBench testBench, int addr, int cycles, string parameters)
			: base(testBench, addr, cycles, parameters)
		{
			Match m = assertionOperation.Match(parameters);
			if (!m.Success)
				throw new TestCommand.MalformedException("Bad Assert Command Syntax");
			try {
				Processor.Property.TryParse(m.Groups["a"].Value.Trim(), out a);
				Processor.Property.TryParse(m.Groups["b"].Value.Trim(), out b);
			} catch (Processor.Property.ParseException e) {
				throw new TestCommand.MalformedException("Invalid operand", e);
			}
			string op = m.Groups["op"].Value.Trim();
				
			if (string.Compare(op, "==", true) == 0) {
				o = Operation.EQ;
			} else if (string.Compare(op, "!=", true) == 0)	{
				o = Operation.NE;
			} else {
				//TODO: sanitise error messages
				throw new TestCommand.MalformedException("Invalid Assertion operator" + o);
			}
			
		}

		//FIXME: move this regex stuff to the constructor
		public override void Execute(Processor proc)
		{
			int a = this.a.Evaluate(proc);
			int b = this.b.Evaluate(proc);
			
			bool passed;
			
			switch (o) {
			case Operation.EQ :	passed = a == b; break;
			case Operation.NE : passed = a != b; break;
			default : passed = false; break;
			}
			
			if (!passed)
			{
				int stride = Convert.ToInt32(Environment.GetEnvironmentVariable("ISAG_STRIDE"));
				string fmt = "X" + (Convert.ToInt32(Environment.GetEnvironmentVariable("ISAG_WORD_SIZE")) / 4);
				Logger.Instance.WriteError("Assertion failed 0x{0:"+fmt+"}@{1}, '{2}' <> '{3:"+fmt+"} {4} {5:"+fmt+"}'",
					Address * stride, CyclesAfterEvent, Parameters,
					a, OpToString(o), b);
			}

			TestBench.IncrementAssertionResult(passed);
			return;

		}
		
		private static string OpToString (Operation o) {
			switch (o) {
			case Operation.EQ : return "==";
			case Operation.NE : return "!=";
			default : return "??";
			}
		}

	}
}
