// arrayliteral.cs
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
using System.Globalization;
using System.Text;

namespace Baker.Text
{
    internal sealed class JsArrayLiteral : JsExpression
    {
        private JsAstNodeList m_elements;

        public JsAstNodeList Elements 
        {
            get { return m_elements; }
            set
            {
                m_elements.IfNotNull(n => n.Parent = (n.Parent == this) ? null : n.Parent);
                m_elements = value;
                m_elements.IfNotNull(n => n.Parent = this);
            }
        }

        public bool MayHaveIssues { get; set; }

        public JsArrayLiteral(JsContext context, JsParser parser)
            : base(context, parser)
        {
        }

        public override IEnumerable<JsAstNode> Children
        {
            get
            {
                return EnumerateNonNullNodes(Elements);
            }
        }

        public override void Accept(IJsVisitor visitor)
        {
            if (visitor != null)
            {
                visitor.Visit(this);
            }
        }

        public override bool ReplaceChild(JsAstNode oldNode, JsAstNode newNode)
        {
            // if the old node isn't our element list, ignore the cal
            if (oldNode == Elements)
            {
                if (newNode == null)
                {
                    // we want to remove the list altogether
                    Elements = null;
                    return true;
                }
                else
                {
                    // if the new node isn't an AstNodeList, then ignore the call
                    JsAstNodeList newList = newNode as JsAstNodeList;
                    if (newList != null)
                    {
                        // replace it
                        Elements = newList;
                        return true;
                    }
                }
            }
            return false;
        }

        public override bool IsConstant
        {
            get
            {
                return Elements == null ? true : Elements.IsConstant;
            }
        }
    }
}
