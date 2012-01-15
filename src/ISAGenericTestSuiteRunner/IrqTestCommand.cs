using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using HDLToolkit;

namespace ISAGenericTestSuiteRunner
{
	public class IrqTestCommand : TestCommand
	{
		private int mask;
		private int val;
		
		private static Regex irqOperation = new Regex("(?<mask>.*?)->(?<value>.*)", RegexOptions.IgnoreCase);
		
		public IrqTestCommand (TestBench testBench, int addr, int cycles, string parameters)
			: base(testBench, addr, cycles, parameters)
		{
			Match m = irqOperation.Match(Parameters);
			if (m.Success) {
				if (
					int.TryParse(m.Groups["mask"].Value.Trim(), out this.mask) &&
					int.TryParse(m.Groups["value"].Value.Trim(), out this.val)
				) return;
			}
			
			Console.WriteLine("Malformed irq action:" + Parameters);
		}

		public override void Execute(Processor proc)
		{
				proc.SetIrqs(mask, val);				
		}
		
	}
}

