// Copyright 2011 Nathan Rossi - http://nathanrossi.com
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HDLToolkit.Xilinx.Simulation;
using HDLToolkit.Xilinx;
using HDLToolkit;
using HDLToolkit.Framework;
using HDLToolkit.Framework.Simulation;
using System.IO;
using System.Text.RegularExpressions;
using System.Globalization;

namespace ISAGenericTestSuiteRunner
{
	class Program
	{
		static void Main(string[] args)
		{
			bool debugEnable = false;
			bool guiEnable = false;
			bool includeArg = false;
			List<string> files = new List<string>();
			List<string> testBenchIncludes = new List<string>();
			foreach (string a in args)
			{
				if (includeArg)
				{
					includeArg = false;
					testBenchIncludes.Add(a);
				}
				else if (string.Compare(a, "-d", true) == 0)
				{
					debugEnable = true;
				}
				else if (string.Compare(a, "-g", true) == 0)
				{
					guiEnable = true;
				}
				else if (string.Compare(a, "-I", true) == 0)
				{
					includeArg = true;
				}
				else
				{
					files.Add(a);
				}
			}

			if (debugEnable)
			{
				Logger.Instance.VerbosityLevel = Logger.Verbosity.Debug;
			}

			XilinxRepository repo = new XilinxRepository();
			repo.AddSearchPath(PathHelper.Combine(XilinxHelper.GetRootXilinxPath(), "EDK", "hw"));
			string environRepos = Environment.GetEnvironmentVariable("REPOSITORY");
			if (!string.IsNullOrEmpty(environRepos))
			{
				string[] paths = environRepos.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
				foreach (string path in paths)
				{
					string fullPath = Path.GetFullPath(path.Trim('"'));
					Logger.Instance.WriteVerbose("Repository: Adding {0}", fullPath);
					repo.AddSearchPath(fullPath);
				}
			}

			string testRoot = PathHelper.Combine(repo.GetLibraryDefaultRootPath("isa_generic_v2_00_a"), "test");

			foreach (string file in files)
			{
				string fullFilePath = file;
				if (!Path.IsPathRooted(fullFilePath))
				{
					fullFilePath = PathHelper.Combine(testRoot, file);
				}

				if (File.Exists(fullFilePath))
				{
					try
					{
						TestRunner runner = new TestRunner(repo, fullFilePath);
						runner.TestBenchIncludes.AddRange(testBenchIncludes);
						if (guiEnable)
						{
							runner.GuiEnabled = true;
						}
						runner.Run();
					}
					catch (Exception ex)
					{
						Console.WriteLine("Exception {0}", ex.Message);
						Console.WriteLine("{0}", ex.StackTrace);
						Console.WriteLine("Continuing...");
					}
				}
				else
				{
					Logger.Instance.WriteError("{0} does not exist", Path.GetFileName(fullFilePath));
				}
			}
		}
	}
}
