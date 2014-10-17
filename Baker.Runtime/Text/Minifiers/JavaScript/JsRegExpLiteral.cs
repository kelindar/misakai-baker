// regexpliteral.cs
//
// Copyright 2010 Microsoft Corporation
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
using System.Globalization;
using System.Text.RegularExpressions;

namespace Baker.Text
{
    internal sealed class JsRegExpLiteral : JsExpression
    {
        public string Pattern { get; set; }
        public string PatternSwitches { get; set; }

        public override bool IsConstant
        {
            get
            {
                return true;
            }
        }

        public JsRegExpLiteral(JsContext context, JsParser parser)
            : base(context, parser)
        {
        }

        public override void Accept(IJsVisitor visitor)
        {
            if (visitor != null)
            {
                visitor.Visit(this);
            }
        }

        public override bool IsEquivalentTo(JsAstNode otherNode)
        {
            var otherRegExp = otherNode as JsRegExpLiteral;
            return otherRegExp != null
                && string.CompareOrdinal(Pattern, otherRegExp.Pattern) == 0
                && string.CompareOrdinal(PatternSwitches, otherRegExp.PatternSwitches) == 0;
        }
    }
}
