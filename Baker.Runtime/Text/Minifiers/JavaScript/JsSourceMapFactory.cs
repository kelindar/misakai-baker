// V3SourceMap.cs
//
// Copyright 2012 Microsoft Corporation
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
using System.IO;

namespace Baker.Text
{
    internal static class JsSourceMapFactory
    {
        /// <summary>
        /// Create an instance of a source map writer of the given name and from the given base stream.
        /// </summary>
        /// <param name="writer">base stream</param>
        /// <param name="implementationName">implementation name to create</param>
        /// <returns>instance of a source map writer</returns>
        public static IJsSourceMap Create(TextWriter writer, string implementationName)
        {
            IJsSourceMap implementation = null;

            // which implementation to instantiate?
            if (string.Compare(implementationName, JsV3SourceMap.ImplementationName, StringComparison.OrdinalIgnoreCase) == 0)
            {
                implementation = new JsV3SourceMap(writer);
            }
            else if (string.Compare(implementationName, JsScriptSharpSourceMap.ImplementationName, StringComparison.OrdinalIgnoreCase) == 0)
            {
                implementation = new JsScriptSharpSourceMap(writer);
            }

            return implementation;
        }
    }
}
