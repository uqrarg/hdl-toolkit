using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using HDLToolkit;

namespace ISAGenericTestSuiteRunner
{
	public static class TestBenchGenerator
	{
		public static string GenerateTestBench(TestBench test, string workingDirectory, string templateFile)
		{
			string assemblyFile = PathHelper.Combine(workingDirectory, "test.s");
			Logger.Instance.WriteVerbose("Generating Assembly file");
			File.WriteAllText(assemblyFile, test.GenerateAssembly());

			MemoryStream code = GenerateMachineCode(workingDirectory, assemblyFile);

			return TestBenchTemplate(code, templateFile);
		}

		public static MemoryStream GenerateMachineCode(string workingDirectory, string asmFile)
		{
			Logger.Instance.WriteVerbose("Generating Machine code from assembly file using gcc");
			ProcessHelper.ProcessExecutionResult result = ProcessHelper.ExecuteProcess(workingDirectory,
				Environment.GetEnvironmentVariable("CROSS_COMPILE") + "gcc",
				"-x assembler-with-cpp \"" + Path.GetFullPath(asmFile) + "\" -nostartfiles -nodefaultlibs");

			if (!File.Exists(PathHelper.Combine(workingDirectory, "a.out")))
			{
				Logger.Instance.WriteError(result.StandardError);
			}

			Logger.Instance.WriteVerbose("Generating binary output from elf");
			ProcessHelper.ExecuteProcess(workingDirectory,
				Environment.GetEnvironmentVariable("CROSS_COMPILE") + "objcopy",
				"-O binary a.out a.bin");

			Logger.Instance.WriteVerbose("Reading in binary machine code");
			MemoryStream stream = new MemoryStream();
			using (FileStream reader = new FileStream(PathHelper.Combine(workingDirectory, "a.bin"), FileMode.Open, FileAccess.Read))
			{
				int b = 0;
				while ((b = reader.ReadByte()) != -1)
				{
					stream.WriteByte((byte)b);
				}
			}

			return stream;
		}

		public static string TestBenchTemplate(MemoryStream code, string templateFile)
		{
			StringBuilder data = new StringBuilder();
			string template = File.ReadAllText(templateFile);
			int currentAddress = 0;

			Logger.Instance.WriteVerbose("Generating VHDL Testbench");

			//FIXME: these should be command line arguments and not environment variables
			int numBytesPerBlock = Convert.ToInt32(Environment.GetEnvironmentVariable("ISAG_INSTR_SIZE_BYTES"));
			int addrStride = Convert.ToInt32(Environment.GetEnvironmentVariable("ISAG_STRIDE"));
			bool bigEndian = Convert.ToInt32(Environment.GetEnvironmentVariable("ISAG_BIG_ENDIAN")) == 1;
			string fmt = "X" + (numBytesPerBlock * 2);
			
			code.Seek(0, SeekOrigin.Begin);

			int currentBlockIndex = 0;
			ulong currentBlock = 0;
			int currentData = 0;
			while ((currentData = code.ReadByte()) != -1)
			{
				//assumes little endian
				currentBlock = bigEndian ?
					(currentBlock << 8) | (ulong)currentData :
					(currentBlock >> 8) | ((ulong)currentData << ((numBytesPerBlock-1) * 8));
				currentBlockIndex++;
				if (currentBlockIndex == numBytesPerBlock)
				{
					currentBlockIndex = 0;
					data.AppendLine(string.Format("\t\t\tipif_addr_data_pair_format(x\"{0:X8}\", x\"{1:"+fmt+"}\"),", currentAddress, currentBlock));
					currentAddress += addrStride;
					currentBlock = 0;
				}
			}

			// FIXME: this is a hack because of VHDLs inability to support single element arrays?
			data.AppendLine(string.Format("\t\t\tipif_addr_data_pair_format(x\"{0:X8}\", x\"{1:"+fmt+"}\")", -1, 0));

			return template.Replace("##DATAARRAY", data.ToString());
		}
	}
}
