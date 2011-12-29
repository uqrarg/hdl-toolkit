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

		public static string PreProcessTestBench(string fileContents, List<string> includeDirectories)
		{
			Logger.Instance.WriteVerbose("Preprocessing testbench with gcc");

			List<string> arguments = new List<string>();
			arguments.Add("-E -P -C -w"); // standard preprocessing flags
			// Include directories
			foreach (string include in includeDirectories)
			{
				arguments.Add("-I" + include);
			}

			string input = PathHelper.Combine(SystemHelper.GetTemporaryDirectory(), "isag-tb-" + Guid.NewGuid().ToString() + ".S");
			string output = input + ".e";

			arguments.Add("-x assembler-with-cpp " + input); // file input flags
			arguments.Add("-o " + output);

			File.WriteAllText(input, fileContents);

			ProcessHelper.ProcessExecutionResult result = ProcessHelper.ExecuteProcess(
				Environment.CurrentDirectory, Environment.GetEnvironmentVariable("CROSS_COMPILE") + "gcc", arguments);

			Logger.Instance.WriteDebug(result.StandardError);
			Logger.Instance.WriteDebug(result.StandardOutput);

			string preprocessedContents = File.ReadAllText(output);

			File.Delete(input);
			File.Delete(output);

			return preprocessedContents;
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

			code.Seek(0, SeekOrigin.Begin);

			int currentBlockIndex = 0;
			byte[] currentBlock = new byte[numBytesPerBlock];
			int currentData = 0;
			while ((currentData = code.ReadByte()) != -1)
			{
				// Fill the array from the right to the left (right side = LSB)
				currentBlock[numBytesPerBlock - ++currentBlockIndex] = (byte)currentData;
				// Block is complete
				if (currentBlockIndex == numBytesPerBlock)
				{
					currentBlockIndex = 0;

					// Swap the order for big endian systems
					if (bigEndian)
					{
						Array.Reverse(currentBlock);
					}
					data.AppendLine(string.Format("\t\t\tipif_addr_data_pair_format(x\"{0:X8}\", x\"{1}\"),",
						currentAddress, StringHelpers.BytesToString(currentBlock, 0, numBytesPerBlock)));
					currentAddress += addrStride;
				}
			}

			// FIXME: this is a hack because of VHDLs inability to support single element arrays?
			data.AppendLine(string.Format("\t\t\tipif_addr_data_pair_format(x\"{0:X8}\", x\"{1}\")",
				-1, StringHelpers.BytesToString(new byte[numBytesPerBlock], 0, numBytesPerBlock)));

			return template.Replace("##DATAARRAY", data.ToString());
		}
	}
}
