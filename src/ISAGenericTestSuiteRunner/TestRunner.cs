using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HDLToolkit;
using HDLToolkit.Xilinx.Simulation;
using System.IO;
using HDLToolkit.Xilinx;
using HDLToolkit.Framework;

namespace ISAGenericTestSuiteRunner
{
	public class TestRunner
	{
		public string TestBenchPath { get; set; }
		public List<string> TestBenchIncludes { get; set; }
		public bool GuiEnabled { get; set; }
		public XilinxRepository Repository { get; set; }

		TestBench bench;
		ISimSimulator simulator;
		Processor proc;

		// Paths
		private string workingDirectory;
		private string isagLibPath;
		private string isagTestPath;
		private string pregenPrjFile;
		private string fileTest;
		private string fileTemplate;
		private string fileTemplateBuilt;
		private string simulationExe;

		public TestRunner(XilinxRepository repo, string testBenchPath)
		{
			TestBenchPath = testBenchPath;
			TestBenchIncludes = new List<string>();
			Repository = repo;
		}

		private void CleanUp()
		{
			if (simulator != null)
			{
				// Stop processes
				if (simulator.Running)
				{
					simulator.Kill();
					simulator.WaitForExit();
				}
				simulator = null;
				proc = null;
			}

			if (workingDirectory != null)
			{
				// Clean up
				Directory.Delete(workingDirectory, true);
				workingDirectory = null;
			}
		}

		private void SetupPaths()
		{
			workingDirectory = SystemHelper.GetTemporaryDirectory();

			isagLibPath = Repository.GetLibraryDefaultRootPath("isa_generic_v2_00_a");
			isagTestPath = PathHelper.Combine(isagLibPath, "test");
			pregenPrjFile = PathHelper.Combine(isagTestPath, "simulation.prj");
			fileTest = TestBenchPath;
			fileTemplate = PathHelper.Combine(isagTestPath, "proc_exec_test_template.vhd");
			fileTemplateBuilt = PathHelper.Combine(workingDirectory, "testbench.vhd");
		}

		private void Setup()
		{
			SetupPaths();

			// Load test bench
			bench = TestBench.Load(fileTest, TestBenchIncludes);

			// Generate test bench vhdl
			File.WriteAllText(fileTemplateBuilt, TestBenchGenerator.GenerateTestBench(bench, workingDirectory, fileTemplate));

			// Automatically Generate the prj file
			string prjFilePath = PathHelper.Combine(workingDirectory, "prj.prj");
			PrjFile prjFile = new PrjFile(Repository);
			prjFile.AddAllInLibrary(Repository.GetLibrary("isa_generic_v2_00_a"));
			string prjFileContents = prjFile.ToString() + Environment.NewLine + 
				string.Format("vhdl isa_generic_v2_00_a \"{0}\"", fileTemplateBuilt) + Environment.NewLine;
			File.WriteAllText(prjFilePath, prjFileContents);

			Logger.Instance.WriteVerbose("Building Simulation");
			// Build the isim exe
			FuseBuild.BuildResult result = FuseBuild.BuildProject(workingDirectory, prjFilePath, "isa_generic_v2_00_a.proc_exec_test");
			simulationExe = result.ExecutableFile;
		}

		private void Start()
		{
			// Setup and start simulation
			Logger.Instance.WriteVerbose("Starting Simulation");
			simulator = new ISimSimulator(workingDirectory, simulationExe);
			proc = new Processor(simulator);

			if (GuiEnabled)
			{
				simulator.RunGraphicalUserInterface = true;
			}

			// Start
			simulator.Start();
			//Advance the time just a fraction past the clock edge
			//FIXME: move this somewhere more sensible
			simulator.RunFor(1);
		}

		public void Run()
		{
			try
			{
				Setup();
				Start();

				Logger.Instance.WriteVerbose("Simulation Ready");

				if (!GuiEnabled)
				{

					// Process test bench
					while (true)
					{
						// Run until the first instruction is next
						proc.RunToNextValidInstruction();
						ProcessorState state = proc.GetCurrentState();
						bench.RunAssertions(state);

						if (bench.IsTestComplete())
						{
							break;
						}
						proc.RunCycle();
					}
				}
				else
				{
					simulator.WaitForExit();
				}
			}
			catch
			{
				// In the event of an exception clean up the output
				PrintState(true);
				throw;
			}

			CleanUp();
			PrintState(false);
		}

		private void PrintState(bool forceFailed)
		{
			// Print the state
			Console.Write("{0}", Path.GetFileName(fileTest));
			PrintAssertionsState(bench, forceFailed);
		}

		private static void PrintAssertionsState(TestBench test, bool forceFailed)
		{
			Console.CursorLeft = Console.WindowWidth - 12;
			Console.Write(" [ ");
			if (test == null || test.failedAssertions > 0 || test.passedAssertions == 0 || forceFailed)
			{
				using (new ConsoleColorScope(ConsoleColor.Red))
				{
					Console.Write("failed");
				}
			}
			else
			{
				using (new ConsoleColorScope(ConsoleColor.Green))
				{
					Console.Write("passed");
				}
			}
			Console.WriteLine(" ]");
		}
	}
}
