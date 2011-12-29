﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using System.IO;
using HDLToolkit;

namespace ISAGenericTestSuiteRunner
{
	public class TestBench
	{
		private int Stride;

		private List<string> instructionsList;
		private int instructionsCount = -1;

		private List<TestCommand> commands;

		public int failedAssertions = 0;
		public int passedAssertions = 0;
		public bool endCalled = false;

		private class QueuedCommand
		{
			public TestCommand Command { get; set; }
			public int CycleToWait { get; set; }

			public QueuedCommand()
			{
			}
		}

		private List<QueuedCommand> queuedCommands = new List<QueuedCommand>();

		public TestBench()
		{
			Stride = Convert.ToInt32(Environment.GetEnvironmentVariable("ISAG_STRIDE"));
			instructionsList = new List<string>();
			commands = new List<TestCommand>();
		}

		public void Reset()
		{
			queuedCommands.Clear();

			failedAssertions = 0;
			passedAssertions = 0;
			endCalled = false;
		}

		public void RunAssertions(ProcessorState state)
		{
			int nextPhysicalPC = state.PC / Stride;

			// find all commands to be made
			foreach (TestCommand c in commands)
			{
				if (c.Address == nextPhysicalPC)
				{
					// enqueuing the command
					Logger.Instance.WriteDebug("Enqueued command {0}::'{1}'", c.GetType().ToString(), c.Parameters);
					queuedCommands.Add(new QueuedCommand() { Command = c, CycleToWait = c.CyclesAfterEvent });
				}
			}

			// check all queued commands and execute any
			List<QueuedCommand> toRemove = new List<QueuedCommand>();
			foreach (QueuedCommand q in queuedCommands)
			{
				if (q.CycleToWait == 0)
				{
					Logger.Instance.WriteDebug("Executed command {0}::'{1}'", q.Command.GetType().ToString(), q.Command.Parameters);
					q.Command.Execute(this, state);
					toRemove.Add(q);
				}
				else
				{
					q.CycleToWait--;
				}
			}
			foreach (QueuedCommand q in toRemove)
			{
				queuedCommands.Remove(q);
			}
		}

		public void EndTest()
		{
			endCalled = true;
		}

		public bool IsTestComplete()
		{
			return endCalled;
		}

		public void IncrementAssertionResult(bool passed)
		{
			if (passed)
			{
				passedAssertions++;
			}
			else
			{
				failedAssertions++;
			}
		}

		public string GenerateAssembly()
		{
			StringBuilder builder = new StringBuilder();
			int instruction = 0;

			foreach (string i in instructionsList)
			{
				builder.AppendLine(i + "   // INSTRUCTION " + instruction++.ToString());
			}

			return builder.ToString();
		}

		#region Parsing
		private static TestBench Load(string fileContents)
		{
			TestBench bench = new TestBench();

			using (StringReader reader = new StringReader(fileContents))
			{
				string line = null;
				while ((line = reader.ReadLine()) != null)
				{
					line = line.Trim();
					if (!string.IsNullOrEmpty(line))
					{
						// Ignore comments, and print a warning for ##todo statements
						if (line.StartsWith("##todo", StringComparison.InvariantCultureIgnoreCase))
						{
							Logger.Instance.WriteWarning("TODO: {1}", line);
							continue;
						}
						if (line.StartsWith("##"))
						{
							continue;
						}

						// Parse the command
						if (ParseCommand(line, bench))
						{
							continue;
						}
						bench.instructionsList.Add(line);
						bench.instructionsCount++;
					}
				}
			}

			return bench;
		}

		public static TestBench Load(string file, List<string> includeDirectories)
		{
			Logger.Instance.WriteDebug("Loading test bench...");

			string testBenchContents = File.ReadAllText(file);

			Logger.Instance.WriteDebug("Pre-processing test bench...");

			// Preprocessing
			testBenchContents = TestBenchGenerator.PreProcessTestBench(testBenchContents, includeDirectories);

			Logger.Instance.WriteDebug("Pre-processed test bench == ");
			Logger.Instance.WriteDebug(testBenchContents.Replace("\n", "\n\t"));

			// If valid, load the test bench
			if (!string.IsNullOrEmpty(testBenchContents))
			{
				return Load(testBenchContents);
			}
			return null;
		}

		private static Regex commandRegex = new Regex(@"#(?<command>.*?)(@(?<cycles>.*?))?\((?<content>.*?)\)", RegexOptions.IgnoreCase);
		private static bool ParseCommand(string command, TestBench bench)
		{
			Match m = commandRegex.Match(command);
			if (m.Success)
			{
				Logger.Instance.WriteDebug("Command:" + command);
				// Standard command
				// #type@cycleoffset(parameters)

				string type = m.Groups["command"].Value.ToLower();
				string cycles = m.Groups["cycles"].Value;
				string content = m.Groups["content"].Value;

				int cyclesOffset = 1;
				if (!string.IsNullOrEmpty(cycles) && !int.TryParse(cycles, out cyclesOffset))
				{
					throw new Exception(string.Format("Invalid integer '{0}' in command '{1}'", cycles, command));
				}

				// Determine actual command class
				TestCommand cmd = null;
				switch (type)
				{
					case "end":
						cmd = new EndTestCommand(bench.instructionsCount, cyclesOffset, content);
						break;
					case "test":
						cmd = new AssertTestCommand(bench.instructionsCount, cyclesOffset, content);
						break;
					case "skip":
						bench.instructionsCount += cyclesOffset;
						break;
					default:
						throw new Exception(string.Format("Invalid type '{0}' in command '{1}'", type, command));
				}
				
				if (cmd != null)
				{
					bench.commands.Add(cmd);
				}
				return true;
			}
			else
			{
				return false;
			}
		}
		#endregion
	}
}
