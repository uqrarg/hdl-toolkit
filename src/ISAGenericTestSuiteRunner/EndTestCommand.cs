using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ISAGenericTestSuiteRunner
{
	public class EndTestCommand : TestCommand
	{
		public EndTestCommand(TestBench testBench, int addr, int cycles, string parameters)
			: base(testBench, addr, cycles, parameters)
		{
		}

		public override void Execute(Processor proc)
		{
			throw new EndTestCommandExc();
		}
		
		public class EndTestCommandExc : Exception {}
	}
}
