﻿// Copyright 2011 Nathan Rossi - http://nathanrossi.com
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
using System.IO;

namespace HDLToolkit
{
	public static class StringHelpers
	{
		public static string ExpandString(string pad, int count)
		{
			int counter = count;
			string padded = "";
			while (counter > 0)
			{
				padded += pad;
				counter--;
			}
			return padded;
		}

		public class Utf8StringWriter : StringWriter
		{
			public override Encoding Encoding { get { return Encoding.UTF8; } }
		}

		/// <summary>
		/// Converts an array of byte values into a string of the values in hexadecimal notation.
		/// </summary>
		/// <param name="values">An array of values for each byte</param>
		/// <param name="offset">Start of the values in the array</param>
		/// <param name="length">How many bytes in length (including the offset)</param>
		/// <returns></returns>
		public static string BytesToString(byte[] values, int offset, int length)
		{
			StringBuilder builder = new StringBuilder();
			for (int i = offset; i < offset + length; i++)
			{
				builder.AppendFormat("{0:X2}", values[i]);
			}
			return builder.ToString();
		}
	}
}
