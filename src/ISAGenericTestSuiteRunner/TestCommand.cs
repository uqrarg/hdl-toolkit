using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ISAGenericTestSuiteRunner
{
	public abstract class TestCommand
	{
		public TestBench TestBench { get; private set; }
		public string Parameters { get; set; }

		public int Address { get; set; }
		public int CyclesAfterEvent { get; set; }

		public TestCommand(TestBench testBench, int addr, int cycles, string parameters)
		{
			TestBench = testBench;
			Address = addr;
			CyclesAfterEvent = cycles;
			Parameters = parameters;
		}

		public virtual void Execute(Processor proc)
		{

		}
		
		public class MalformedException : Exception {
			
			public MalformedException (string msg) :
				base("Malformed test Command: " + msg) {}
			
			public MalformedException (string msg, Exception inner) :
				base("Malformed test Command: " + msg, inner) {}
			
		}
	}

}
