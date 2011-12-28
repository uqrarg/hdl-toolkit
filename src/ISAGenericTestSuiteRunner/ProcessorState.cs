using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HDLToolkit.Xilinx.Simulation;

namespace ISAGenericTestSuiteRunner
{
	public class ProcessorState
	{
		public struct ProgramCounterState
		{
			public int Value { get; set; }
			public bool Valid { get; set; }
		}

		public int PC { get; set; }

		public int[] GpRegisters { get; set; }
		public int[] SpRegisters { get; set; }

		public ProcessorState()
		{
		}
	}
}
