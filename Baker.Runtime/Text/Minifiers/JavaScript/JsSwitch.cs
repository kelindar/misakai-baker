// switch.cs
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

using System.Collections.Generic;
using System.Text;

namespace Baker.Text
{
    internal sealed class JsSwitch : JsAstNode
    {
        private JsAstNode m_expression;
        private JsAstNodeList m_cases;

        public JsAstNode Expression
        {
            get { return m_expression; }
            set
            {
                m_expression.IfNotNull(n => n.Parent = (n.Parent == this) ? null : n.Parent);
                m_expression = value;
                m_expression.IfNotNull(n => n.Parent = this);
            }
        }

        public JsAstNodeList Cases
        {
            get { return m_cases; }
            set
            {
                m_cases.IfNotNull(n => n.Parent = (n.Parent == this) ? null : n.Parent);
                m_cases = value;
                m_cases.IfNotNull(n => n.Parent = this);
            }
        }

        public bool BraceOnNewLine { get; set; }
        public JsContext BraceContext { get; set; }

        public JsActivationObject BlockScope { get; set; }

        public JsSwitch(JsContext context, JsParser parser)
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

        internal override bool RequiresSeparator
        {
            get
            {
                // switch always has curly-braces, so we don't
                // require the separator
                return false;
            }
        }

        public override IEnumerable<JsAstNode> Children
        {
            get
            {
                return EnumerateNonNullNodes(Expression, Cases);
            }
        }

        public override bool ReplaceChild(JsAstNode oldNode, JsAstNode newNode)
        {
            if (Expression == oldNode)
            {
                Expression = newNode;
                return true;
            }
            if (Cases == oldNode)
            {
                JsAstNodeList newList = newNode as JsAstNodeList;
                if (newNode == null || newList != null)
                {
                    // remove it
                    Cases = newList;
                    return true;
                }
            }

            return false;
        }
    }
}
